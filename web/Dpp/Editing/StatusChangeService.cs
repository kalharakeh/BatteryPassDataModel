using BatteryPassWeb.Dpp.Access;
using System.Globalization;
using System.Text.Json.Nodes;
using BatteryPassWeb.Dpp.Catalog;
using BatteryPassWeb.Dpp.Publication;
using BatteryPassWeb.Dpp.Store;
using BatteryPassWeb.Services;
using MongoDB.Bson;

namespace BatteryPassWeb.Dpp.Editing;

/// <summary>What happened to the battery, as the status window asks it (decision 34). 4c adds the three that lead to a new passport.</summary>
public enum StatusEvent
{
    /// <summary>A repair (design 7.9): "re-used" for an original battery, otherwise the status stays (J-15).</summary>
    Repaired,

    /// <summary>Waste (design 7.6): final; the passport stays active until the team marks it for deletion.</summary>
    Waste,

    /// <summary>
    /// The team corrects mistyped re-stated values (decision 34): the status stays, nothing joins the history, and a new
    /// version is published. Cluster admins ask the team.
    /// </summary>
    Corrected,

    /// <summary>Prepared for re-use and placed on the market again (design 7.5): "re-used", and a new linked passport follows.</summary>
    PlacedOnMarketAgain,

    /// <summary>Remanufactured (design 7.5): a new linked passport follows.</summary>
    Remanufactured,

    /// <summary>Repurposed, for a new use (design 7.5): a new linked passport follows.</summary>
    Repurposed,

    /// <summary>
    /// The team undoes a "waste" that was set by mistake (R10): the battery goes back to the status it had before, with a
    /// required reason, and a new version is published. Only the team; the history and the audit log keep both steps.
    /// </summary>
    WasteUndone,
}

/// <summary>One status change: what happened, the re-stated values, and for a repair its day (yyyy-MM-dd) and what was repaired.</summary>
public sealed record StatusChange(
    StatusEvent What,
    IReadOnlyDictionary<string, JsonNode?> Restated,
    string? RepairDate = null,
    string? RepairDescription = null,
    string? Reason = null);

/// <summary>The new version, or why nothing changed; <see cref="Keys"/> says which value was refused.</summary>
public sealed record StatusChangeResult(bool Found, PassportVersion? Version, IReadOnlyList<KeyResult> Keys, string? Problem, string? NewStatus = null);

