using System.Globalization;
using System.Text.Json.Nodes;
using BatteryPassWeb.Dpp.Access;
using BatteryPassWeb.Dpp.Catalog;
using BatteryPassWeb.Dpp.Editing;
using BatteryPassWeb.Dpp.Pages;
using BatteryPassWeb.Dpp.Publication;
using BatteryPassWeb.Dpp.Store;
using BatteryPassWeb.Services;
using MongoDB.Bson;

namespace BatteryPassWeb.Dpp.Api;

/// <summary>Why a battery can't be written to by this sender; <see cref="None"/> when it can.</summary>
public enum WriteRefusal
{
    None,

    /// <summary>No battery this sender may know of has the ID: unknown, or a draft for a cluster token (J-6).</summary>
    NotFound,

    /// <summary>A published battery outside the token's clusters.</summary>
    NotYours,

    /// <summary>A serial number that more than one battery the sender may change has.</summary>
    Ambiguous,
}

/// <summary>The battery a write names, or why there is none.</summary>
public sealed record WriteTarget(BatteryPack? Pack, WriteRefusal Refusal)
{
    public string Identifier => Pack?.Values.GetValueOrDefault("uniqueProductIdentifier")?.Value?.GetValue<string>() ?? string.Empty;
}

/// <summary>One saved live value from the change log, as a reader may see it.</summary>
public sealed record LiveHistoryEntry(AttributeDefinition Attribute, JsonNode Value, DateTime MeasuredAtUtc, DateTime SavedAtUtc, string Source);

/// <summary>A batch a new battery can be made in, and the economic operators that can place it on the market.</summary>
public sealed record CreationChoices(IReadOnlyList<BatchChoice> Batches, IReadOnlyList<EconomicOperator> Operators);

