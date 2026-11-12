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
/// passport and every earlier passport of the same battery), with everything the app holds about it. What stays is one
/// marker per removed passport, so each of its IDs answers 410 and is never used again, and ONE audit entry.
/// </summary>
/// <remarks>
/// Round three's rule: everything a removal will do is written down first, in one write, before anything is deleted,
/// and finishing reads only what was written down. Every marker of the chain is written in one bulk write and carries
/// the whole plan (the members and their IDs, the documents and upload records it takes, the audit entry it owes), so
/// any one marker finishes the whole removal. Removing is writing the markers and then finishing; "Finish removing",
/// the hourly check and a demo seed run the same finish, which is safe to run any number of times.
/// </remarks>
public sealed class PassportRemoval(DppStore store, DemoBoundary demo, DocumentStore documents, ApplicationAuditService audit, TimeProvider clock, ILogger<PassportRemoval>? logger = null)
{
    public const string RemovedEvent = "dpp.passport-removed";

    /// <summary>What every address of a removed passport answers (410).</summary>
    public const string Gone = "This battery's passport no longer exists. It was removed after the battery was recycled or exported.";

    /// <summary>What a change to a battery whose removal has started is told.</summary>
    public const string BeingRemoved = "This battery's passport is being removed.";

    public const string OnlyTheTeam = "only the team removes a passport";
    public const string NotMarked = "only a passport marked for deletion can be removed";
    public const string EarlierStillActive = "an earlier passport of this battery is still active, so the battery can't be removed yet";
    public const string TypeTheId = "type the battery's ID exactly as it is shown, to confirm";
    public const string NoBattery = "the battery was not found";

    /// <summary>The steps of a finish, in order; each is logged when done (and a test can fail right after one).</summary>
    public static readonly string[] Steps = ["markers", "audit entry", "versions", "documents", "uploads", "audit rows", "packs"];

    /// <summary>The marker of the removed passport that answered to this ID; null when none had it.</summary>
    public Task<RemovedPassport?> FindAsync(string? id, CancellationToken cancellationToken = default) => store.FindRemovedAsync(id, cancellationToken);

    /// <summary>A pack's battery ID (#6) as stored; empty when it has none.</summary>
    public static string BatteryIdOf(BatteryPack pack)
    {
        ArgumentNullException.ThrowIfNull(pack);
        return pack.Values.GetValueOrDefault("uniqueProductIdentifier")?.Value?.GetValue<string>() ?? string.Empty;
    }

    /// <summary>What removing this battery would take, for the window; null when it can't be removed.</summary>
    public async Task<RemovalPlan?> PlanAsync(string packId, CancellationToken cancellationToken = default) =>
        (await CheckPlanAsync(packId, cancellationToken)).Plan;

    /// <summary>The plan, or why there is none: the newest passport must be marked for deletion, every earlier one inactive.</summary>
    public async Task<(RemovalPlan? Plan, string? Problem)> CheckPlanAsync(string packId, CancellationToken cancellationToken = default)
    {
        if (await store.GetAsync<BatteryPack>(packId, cancellationToken) is not { } pack)
        {
            return (null, NoBattery);
        }

        if (await store.GetLatestVersionAsync(pack.Id, cancellationToken) is not { DppStatus: DppStatuses.MarkedForDeletion })
        {
            return (null, NotMarked);
        }

        var chain = new List<BatteryPack> { pack };
        var seen = new HashSet<string>(StringComparer.Ordinal) { pack.Id };
        for (var at = pack.SuccessorOf; at is not null && seen.Add(at) && await store.GetAsync<BatteryPack>(at, cancellationToken) is { } earlier; at = earlier.SuccessorOf)
        {
            // The chain holds no active passport (round three): an earlier one still active is a battery in use.
            if (await store.GetActiveVersionAsync(earlier.Id, cancellationToken) is not null)
            {
                return (null, EarlierStillActive);
            }

            chain.Add(earlier);
        }

        return (new RemovalPlan(pack, chain, BatteryIdOf(pack)), null);
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
        if (await store.GetAsync<RemovedPassport>(pack.Id, cancellationToken) is { } started)
        {
            await FinishCoreAsync(started);
            return null;
        }

        var (plan, problem) = await CheckPlanAsync(pack.Id, cancellationToken);
        if (plan is null)
        {
            return problem;
        }

        var typed = typedId?.Trim() ?? string.Empty;
        if (plan.BatteryId.Length == 0 || typed.Length == 0
            || !(typed.Equals(plan.BatteryId, StringComparison.OrdinalIgnoreCase) || typed.Equals(IdentifierForms.Short(plan.BatteryId), StringComparison.OrdinalIgnoreCase)))
        {
            return TypeTheId;
        }

        // Written down first, in one write: every marker of the chain, each carrying the whole plan.
        var markers = await WriteDownAsync(sender.Who, plan);
        await FinishCoreAsync(markers[0]);
        return null;
    }

