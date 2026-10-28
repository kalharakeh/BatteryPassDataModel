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
    PublicationChecks checks,
    PassportSigner signer,
    IIdentifierScheme identifiers,
    ApplicationAuditService audit,
    TimeProvider clock) : IPackChangeListener
{
    public const string PublishedEvent = "dpp.published";
    public const string BlockedEvent = "dpp.publish-blocked";

    public Task<PublicationResult> PublishAsync(ChangeSender sender, string packId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(sender);
        return sender.Kind == SenderKind.Team
            ? PublishCoreAsync(packId, sender.Who, retryOnConflict: true, cancellationToken)
            : Task.FromResult(new PublicationResult(null, null, "only the team publishes passports"));
    }

    public async Task ValuesSavedAsync(TargetKind target, string targetId, IReadOnlyCollection<string> keys, string who, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(keys);
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
                continue;
            }

            var result = await PublishCoreAsync(packId, who, retryOnConflict: true, cancellationToken);
            if (result.Problem is not null)
            {
                await audit.AppendApplicationAuditEventAsync(
                    BlockedEvent,
                    who,
                    "system",
                    "publication",
                    $"A new version of pack {packId} was blocked; version {active.Number} stays active",
                    new BsonDocument { ["blocking"] = new BsonArray(result.Report?.Blocking ?? [result.Problem]) },
                    entityType: "dpp-pack",
                    entityId: packId,
                    cancellationToken: cancellationToken);
            }
        }
    }

    private async Task<PublicationResult> PublishCoreAsync(string packId, string who, bool retryOnConflict, CancellationToken cancellationToken)
    {
        var sources = await store.LoadSourcesAsync(packId, cancellationToken);
        if (sources is null)
        {
            return new PublicationResult(null, null, $"pack {packId}, or a record it links to, was not found");
        }

        var now = clock.GetUtcNow().UtcDateTime;
        var passportId = identifiers.NewPassportIdentifier();
        var passport = PassportAssembler.Assemble(sources, new PassportHeader(passportId, DppStatuses.Active, now));
        var report = checks.Check(passport);
        if (!report.CanPublish)
        {
            return new PublicationResult(null, report, "the passport can't be published yet; see the blocking items");
        }

        var core = PassportStaticCore.Extract(passport);
        var active = await store.GetActiveVersionAsync(packId, cancellationToken);
        if (active is not null && PassportStaticCore.ContentHash(active.StaticCore) == PassportStaticCore.ContentHash(core))
        {
            return new PublicationResult(active, report, null, Unchanged: true);
        }

        var version = new PassportVersion
        {
            Id = $"version-{Guid.NewGuid():N}",
            PackId = packId,
            Number = await store.GetLatestVersionNumberAsync(packId, cancellationToken) + 1,
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
            return await PublishCoreAsync(packId, who, retryOnConflict: false, cancellationToken);
        }

        await store.ArchiveOtherVersionsAsync(packId, version.Id, cancellationToken);
        await audit.AppendApplicationAuditEventAsync(
            PublishedEvent,
            who,
            "team",
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
