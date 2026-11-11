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
}

/// <summary>One published passport after an action that can publish: what happened, its active version, and what holds it back.</summary>
public sealed record PassportOutcome(string PackId, PublishOutcome Outcome, int Version, IReadOnlyList<string> Blocking);

/// <summary>A published passport whose saved data differs from its active <see cref="Version"/>, and what blocks the new one.</summary>
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
    BatteryIdPool? pool = null) : IPackChangeListener
{
    public const string PublishedEvent = "dpp.published";
    public const string BlockedEvent = "dpp.publish-blocked";
    public const string InactiveEvent = "dpp.passport-inactive";

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
    /// Publishes a published passport's waiting change when nothing blocks it (round three, 4): after a mark is undone, or
    /// when the team presses "Publish the waiting changes". Null for a passport with no active version. A blocked one
    /// keeps its version (and its marker); one with nothing waiting reports nothing changed.
    /// </summary>
    public async Task<PassportOutcome?> PublishWaitingAsync(ChangeSender sender, string packId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(sender);
        if (await store.GetActiveVersionAsync(packId, cancellationToken) is not { } active)
        {
            return null;
        }

        if (await WaitingAsync(packId, cancellationToken) is { } waiting)
        {
            if (waiting.Blocking.Count > 0)
            {
                await store.SetChangesWaitingAsync(packId, clock.GetUtcNow().UtcDateTime, cancellationToken);
                return new PassportOutcome(packId, PublishOutcome.HeldBack, active.Number, waiting.Blocking);
            }

            await RepublishOneAsync(sender, packId, active, cancellationToken, afterLiveValue: true);
        }
        else
        {
            await store.SetChangesWaitingAsync(packId, null, cancellationToken);
        }

        return (await OutcomesAsync([(packId, active.Number)], cancellationToken)).Single();
    }

    /// <summary>The marker of a waiting change goes once the passport shows its saved data (round three, 1).</summary>
    private async Task ClearWaitingAsync(BatteryPack pack, CancellationToken cancellationToken)
    {
        if (pack.ChangesWaitingSinceUtc is not null)
        {
            await store.SetChangesWaitingAsync(pack.Id, null, cancellationToken);
        }
    }

    /// <summary>
    /// Puts a battery's marker right after its page worked out the truth (round three, 1): set while a change waits,
    /// cleared when none does.
    /// </summary>
    public Task<bool> MarkWaitingAsync(string packId, bool waiting, CancellationToken cancellationToken = default) =>
        store.SetChangesWaitingAsync(packId, waiting ? clock.GetUtcNow().UtcDateTime : null, cancellationToken);

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
            else if (await WaitingAsync(packId, cancellationToken) is { } waiting)
            {
                outcomes.Add(new PassportOutcome(packId, PublishOutcome.HeldBack, now, waiting.Blocking));
            }
            else
            {
                outcomes.Add(new PassportOutcome(packId, PublishOutcome.NothingChanged, now, []));
            }
        }

        return outcomes;
    }

    /// <summary>
    /// A real battery ID on demo product data blocks the first publication (U16). A battery already published, made before
    /// that rule, keeps its status changes and new versions: for it the sentence is only a warning.
    /// </summary>
    private static PublicationReport WithDemoRule(PassportSources sources, PublicationReport report, int versionNumber) =>
        !DemoDataRule.Conflicts(sources)
            ? report
            : versionNumber == 1
                ? report with { Blocking = [.. report.Blocking, DemoDataRule.Message] }
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

        foreach (var packId in packIds)
        {
            var active = await store.GetActiveVersionAsync(packId, cancellationToken);
            if (active is null)
            {
                continue; // a draft: publication is the team's step
            }

            if (!staticChange)
            {
                // #4 is when the update was made available, not when it was measured (file, #4).
                await store.SetVersionLastUpdatedAsync(active.Id, clock.GetUtcNow().UtcDateTime, cancellationToken);

                // A change held back because the passport couldn't publish follows once this live value clears the block
                // (round two, A1): from the feed, an upload, the API or a cluster admin alike. Only a battery marked as
                // having a change waiting is looked at further: one read for the others (round three, 1).
                if (await store.GetAsync<BatteryPack>(packId, cancellationToken) is { ChangesWaitingSinceUtc: not null }
                    && await WaitingAsync(packId, cancellationToken) is { Blocking.Count: 0 })
                {
                    await RepublishOneAsync(sender, packId, active, cancellationToken, afterLiveValue: true);
                }

                continue;
            }

            await RepublishOneAsync(sender, packId, active, cancellationToken);
        }
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
            if (await store.GetActiveVersionAsync(packId, cancellationToken) is { } active)
            {
                await RepublishOneAsync(sender, packId, active, cancellationToken);
            }
        }
    }

    private async Task RepublishOneAsync(ChangeSender sender, string packId, PassportVersion active, CancellationToken cancellationToken, bool afterLiveValue = false)
    {
        var result = await PublishCoreAsync(packId, sender, retryOnConflict: true, cancellationToken, afterLiveValue);
        if (result.Problem is not null)
        {
            // The saved change waits: the marker tells the team's list and the next live save (round three, 1).
            await store.SetChangesWaitingAsync(packId, clock.GetUtcNow().UtcDateTime, cancellationToken);
            await audit.AppendApplicationAuditEventAsync(
                BlockedEvent,
                sender.Who,
                "system",
                "publication",
                $"A new version of pack {packId} was blocked; version {active.Number} stays active",
                new BsonDocument { ["blocking"] = new BsonArray(result.Report?.Blocking ?? [result.Problem]) },
                entityType: "dpp-pack",
                entityId: packId,
                cancellationToken: cancellationToken);
        }
    }

    /// <param name="sender">Who the new version is published for; the log names them (a cluster admin's status change, step 4b).</param>
    /// <param name="afterLiveValue">True when a live value cleared the way for a change that was waiting: the log says so (round three, 5).</param>
    private async Task<PublicationResult> PublishCoreAsync(string packId, ChangeSender sender, bool retryOnConflict, CancellationToken cancellationToken, bool afterLiveValue = false)
    {
        var who = sender.Who;
        var sources = await store.LoadSourcesAsync(packId, cancellationToken);
        if (sources is null)
        {
            return new PublicationResult(null, null, $"pack {packId}, or a record it links to, was not found");
        }

        var now = clock.GetUtcNow().UtcDateTime;
        var number = await store.GetLatestVersionNumberAsync(packId, cancellationToken) + 1;
        var passportId = identifiers.NewPassportIdentifier(PackIdentifier(sources.Pack), number);
        var passport = PassportAssembler.Assemble(sources, new PassportHeader(passportId, DppStatuses.Active, now));
        var report = WithDemoRule(sources, checks.Check(passport, sources.Model.Answers()), number);
        if (!report.CanPublish)
        {
            return new PublicationResult(null, report, "the passport can't be published yet; see the blocking items");
        }

        var core = PassportStaticCore.Extract(passport);
        var active = await store.GetActiveVersionAsync(packId, cancellationToken);
        if (active is not null && PassportStaticCore.ContentHash(active.StaticCore) == PassportStaticCore.ContentHash(core))
        {
            await ClearWaitingAsync(sources.Pack, cancellationToken); // the passport shows its saved data
            return new PublicationResult(active, report, null, Unchanged: true);
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
        try
        {
            await store.InsertAsync(version, cancellationToken);
        }
        catch (MongoWriteException exception) when (retryOnConflict && exception.WriteError.Category == ServerErrorCategory.DuplicateKey)
        {
            // Another save published this pack at the same moment: start again from the new state, once.
            return await PublishCoreAsync(packId, sender, retryOnConflict: false, cancellationToken);
        }

        await store.ArchiveOtherVersionsAsync(packId, version.Id, cancellationToken);
        await ClearWaitingAsync(sources.Pack, cancellationToken);
        if (version.Number == 1 && pool is not null)
        {
            await pool.MarkPublishedAsync(packId, cancellationToken); // its released ID is fixed for life (6d)
        }

        if (version.Number == 1 && sources.Pack.SuccessorOf is { } predecessor)
        {
            await InactivateAsync(sender, predecessor, $"its new passport {passportId} is published", cancellationToken);
        }

        await audit.AppendApplicationAuditEventAsync(
            PublishedEvent,
            who,
            sender.Kind.ToString(),
            "publication",
            $"Version {version.Number} of pack {packId} published",
            new BsonDocument
            {
                ["passportId"] = passportId,
                ["number"] = version.Number,
                ["warnings"] = new BsonArray(report.Warnings),
            },
            entityType: "dpp-pack",
            entityId: packId,
            cancellationToken: cancellationToken);
        return new PublicationResult(version, report, null);
    }
}
