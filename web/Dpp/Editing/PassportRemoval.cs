using BatteryPassWeb.Dpp.Access;
using BatteryPassWeb.Dpp.Documents;
using BatteryPassWeb.Dpp.Identifiers;
using BatteryPassWeb.Dpp.Pages;
using BatteryPassWeb.Dpp.Store;
using BatteryPassWeb.Services;
using MongoDB.Bson;

namespace BatteryPassWeb.Dpp.Editing;

/// <summary>What a removal takes: the marked passport and every earlier passport of the same battery, newest first.</summary>
public sealed record RemovalPlan(BatteryPack Newest, IReadOnlyList<BatteryPack> Chain, string BatteryId)
{
    /// <summary>The earlier passports' battery IDs, which go with it (decision 73).</summary>
    public IReadOnlyList<string> EarlierIds => Chain.Skip(1).Select(PassportRemoval.BatteryIdOf).Where(id => id.Length > 0).ToArray();
}

/// <summary>
/// Finding R08 (the manager's decisions 72 and 73): a recycled or exported battery's passport ceases to exist. For a
/// battery marked for deletion the team removes it after typing the battery's ID. The whole chain goes (the marked
/// passport and every earlier passport of the same battery), with everything the app holds about it: versions, values,
/// documents and their files, hand-overs, kept upload checks, its lines in the upload log and every audit row. What stays
/// is one marker per removed passport, so each of its IDs answers 410 and is never used again, and ONE audit entry.
/// </summary>
/// <remarks>
/// The order makes a removal that stops half way finishable from the marker alone: the markers, then the audit entry (it
/// says the removal started and what it takes), then the data, and each pack last. Every reader asks the marker before
/// anything else, so the addresses answer 410 from the first write on. "Finish removing" on the battery's page and the
/// hourly passport check finish a removal that stopped (<see cref="FinishAsync(string, CancellationToken)"/>).
/// </remarks>
public sealed class PassportRemoval(DppStore store, DemoBoundary demo, DocumentStore documents, ApplicationAuditService audit, TimeProvider clock)
{
    public const string RemovedEvent = "dpp.passport-removed";

    /// <summary>What every address of a removed passport answers (410).</summary>
    public const string Gone = "This battery's passport no longer exists. It was removed after the battery was recycled or exported.";

    public const string OnlyTheTeam = "only the team removes a passport";
    public const string NotMarked = "only a passport marked for deletion can be removed";
    public const string TypeTheId = "type the battery's ID exactly as it is shown, to confirm";
    public const string NoBattery = "the battery was not found";

    /// <summary>The marker of the removed passport that answered to this ID; null when none had it.</summary>
    public Task<RemovedPassport?> FindAsync(string? id, CancellationToken cancellationToken = default) => store.FindRemovedAsync(id, cancellationToken);

    /// <summary>A pack's battery ID (#6) as stored; empty when it has none.</summary>
    public static string BatteryIdOf(BatteryPack pack)
    {
        ArgumentNullException.ThrowIfNull(pack);
        return pack.Values.GetValueOrDefault("uniqueProductIdentifier")?.Value?.GetValue<string>() ?? string.Empty;
    }

    /// <summary>What removing this battery would take, for the window; null when it can't be removed.</summary>
    public async Task<RemovalPlan?> PlanAsync(string packId, CancellationToken cancellationToken = default)
    {
        if (await store.GetAsync<BatteryPack>(packId, cancellationToken) is not { } pack
            || await store.GetLatestVersionAsync(pack.Id, cancellationToken) is not { DppStatus: DppStatuses.MarkedForDeletion })
        {
            return null;
        }

        var chain = new List<BatteryPack> { pack };
        var seen = new HashSet<string>(StringComparer.Ordinal) { pack.Id };
        for (var at = pack.SuccessorOf; at is not null && seen.Add(at) && await store.GetAsync<BatteryPack>(at, cancellationToken) is { } earlier; at = earlier.SuccessorOf)
        {
            chain.Add(earlier);
        }

        return new RemovalPlan(pack, chain, BatteryIdOf(pack));
    }

