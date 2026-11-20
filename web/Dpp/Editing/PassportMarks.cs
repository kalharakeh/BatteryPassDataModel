using BatteryPassWeb.Dpp.Access;
using BatteryPassWeb.Dpp.Store;
using BatteryPassWeb.Services;
using MongoDB.Bson;

using BatteryPassWeb.Dpp.Publication;

namespace BatteryPassWeb.Dpp.Editing;

/// <summary>
/// "Invalid" and "marked for deletion" (design 7.6 and 7.7, J-8, decision 34; step 4c-2). The team or market
/// surveillance marks an active passport invalid, with a reason; the team marks a waste battery's passport for deletion,
/// after recycling or export. Either sets the active version's DPP status (#2) in place, with no new version, and the
/// passport then takes no more changes. Only the team undoes either; the reason stays in the audit log. Null when done.
/// </summary>
public sealed class PassportMarks(DppStore store, DemoBoundary demo, ApplicationAuditService audit, TimeProvider clock, PublicationService? publication = null)
{
    public const string InvalidEvent = "dpp.passport-invalid";
    public const string DeletionEvent = "dpp.passport-marked-for-deletion";
    public const string UndoneEvent = "dpp.passport-mark-undone";

    public const string NoBattery = "the battery was not found";
    public const string OnlyActive = "only an active passport is marked";
    public const string NotYoursToMarkInvalid = "only the team and market surveillance mark a passport invalid";
    public const string NeedsReason = "give the reason";
    public const string OnlyTheTeamMarksForDeletion = "only the team marks a passport for deletion";
    public const string OnlyWaste = "only a waste battery's passport is marked for deletion, after recycling or export";
    public const string OnlyTheTeamUndoes = "only the team undoes it";
    public const string NothingToUndo = "the passport is neither invalid nor marked for deletion";

    /// <summary>The longest reason kept, in characters.</summary>
    public const int MaxReason = 500;

    public async Task<string?> MarkInvalidAsync(ChangeSender sender, string packId, string? reason, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(sender);
        if (sender.Kind is not (SenderKind.Team or SenderKind.MarketSurveillance))
        {
            return NotYoursToMarkInvalid;
        }

        var why = reason?.Trim() ?? string.Empty;
        if (why.Length == 0)
        {
            return NeedsReason;
        }

        return await MarkAsync(sender, packId, DppStatuses.Invalid, why.Length > MaxReason ? why[..MaxReason] : why, cancellationToken);
    }

    /// <param name="reason">Why, as the team gives it in the "Mark…" window (redesign R5); optional.</param>
    public async Task<string?> MarkForDeletionAsync(ChangeSender sender, string packId, string? reason = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(sender);
        if (sender.Kind != SenderKind.Team)
        {
            return OnlyTheTeamMarksForDeletion;
        }

        if (await store.GetAsync<BatteryPack>(packId, cancellationToken) is { } pack && StatusChangeService.CurrentStatus(pack) != "waste"
            && await store.GetActiveVersionAsync(packId, cancellationToken) is not null)
        {
            return OnlyWaste;
        }

        var why = reason?.Trim() ?? string.Empty;
        return await MarkAsync(sender, packId, DppStatuses.MarkedForDeletion, why.Length == 0 ? null : why.Length > MaxReason ? why[..MaxReason] : why, cancellationToken);
    }

    /// <summary>The team puts an invalid or marked-for-deletion passport back to active (J-8).</summary>
    /// <summary>Why "Undo" is refused on a battery whose removal has started (finding R08, round three).</summary>
    public const string BeingRemoved = "the battery's passport is being removed, so it can't be made active again";

    public async Task<string?> UndoAsync(ChangeSender sender, string packId, CancellationToken cancellationToken = default) =>
        (await UndoWithOutcomeAsync(sender, packId, cancellationToken)).Problem;

    /// <summary>
    /// As <see cref="UndoAsync"/>, and a change saved while the passport was marked is published once it is active again,
    /// when nothing blocks it (round three, 4); <c>Published</c> says what the passport got.
    /// </summary>
    public async Task<(string? Problem, PassportOutcome? Published)> UndoWithOutcomeAsync(ChangeSender sender, string packId, CancellationToken cancellationToken = default)
    {
        if (await UndoCoreAsync(sender, packId, cancellationToken) is { } problem)
        {
            return (problem, null);
        }

        return (null, publication is null ? null : await publication.PublishWaitingAsync(sender, packId, CancellationToken.None));
    }

