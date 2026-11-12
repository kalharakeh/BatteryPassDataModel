using BatteryPassWeb.Dpp.Access;
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
    string Source = "form",
    string? PredecessorPackId = null,
    bool IsDemo = false,
    bool FromPool = false);

/// <summary>The created pack, or the problem that stopped it; <see cref="Keys"/> says what happened to each value.</summary>
public sealed record PackCreationResult(BatteryPack? Pack, IReadOnlyList<KeyResult> Keys, string? Problem);

/// <summary>
/// Creates a draft pack (design 7.1): the team only, all values accepted or nothing saved. The app issues #6, sets
/// #22 to "original" and points #92 at the pack's accident page; everything else goes through the save pipeline's checks.
/// </summary>
public sealed class PackCreationService(
    DppStore store,
    DemoBoundary demo,
    SavePipeline pipeline,
    IIdentifierScheme identifiers,
    DppLinks links,
    ApplicationAuditService audit,
    TimeProvider clock,
    BatteryIdPool? pool = null)
{
    /// <summary>Why a picked battery ID can't be used: someone took or voided it first, or it is for another model.</summary>
    public const string NotAvailable = "that serial number isn't an available released battery ID for this battery's model; pick one released on the Battery IDs page";

    public const string AuditEventType = "dpp.pack-created";

    /// <summary>The start of the refusal when the serial number can't make a battery ID (6d).</summary>
    public const string CantGetId = "the battery can't get its battery ID";

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

        // To a demo sender, real product data is as unknown: the same answer, before anything else is looked at (finding R03).
        if (await store.GetAsync<ProductionBatch>(request.BatchId, cancellationToken) is not { } requestedBatch
            || (sender.DemoOnly && !(requestedBatch.IsDemo && (await store.GetAsync<BatteryModel>(requestedBatch.ModelId, cancellationToken))?.IsDemo == true)))
        {
            return Failed($"batch {request.BatchId} not found");
        }

        if (await store.GetAsync<EconomicOperator>(request.OperatorId, cancellationToken) is not { } requestedOperator
            || (sender.DemoOnly && !requestedOperator.IsDemo))
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

        // A demo sender takes only a demo ID: any other serial number gets the answer an unknown one gets, before the check
        // below could say that a real battery has it (finding R03).
        if (sender.DemoOnly && request.PredecessorPackId is null && !request.IsDemo
            && !(request.FromPool && pool is not null && await pool.GetAsync(evaluation.Accepted["batterySerialNumber"].GetValue<string>(), cancellationToken) is { IsDemo: true }))
        {
            return Failed(NotAvailable);
        }

        // A battery's new linked passport keeps its serial number (design 7.5 and 8.2): its earlier passports may share it,
        // all of them, not only the one it replaces (redesign R6: a battery placed on the market twice).
        if (await store.SerialNumberTakenAsync(
            evaluation.Accepted["batterySerialNumber"].GetValue<string>(),
            request.PredecessorPackId is null ? [] : await store.ChainPackIdsAsync(request.PredecessorPackId, cancellationToken),
            cancellationToken))
        {
            var keys = evaluation.Results
                .Select(result => result.Key == "batterySerialNumber" ? result with { Outcome = KeyOutcome.Rejected, Message = SavePipeline.SerialNumberTaken } : result)
                .ToArray();
            return new PackCreationResult(null, keys, "some values were rejected; nothing was saved");
        }

        var batch = (await store.GetAsync<ProductionBatch>(request.BatchId, cancellationToken))!;
        var model = await store.GetAsync<BatteryModel>(batch.ModelId, cancellationToken);
        var economicOperator = (await store.GetAsync<EconomicOperator>(request.OperatorId, cancellationToken))!;
        var onDemoData = model is not null && DemoDataRule.UsesDemoData(batch, model, economicOperator);

        var now = clock.GetUtcNow().UtcDateTime;
        var pack = new BatteryPack
        {
            Id = $"pack-{Guid.NewGuid():N}",
            BatchId = request.BatchId,
            OperatorId = request.OperatorId,
            ClusterId = request.ClusterId,
            SuccessorOf = request.PredecessorPackId,
            IsDemo = request.IsDemo, // a demo battery's ID is marked TEST
        };
        pack.Values["batteryStatus"] = Stored(new JsonObject { ["batteryStatusValues"] = "original" }, now, "system", sender.Who);
        pack.Values["informationOnAccidents"] = Stored(links.AccidentPageLink(pack.Id), now, "system", sender.Who);
        foreach (var (key, value) in evaluation.Accepted)
        {
            pack.Values[key] = Stored(value, now, request.Source, sender.Who);
        }

        // #6 (step 6d): a battery takes a released ID, picked in one step so that no one else can; a battery's new passport
        // continues its first one's (-R1, -R2, …), and a battery made without the pool (tests, scripts) makes its own.
        if (request.FromPool && request.PredecessorPackId is null && pool is not null)
        {
            // Never a real released ID while the ID settings are missing: the battery would keep a test ID (6d-2b).
            if (pool.MissingSettings is { Count: > 0 } missing && await pool.GetAsync(evaluation.Accepted["batterySerialNumber"].GetValue<string>(), cancellationToken) is { IsDemo: false })
            {
                return Failed(IdentifierForms.SettingsMissing(missing));
            }

            // A real released ID is refused on demo product data before it is picked, so it stays available (U16).
            if (onDemoData && !pack.IsDemo && await pool.GetAsync(evaluation.Accepted["batterySerialNumber"].GetValue<string>(), cancellationToken) is { IsDemo: false })
            {
                return Failed(DemoDataRule.MessageFor(batch, model, economicOperator));
            }

            if (await pool.PickAsync(evaluation.Accepted["batterySerialNumber"].GetValue<string>(), pack.Id, batch.ModelId, cancellationToken) is not { } picked)
            {
                return Failed(NotAvailable);
            }

            pack.IsDemo |= picked.IsDemo; // a demo ID makes a demo battery, which clearing the demo data removes
            pack.Values["batterySerialNumber"] = Stored(JsonValue.Create(picked.SerialNumber), now, request.Source, sender.Who);
            pack.Values["uniqueProductIdentifier"] = Stored(JsonValue.Create(picked.Identifier), now, "system", sender.Who);
        }
        else
        {
            var made = await identifiers.NewPackIdentifierAsync(pack, cancellationToken);
            if (made.Identifier is not { } identifier)
            {
                return Failed($"{CantGetId}: {made.Problem}");
            }

            if (onDemoData && !pack.IsDemo && !IdentifierForms.IsTest(identifier))
            {
                return Failed(DemoDataRule.MessageFor(batch, model, economicOperator));
            }

            pack.Values["uniqueProductIdentifier"] = Stored(JsonValue.Create(identifier), now, "system", sender.Who);
        }

        // A demo sender makes only demo batteries: a real ID is refused before the battery is stored, and a picked
        // released ID is available again (finding R03).
        if (!DemoScope.Reaches(sender, DemoScope.Inside(pack, batch, model, economicOperator, await demo.ClusterIsDemoOrNoneAsync(pack.ClusterId, cancellationToken))))
        {
            if (request.FromPool && request.PredecessorPackId is null && pool is not null)
            {
                await pool.GiveBackAsync(pack.Id, CancellationToken.None);
            }

            return Failed(DemoScope.RealBattery);
        }

        // A real battery doesn't go into a demo customer (round two D); the picked ID is available again.
        if (pack.ClusterId is { Length: > 0 } && DemoDataRule.HasRealId(pack) && await demo.ClusterIsDemoOrNoneAsync(pack.ClusterId, cancellationToken))
        {
            if (request.FromPool && request.PredecessorPackId is null && pool is not null)
            {
                await pool.GiveBackAsync(pack.Id, CancellationToken.None);
            }

            return Failed(DemoDataRule.DemoCustomer);
        }

        try
        {
            await store.InsertAsync(pack, cancellationToken);
        }
        catch when (pool is not null && request.FromPool)
        {
            await pool.GiveBackAsync(pack.Id, CancellationToken.None); // the ID is available again
            throw;
        }

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
