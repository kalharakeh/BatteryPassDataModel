using System.Text.Json.Nodes;
using BatteryPassWeb.Dpp.Catalog;
using BatteryPassWeb.Dpp.Identifiers;
using BatteryPassWeb.Dpp.Publication;
using BatteryPassWeb.Dpp.Store;
using BatteryPassWeb.Dpp.Values;
using BatteryPassWeb.Services;
using MongoDB.Bson;

namespace BatteryPassWeb.Dpp.Editing;

/// <summary>Another operator who places the battery on the market again: their #7 and #14, and their new passport's ID or address.</summary>
public sealed record OtherOperator(string? OperatorId, string? OperatorInfo, string? Passport);

/// <summary>
/// The battery's new linked passport (design 7.5, decisions 13, 19 and 34; step 4c-1), once a status change placed it on
/// the market again. Scania's: the team makes a new draft pack that starts from the old one, and publishing it makes the
/// old passport inactive. Another operator's: the team or the battery's cluster admins enter the operator and, once known,
/// link its new passport, which makes the old one inactive. Until then the team can cancel and put the old status back.
/// </summary>
public sealed class SuccessionService(
    DppStore store,
    PackCreationService creation,
    SavePipeline pipeline,
    PublicationService publication,
    ApplicationAuditService audit,
    TimeProvider clock)
{
    public const string SuccessorMadeEvent = "dpp.successor-made";
    public const string OtherOperatorEvent = "dpp.successor-other-operator";
    public const string CancelledEvent = "dpp.successor-cancelled";

    public const string NoBattery = "the battery was not found";
    public const string NotAwaited = "the battery isn't waiting for a new passport";
    public const string TeamMakesIt = "the team makes Scania's new passport";
    public const string TeamCancels = "only the team cancels the new passport";
    public const string NotYours = "only the team and the battery's cluster admins name the operator";
    public const string NoOperator = "choose an economic operator";
    public const string AlreadyMade = "the new passport is already made";
    public const string PassportNotAnAddress = "the new passport's ID must be a battery or passport ID, or a web address that starts with http:// or https://";

    public const string LinkNeedsPassport = "give the new passport's ID or web address to link it";
    public const string NeedsOperator = "give the operator's identifier or name";

    /// <summary>The pack values the new passport starts from (Q2 of 4c): serial number, service provider, manufacturing date.</summary>
    public static readonly string[] CarriedValues = ["batterySerialNumber", "thirdPartyServiceProvider", "manufacturingDate"];

    /// <summary>
    /// Makes Scania's new passport as a draft (design 7.5, J-2): a new battery ID, the chosen operator, the old battery's
    /// batch, cluster, serial number, service provider and manufacturing date, its new status and re-stated values, and
    /// its module list and its last measured values. The warranty (#19) is left for the team. Not copied: the repair history and accident reports,
    /// which stay with the old passport.
    /// </summary>
    public async Task<(string? SuccessorPackId, string? Problem)> CreateSuccessorAsync(ChangeSender sender, string packId, string? operatorId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(sender);
        if (await store.LoadSourcesAsync(packId, cancellationToken) is not { } sources)
        {
            return (null, NoBattery);
        }

        var old = sources.Pack;
        if (sender.Kind != SenderKind.Team)
        {
            return (null, TeamMakesIt);
        }

        if (old.Succession is not { LinkedAtUtc: null } succession)
        {
            return (null, NotAwaited);
        }

        if (succession.SuccessorPackId is not null || succession.OtherPassport is not null)
        {
            return (null, AlreadyMade);
        }

        if (string.IsNullOrWhiteSpace(operatorId) || await store.GetAsync<EconomicOperator>(operatorId, cancellationToken) is null)
        {
            return (null, NoOperator);
        }

        var values = CarriedValues
            .Where(old.Values.ContainsKey)
            .ToDictionary(key => key, key => old.Values[key].Value?.DeepClone(), StringComparer.Ordinal);
        var created = await creation.CreateAsync(sender, new NewPack(old.BatchId, operatorId, old.ClusterId, values, "form", PredecessorPackId: old.Id, IsDemo: old.IsDemo), cancellationToken);
        if (created.Pack is not { } pack)
        {
            return (null, created.Problem ?? "the new passport couldn't be made");
        }

        // The new status and the re-stated values, as the old passport's last status change stated them.
        var now = clock.GetUtcNow().UtcDateTime;
        var carried = new Dictionary<string, Values.AttributeValue>(StringComparer.Ordinal)
        {
            ["batteryStatus"] = new() { Value = new JsonObject { ["batteryStatusValues"] = succession.Status }, GeneratedAtUtc = now, Source = "system", Who = sender.Who },
        };
        foreach (var attribute in StatusChangeService.RestatedAttributes(sources.Model))
        {
            if ((old.Values.GetValueOrDefault(attribute.Property) ?? sources.Model.Values.GetValueOrDefault(attribute.Property)) is { } value)
            {
                carried[attribute.Property] = value;
            }
        }

        // The battery's last measured values continue on the new passport, with their measurement times: it is the
        // same battery, and the passport needs them to be published (7.1). #92 stays the new passport's own page.
        foreach (var attribute in AttributeCatalog.All.Where(attribute => attribute.Change == ChangeRule.Live && attribute.Property != Documents.DocumentService.Accidents))
        {
            if (old.Values.GetValueOrDefault(attribute.Property) is { } value)
            {
                carried[attribute.Property] = value;
            }
        }

        await store.SetValuesAsync<BatteryPack>(pack.Id, carried, cancellationToken);
        await store.SetPackModulesAsync(pack.Id, old.Modules, cancellationToken);
        succession.SuccessorPackId = pack.Id;
        await store.SetSuccessionAsync(old.Id, succession, cancellationToken);
        await audit.AppendApplicationAuditEventAsync(
            SuccessorMadeEvent,
            sender.Who,
            sender.Kind.ToString(),
            "form",
            $"New passport of battery {Serial(old)} made as a draft ({succession.Status})",
            new BsonDocument { ["successor"] = pack.Id, ["status"] = succession.Status },
            entityType: "dpp-pack",
            entityId: old.Id,
            cancellationToken: cancellationToken);
        return (pack.Id, null);
    }

    /// <summary>
    /// Another operator places the battery on the market again (design 7.5): their details are saved, and with
    /// <paramref name="link"/> their new passport's ID or address links it, which makes the old passport inactive.
    /// Null when done; otherwise why not.
    /// </summary>
    public async Task<string?> SaveOtherOperatorAsync(ChangeSender sender, string packId, OtherOperator details, bool link, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(sender);
        ArgumentNullException.ThrowIfNull(details);
        if (await store.GetAsync<BatteryPack>(packId, cancellationToken) is not { } old)
        {
            return NoBattery;
        }

        var mayName = sender.Kind == SenderKind.Team
            || (sender.Kind == SenderKind.ClusterAdmin && old.ClusterId is { } cluster && sender.ClusterIds.Contains(cluster));
        if (!mayName)
        {
            return NotYours;
        }

        if (old.Succession is not { LinkedAtUtc: null } succession)
        {
            return NotAwaited;
        }

        if (succession.SuccessorPackId is not null)
        {
            return AlreadyMade;
        }

        var operatorId = Clean(details.OperatorId);
        var operatorInfo = Clean(details.OperatorInfo);
        var passport = Clean(details.Passport);
        if (operatorId is null && operatorInfo is null)
        {
            return NeedsOperator;
        }

        // The new passport's address ends up as a link on the team's page: an ID of this app or an http(s) address only (S01).
        if (passport is not null && !WebAddress.IsWebAddress(passport) && !IdentifierForms.IsAppId(passport))
        {
            return PassportNotAnAddress;
        }

        // The details are kept even when the link must wait for the new passport's ID, so nothing typed is lost.
        succession.OtherOperatorId = operatorId;
        succession.OtherOperatorInfo = operatorInfo;
        succession.OtherPassport = passport;
        await store.SetSuccessionAsync(old.Id, succession, cancellationToken);
        if (link && passport is null)
        {
            return LinkNeedsPassport;
        }

        await audit.AppendApplicationAuditEventAsync(
            OtherOperatorEvent,
            sender.Who,
            sender.Kind.ToString(),
            "form",
            link
                ? $"Battery {Serial(old)} is placed on the market again by {operatorInfo ?? operatorId}; new passport {passport}"
                : $"Battery {Serial(old)} will be placed on the market again by {operatorInfo ?? operatorId}",
            new BsonDocument
            {
                ["operatorId"] = (BsonValue?)operatorId ?? BsonNull.Value,
                ["operator"] = (BsonValue?)operatorInfo ?? BsonNull.Value,
                ["passport"] = (BsonValue?)passport ?? BsonNull.Value,
                ["linked"] = link,
            },
            entityType: "dpp-pack",
            entityId: old.Id,
            cancellationToken: cancellationToken);
        if (link)
        {
            await publication.InactivateAsync(sender, old.Id, $"another operator's new passport {passport} is linked", cancellationToken);
        }

        return null;
    }

    /// <summary>
    /// The team cancels the new passport while the old one isn't yet inactive (decision 34): a draft new pack is
    /// deleted, the old status is put back with a new version, and the battery no longer waits. Null when done.
    /// </summary>
    public async Task<string?> CancelAsync(ChangeSender sender, string packId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(sender);
        if (await store.GetAsync<BatteryPack>(packId, cancellationToken) is not { } old)
        {
            return NoBattery;
        }

        if (sender.Kind != SenderKind.Team)
        {
            return TeamCancels;
        }

        if (old.Succession is not { LinkedAtUtc: null } succession)
        {
            return NotAwaited;
        }

        if (succession.SuccessorPackId is { } successorId)
        {
            if (await store.GetLatestVersionNumberAsync(successorId, cancellationToken) > 0)
            {
                return NotAwaited; // published: the old passport is already inactive
            }

            await store.DeleteAsync<BatteryPack>(successorId, cancellationToken);
        }

        await store.SetSuccessionAsync(old.Id, null, cancellationToken);
        var saved = await pipeline.SaveStatusChangeAsync(
            sender,
            old.Id,
            new Dictionary<string, JsonNode?> { ["batteryStatus"] = new JsonObject { ["batteryStatusValues"] = succession.StatusBefore } },
            cancellationToken);
        await audit.AppendApplicationAuditEventAsync(
            CancelledEvent,
            sender.Who,
            sender.Kind.ToString(),
            "form",
            $"New passport of battery {Serial(old)} cancelled; status back to {succession.StatusBefore}",
            new BsonDocument { ["from"] = succession.Status, ["to"] = succession.StatusBefore, ["saved"] = saved.Saved },
            entityType: "dpp-pack",
            entityId: old.Id,
            cancellationToken: cancellationToken);
        return null;
    }

    private static string? Clean(string? text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim();

    private static string Serial(BatteryPack pack) =>
        pack.Values.GetValueOrDefault("batterySerialNumber")?.Value is JsonValue value && value.TryGetValue<string>(out var text) ? text : pack.Id;
}
