using BatteryPassWeb.Dpp.Access;
using BatteryPassWeb.Dpp.Store;
using BatteryPassWeb.Services;
using MongoDB.Bson;

namespace BatteryPassWeb.Dpp.Editing;

/// <summary>
/// "Invalid" and "marked for deletion" (design 7.6 and 7.7, J-8, decision 34; step 4c-2). The team or market
/// surveillance marks an active passport invalid, with a reason; the team marks a waste battery's passport for deletion,
/// after recycling or export. Either sets the active version's DPP status (#2) in place, with no new version, and the
/// passport then takes no more changes. Only the team undoes either; the reason stays in the audit log. Null when done.
/// </summary>
public sealed class PassportMarks(DppStore store, DemoBoundary demo, ApplicationAuditService audit, TimeProvider clock)
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
    public async Task<string?> UndoAsync(ChangeSender sender, string packId, CancellationToken cancellationToken = default)
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

        var latest = await store.GetLatestVersionAsync(packId, cancellationToken);
        if (latest is null || latest.DppStatus is not (DppStatuses.Invalid or DppStatuses.MarkedForDeletion))
        {
            return NothingToUndo;
        }

        var now = clock.GetUtcNow().UtcDateTime;
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