    private async Task<string?> UndoCoreAsync(ChangeSender sender, string packId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(sender);
        if (await store.GetAsync<BatteryPack>(packId, cancellationToken) is not { } pack)
        {
            return NoBattery;
        }

        if (sender.Kind != SenderKind.Team || !await demo.ReachesAsync(sender, pack, cancellationToken)) // finding R03
        {
            return OnlyTheTeamUndoes;
        }

        // A battery whose removal has started stays removed (finding R08, round three): its addresses already answer 410.
        if (await store.GetAsync<RemovedPassport>(pack.Id, cancellationToken) is not null)
        {
            return BeingRemoved;
        }

        var latest = await store.GetLatestVersionAsync(packId, cancellationToken);
        if (latest is null || latest.DppStatus is not (DppStatuses.Invalid or DppStatuses.MarkedForDeletion))
        {
            return NothingToUndo;
        }

        // Round five, the rule: changes saved while the passport was marked may be missing from it, so the battery is
        // marked FIRST, before its version becomes active again; from here nothing runs on the request's token. The
        // publish that follows clears the marker only if nothing differs.
        var now = clock.GetUtcNow().UtcDateTime;
        cancellationToken = CancellationToken.None;
        await store.MarkChangesWaitingAsync([packId], now, cancellationToken);
        await store.SetVersionStatusAsync(latest.Id, DppStatuses.Active, now, cancellationToken);
        await store.SetMarkAsync(packId, null, cancellationToken);
        await audit.AppendApplicationAuditEventAsync(
            UndoneEvent,
            sender.Who,
            sender.Kind.ToString(),
            "form",
            $"Battery {Serial(pack)}: {DppStatuses.Words(latest.DppStatus).ToLowerInvariant()} undone; the passport is active again",
            new BsonDocument
            {
                ["was"] = latest.DppStatus,
                ["reason"] = (BsonValue?)pack.Mark?.Reason ?? BsonNull.Value,
                ["markedBy"] = (BsonValue?)pack.Mark?.By ?? BsonNull.Value,
            },
            entityType: "dpp-pack",
            entityId: packId,
            cancellationToken: cancellationToken);
        return null;
    }

    private async Task<string?> MarkAsync(ChangeSender sender, string packId, string status, string? reason, CancellationToken cancellationToken)
    {
        if (await store.GetAsync<BatteryPack>(packId, cancellationToken) is not { } pack)
        {
            return NoBattery;
        }

        if (!await demo.ReachesAsync(sender, pack, cancellationToken)) // a demo account marks only passports inside the demo (finding R03)
        {
            return DemoScope.RealBattery;
        }

        if (await store.GetActiveVersionAsync(packId, cancellationToken) is not { } active)
        {
            return OnlyActive;
        }

        var now = clock.GetUtcNow().UtcDateTime;
        await store.ArchiveOtherVersionsAsync(packId, active.Id, active.Number, CancellationToken.None); // only one active version first (round six, 2)
        await store.SetVersionStatusAsync(active.Id, status, now, cancellationToken);
        await store.SetMarkAsync(packId, new PassportMark { Status = status, Reason = reason, By = sender.Who, ByRole = sender.Kind.ToString(), AtUtc = now }, cancellationToken);
        await audit.AppendApplicationAuditEventAsync(
            status == DppStatuses.Invalid ? InvalidEvent : DeletionEvent,
            sender.Who,
            sender.Kind.ToString(),
            "form",
            status == DppStatuses.Invalid
                ? $"Battery {Serial(pack)}: passport marked invalid. {reason}"
                : $"Battery {Serial(pack)}: passport marked for deletion",
            new BsonDocument { ["version"] = active.Number, ["reason"] = (BsonValue?)reason ?? BsonNull.Value },
            entityType: "dpp-pack",
            entityId: packId,
            cancellationToken: cancellationToken);
        return null;
    }

    private static string Serial(BatteryPack pack) =>
        pack.Values.GetValueOrDefault("batterySerialNumber")?.Value is System.Text.Json.Nodes.JsonValue value && value.TryGetValue<string>(out var text) ? text : pack.Id;
}