    /// <summary>
    /// Finishes the removal this pack belongs to, from its marker alone. Nothing happens without a marker. True when
    /// something was still left (or owed, as the audit entry).
    /// </summary>
    public async Task<bool> FinishAsync(string packId, CancellationToken cancellationToken = default)
    {
        if (await store.GetAsync<RemovedPassport>(packId, cancellationToken) is not { } marker)
        {
            return false;
        }

        var left = await IsUnfinishedAsync(marker, CancellationToken.None);
        await FinishCoreAsync(marker);
        return left;
    }

    /// <summary>The removals that stopped half way: one marker of each (by its newest pack's ID, or its own).</summary>
    public async Task<IReadOnlyList<string>> UnfinishedAsync(CancellationToken cancellationToken = default, bool demoOnly = false)
    {
        var unfinished = new List<string>();
        var newest = new HashSet<string>(StringComparer.Ordinal);
        foreach (var marker in await store.ListAsync<RemovedPassport>(cancellationToken))
        {
            if ((!demoOnly || marker.IsDemo) && await IsUnfinishedAsync(marker, cancellationToken) && newest.Add(marker.Newest ?? marker.Id))
            {
                unfinished.Add(marker.Id);
            }
        }

        return unfinished;
    }

    private async Task<bool> IsUnfinishedAsync(RemovedPassport marker, CancellationToken cancellationToken)
    {
        if (!marker.AuditWritten)
        {
            return true;
        }

        foreach (var member in Members(marker))
        {
            if (await store.IsAnythingLeftAsync(member.PackId, cancellationToken)
                || (await documents.ListOwnedAsync(TargetKind.Pack, member.PackId, cancellationToken)).Count > 0
                || await store.GetAsync<RemovedPassport>(member.PackId, cancellationToken) is null)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Writes down what the removal will do, before anything is deleted: the chain's members and their IDs, the documents
    /// (withdrawn accident reports included) and upload records it takes, the rows that only records point at, and the
    /// audit entry it owes. One bulk write of every marker; each names the whole chain.
    /// </summary>
    private async Task<IReadOnlyList<RemovedPassport>> WriteDownAsync(string who, RemovalPlan plan)
    {
        var none = CancellationToken.None;
        var now = clock.GetUtcNow().UtcDateTime;
        var packIds = plan.Chain.Select(item => item.Id).ToList();
        var serial = BatteryPages.Text(plan.Newest, "batterySerialNumber");
        var serialIsTheirs = serial is not null && (await store.FindPacksBySerialNumberAsync(serial, none)).All(other => packIds.Contains(other.Id));
        var members = new List<RemovalMember>();
        int versions = 0;
        foreach (var item in plan.Chain)
        {
            var itemVersions = await store.ListVersionsAsync(item.Id, none);
            versions += itemVersions.Count;
            var batteryId = BatteryIdOf(item);
            var ids = new[] { item.Id, batteryId, IdentifierForms.Short(batteryId) }
                .Concat(itemVersions.SelectMany(version => new[] { version.DigitalProductPassportId, IdentifierForms.Short(version.DigitalProductPassportId) }))
                .Concat(item.Id == plan.Newest.Id && serialIsTheirs ? [serial!] : []) // the serial number answers 410 too, unless another battery has it
                .Where(id => id.Length > 0)
                .Distinct(StringComparer.Ordinal)
                .ToList();
            members.Add(new RemovalMember { PackId = item.Id, BatteryId = batteryId, Ids = ids, IsDemo = item.IsDemo });
        }

        // What only records point at: the documents (owned now, or withdrawn earlier), the upload records that name the
        // battery (found by pack or by an ID as typed), and the drafts a cancelled successor left in the audit log.
        var names = members.SelectMany(member => member.Ids).Concat(serial is null ? [] : [serial]).Distinct(StringComparer.Ordinal).ToList();
        var documentIds = new List<string>();
        foreach (var item in plan.Chain)
        {
            documentIds.AddRange((await documents.ListOwnedAsync(TargetKind.Pack, item.Id, none)).Select(document => document.Id));
        }

        documentIds.AddRange(await store.WithdrawnDocumentIdsAsync(packIds, none));
        var uploadIds = await store.UploadIdsNamingAsync(packIds, names, none);
        var uploadLines = await store.CountUploadLinesAsync(uploadIds, packIds, names, none);
        var others = await store.SuccessorDraftIdsAsync(packIds, none);
        var earlier = plan.EarlierIds.Select(IdentifierForms.Short).ToArray();
        var documentCount = documentIds.Distinct(StringComparer.Ordinal).Count();
        var entry = new RemovalAudit
        {
            Who = who,
            BatteryId = plan.BatteryId,
            EarlierBatteryIds = [.. plan.EarlierIds],
            Versions = versions,
            Documents = documentCount,
            UploadLines = uploadLines,
            Message = $"Passport of battery {IdentifierForms.Short(plan.BatteryId)} removed after the battery was recycled or exported"
                + (earlier.Length == 0 ? string.Empty : $", with its earlier {(earlier.Length == 1 ? "passport" : "passports")} {string.Join(", ", earlier)}")
                + $": {versions} {(versions == 1 ? "version" : "versions")}, {documentCount} {(documentCount == 1 ? "document" : "documents")}, {uploadLines} upload {(uploadLines == 1 ? "line" : "lines")}.",
        };

        var markers = members.Select(member => new RemovedPassport
        {
            Id = member.PackId,
            BatteryId = member.BatteryId,
            Ids = member.Ids,
            RemovedAtUtc = now,
            RemovedBy = who,
            IsDemo = member.IsDemo,
            Newest = plan.Newest.Id,
            Members = members,
            Names = names,
            DocumentIds = documentIds.Distinct(StringComparer.Ordinal).ToList(),
            UploadIds = [.. uploadIds],
            OtherEntityIds = [.. others],
            Audit = entry,
        }).ToList();
        await store.AddRemovalMarkersAsync(markers, none);
        Step(plan.Newest.Id, "markers");
        return markers;
    }

    /// <summary>
    /// Finishes a removal from one marker, by what it wrote down; each step is safe to run again. The markers, then the
    /// audit entry it owes, then the data, every pack last.
    /// </summary>
    private async Task FinishCoreAsync(RemovedPassport marker)
    {
        var none = CancellationToken.None;
        var members = Members(marker);
        var packIds = members.Select(member => member.PackId).ToList();
        var newest = marker.Newest ?? marker.Id;

        // 1. Every marker of the chain (a crash can't have left one out, but a marker from before round three can).
        await store.AddMissingRemovalMarkersAsync(members.Select(member => MarkerFor(marker, member)).ToList(), none);
        Step(newest, "markers");

        // 2. The one audit entry, owed until it is written (decision 72).
        if (marker.Audit is { } entry && !await store.RemovalEntryExistsAsync(newest, none))
        {
            await audit.AppendApplicationAuditEventAsync(
                RemovedEvent,
                entry.Who,
                nameof(SenderKind.Team),
                "form",
                entry.Message,
                new BsonDocument
                {
                    ["batteryId"] = entry.BatteryId,
                    ["earlierBatteryIds"] = new BsonArray(entry.EarlierBatteryIds),
                    ["versions"] = entry.Versions,
                    ["documents"] = entry.Documents,
                    ["uploadLines"] = entry.UploadLines,
                },
                entityType: "dpp-removal",
                entityId: newest,
                batteryId: entry.BatteryId,
                cancellationToken: none);
        }

        await store.SetRemovalAuditWrittenAsync(packIds, none);
        Step(newest, "audit entry");

        // 3. The data: versions, values, documents and files, upload lines, hand-overs and checks, audit rows; packs last.
        foreach (var packId in packIds)
        {
            await store.DeleteVersionsAsync(packId, none);
            await store.ClearPackDataAsync(packId, none); // values, live values, modules, repairs, marks: the pack stays empty until last
        }

        Step(newest, "versions");
        foreach (var packId in packIds)
        {
            await documents.DeleteOwnedAsync(TargetKind.Pack, packId, none);
        }

        await documents.DeleteManyAsync(marker.DocumentIds, none); // anything written down that is still there
        Step(newest, "documents");
        await store.RemoveUploadLinesAsync(marker.UploadIds, packIds, marker.Names, none); // only the records this removal named
        Step(newest, "uploads");
        foreach (var packId in packIds)
        {
            await store.DeleteHandOversAndChecksAsync(packId, none);
        }

        var goneUploads = marker.UploadIds.Except(await store.ExistingUploadIdsAsync(marker.UploadIds, none), StringComparer.Ordinal);
        await store.DeleteAuditRowsAsync(
            [.. packIds, .. marker.DocumentIds, .. goneUploads, .. marker.OtherEntityIds],
            [.. members.SelectMany(member => member.Ids), .. marker.Names],
            RemovedEvent,
            none);
        await store.PullFromCheckEntriesAsync(marker.Names, none); // the check's own entries list serial numbers
        Step(newest, "audit rows");
        foreach (var packId in packIds)
        {
            await store.DeleteAsync<BatteryPack>(packId, none);
        }

        Step(newest, "packs");
    }

    /// <summary>The chain this marker names; a marker from before round three names only itself.</summary>
    private static IReadOnlyList<RemovalMember> Members(RemovedPassport marker) =>
        marker.Members.Count > 0 ? marker.Members : [new RemovalMember { PackId = marker.Id, BatteryId = marker.BatteryId, Ids = marker.Ids, IsDemo = marker.IsDemo }];

    private static RemovedPassport MarkerFor(RemovedPassport from, RemovalMember member) => new()
    {
        Id = member.PackId,
        BatteryId = member.BatteryId,
        Ids = member.Ids,
        RemovedAtUtc = from.RemovedAtUtc,
        RemovedBy = from.RemovedBy,
        IsDemo = member.IsDemo,
        Newest = from.Newest ?? from.Id,
        Members = from.Members,
        Names = from.Names,
        DocumentIds = from.DocumentIds,
        UploadIds = from.UploadIds,
        OtherEntityIds = from.OtherEntityIds,
        Audit = from.Audit,
        AuditWritten = from.AuditWritten,
    };

    private void Step(string packId, string step) => logger?.LogDebug("Removal of {PackId}: {Step} done.", packId, step);
}