/// <summary>
/// The external API's writing side (decision 29, step 3c-2), on the same services as the team's pages: creating a
/// battery (<see cref="PackCreationService"/>), publishing (<see cref="PublicationService"/>) and the live values
/// (<see cref="SavePipeline"/>). Who may change what is the pipeline's rule (design 6.1, J-10); this class only finds the
/// battery and keeps the live-value call to the live values. Their history is read from the pipeline's change log.
/// </summary>
public sealed class BatteryApi(
    DppStore store,
    SavePipeline pipeline,
    PackCreationService creation,
    PublicationService publication,
    BatteryPages batteries,
    ClusterRepository clusters,
    ApplicationAuditService audit,
    TimeProvider clock,
    Identifiers.BatteryIdPool? pool = null)
{
    /// <summary>The source stored with values a battery system sends through the API ("battery system" on the pages).</summary>
    public const string FeedSource = "feed";

    /// <summary>The source stored with values the API sends when it creates a battery.</summary>
    public const string ApiSource = "api";

    /// <summary>
    /// The live values (design 7.3): the attributes that change in place and that a battery system feeds, from the
    /// catalogue. Today #63, #83, #87 and E1–E4 (decision 28).
    /// </summary>
    public static IReadOnlyList<AttributeDefinition> LiveSet { get; } = AttributeCatalog.All
        .Where(attribute => attribute.Change == ChangeRule.Live && attribute.Editors.HasFlag(AttributeEditors.BatterySystemFeed))
        .ToArray();

    /// <summary>How far ahead of the app's clock a measurement time may be, for clocks slightly apart.</summary>
    public static readonly TimeSpan ClockTolerance = TimeSpan.FromMinutes(5);

    /// <summary>The longest history one call reads, in hours (one week, as the telemetry history did).</summary>
    public const int MaxHistoryHours = 168;

    /// <summary>"#63, #83, #87, E1, E2, E3 and E4", for messages.</summary>
    public static string LiveSetWords { get; } =
        $"{string.Join(", ", LiveSet.SkipLast(1).Select(attribute => attribute.Label))} and {LiveSet[^1].Label}";

    /// <summary>
    /// The battery a write names: its battery ID (#6), a passport ID (#5) or its serial number, as the reading calls take
    /// them. The team may change any battery, drafts included; any other sender only the published batteries of its own
    /// clusters, and a draft is unknown to it (J-6).
    /// </summary>
    public async Task<WriteTarget> FindAsync(ChangeSender sender, string? id, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(sender);
        var text = Uri.UnescapeDataString(id?.Trim() ?? string.Empty); // a web-form ID's slashes arrive encoded (%2F) in an address (6d)
        if (text.Length == 0)
        {
            return new WriteTarget(null, WriteRefusal.NotFound);
        }

        var candidates = new List<BatteryPack>();
        if (await store.FindPackByUniqueProductIdentifierAsync(text, cancellationToken) is { } byIdentifier)
        {
            candidates.Add(byIdentifier);
        }
        else if (await store.GetVersionByPassportIdAsync(text, cancellationToken) is { } version
            && await store.GetAsync<BatteryPack>(version.PackId, cancellationToken) is { } byPassport)
        {
            candidates.Add(byPassport);
        }
        else
        {
            candidates.AddRange(await store.FindPacksBySerialNumberAsync(text, cancellationToken));
        }

        var known = new List<BatteryPack>();
        foreach (var pack in candidates)
        {
            if (sender.Kind == SenderKind.Team || await store.GetActiveVersionAsync(pack.Id, cancellationToken) is not null)
            {
                known.Add(pack);
            }
        }

        // A serial number names the battery whose passport isn't inactive: its new linked passport (step 4c-1, decision 34).
        if (known.Count > 1)
        {
            var current = new List<BatteryPack>();
            foreach (var pack in known)
            {
                if (await store.PassportStatusAsync(pack.Id, cancellationToken) != DppStatuses.Inactive)
                {
                    current.Add(pack);
                }
            }

            known = current;
        }

        var mine = known.Where(pack => MayChange(sender, pack)).ToArray();
        return mine.Length switch
        {
            1 => new WriteTarget(mine[0], WriteRefusal.None),
            > 1 => new WriteTarget(null, WriteRefusal.Ambiguous),
            _ => new WriteTarget(null, known.Count > 0 ? WriteRefusal.NotYours : WriteRefusal.NotFound),
        };
    }

    /// <summary>
    /// Saves live values measured at one moment, through the save pipeline (design 6.2): each key is judged on its own
    /// and a refused key doesn't block the others (J-12). A key outside the live set is refused here, whoever sends it:
    /// static values change on the team's pages and, from step 5, by upload.
    /// </summary>
    public async Task<SaveResult> WriteLiveValuesAsync(
        ChangeSender sender,
        BatteryPack pack,
        IReadOnlyDictionary<string, JsonNode?> values,
        DateTime? measuredAtUtc,
        bool preview,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(sender);
        ArgumentNullException.ThrowIfNull(pack);
        ArgumentNullException.ThrowIfNull(values);
        var notLive = values.Keys
            .Where(key => AttributeCatalog.FindByProperty(key) is { } attribute && !LiveSet.Contains(attribute))
            .ToHashSet(StringComparer.Ordinal);
        var live = values.Where(entry => !notLive.Contains(entry.Key)).ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal);
        var saved = live.Count == 0
            ? new SaveResult(true, false, [])
            : await pipeline.SaveAsync(sender, new ChangeSet(TargetKind.Pack, pack.Id, live, FeedSource, measuredAtUtc), preview, cancellationToken);
        var results = saved.Keys.ToDictionary(result => result.Key, StringComparer.Ordinal);
        var keys = values.Keys
            .Select(key => notLive.Contains(key)
                ? new KeyResult(key, KeyOutcome.Rejected, $"{AttributeCatalog.FindByProperty(key)!.Label} is not a live value; this call takes {LiveSetWords}")
                : results[key])
            .ToArray();
        return saved with { Keys = keys };
    }

    /// <summary>
    /// The live values saved for a battery since a moment, newest measurement first, as the reader may see them: the one
    /// rule (<see cref="AudienceRules.MaySee"/>) for the battery's published cluster, so a change to who sees what
    /// changes the history too. <paramref name="hours"/> counts back from now, by measurement time. Read from the change
    /// log, which keeps every saved value with its measurement time; an entry written before 3c-2 has none, and its save
    /// time stands in.
    /// </summary>
    public Task<IReadOnlyList<LiveHistoryEntry>> HistoryAsync(Reader reader, string packId, int hours, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(reader);
        ArgumentOutOfRangeException.ThrowIfLessThan(hours, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(hours, MaxHistoryHours);
        return ReadAsync(reader, packId, clock.GetUtcNow().UtcDateTime.AddHours(-hours), null, cancellationToken);
    }

    /// <summary>
    /// The detail page's week of live values (redesign R7, the owner's choice "Latest week with data"): the last 7 days
    /// while the battery reported in them; otherwise the 7 days ending at <paramref name="lastReadingUtc"/>, its last
    /// reading (from its stored values: no history scan). One query from the week's start; the same rule of who sees what.
    /// </summary>
    public async Task<(IReadOnlyList<LiveHistoryEntry> Week, DateTime EndUtc)> LatestWeekAsync(Reader reader, string packId, DateTime? lastReadingUtc, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(reader);
        var endUtc = BatteryPassWeb.Dpp.Pages.LiveTrends.WeekEnd(lastReadingUtc, clock.GetUtcNow().UtcDateTime);
        return (await ReadAsync(reader, packId, endUtc.AddHours(-MaxHistoryHours), endUtc, cancellationToken), endUtc);
    }

    private async Task<IReadOnlyList<LiveHistoryEntry>> ReadAsync(Reader reader, string packId, DateTime sinceUtc, DateTime? untilUtc, CancellationToken cancellationToken)
    {
        var batteryClusters = await store.FindPublishedClusterIdsUsingAsync(packId: packId, cancellationToken: cancellationToken);
        var visible = LiveSet.Where(attribute => AudienceRules.MaySee(reader, attribute, batteryClusters)).ToDictionary(attribute => attribute.Property, StringComparer.Ordinal);
        if (visible.Count == 0)
        {
            return [];
        }

        var entries = new List<LiveHistoryEntry>();
        var events = await audit.ListEntityEventsAsync(SavePipeline.AuditEventType, "dpp-pack", packId, sinceUtc, cancellationToken);
        foreach (var change in events)
        {
            var savedAt = Moment(BsonHelpers.GetString(change, "createdAt")) ?? sinceUtc;
            var keys = change.GetValue("metadata", new BsonDocument()).AsBsonDocument.GetValue("keys", new BsonArray()).AsBsonArray;
            foreach (var key in keys.OfType<BsonDocument>())
            {
                if (key.GetValue("outcome", string.Empty) != KeyOutcome.Saved.ToString()
                    || !visible.TryGetValue(key.GetValue("key", string.Empty).ToString()!, out var attribute)
                    || key.GetValue("value", BsonNull.Value) is not BsonString text
                    || JsonNode.Parse(text.Value) is not { } value)
                {
                    continue;
                }

                var measuredAt = Moment(key.GetValue("generatedAt", BsonNull.Value) is BsonString generated ? generated.Value : null) ?? savedAt;
                if (measuredAt >= sinceUtc && (untilUtc is null || measuredAt <= untilUtc))
                {
                    entries.Add(new LiveHistoryEntry(attribute, value, measuredAt, savedAt, change.GetValue("source", string.Empty).ToString()!));
                }
            }
        }

        return entries.OrderByDescending(entry => entry.MeasuredAtUtc).ThenByDescending(entry => entry.SavedAtUtc).ToArray();
    }

    /// <summary>True when a measurement time lies ahead of the app's clock by more than <see cref="ClockTolerance"/>.</summary>
    public bool InTheFuture(DateTime measuredAtUtc) => measuredAtUtc.ToUniversalTime() > clock.GetUtcNow().UtcDateTime + ClockTolerance;

    /// <summary>The batches and economic operators a new battery can name, as the team's New battery page offers them.</summary>
    public async Task<CreationChoices> ChoicesAsync(CancellationToken cancellationToken = default)
    {
        var page = await batteries.NewAsync(cancellationToken: cancellationToken);
        return new CreationChoices(page.Batches, page.Operators);
    }

    /// <summary>
    /// Creates a draft battery (design 7.1), for the team only: the same rules as the New battery page. The cluster is
    /// optional but must exist.
    /// </summary>
    public async Task<PackCreationResult> CreateAsync(
        ChangeSender sender,
        string? batchId,
        string? operatorId,
        string? clusterId,
        IReadOnlyDictionary<string, JsonNode?> values,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(sender);
        ArgumentNullException.ThrowIfNull(values);
        if (sender.Kind != SenderKind.Team)
        {
            return Problem("only the team creates batteries");
        }

        if (string.IsNullOrWhiteSpace(batchId) || string.IsNullOrWhiteSpace(operatorId))
        {
            return Problem("batchId and operatorId are required; GET batches and GET operators list them");
        }

        var cluster = string.IsNullOrWhiteSpace(clusterId) ? null : ClusterRepository.NormalizeClusterId(clusterId);
        if (cluster is not null && await clusters.GetClusterByIdAsync(cluster, cancellationToken) is null)
        {
            return Problem($"cluster {cluster} doesn't exist; name a registered cluster or none");
        }

        return await creation.CreateAsync(sender, new NewPack(batchId.Trim(), operatorId.Trim(), cluster, values, ApiSource, FromPool: pool is not null), cancellationToken);

        static PackCreationResult Problem(string problem) => new(null, [], problem);
    }

    /// <summary>Publishes the battery's passport, or a new version of it (design 7.1, 7.2); the team only.</summary>
    public Task<PublicationResult> PublishAsync(ChangeSender sender, BatteryPack pack, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(pack);
        return publication.PublishAsync(sender, pack.Id, cancellationToken);
    }

    /// <summary>The team changes every battery; a cluster admin or feed only those of its clusters, as the save pipeline checks again.</summary>
    private static bool MayChange(ChangeSender sender, BatteryPack pack) =>
        sender.Kind == SenderKind.Team || (pack.ClusterId is not null && sender.ClusterIds.Contains(pack.ClusterId));

    private static DateTime? Moment(string? text) =>
        DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var moment) ? moment.UtcDateTime : null;
}
