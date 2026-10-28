using System.Text.Json.Nodes;
using BatteryPassWeb.Dpp.Documents;
using BatteryPassWeb.Dpp.Identifiers;
using BatteryPassWeb.Dpp.Store;
using BatteryPassWeb.Dpp.Values;
using BatteryPassWeb.Services;

namespace BatteryPassWeb.Dpp.Editing;

/// <summary>A new pack: its batch (which gives model and site), operator, cluster and the values the team enters (#11 required; #13, #17, #19 optional).</summary>
public sealed record NewPack(
    string BatchId,
    string OperatorId,
    string? ClusterId,
    IReadOnlyDictionary<string, JsonNode?> Values,
    string Source = "form");

/// <summary>The created pack, or the problem that stopped it; <see cref="Keys"/> says what happened to each value.</summary>
public sealed record PackCreationResult(BatteryPack? Pack, IReadOnlyList<KeyResult> Keys, string? Problem);

/// <summary>
/// Creates a draft pack (design 7.1): the team only, all values accepted or nothing saved. The app issues #6, sets
/// #22 to "original" and points #92 at the pack's accident page; everything else goes through the save pipeline's checks.
/// </summary>
public sealed class PackCreationService(
    DppStore store,
    SavePipeline pipeline,
    IIdentifierScheme identifiers,
    DppLinks links,
    ApplicationAuditService audit,
    TimeProvider clock)
{
    public const string AuditEventType = "dpp.pack-created";

    public async Task<PackCreationResult> CreateAsync(ChangeSender sender, NewPack request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(sender);
        ArgumentNullException.ThrowIfNull(request);

        if (sender.Kind != SenderKind.Team)
        {
            return Failed("only the team creates packs");
        }

        if (!links.IsConfigured)
        {
            return Failed(DppLinks.NotConfigured);
        }

        if (await store.GetAsync<ProductionBatch>(request.BatchId, cancellationToken) is null)
        {
            return Failed($"batch {request.BatchId} not found");
        }

        if (await store.GetAsync<EconomicOperator>(request.OperatorId, cancellationToken) is null)
        {
            return Failed($"economic operator {request.OperatorId} not found");
        }

        var evaluation = pipeline.Evaluate(sender, TargetKind.Pack, request.ClusterId, request.Values);
        if (evaluation.Results.Any(result => result.Outcome == KeyOutcome.Rejected))
        {
            return new PackCreationResult(null, evaluation.Results, "some values were rejected; nothing was saved");
        }

        if (!evaluation.Accepted.ContainsKey("batterySerialNumber"))
        {
            return new PackCreationResult(null, evaluation.Results, "the battery serial number (#11) is required");
        }

        var now = clock.GetUtcNow().UtcDateTime;
        var pack = new BatteryPack
        {
            Id = $"pack-{Guid.NewGuid():N}",
            BatchId = request.BatchId,
            OperatorId = request.OperatorId,
            ClusterId = request.ClusterId,
        };
        pack.Values["uniqueProductIdentifier"] = Stored(JsonValue.Create(identifiers.NewPackIdentifier()), now, "system", sender.Who);
        pack.Values["batteryStatus"] = Stored(new JsonObject { ["batteryStatusValues"] = "original" }, now, "system", sender.Who);
        pack.Values["informationOnAccidents"] = Stored(links.AccidentPageLink(pack.Id), now, "system", sender.Who);
        foreach (var (key, value) in evaluation.Accepted)
        {
            pack.Values[key] = Stored(value, now, request.Source, sender.Who);
        }

        await store.InsertAsync(pack, cancellationToken);
        await audit.AppendApplicationAuditEventAsync(
            AuditEventType,
            sender.Who,
            sender.Kind.ToString(),
            request.Source,
            $"Draft pack {pack.Id} created",
            entityType: "dpp-pack",
            entityId: pack.Id,
            cancellationToken: cancellationToken);
        return new PackCreationResult(pack, evaluation.Results, null);

        static PackCreationResult Failed(string problem) => new(null, [], problem);
    }

    private static AttributeValue Stored(JsonNode value, DateTime generatedAtUtc, string source, string who) =>
        new() { Value = value, GeneratedAtUtc = generatedAtUtc, Source = source, Who = who };
}
