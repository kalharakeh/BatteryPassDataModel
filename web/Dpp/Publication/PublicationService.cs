using BatteryPassWeb.Dpp.Access;
using BatteryPassWeb.Dpp.Assembly;
using BatteryPassWeb.Dpp.Catalog;
using BatteryPassWeb.Dpp.Editing;
using BatteryPassWeb.Dpp.Identifiers;
using BatteryPassWeb.Dpp.Signing;
using BatteryPassWeb.Dpp.Store;
using BatteryPassWeb.Services;
using MongoDB.Bson;
using MongoDB.Driver;

namespace BatteryPassWeb.Dpp.Publication;

/// <summary>What a publish attempt did for one published passport (round two, A3).</summary>
public enum PublishOutcome
{
    /// <summary>A new version is published.</summary>
    NewVersion,

    /// <summary>Its saved data equals its active version: nothing to publish.</summary>
    NothingChanged,

    /// <summary>Its saved data differs from its active version, which stays until what blocks it is filled in.</summary>
    HeldBack,

    /// <summary>Nothing blocks it, but its publication failed (an error): it stays marked and is tried again (round five).</summary>
    NotPublished,
}

/// <summary>
/// What the check of all published passports did (decision 65): how many it checked, and how many markers it set,
/// cleared and old active versions it archived, with the serial numbers of the batteries it changed (a capped list).
/// </summary>
public sealed record PassportCheckResult(int Checked, int Marked, int Cleared, int Archived, IReadOnlyList<string> Serials)
{
    /// <summary>Batteries whose check or write failed; they are left as they were, and logged (round seven, 3).</summary>
    public int Failed { get; init; }

    /// <summary>The run left early because the app is stopping (round seven, 3).</summary>
    public bool StoppedEarly { get; init; }

    /// <summary>A check was already running in this app; this one did nothing (round seven, 6).</summary>
    public bool AlreadyRunning { get; init; }

    /// <summary>Removals that had stopped half way, finished by this check (finding R08).</summary>
    public int RemovalsFinished { get; init; }

    public bool ChangedAnything => Marked + Cleared + Archived + RemovalsFinished > 0;

    /// <summary>The plain result line the battery list shows.</summary>
    public string Words
    {
        get
        {
            if (AlreadyRunning)
            {
                return "A check is already running; if it puts anything right, the audit log will say so.";
            }

            var batteries = $"{Checked} published {(Checked == 1 ? "battery" : "batteries")}";
            var what = ChangedAnything
                ? $"Checked {batteries}: {Marked} now marked as waiting, {Cleared} no longer, {Archived} old {(Archived == 1 ? "version" : "versions")} archived."
                : $"Checked {batteries}: nothing to put right.";
            var failed = Failed == 0 ? string.Empty : $" {Failed} couldn't be checked; the next check tries again.";
            failed += RemovalsFinished == 0 ? string.Empty : $" Finished removing {RemovalsFinished} {(RemovalsFinished == 1 ? "passport" : "passports")} whose removal had stopped half way.";
            var stopped = StoppedEarly ? " The app was stopping, so the check stopped early." : string.Empty;
            return what + failed + stopped;
        }
    }
}

/// <summary>One published passport after an action that can publish: what happened, its active version, and what holds it back.</summary>
public sealed record PassportOutcome(string PackId, PublishOutcome Outcome, int Version, IReadOnlyList<string> Blocking);

/// <summary>A published passport whose saved data differs from its active <see cref="Version"/>, and what blocks the new one.</summary>
/// <summary>The batteries a held load reached (<see cref="PublicationService.HoldPublications"/>).</summary>
public sealed class PublicationHold : IDisposable
{
    private readonly HashSet<string> _packIds = new(StringComparer.Ordinal);
    private bool _holding = true;

    public bool IsHolding => _holding;

    public IReadOnlyCollection<string> PackIds => _packIds;

    internal void Note(string packId) => _packIds.Add(packId);

    internal void Release() => _holding = false;

    public void Dispose() => _holding = false;
}

public sealed record WaitingChange(int Version, IReadOnlyList<string> Blocking);

/// <summary>The new version, or why there is none; <see cref="Unchanged"/> when the content equals the active version's.</summary>
public sealed record PublicationResult(PassportVersion? Version, PublicationReport? Report, string? Problem, bool Unchanged = false);