/// <summary>
/// The status change (design 7.4, 7.6 and 7.9; decisions 15, 19 and 34; step 4b). The team or the battery's cluster
/// admins say what happened; the app sets the battery status (#22) from it, saves it with the re-stated fades and
/// lifetime through the save pipeline's status-change way in, adds a repair to the history, and publication makes the
/// new version. All or nothing: a refused value, a missing repair detail or a passport that couldn't get a new version
/// now leaves everything as it was.
/// </summary>
public sealed class StatusChangeService(
    DppStore store,
    DemoBoundary demo,
    SavePipeline pipeline,
    PublicationService publication,
    ApplicationAuditService audit,
    TimeProvider clock)
{
    public const string ChangedEvent = "dpp.status-changed";

    public const string NotYours = "only the team and the battery's cluster admins change its status";
    public const string OnlyPublished = "only a published, active passport changes status";
    public const string WasteIsFinal = "the battery is waste, which is final";
    public const string ValuesRefused = "a re-stated value was refused or missing, so nothing was saved";
    public const string RepairNeedsDate = "give the day of the repair";
    public const string RepairNotInTheFuture = "the day of the repair can't be in the future";
    public const string RepairNeedsDescription = "say what was repaired";
    public const string OnlyTheTeamCorrects = "only the team corrects the values; ask the team";
    public const string OnlyTheTeamUndoesWaste = "only the team undoes waste; ask the team";
    public const string UndoNeedsReason = "say why the waste is undone";
    public const string NotWaste = "the battery isn't waste";
    public const string WaitingForSuccessor = "the battery is waiting for its new passport; the team can cancel it";

    /// <summary>Why a passport that is no longer active changes nothing (step 4c).</summary>
    public static string NoLongerActive(string status) => SavePipeline.NoLongerChanges(status);

    /// <summary>What happened that places the battery on the market again, with a new linked passport (design 7.5).</summary>
    public static bool LeadsToNewPassport(StatusEvent what) => what is StatusEvent.PlacedOnMarketAgain or StatusEvent.Remanufactured or StatusEvent.Repurposed;
    public const string NoteNeedsText = "write the note first";
    public const string NoBattery = "the battery was not found";
    public const string NoteAddedEvent = "dpp.history-note-added";

    /// <summary>The longest repair description kept, in characters.</summary>
    public const int MaxDescription = 500;

    private const string StatusProperty = "batteryStatus";

    /// <summary>
    /// The values a status change re-states (decision 15): capacity fade, power fade, internal resistance increase and
    /// expected lifetime, and the round trip efficiency fade where the model has it (#74 is conditional).
    /// </summary>
    public static IReadOnlyList<AttributeDefinition> RestatedAttributes(BatteryModel model)
    {
        ArgumentNullException.ThrowIfNull(model);
        return AttributeCatalog.All
            .Where(attribute => attribute.Change == ChangeRule.RestatedOnStatusChange)
            .Where(attribute => attribute.Requirement != Requirement.Conditional || model.Values.ContainsKey(attribute.Property))
            .OrderBy(attribute => attribute.Number)
            .ToArray();
    }

    /// <summary>
    /// The battery status after what happened (design 7.4 and 7.9, J-15); null when it can't happen. Waste is final. A
    /// repair makes an original battery "re-used" and leaves a re-used, remanufactured or repurposed one as it is.
    /// </summary>
    public static string? NewStatus(string current, StatusEvent what)
    {
        string[] inUse = ["original", "re-used", "remanufactured", "repurposed"];
        if (what == StatusEvent.Corrected)
        {
            return inUse.Contains(current) || current == "waste" ? current : null;
        }

        if (!inUse.Contains(current))
        {
            return null;
        }

        return what switch
        {
            StatusEvent.Repaired => current == "original" ? "re-used" : current,
            StatusEvent.Waste => "waste",
            StatusEvent.PlacedOnMarketAgain => "re-used",
            StatusEvent.Remanufactured => "remanufactured",
            StatusEvent.Repurposed => "repurposed",
            _ => null,
        };
    }

    /// <summary>
    /// The status a waste battery had before the waste: that of its newest published version that isn't waste, read from
    /// the passport's own versions (so also for a waste set long ago). "original" when no version says anything else.
    /// </summary>
    private async Task<string> StatusBeforeWasteAsync(string packId, CancellationToken cancellationToken)
    {
        var attribute = AttributeCatalog.FindByProperty(StatusProperty)!;
        foreach (var version in (await store.ListVersionsAsync(packId, cancellationToken)).OrderByDescending(version => version.Number))
        {
            var parent = attribute.Section == AttributeDefinition.HeaderSection
                ? version.StaticCore[AttributeDefinition.HeaderSection]
                : version.StaticCore[AttributeDefinition.DataRoot]?[attribute.Section];
            if (parent?[attribute.Property]?["batteryStatusValues"]?.GetValue<string>() is { } status && status != "waste")
            {
                return status;
            }
        }

        return "original";
    }

    /// <summary>The battery status stored on a pack, e.g. "original"; null when it has none.</summary>
    public static string? CurrentStatus(BatteryPack pack)
    {
        ArgumentNullException.ThrowIfNull(pack);
        return pack.Values.GetValueOrDefault(StatusProperty)?.Value?["batteryStatusValues"]?.GetValue<string>();
    }

    /// <param name="preview">True for an upload's check (step 5d): every rule is checked and nothing changes.</param>
    /// <param name="source">"form", or "upload" for a status change in an uploaded file (step 5d).</param>
    public async Task<StatusChangeResult> ChangeAsync(ChangeSender sender, string packId, StatusChange change, CancellationToken cancellationToken = default, bool preview = false, string source = "form")
    {
        ArgumentNullException.ThrowIfNull(sender);
        ArgumentNullException.ThrowIfNull(change);

        var sources = await store.LoadSourcesAsync(packId, cancellationToken);
        if (sources is null)
        {
            return new StatusChangeResult(false, null, [], null);
        }

        var pack = sources.Pack;
        var mayChange = await demo.ReachesAsync(sender, pack, cancellationToken) && (sender.Kind == SenderKind.Team // finding R03
            || (sender.Kind == SenderKind.ClusterAdmin && pack.ClusterId is { } cluster && sender.ClusterIds.Contains(cluster)));
        if (!mayChange)
        {
            return Refused(NotYours);
        }

        if (change.What == StatusEvent.Corrected && sender.Kind != SenderKind.Team)
        {
            return Refused(OnlyTheTeamCorrects);
        }

        if (await store.GetActiveVersionAsync(packId, cancellationToken) is null)
        {
            return Refused(await store.PassportStatusAsync(packId, cancellationToken) is { } status && DppStatuses.IsNoLongerActive(status) ? NoLongerActive(status) : OnlyPublished);
        }

        if (pack.Succession is { LinkedAtUtc: null } && change.What != StatusEvent.Corrected)
        {
            return Refused(WaitingForSuccessor);
        }

        var from = CurrentStatus(pack) ?? "original";
        var undoReason = change.Reason?.Trim() ?? string.Empty;
        if (change.What == StatusEvent.WasteUndone)
        {
            if (sender.Kind != SenderKind.Team)
            {
                return Refused(OnlyTheTeamUndoesWaste);
            }

            if (from != "waste")
            {
                return Refused(NotWaste);
            }

            if (undoReason.Length == 0)
            {
                return Refused(UndoNeedsReason);
            }

            if (undoReason.Length > MaxDescription)
            {
                undoReason = undoReason[..MaxDescription];
            }
        }
        else if (from == "waste" && change.What != StatusEvent.Corrected)
        {
            return Refused(WasteIsFinal);
        }

        // Back to the status of the last published version before the waste (R10); "original" when there is none.
        var before = change.What == StatusEvent.WasteUndone ? await StatusBeforeWasteAsync(packId, cancellationToken) : null;
        if ((before ?? NewStatus(from, change.What)) is not { } to)
        {
            return Refused($"a battery that is {from} can't become {change.What.ToString().ToLowerInvariant()}");
        }

        RepairEntry? repair = null;
        if (change.What == StatusEvent.Repaired)
        {
            var today = DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime);
            if (!DateOnly.TryParseExact(change.RepairDate?.Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var day))
            {
                return Refused(RepairNeedsDate);
            }

            if (day > today)
            {
                return Refused(RepairNotInTheFuture);
            }

            var description = change.RepairDescription?.Trim() ?? string.Empty;
            if (description.Length == 0)
            {
                return Refused(RepairNeedsDescription);
            }

            repair = new RepairEntry
            {
                Date = day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                Description = description.Length > MaxDescription ? description[..MaxDescription] : description,
                RecordedBy = sender.Who,
                RecordedAs = sender.Kind.ToString(),
                RecordedAtUtc = clock.GetUtcNow().UtcDateTime,
            };
        }

        // The status change publishes a new version, so a passport that couldn't get one now changes nothing.
        if (await publication.CheckAsync(packId, cancellationToken) is not { CanPublish: true })
        {
            return Refused("the passport can't get a new version now; the team sees why on its battery page");
        }

        var values = new Dictionary<string, JsonNode?>(StringComparer.Ordinal)
        {
            [StatusProperty] = new JsonObject { ["batteryStatusValues"] = to },
        };
        foreach (var attribute in RestatedAttributes(sources.Model))
        {
            values[attribute.Property] = change.Restated.GetValueOrDefault(attribute.Property)?.DeepClone();
        }

        var saved = await pipeline.SaveStatusChangeAsync(sender, packId, values, cancellationToken, preview, source);
        if (preview)
        {
            return saved.Keys.All(key => key.Outcome == KeyOutcome.Saved)
                ? new StatusChangeResult(true, null, saved.Keys, null, to)
                : new StatusChangeResult(true, null, saved.Keys, ValuesRefused);
        }

        if (!saved.Saved)
        {
            return new StatusChangeResult(true, null, saved.Keys, ValuesRefused);
        }

        if (repair is not null)
        {
            await store.AddRepairAsync(packId, repair, cancellationToken);
        }

        if (LeadsToNewPassport(change.What))
        {
            await store.SetSuccessionAsync(packId, new Succession { StatusBefore = from, Status = to, StartedAtUtc = clock.GetUtcNow().UtcDateTime }, cancellationToken);
        }

        var version = await store.GetActiveVersionAsync(packId, cancellationToken);
        var serial = pack.Values.GetValueOrDefault("batterySerialNumber")?.Value?.GetValue<string>() ?? packId;
        var metadata = new BsonDocument
        {
            ["what"] = change.What.ToString(),
            ["from"] = from,
            ["to"] = to,
            ["version"] = version?.Number ?? 0,
        };
        if (repair is not null)
        {
            metadata["repairDate"] = repair.Date;
            metadata["repair"] = repair.Description;
        }

        if (change.What == StatusEvent.WasteUndone)
        {
            metadata["reason"] = undoReason;
        }

        await audit.AppendApplicationAuditEventAsync(
            ChangedEvent,
            sender.Who,
            sender.Kind.ToString(),
            source,
            change.What switch
            {
                StatusEvent.Repaired => $"Battery {serial} repaired: {repair!.Description} Status {to}.",
                StatusEvent.Corrected => $"Battery {serial}: values corrected; status {to}.",
                StatusEvent.WasteUndone => $"Battery {serial}: waste undone, back to {to}. {undoReason}",
                _ => $"Battery {serial} is now {to}.",
            },
            metadata,
            entityType: "dpp-pack",
            entityId: packId,
            cancellationToken: cancellationToken);
        return new StatusChangeResult(true, version, saved.Keys, null, to);

        static StatusChangeResult Refused(string problem) => new(true, null, [], problem);
    }

    /// <summary>
    /// Adds a note marked "Correction" to the repair history (decision 34): entries are never changed or removed, so a
    /// mistake is corrected with one of these. The team or the battery's cluster admins, on a published passport. The
    /// history isn't passport data, so no version is made. Null when it was added; otherwise why not.
    /// </summary>
    public async Task<string?> AddNoteAsync(ChangeSender sender, string packId, string? text, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(sender);
        if (await store.GetAsync<BatteryPack>(packId, cancellationToken) is not { } pack)
        {
            return NoBattery;
        }

        var mayAdd = await demo.ReachesAsync(sender, pack, cancellationToken) && (sender.Kind == SenderKind.Team // finding R03
            || (sender.Kind == SenderKind.ClusterAdmin && pack.ClusterId is { } cluster && sender.ClusterIds.Contains(cluster)));
        if (!mayAdd)
        {
            return NotYours;
        }

        if (await store.GetActiveVersionAsync(packId, cancellationToken) is null)
        {
            return await store.PassportStatusAsync(packId, cancellationToken) is { } status && DppStatuses.IsNoLongerActive(status) ? NoLongerActive(status) : OnlyPublished;
        }

        var note = text?.Trim() ?? string.Empty;
        if (note.Length == 0)
        {
            return NoteNeedsText;
        }

        var now = clock.GetUtcNow().UtcDateTime;
        var entry = new RepairEntry
        {
            Date = DateOnly.FromDateTime(now).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            Description = note.Length > MaxDescription ? note[..MaxDescription] : note,
            RecordedBy = sender.Who,
            RecordedAs = sender.Kind.ToString(),
            RecordedAtUtc = now,
            IsCorrection = true,
        };
        await store.AddRepairAsync(packId, entry, cancellationToken);
        await audit.AppendApplicationAuditEventAsync(
            NoteAddedEvent,
            sender.Who,
            sender.Kind.ToString(),
            "form",
            $"Note added to the repair history of battery {pack.Values.GetValueOrDefault("batterySerialNumber")?.Value?.GetValue<string>() ?? packId}: {entry.Description}",
            new BsonDocument { ["note"] = entry.Description },
            entityType: "dpp-pack",
            entityId: packId,
            cancellationToken: cancellationToken);
        return null;
    }
}