    /// <summary>Removes the passport and its earlier ones. Null when done; otherwise why not, as a refusal ("Not removed: …").</summary>
    public async Task<string?> RemoveAsync(ChangeSender sender, string packId, string? typedId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(sender);
        if (sender.Kind != SenderKind.Team)
        {
            return OnlyTheTeam;
        }

        if (await store.GetAsync<BatteryPack>(packId, cancellationToken) is not { } pack || !await demo.ReachesAsync(sender, pack, cancellationToken))
        {
            return NoBattery;
        }

        // A removal that stopped half way is finished, whatever is typed: it was confirmed when it started.
        if (await store.GetAsync<RemovedPassport>(pack.Id, cancellationToken) is not null)
        {
            await FinishAsync(pack.Id, cancellationToken);
            return null;
        }

        if (await PlanAsync(pack.Id, cancellationToken) is not { } plan)
        {
            return NotMarked;
        }

        var typed = typedId?.Trim() ?? string.Empty;
        if (plan.BatteryId.Length == 0 || typed.Length == 0
            || !(typed.Equals(plan.BatteryId, StringComparison.OrdinalIgnoreCase) || typed.Equals(IdentifierForms.Short(plan.BatteryId), StringComparison.OrdinalIgnoreCase)))
        {
            return TypeTheId;
        }

        // From the first write on nothing stops half way because a tab closed: no request token.
        var none = CancellationToken.None;
        var now = clock.GetUtcNow().UtcDateTime;
        var chainIds = plan.Chain.Select(item => item.Id).ToHashSet(StringComparer.Ordinal);
        var serial = BatteryPages.Text(pack, "batterySerialNumber");
        var serialIsTheirs = serial is not null
            && (await store.FindPacksBySerialNumberAsync(serial, none)).All(other => chainIds.Contains(other.Id));
        int versions = 0, files = 0, uploads = 0;
        var markers = new List<RemovedPassport>();
        foreach (var item in plan.Chain)
        {
            var itemVersions = await store.ListVersionsAsync(item.Id, none);
            versions += itemVersions.Count;
            files += (await documents.ListOwnedAsync(TargetKind.Pack, item.Id, none)).Count;
            uploads += await store.CountUploadLinesAsync(item.Id, none);
            var batteryId = BatteryIdOf(item);
            var ids = new[] { item.Id, batteryId, IdentifierForms.Short(batteryId) }
                .Concat(itemVersions.SelectMany(version => new[] { version.DigitalProductPassportId, IdentifierForms.Short(version.DigitalProductPassportId) }))
                .Concat(item.Id == pack.Id && serialIsTheirs ? [serial!] : []) // the serial number answers 410 too, unless another battery has it
                .Where(id => id.Length > 0)
                .Distinct(StringComparer.Ordinal)
                .ToList();
            markers.Add(new RemovedPassport { Id = item.Id, BatteryId = batteryId, Ids = ids, RemovedAtUtc = now, RemovedBy = sender.Who, IsDemo = item.IsDemo, Newest = pack.Id });
        }

        // 1. The markers: every address answers 410 from here on, and the removal can be finished from them alone.
        foreach (var marker in markers)
        {
            await store.AddRemovedAsync(marker, none);
        }

        // 2. The one audit entry the app keeps about the battery (decision 72): who, when, its IDs and what goes.
        var shortId = IdentifierForms.Short(plan.BatteryId);
        var earlier = plan.EarlierIds.Select(IdentifierForms.Short).ToArray();
        await audit.AppendApplicationAuditEventAsync(
            RemovedEvent,
            sender.Who,
            sender.Kind.ToString(),
            "form",
            $"Passport of battery {shortId} removed after the battery was recycled or exported"
                + (earlier.Length == 0 ? string.Empty : $", with its earlier {(earlier.Length == 1 ? "passport" : "passports")} {string.Join(", ", earlier)}")
                + $": {versions} {(versions == 1 ? "version" : "versions")}, {files} {(files == 1 ? "document" : "documents")}, {uploads} upload {(uploads == 1 ? "line" : "lines")}.",
            new BsonDocument
            {
                ["batteryId"] = plan.BatteryId,
                ["earlierBatteryIds"] = new BsonArray(plan.EarlierIds),
                ["versions"] = versions,
                ["documents"] = files,
                ["uploadLines"] = uploads,
            },
            entityType: "dpp-removal",
            entityId: pack.Id,
            batteryId: plan.BatteryId,
            cancellationToken: none);

        // 3. The data, each pack last.
        foreach (var marker in markers)
        {
            await FinishAsync(marker, none);
        }

        return null;
    }

    /// <summary>
    /// Finishes the removal of this pack from its marker: deletes whatever is left of it, the pack last. Nothing happens
    /// without a marker. True when something was left.
    /// </summary>
    public async Task<bool> FinishAsync(string packId, CancellationToken cancellationToken = default)
    {
        if (await store.GetAsync<RemovedPassport>(packId, cancellationToken) is not { } marker)
        {
            return false;
        }

        var left = await store.IsAnythingLeftAsync(packId, CancellationToken.None);
        await FinishAsync(marker, CancellationToken.None);
        return left;
    }

    /// <summary>The removals that stopped half way: markers whose pack, versions or documents are still there.</summary>
    public async Task<IReadOnlyList<string>> UnfinishedAsync(CancellationToken cancellationToken = default)
    {
        var unfinished = new List<string>();
        foreach (var marker in await store.ListAsync<RemovedPassport>(cancellationToken))
        {
            if (await store.IsAnythingLeftAsync(marker.Id, cancellationToken) || (await documents.ListOwnedAsync(TargetKind.Pack, marker.Id, cancellationToken)).Count > 0)
            {
                unfinished.Add(marker.Id);
            }
        }

        return unfinished;
    }

    private async Task FinishAsync(RemovedPassport marker, CancellationToken none)
    {
        var documentIds = (await documents.ListOwnedAsync(TargetKind.Pack, marker.Id, none)).Select(document => document.Id).ToArray();
        await store.DeleteVersionsAsync(marker.Id, none);
        await store.ClearPackDataAsync(marker.Id, none); // values, live values, modules, repairs, marks: the pack stays empty until last
        await documents.DeleteOwnedAsync(TargetKind.Pack, marker.Id, none); // accident reports and their files
        var uploadIds = await store.RemoveUploadLinesAsync(marker.Id, none); // the upload records left without a line go too
        await store.DeleteHandOversAndChecksAsync(marker.Id, none);
        await store.DeleteAuditRowsAsync(marker.Id, marker.Ids, [.. documentIds, .. uploadIds], RemovedEvent, none);
        await store.DeleteAsync<BatteryPack>(marker.Id, none);
    }
}