/// <summary>
/// Publishes passport versions (design 4.2, 7.1, 7.2). The team publishes a draft; after that, accepted static changes
/// publish a new version by themselves and live changes only refresh #4. Nothing a change can do makes a published
/// pack unpublishable, because the save pipeline never deletes a value and checks each one; if a republish is ever
/// blocked anyway, the active version stays and the block is logged.
/// </summary>
public sealed class PublicationService(
    DppStore store,
    DemoBoundary demo,
    PublicationChecks checks,
    PassportSigner signer,
    IIdentifierScheme identifiers,
    ApplicationAuditService audit,
    TimeProvider clock,
    BatteryIdPool? pool = null,
    ILogger<PublicationService>? logger = null,
    PassportRemoval? removal = null) : IPackChangeListener
{
    public const string PublishedEvent = "dpp.published";
    public const string BlockedEvent = "dpp.publish-blocked";
    public const string InactiveEvent = "dpp.passport-inactive";
    public const string FailedEvent = "dpp.publish-failed";
    public const string CheckedEvent = "dpp.passports-checked";

    /// <summary>The most serial numbers the check's audit entry lists.</summary>
    public const int CheckSerialsShown = 50;

    /// <summary>Why a passport that is no longer active gets no new version.</summary>
    public static string NoNewVersion(string status) => $"the passport is {DppStatuses.Words(status).ToLowerInvariant()}, so it gets no new version";

    /// <summary>
    /// The checks the pack's passport would meet if it were published now, without publishing anything (the battery
    /// page shows them); null when the pack, or a record it links to, is missing.
    /// </summary>
    public Task<PublicationReport?> CheckAsync(string packId, CancellationToken cancellationToken = default) =>
        CheckAsync(packId, null, cancellationToken);

    /// <summary>The checks as above, as if the pack also had <paramref name="added"/> (a correction that adds #19, R01).</summary>
    public async Task<PublicationReport?> CheckAsync(string packId, IReadOnlyDictionary<string, System.Text.Json.Nodes.JsonNode>? added, CancellationToken cancellationToken = default)
    {
        var sources = await store.LoadSourcesAsync(packId, cancellationToken);
        if (sources is null)
        {
            return null;
        }

        foreach (var (key, value) in added ?? new Dictionary<string, System.Text.Json.Nodes.JsonNode>())
        {
            sources.Pack.Values[key] = new Values.AttributeValue { Value = value.DeepClone(), GeneratedAtUtc = clock.GetUtcNow().UtcDateTime, Source = "form" };
        }

        var number = await store.GetLatestVersionNumberAsync(packId, cancellationToken) + 1;
        var header = new PassportHeader(identifiers.NewPassportIdentifier(PackIdentifier(sources.Pack), number), DppStatuses.Active, clock.GetUtcNow().UtcDateTime);
        return WithDemoRule(sources, checks.Check(PassportAssembler.Assemble(sources, header), sources.Model.Answers()), number);
    }

    /// <summary>
    /// The publication checks of many drafts at once, as <see cref="CheckAsync(string, CancellationToken)"/> makes them for one
    /// (the team's "To do" page, finding U05): the records are read once per collection, not once per battery. A draft
    /// whose batch, model, site or operator is missing is left out, as the single check returns nothing for it.
    /// </summary>
    public async Task<IReadOnlyDictionary<string, PublicationReport>> CheckDraftsAsync(IReadOnlyCollection<string> packIds, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(packIds);
        var reports = new Dictionary<string, PublicationReport>(StringComparer.Ordinal);
        if (packIds.Count == 0)
        {
            return reports;
        }

        var wanted = packIds.ToHashSet(StringComparer.Ordinal);
        var packs = (await store.ListAsync<BatteryPack>(cancellationToken)).Where(pack => wanted.Contains(pack.Id)).ToArray();
        var batches = (await store.ListAsync<ProductionBatch>(cancellationToken)).ToDictionary(batch => batch.Id, StringComparer.Ordinal);
        var models = (await store.ListAsync<BatteryModel>(cancellationToken)).ToDictionary(model => model.Id, StringComparer.Ordinal);
        var sites = (await store.ListAsync<ManufacturingSite>(cancellationToken)).ToDictionary(site => site.Id, StringComparer.Ordinal);
        var operators = (await store.ListAsync<EconomicOperator>(cancellationToken)).ToDictionary(item => item.Id, StringComparer.Ordinal);
        foreach (var pack in packs)
        {
            if (batches.GetValueOrDefault(pack.BatchId) is not { } batch
                || models.GetValueOrDefault(batch.ModelId) is not { } model
                || sites.GetValueOrDefault(batch.SiteId) is not { } site
                || operators.GetValueOrDefault(pack.OperatorId) is not { } economicOperator)
            {
                continue;
            }

            var sources = new PassportSources(pack, batch, model, site, economicOperator);
            var header = new PassportHeader(identifiers.NewPassportIdentifier(PackIdentifier(pack), 1), DppStatuses.Active, clock.GetUtcNow().UtcDateTime);
            reports[pack.Id] = WithDemoRule(sources, checks.Check(PassportAssembler.Assemble(sources, header), model.Answers()), 1);
        }

        return reports;
    }

    /// <summary>
    /// Whether a published passport's saved data differs from its active version: a change that was saved but held back
    /// because the passport couldn't publish (round two, A). Null when it has no active version or nothing differs.
    /// </summary>
    public async Task<WaitingChange?> WaitingAsync(string packId, CancellationToken cancellationToken = default)
    {
        if (await store.GetActiveVersionAsync(packId, cancellationToken) is not { } active
            || await store.LoadSourcesAsync(packId, cancellationToken) is not { } sources)
        {
            return null;
        }

        var passport = PassportAssembler.Assemble(sources, new PassportHeader(active.DigitalProductPassportId, DppStatuses.Active, clock.GetUtcNow().UtcDateTime));
        if (PassportStaticCore.ContentHash(PassportStaticCore.Extract(passport)) == PassportStaticCore.ContentHash(active.StaticCore))
        {
            return null;
        }

        var report = WithDemoRule(sources, checks.Check(passport, sources.Model.Answers()), active.Number + 1);
        return new WaitingChange(active.Number, report.Blocking);
    }

    /// <summary>
    /// Decision 65: one check of every published battery, off any request (the background service, or the team's "Check
    /// all passports now"). It loads in bulk what the team's list loads, packs first (with their markers' tokens) and the
    /// data after; compares each battery's saved data with its active version as its page does; sets or clears the marker
    /// (a clear only while the marker is still the one read, so a set never loses to it); archives all but the highest
    /// active version; writes one audit entry when it changed anything; and never publishes. Reads: none per battery.
    /// </summary>
    public async Task<PassportCheckResult> ReconcileAsync(string who, CancellationToken cancellationToken = default)
    {
        // One check at a time in this app: a press during a run, or the hourly run during a press, does nothing and says
        // so (round seven, 6). Another app instance may run its own: that is harmless.
        if (!await checkGate.WaitAsync(0, CancellationToken.None))
        {
            logger?.LogInformation("A passport check asked for by {Who} found one already running; it did nothing.", who);
            return new PassportCheckResult(0, 0, 0, 0, []) { AlreadyRunning = true };
        }

        try
        {
            return await ReconcileCoreAsync(who, cancellationToken);
        }
        finally
        {
            checkGate.Release();
        }
    }

    /// <summary>The single check in this app at a time (round seven, 6).</summary>
    private readonly SemaphoreSlim checkGate = new(1, 1);

    private async Task<PassportCheckResult> ReconcileCoreAsync(string who, CancellationToken cancellationToken)
    {
        // Finding R08: a removal that stopped half way is finished here, and logged; a battery being removed is passed by.
        var removalsFinished = 0;
        if (removal is not null)
        {
            foreach (var packId in await removal.UnfinishedAsync(cancellationToken))
            {
                try
                {
                    await removal.FinishAsync(packId, CancellationToken.None);
                    removalsFinished++;
                    logger?.LogInformation("The passport check finished removing pack {PackId}, whose removal had stopped half way.", packId);
                }
                catch (Exception exception) when (exception is not OperationCanceledException)
                {
                    logger?.LogError(exception, "The passport check couldn't finish removing pack {PackId}; the next check tries again.", packId);
                }
            }
        }

        var removedIds = (await store.ListAsync<RemovedPassport>(cancellationToken)).Select(marker => marker.Id).ToHashSet(StringComparer.Ordinal);
        var packs = await store.ListAsync<BatteryPack>(cancellationToken);
        // Only the active versions in full, the content compared; the battery, number and status of all (round seven, 2).
        var newest = (await store.ListVersionSummariesAsync(cancellationToken)).GroupBy(version => version.PackId, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.MaxBy(version => version.Number)!, StringComparer.Ordinal);
        logger?.LogDebug("The passport check read the versions of {Count} batteries.", newest.Count);
        var actives = (await store.ListAllActiveVersionsAsync(cancellationToken))
            .GroupBy(version => version.PackId, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.OrderByDescending(version => version.Number).ToArray(), StringComparer.Ordinal);
        var batches = (await store.ListAsync<ProductionBatch>(cancellationToken)).ToDictionary(item => item.Id, StringComparer.Ordinal);
        var models = (await store.ListAsync<BatteryModel>(cancellationToken)).ToDictionary(item => item.Id, StringComparer.Ordinal);
        var sites = (await store.ListAsync<ManufacturingSite>(cancellationToken)).ToDictionary(item => item.Id, StringComparer.Ordinal);
        var operators = (await store.ListAsync<EconomicOperator>(cancellationToken)).ToDictionary(item => item.Id, StringComparer.Ordinal);
        logger?.LogDebug("The passport check read the active versions of {Count} batteries.", actives.Count);

        var now = clock.GetUtcNow().UtcDateTime;
        int checkedCount = 0, marked = 0, cleared = 0, archived = 0, failed = 0;
        var stoppedEarly = false;
        var serials = new List<string>();
        try
        {
            foreach (var pack in packs)
            {
                // The app is stopping: leave the loop; what was done is still written to the log below (round seven, 3).
                if (cancellationToken.IsCancellationRequested)
                {
                    stoppedEarly = true;
                    break;
                }

                if (removedIds.Contains(pack.Id) || !actives.TryGetValue(pack.Id, out var versions))
                {
                    continue; // being removed (finding R08), a draft, or no longer active
                }

                if (!newest.TryGetValue(pack.Id, out var newestVersion))
                {
                    continue; // published between the two reads: the next check sees it whole
                }

                checkedCount++;
                try
                {
                    var (packMarked, packCleared, packArchived) = await CheckOneAsync(pack, versions, newestVersion, batches, models, sites, operators, now);
                    marked += packMarked;
                    cleared += packCleared;
                    archived += packArchived;
                    if (packMarked + packCleared + packArchived > 0 && serials.Count < CheckSerialsShown)
                    {
                        serials.Add(pack.Values.GetValueOrDefault("batterySerialNumber")?.Value?.ToString() ?? pack.Id);
                    }
                }
                catch (Exception exception) when (exception is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
                {
                    // One battery's failed write doesn't end the run: it is logged, and the next check tries again (round seven, 3).
                    failed++;
                    logger?.LogError(exception, "The passport check couldn't put battery {PackId} right.", pack.Id);
                }
            }
        }
        finally
        {
            // Written for what was done, also when the run ended early; the counts are what changed (round seven, 3).
            if (marked + cleared + archived + failed + removalsFinished > 0)
            {
                await WriteCheckedAsync(who, new PassportCheckResult(checkedCount, marked, cleared, archived, serials) { Failed = failed, StoppedEarly = stoppedEarly, RemovalsFinished = removalsFinished });
            }
        }

        return new PassportCheckResult(checkedCount, marked, cleared, archived, serials) { Failed = failed, StoppedEarly = stoppedEarly, RemovalsFinished = removalsFinished };
    }

    /// <summary>
    /// The check's audit entry. A failure to write it is logged, never thrown: the changes are made by then, and the
    /// button mustn't answer an error page for them.
    /// </summary>
    private async Task WriteCheckedAsync(string who, PassportCheckResult done)
    {
        try
        {
            await audit.AppendApplicationAuditEventAsync(
                CheckedEvent,
                who,
                "system",
                "check",
                done.Words,
                new BsonDocument
                {
                    ["checked"] = done.Checked,
                    ["marked"] = done.Marked,
                    ["cleared"] = done.Cleared,
                    ["archived"] = done.Archived,
                    ["failed"] = done.Failed,
                    ["stoppedEarly"] = done.StoppedEarly,
                    ["removalsFinished"] = done.RemovalsFinished,
                    ["batteries"] = new BsonArray(done.Serials),
                },
                entityType: "dpp-passports",
                entityId: "all",
                cancellationToken: CancellationToken.None);
        }
        catch (Exception exception)
        {
            logger?.LogError(exception, "The passport check couldn't write its audit entry: {Words}", done.Words);
        }
    }

    /// <summary>
    /// The check of one published battery: what it marked, cleared and archived, as the writes report it (round seven, 3).
    /// </summary>
    private async Task<(int Marked, int Cleared, int Archived)> CheckOneAsync(
        BatteryPack pack,
        PassportVersion[] versions,
        VersionSummary newest,
        IReadOnlyDictionary<string, ProductionBatch> batches,
        IReadOnlyDictionary<string, BatteryModel> models,
        IReadOnlyDictionary<string, ManufacturingSite> sites,
        IReadOnlyDictionary<string, EconomicOperator> operators,
        DateTime now)
    {
        var none = CancellationToken.None;

        // The newest version is no longer active (invalid, marked for deletion, inactive) with a lower one still active,
        // as a mark or a hand-over left it before round six: the lower ones are archived, so readers serve the newest
        // version's state, and no marker is set: a passport no longer active takes no new version (round seven, 1).
        if (newest.DppStatus != DppStatuses.Active)
        {
            return (0, 0, (int)await store.ArchiveOtherVersionsAsync(pack.Id, newest.Id, newest.Number, none));
        }

        var active = versions[0];
        var archived = versions.Length > 1 ? (int)await store.ArchiveOtherVersionsAsync(pack.Id, active.Id, active.Number, none) : 0;

        var differs = true; // a battery whose records can't be put together is marked: the safe side
        if (batches.GetValueOrDefault(pack.BatchId) is { } batch
            && models.GetValueOrDefault(batch.ModelId) is { } model
            && sites.GetValueOrDefault(batch.SiteId) is { } site
            && operators.GetValueOrDefault(pack.OperatorId) is { } economicOperator)
        {
            try
            {
                var passport = PassportAssembler.Assemble(new PassportSources(pack, batch, model, site, economicOperator), new PassportHeader(active.DigitalProductPassportId, DppStatuses.Active, now));
                differs = PassportStaticCore.ContentHash(PassportStaticCore.Extract(passport)) != PassportStaticCore.ContentHash(active.StaticCore);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                differs = true;
            }
        }

        if (differs && pack.ChangesWaitingSinceUtc is null)
        {
            return ((int)await store.MarkChangesWaitingAsync([pack.Id], now, none), 0, archived);
        }

        if (!differs && pack.ChangesWaitingSinceUtc is not null)
        {
            return (0, await store.ClearChangesWaitingAsync(pack.Id, pack.ChangesWaitingToken, none) ? 1 : 0, archived);
        }

        return (0, 0, archived);
    }

    /// <summary>
    /// Publishes a published passport's waiting change when nothing blocks it (round three, 4): after a mark is undone, or
    /// when the team presses "Publish the waiting changes". Null for a passport with no active version. A blocked one
    /// keeps its version (and its marker); one with nothing waiting reports nothing changed.
    /// </summary>
    public async Task<PassportOutcome?> PublishWaitingAsync(ChangeSender sender, string packId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(sender);
        if (await store.GetActiveVersionAsync(packId, cancellationToken) is not { } active
            || await store.GetAsync<BatteryPack>(packId, cancellationToken) is not { } seen)
        {
            return null;
        }

        // The marker's token is read before the data, so a clear never removes a change marked meanwhile (round four, 2).
        if (await WaitingAsync(packId, cancellationToken) is { } waiting)
        {
            if (waiting.Blocking.Count > 0)
            {
                await store.MarkChangesWaitingAsync([packId], clock.GetUtcNow().UtcDateTime, CancellationToken.None);
                return new PassportOutcome(packId, PublishOutcome.HeldBack, active.Number, waiting.Blocking);
            }

            await RepublishOneAsync(sender, packId, active, CancellationToken.None, withWaiting: true);
        }
        else
        {
            await store.ClearChangesWaitingAsync(packId, seen.ChangesWaitingToken, CancellationToken.None);
        }

        return (await OutcomesAsync([(packId, active.Number)], CancellationToken.None)).Single();
    }

    /// <summary>
    /// The marker of a waiting change goes once the passport shows its saved data (round three, 1), only while it holds
    /// the token read with the pack, before the rest of the data (round four, 2).
    /// </summary>
    private async Task ClearWaitingAsync(BatteryPack pack, CancellationToken cancellationToken)
    {
        if (pack.ChangesWaitingSinceUtc is not null)
        {
            await store.ClearChangesWaitingAsync(pack.Id, pack.ChangesWaitingToken, cancellationToken);
        }
    }

    /// <summary>
    /// Puts a battery's marker right after its page worked out the truth (round three, 1): set while a change waits,
    /// cleared when none does.
    /// </summary>
    /// <param name="tokenSeen">The marker's token as the page read it with the pack, before the data (round four, 2).</param>
    public async Task MarkWaitingAsync(string packId, bool waiting, string? tokenSeen, CancellationToken cancellationToken = default)
    {
        // A write that puts the marker right isn't stopped by the request's token either (round five).
        if (waiting)
        {
            await store.MarkChangesWaitingAsync([packId], clock.GetUtcNow().UtcDateTime, CancellationToken.None);
        }
        else
        {
            await store.ClearChangesWaitingAsync(packId, tokenSeen, CancellationToken.None);
        }
    }

    /// <summary>
    /// What an action did for each published passport it reached, from the version each had before (round two, A3): a
    /// new version, nothing to publish, or held back with what blocks it.
    /// </summary>
    public async Task<IReadOnlyList<PassportOutcome>> OutcomesAsync(IEnumerable<(string PackId, int VersionBefore)> passports, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(passports);
        var outcomes = new List<PassportOutcome>();
        foreach (var (packId, before) in passports)
        {
            var now = (await store.GetActiveVersionAsync(packId, cancellationToken))?.Number ?? before;
            if (now > before)
            {
                outcomes.Add(new PassportOutcome(packId, PublishOutcome.NewVersion, now, []));
            }
            else if (await WaitingOrFailedAsync(packId, cancellationToken) is { } waiting)
            {
                outcomes.Add(waiting.Blocking.Count == 0
                    ? new PassportOutcome(packId, PublishOutcome.NotPublished, now, [])
                    : new PassportOutcome(packId, PublishOutcome.HeldBack, now, waiting.Blocking));
            }
            else
            {
                outcomes.Add(new PassportOutcome(packId, PublishOutcome.NothingChanged, now, []));
            }
        }

        return outcomes;
    }

    /// <summary>A passport's waiting change, or an empty one when even working that out fails (its record is broken).</summary>
    private async Task<WaitingChange?> WaitingOrFailedAsync(string packId, CancellationToken cancellationToken)
    {
        try
        {
            return await WaitingAsync(packId, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return new WaitingChange(0, []);
        }
    }

    /// <summary>
    /// A real battery ID on demo product data blocks the first publication (U16). A battery already published, made before
    /// that rule, keeps its status changes and new versions: for it the sentence is only a warning.
    /// </summary>
    private static PublicationReport WithDemoRule(PassportSources sources, PublicationReport report, int versionNumber) =>
        !DemoDataRule.Conflicts(sources)
            ? ExampleValues.Check(sources, report, versionNumber)
            : versionNumber == 1
                ? report with { Blocking = [.. report.Blocking, DemoDataRule.MessageFor(sources.Batch, sources.Model, sources.Operator)] }
                : report with { Warnings = [.. report.Warnings, DemoDataRule.Message] };

    /// <summary>The battery ID (#6) a version's passport ID names (6d); the pack's own ID for a pack without one.</summary>
    private static string PackIdentifier(BatteryPack pack) =>
        pack.Values.GetValueOrDefault("uniqueProductIdentifier")?.Value?.GetValue<string>() ?? pack.Id;

    public async Task<PublicationResult> PublishAsync(ChangeSender sender, string packId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(sender);
        if (sender.Kind != SenderKind.Team)
        {
            return new PublicationResult(null, null, "only the team publishes passports");
        }

        if (!await demo.ReachesAsync(sender, packId, cancellationToken)) // finding R03
        {
            return new PublicationResult(null, null, DemoScope.RealBattery);
        }

        // A passport with no active version is either a draft or no longer active (step 4c): only a draft is published.
        if (await store.PassportStatusAsync(packId, cancellationToken) is { } status && DppStatuses.IsNoLongerActive(status))
        {
            return new PublicationResult(null, null, NoNewVersion(status));
        }

        return await PublishCoreAsync(packId, sender, retryOnConflict: true, cancellationToken);
    }

    /// <summary>
    /// Makes a passport inactive once its new linked passport is published or linked (design 7.5, J-13): its active
    /// version's DPP status becomes "inactive" and #4 moves to now. No new version: #2 isn't signed (4.3).
    /// </summary>
    public async Task InactivateAsync(ChangeSender sender, string packId, string why, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(sender);
        if (await store.GetActiveVersionAsync(packId, cancellationToken) is not { } active)
        {
            return;
        }

        var now = clock.GetUtcNow().UtcDateTime;
        await store.ArchiveOtherVersionsAsync(packId, active.Id, active.Number, CancellationToken.None); // only one active version first (round six, 2)
        await store.SetVersionStatusAsync(active.Id, DppStatuses.Inactive, now, cancellationToken);
        if (await store.GetAsync<BatteryPack>(packId, cancellationToken) is { Succession: { } succession })
        {
            succession.LinkedAtUtc = now;
            await store.SetSuccessionAsync(packId, succession, cancellationToken);
        }

        await audit.AppendApplicationAuditEventAsync(
            InactiveEvent,
            sender.Who,
            sender.Kind.ToString(),
            "publication",
            $"The passport of pack {packId} is inactive: {why}",
            new BsonDocument { ["version"] = active.Number, ["why"] = why },
            entityType: "dpp-pack",
            entityId: packId,
            cancellationToken: cancellationToken);
    }

    public async Task ValuesSavedAsync(TargetKind target, string targetId, IReadOnlyCollection<string> keys, ChangeSender sender, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(keys);
        ArgumentNullException.ThrowIfNull(sender);
        var packIds = target switch
        {
            TargetKind.Pack => [targetId],
            TargetKind.Model => await store.FindPackIdsUsingAsync(modelId: targetId, cancellationToken: cancellationToken),
            TargetKind.Site => await store.FindPackIdsUsingAsync(siteId: targetId, cancellationToken: cancellationToken),
            TargetKind.Batch => await store.FindPackIdsUsingAsync(batchId: targetId, cancellationToken: cancellationToken),
            TargetKind.Operator => await store.FindPackIdsUsingAsync(operatorId: targetId, cancellationToken: cancellationToken),
            _ => [],
        };
        var staticChange = keys.Any(key => AttributeCatalog.FindByProperty(key)?.Change != ChangeRule.Live);

        // A static change: the save pipeline marked every published passport it reaches before writing and again after
        // (round five), and each one's marker goes when its new version (or "nothing changed") is done. One battery's
        // failure never stops the others: it stays marked, and the error is logged with it.
        foreach (var packId in packIds)
        {
            try
            {
                await ValuesSavedOneAsync(packId, staticChange, sender, cancellationToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                await FailedAsync(sender, packId, exception);
            }
        }
    }

    /// <summary>Logs a battery whose publication failed; it stays marked as waiting and is tried again (round five).</summary>
    private Task FailedAsync(ChangeSender sender, string packId, Exception exception) =>
        audit.AppendApplicationAuditEventAsync(
            FailedEvent,
            sender.Who,
            "system",
            "publication",
            $"A new version of pack {packId} couldn't be published: {exception.GetType().Name}; it stays marked as waiting",
            new BsonDocument { ["error"] = exception.Message },
            entityType: "dpp-pack",
            entityId: packId,
            cancellationToken: CancellationToken.None);

    private async Task ValuesSavedOneAsync(string packId, bool staticChange, ChangeSender sender, CancellationToken cancellationToken)
    {
        var active = await store.GetActiveVersionAsync(packId, cancellationToken);
        if (active is null)
        {
            return; // a draft, or a passport no longer active: publication is the team's step
        }

        if (!staticChange)
        {
            // #4 is when the update was made available, not when it was measured (file, #4).
            await store.SetVersionLastUpdatedAsync(active.Id, clock.GetUtcNow().UtcDateTime, cancellationToken);

            // A change held back because the passport couldn't publish follows once this live value clears the block
            // (round two, A1): from the feed, an upload, the API or a cluster admin alike. Only a battery marked as
            // having a change waiting is looked at further: one read for the others (round three, 1).
            if (await store.GetAsync<BatteryPack>(packId, cancellationToken) is { ChangesWaitingSinceUtc: not null } marked)
            {
                var waiting = await WaitingAsync(packId, cancellationToken);
                if (waiting is null)
                {
                    // Nothing differs after all: the marker was wrong, and goes (round four, 4).
                    await store.ClearChangesWaitingAsync(packId, marked.ChangesWaitingToken, cancellationToken);
                }
                else if (waiting.Blocking.Count == 0)
                {
                    await RepublishOneAsync(sender, packId, active, cancellationToken, withWaiting: true);
                }
            }

            return;
        }

        await RepublishOneAsync(sender, packId, active, cancellationToken);
    }

    /// <summary>
    /// Brings published passports up to date after something that isn't a value changed what they may publish: a model's
    /// answers to its three questions (R01). A passport whose content is unchanged keeps its version; one that still
    /// can't publish keeps it too, and the block is logged.
    /// </summary>
    public async Task RepublishAsync(ChangeSender sender, IEnumerable<string> packIds, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(sender);
        ArgumentNullException.ThrowIfNull(packIds);
        foreach (var packId in packIds)
        {
            try
            {
                if (await store.GetActiveVersionAsync(packId, CancellationToken.None) is { } active)
                {
                    await RepublishOneAsync(sender, packId, active, CancellationToken.None);
                }
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                await FailedAsync(sender, packId, exception); // the others go on (round five)
            }
        }
    }

    /// <summary>The load whose publications are held back, in the flow of the code that began it; nobody else's saves are held (FU1).</summary>
    private static readonly AsyncLocal<PublicationHold?> Held = new();

    /// <summary>
    /// Holds back the new versions that the saves made by the code that calls this would publish (FU1): a battery they
    /// reach is only noted, its waiting marker stays set, and <see cref="PublishHeldAsync"/> publishes each noted battery
    /// once, with all of the changes. Call it from the method that saves, not from a helper that has awaited it, and
    /// dispose the result in a <c>using</c>. Without <see cref="PublishHeldAsync"/> (a load that stops) the batteries
    /// stay marked and are published by the next save that reaches them or by "Publish the waiting changes".
    /// </summary>
    public PublicationHold HoldPublications()
    {
        var hold = new PublicationHold();
        Held.Value = hold;
        return hold;
    }

    /// <summary>Ends the hold and publishes each battery it noted, once (a battery whose passport is unchanged keeps its version).</summary>
    public async Task PublishHeldAsync(PublicationHold hold, ChangeSender sender)
    {
        ArgumentNullException.ThrowIfNull(hold);
        ArgumentNullException.ThrowIfNull(sender);
        hold.Release();
        await RepublishAsync(sender, hold.PackIds);
    }

    private async Task RepublishOneAsync(ChangeSender sender, string packId, PassportVersion active, CancellationToken cancellationToken, bool withWaiting = false)
    {
        if (Held.Value is { IsHolding: true } hold)
        {
            hold.Note(packId); // published once, after the whole load is saved (FU1)
            return;
        }

        var result = await PublishCoreAsync(packId, sender, retryOnConflict: true, cancellationToken, withWaiting);
        if (result.Problem is not null)
        {
            // The marker is the publish attempt's own: set only when something differs (round four, 4).
            await audit.AppendApplicationAuditEventAsync(
                BlockedEvent,
                sender.Who,
                "system",
                "publication",
                result.Report is null
                    ? $"No new version of pack {packId}: {result.Problem}" // e.g. marked or inactive meanwhile (round five)
                    : $"A new version of pack {packId} was blocked; version {active.Number} stays active",
                new BsonDocument { ["blocking"] = new BsonArray(result.Report?.Blocking ?? [result.Problem]) },
                entityType: "dpp-pack",
                entityId: packId,
                cancellationToken: cancellationToken);
        }
    }

    /// <param name="sender">Who the new version is published for; the log names them (a cluster admin's status change, step 4b).</param>
    /// <param name="withWaiting">
    /// True when the version carries a change that was waiting (a live value cleared the way, a mark was undone, or the
    /// team pressed "Publish the waiting changes"): the log names who set it off and says so (round three, 5).
    /// </param>
    private async Task<PublicationResult> PublishCoreAsync(string packId, ChangeSender sender, bool retryOnConflict, CancellationToken cancellationToken, bool withWaiting = false)
    {
        var who = sender.Who;

        // The version number and the active version are read before the data (round four, 3): a slower publisher with
        // older data then collides on the number (unique per pack) with a faster one, and starts again from the new state,
        // instead of publishing its older data as a later version.
        var active = await store.GetActiveVersionAsync(packId, cancellationToken);
        var number = await store.GetLatestVersionNumberAsync(packId, cancellationToken) + 1;
        var sources = await store.LoadSourcesAsync(packId, cancellationToken);
        if (sources is null)
        {
            return new PublicationResult(null, null, $"pack {packId}, or a record it links to, was not found");
        }

        var now = clock.GetUtcNow().UtcDateTime;
        var passportId = identifiers.NewPassportIdentifier(PackIdentifier(sources.Pack), number);
        var passport = PassportAssembler.Assemble(sources, new PassportHeader(passportId, DppStatuses.Active, now));
        var report = WithDemoRule(sources, checks.Check(passport, sources.Model.Answers()), number);

        // Nothing differs from the active version: nothing to publish and nothing waiting, whatever the checks say. The
        // content is compared before the answer "can't be published", so no false marker is set (round four, 4).
        // The comparison is with the active version read after the pack and its token (round six, 3).
        var core = PassportStaticCore.Extract(passport);
        if (await store.GetActiveVersionAsync(packId, cancellationToken) is { } activeNow
            && PassportStaticCore.ContentHash(activeNow.StaticCore) == PassportStaticCore.ContentHash(core))
        {
            await ClearWaitingAsync(sources.Pack, cancellationToken); // the passport shows its saved data
            return new PublicationResult(activeNow, report, null, Unchanged: true);
        }

        if (!report.CanPublish)
        {
            // A published passport whose saved data differs and can't publish: its change waits, marked for the team's
            // list and the next live save. Every caller (a save, the team's or the API's publish) goes through here.
            if (active is not null)
            {
                await store.MarkChangesWaitingAsync([packId], now, CancellationToken.None);
            }

            return new PublicationResult(null, report, "the passport can't be published yet; see the blocking items");
        }

        var version = new PassportVersion
        {
            Id = $"version-{Guid.NewGuid():N}",
            PackId = packId,
            Number = number,
            DigitalProductPassportId = passportId,
            DppStatus = DppStatuses.Active,
            PublishedAtUtc = now,
            LastUpdatedUtc = now,
            PublishedBy = who,
            StaticCore = core,
            Proof = signer.Sign(core, now),
        };
        // A mark (invalid, for deletion) or an inactive state can land while the version is being made: checked again
        // just before inserting (round four, 3).
        if (await store.PassportStatusAsync(packId, cancellationToken) is { } status && DppStatuses.IsNoLongerActive(status))
        {
            return new PublicationResult(null, null, NoNewVersion(status));
        }

        // A battery being removed never gets a version again (finding R08): its marker comes before any deletion.
        if (await store.GetAsync<RemovedPassport>(packId, cancellationToken) is not null)
        {
            return new PublicationResult(null, null, "the passport was removed after the battery was recycled or exported");
        }

        // From the first write onward nothing runs on the request's token (round five). A FIRST publication is marked
        // before its version becomes active: its data was loaded before a model save that its new version may lack, and
        // that save's own marks couldn't reach a battery that had no version yet.
        var none = CancellationToken.None;
        if (active is null)
        {
            await store.MarkChangesWaitingAsync([packId], now, none);
        }

        try
        {
            await store.InsertAsync(version, none);
        }
        catch (MongoWriteException exception) when (retryOnConflict && exception.WriteError.Category == ServerErrorCategory.DuplicateKey)
        {
            // Another save published this pack at the same moment: start again from the new state, once.
            return await PublishCoreAsync(packId, sender, retryOnConflict: false, cancellationToken, withWaiting);
        }

        await store.ArchiveOtherVersionsAsync(packId, version.Id, version.Number, none);
        if (active is null)
        {
            // The first version is active: its marker goes only when the data read now (after the marker) shows nothing
            // differs, and only while it is still the marker read (round five).
            await SettleFirstVersionAsync(packId);
        }
        else
        {
            await ClearWaitingAsync(sources.Pack, none);
        }

        if (version.Number == 1 && pool is not null)
        {
            await pool.MarkPublishedAsync(packId, none); // its released ID is fixed for life (6d)
        }

        if (version.Number == 1 && sources.Pack.SuccessorOf is { } predecessor)
        {
            await InactivateAsync(sender, predecessor, $"its new passport {passportId} is published", none);
        }

        await audit.AppendApplicationAuditEventAsync(
            PublishedEvent,
            who,
            sender.Kind.ToString(),
            "publication",
            withWaiting ? $"Version {version.Number} of pack {packId} published with the changes that were waiting" : $"Version {version.Number} of pack {packId} published",
            new BsonDocument
            {
                ["passportId"] = passportId,
                ["number"] = version.Number,
                ["warnings"] = new BsonArray(report.Warnings),
                ["withWaitingChanges"] = withWaiting,
            },
            entityType: "dpp-pack",
            entityId: packId,
            cancellationToken: none);
        return new PublicationResult(version, report, null);
    }

    /// <summary>
    /// After a first version is made active: the marker set before it is cleared only if the saved data, read after the
    /// marker, equals the new version; otherwise it stays (a model save crossed the publication). Round five.
    /// </summary>
    private async Task SettleFirstVersionAsync(string packId)
    {
        var none = CancellationToken.None;
        if (await store.GetAsync<BatteryPack>(packId, none) is { } pack && await WaitingAsync(packId, none) is null)
        {
            await store.ClearChangesWaitingAsync(packId, pack.ChangesWaitingToken, none);
        }
    }
}
