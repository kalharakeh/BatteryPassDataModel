using System.Text.Json.Nodes;
using BatteryPassWeb.Dpp.Documents;
using BatteryPassWeb.Dpp.Editing;
using BatteryPassWeb.Dpp.Loading;
using BatteryPassWeb.Dpp.Pages;
using MongoDB.Driver;
using BatteryPassWeb.Dpp.Publication;
using BatteryPassWeb.Dpp.Store;
using BatteryPassWeb.Dpp.Values;
using BatteryPassWeb.Tests.Dpp;
using static BatteryPassWeb.Tests.Loading.LoadTesting;

namespace BatteryPassWeb.Tests.Loading;

/// <summary>
/// FU1: one load gives each published battery it reaches at most one new version, made after the whole load is saved,
/// with all of the load's changes in it. An unchanged passport keeps its version. A load that stops part way leaves the
/// waiting marker set, and the next load publishes once.
/// </summary>
[Collection(MongoCollection.Name)]
public sealed class ProductDataLoadOneVersionTests(MongoFixture mongo)
{
    private static FilledWorkbook ForTheSeedBattery() => FilledWorkbook.Empty()
        .Set(FamilySheet, "family|Name", "Core", "Test battery family")
        .Set(ModelSheet, "model|Name", "Core800", "EXB-NMC-100")
        .Clear(ModelSheet, "model|Name", "Core800s")
        .Clear(ModelSheet, "model|Name", "Core700")
        .Clear(ModelSheet, "model|SerialFormat", "Core800");

    private static FilledWorkbook ModelBatchAndDocument() => ForTheSeedBattery()
        .SetNumber(ModelSheet, "model|batteryMass|gramKilogramValue", "Core800", 520)
        .SetNumber(BatchSheet, "batch|Year", FirstBatch, 2026)
        .Set(BatchSheet, "batch|Site", FirstBatch, "Example plant")
        .Set(BatchSheet, "batch|carbonFootprintPerformanceClass", FirstBatch, "Class D")
        .Document("Core800", "model", "safetyMeasures", "safety.pdf", "Safety measures", "en-GB");

    private static SentFile[] Files() => [new("safety.pdf", DocumentTesting.Pdf("safety 2"))];

    private static async Task<PassportVersion> ActiveAsync(LoadRig rig) => (await rig.Store.GetActiveVersionAsync(rig.Rig.PackId))!;

    [Fact]
    public async Task ALoadThatChangesAModelItsBatchAndADocument_ShouldGiveAPublishedBatteryOneNewVersionWithAllOfThem()
    {
        var rig = Around(await PublicationTesting.PublishedRigAsync(mongo));
        var workbook = ModelBatchAndDocument();

        var firstCore = (await ActiveAsync(rig)).StaticCore.ToJsonString();
        var check = await rig.CheckAsync(workbook, Files());
        var result = await rig.Loader.LoadAsync(Who, WorkbookName, workbook.Bytes(), Files(), check.Stamp);

        Assert.True(result.Loaded, result.Message);
        var version = await ActiveAsync(rig);
        Assert.Equal(2, version.Number); // not 4: one for the model, one for the batch, one for the document
        var core = version.StaticCore.ToJsonString();
        // All three changes are in version 2, and none of them was in version 1.
        Assert.Contains("\"gramKilogramValue\":520", core, StringComparison.Ordinal);
        Assert.DoesNotContain("\"gramKilogramValue\":520", firstCore, StringComparison.Ordinal);
        Assert.Contains("\"carbonFootprintPerformanceClass\":\"Class D\"", core, StringComparison.Ordinal);
        Assert.DoesNotContain("\"carbonFootprintPerformanceClass\":\"Class D\"", firstCore, StringComparison.Ordinal);
        Assert.DoesNotContain("/documents/doc-safety-measures", core, StringComparison.Ordinal); // the document of version 1 was replaced
        Assert.Contains("/documents/doc-safety-measures", firstCore, StringComparison.Ordinal);
        Assert.Null((await rig.Store.GetAsync<BatteryPack>(rig.Rig.PackId))!.ChangesWaitingSinceUtc);
        Assert.Equal("Product data loaded: 2 records changed, of 1 battery family, 1 battery model and 1 batch. 1 passport got a new version.", result.Message);
    }

    [Fact]
    public async Task TheCheck_ShouldSayOneNewVersionForEachBatteryReached()
    {
        var rig = Around(await PublicationTesting.PublishedRigAsync(mongo));

        var check = await rig.CheckAsync(ModelBatchAndDocument(), Files());

        Assert.StartsWith("1 published battery is built from records this load changes. It gets one new version, made after the whole load is saved", check.Publication?.Text, StringComparison.Ordinal);
        Assert.DoesNotContain("one for each part saved", check.Publication?.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ALoadThatChangesNothing_ShouldKeepTheVersion()
    {
        var rig = Around(await PublicationTesting.PublishedRigAsync(mongo));
        var workbook = ForTheSeedBattery();

        var result = await rig.LoadAsync(workbook, []);

        Assert.False(result.Loaded);
        Assert.Equal(1, (await ActiveAsync(rig)).Number);
    }

    [Fact]
    public async Task ALoadThatStopsPartWay_ShouldLeaveTheVersionAndTheMarker_AndTheNextLoadPublishesOnce()
    {
        var rig = Around(await PublicationTesting.PublishedRigAsync(mongo));
        var workbook = ModelBatchAndDocument();
        rig.Loader.StepHook = step => step == "model" ? throw new InvalidOperationException("the database went away") : Task.CompletedTask;

        var stopped = await rig.LoadAsync(workbook, Files());

        Assert.False(stopped.Loaded);
        Assert.Equal(1, (await ActiveAsync(rig)).Number); // nothing was published while it ran
        Assert.NotNull((await rig.Store.GetAsync<BatteryPack>(rig.Rig.PackId))!.ChangesWaitingSinceUtc);

        rig.Loader.StepHook = null;
        var again = await rig.LoadAsync(workbook, Files());

        Assert.True(again.Loaded, again.Message);
        Assert.Equal(2, (await ActiveAsync(rig)).Number); // once, with the model's change (saved before the stop) and the rest
        Assert.Contains("520", (await ActiveAsync(rig)).StaticCore.ToJsonString(), StringComparison.Ordinal);
        Assert.Null((await rig.Store.GetAsync<BatteryPack>(rig.Rig.PackId))!.ChangesWaitingSinceUtc);
    }

    [Fact]
    public async Task AHandSaveAfterALoad_ShouldStillPublishAtOnce_TheHoldDoesNotOutliveTheLoad()
    {
        var rig = Around(await PublicationTesting.PublishedRigAsync(mongo));
        Assert.True((await rig.LoadAsync(ModelBatchAndDocument(), Files())).Loaded);
        Assert.Equal(2, (await ActiveAsync(rig)).Number);

        var mass = (await rig.Store.GetAsync<BatteryModel>("model-test"))!.Values["batteryMass"].Value!.DeepClone();
        mass["gramKilogramValue"] = 530;
        var post = new BatteryPassWeb.Dpp.Pages.EditorPost(
            new Dictionary<string, System.Text.Json.Nodes.JsonNode> { ["batteryMass"] = mass },
            new Dictionary<string, string>(),
            new Dictionary<string, int>());
        var saved = await PageTesting.Editor(rig.Rig).SaveAsync(ChangeSender.Team(Who), TargetKind.Model, "model-test", post, confirmed: true);

        Assert.Equal(BatteryPassWeb.Dpp.Pages.EditorOutcome.Saved, saved.Outcome);
        Assert.Equal(3, (await ActiveAsync(rig)).Number); // a hand save gives its own version, as before
    }

    /// <summary>A second batch of the seed model (2025) with a second published battery on it: version 1.</summary>
    private static async Task<string> SecondBatteryAsync(LoadRig rig)
    {
        var seed = rig.Rig.Seed.Sources;
        var batch = new ProductionBatch { Id = "batch-test-2025", ModelId = seed.Batch.ModelId, SiteId = seed.Batch.SiteId, Year = 2025, Values = new Dictionary<string, AttributeValue>(seed.Batch.Values) };
        var values = seed.Pack.Values.ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal);
        values["batterySerialNumber"] = new AttributeValue { Value = JsonValue.Create("VPC8C05-6790"), GeneratedAtUtc = values["batterySerialNumber"].GeneratedAtUtc, Source = "seed" };
        values["uniqueProductIdentifier"] = new AttributeValue { Value = JsonValue.Create("urn:test:battery:VPC8C05-6790"), GeneratedAtUtc = values["uniqueProductIdentifier"].GeneratedAtUtc, Source = "seed" };
        var pack = new BatteryPack { Id = "pack-test-2", BatchId = batch.Id, OperatorId = seed.Pack.OperatorId, ClusterId = seed.Pack.ClusterId, Values = values };
        await rig.Store.InsertAsync(batch);
        await rig.Store.InsertAsync(pack);
        Assert.Null((await rig.Rig.Publication.PublishAsync(PublicationTesting.Team, pack.Id)).Problem);
        return pack.Id;
    }

    [Fact]
    public async Task AReloadAfterAStopAfterTheModel_ShouldPublishEveryBatteryTheWorkbookReaches_OnceEach()
    {
        var rig = Around(await PublicationTesting.PublishedRigAsync(mongo));
        var second = await SecondBatteryAsync(rig);
        var workbook = ModelBatchAndDocument();
        rig.Loader.StepHook = step => step == "model" ? throw new InvalidOperationException("the database went away") : Task.CompletedTask;
        var stopped = await rig.LoadAsync(workbook, Files());
        Assert.False(stopped.Loaded);
        Assert.Contains("stay on their version, with the changes waiting. Loading the workbook again, or \"Publish the waiting changes\" on each page, publishes them.", stopped.Message, StringComparison.Ordinal);
        Assert.Equal(1, (await rig.Store.GetActiveVersionAsync(second))!.Number);
        rig.Loader.StepHook = null;

        var check = await rig.CheckAsync(workbook, Files());
        var again = await rig.LoadAsync(workbook, Files());

        Assert.Contains("1 more published battery has changes waiting from an earlier load or save", check.Publication?.Text, StringComparison.Ordinal);
        Assert.Contains("press \"Publish the waiting changes\"", check.Publication?.Text, StringComparison.Ordinal);
        Assert.True(again.Loaded, again.Message);
        Assert.Equal(2, (await ActiveAsync(rig)).Number); // the battery of the batch the re-load changes
        var other = (await rig.Store.GetActiveVersionAsync(second))!; // reached by the model only: saved before the stop
        Assert.Equal(2, other.Number);
        Assert.Contains("\"gramKilogramValue\":520", other.StaticCore.ToJsonString(), StringComparison.Ordinal);
        Assert.Null((await rig.Store.GetAsync<BatteryPack>(second))!.ChangesWaitingSinceUtc);
    }

    [Fact]
    public async Task AReloadAfterAStopAfterTheLastSave_ShouldSayNothingIsNewAndStillPublish()
    {
        var rig = Around(await PublicationTesting.PublishedRigAsync(mongo));
        var workbook = ModelBatchAndDocument();
        rig.Loader.StepHook = step => step == "batch" ? throw new InvalidOperationException("the database went away") : Task.CompletedTask;
        Assert.False((await rig.LoadAsync(workbook, Files())).Loaded);
        Assert.Equal(1, (await ActiveAsync(rig)).Number);
        rig.Loader.StepHook = null;

        var check = await rig.CheckAsync(workbook, Files());
        var again = await rig.LoadAsync(workbook, Files());

        Assert.False(check.HasChanges); // every record is as the workbook says
        Assert.True(check.CanLoad); // but a battery has changes waiting, so Load is offered
        Assert.NotNull(check.Publication);
        Assert.True(again.Loaded, again.Message);
        Assert.Equal(2, (await ActiveAsync(rig)).Number);
        Assert.Null((await rig.Store.GetAsync<BatteryPack>(rig.Rig.PackId))!.ChangesWaitingSinceUtc);

        var third = await rig.LoadAsync(workbook, Files()); // nothing waits now
        Assert.False(third.Loaded);
        Assert.Equal(2, (await ActiveAsync(rig)).Number);
    }

    [Fact]
    public async Task WhilePublicationsAreHeld_ALiveValueCollectsNothing_AStaticSaveCollectsTheBattery_AndAnUnchangedPassportKeepsItsVersion()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo);
        var sender = ChangeSender.Team(Who);

        using var hold = rig.Publication.HoldPublications();
        await rig.Publication.ValuesSavedAsync(TargetKind.Pack, rig.PackId, ["stateOfChargeSoC"], sender, CancellationToken.None); // a live value: only "last updated"
        Assert.Empty(hold.PackIds);

        await rig.Publication.ValuesSavedAsync(TargetKind.Model, "model-test", ["batteryMass"], sender, CancellationToken.None);
        Assert.Equal([rig.PackId], hold.PackIds);
        Assert.Equal(1, (await rig.Store.GetActiveVersionAsync(rig.PackId))!.Number);

        await rig.Publication.PublishHeldAsync(hold, sender); // nothing differs: the version stays
        Assert.Equal(1, (await rig.Store.GetActiveVersionAsync(rig.PackId))!.Number);
    }

    [Fact]
    public async Task AHeldLiveValueWithAMarker_ShouldBeNotedAndPublishedOnceWithTheWaitingWords_AndTheHoldEndsWithIt()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo);
        var sender = ChangeSender.Team(Who);
        var editor = PageTesting.Editor(rig);
        var mass = (await rig.Store.GetAsync<BatteryModel>("model-test"))!.Values["batteryMass"].Value!.DeepClone();
        mass["gramKilogramValue"] = 530;
        EditorPost Post(JsonNode value) => new(new Dictionary<string, JsonNode> { ["batteryMass"] = value }, new Dictionary<string, string>(), new Dictionary<string, int>());

        using var hold = rig.Publication.HoldPublications();
        await editor.SaveAsync(sender, TargetKind.Model, "model-test", Post(mass), confirmed: true); // a static change: marks the battery, noted
        Assert.Equal(1, (await rig.Store.GetActiveVersionAsync(rig.PackId))!.Number);
        await rig.Publication.ValuesSavedAsync(TargetKind.Pack, rig.PackId, ["stateOfChargeSoC"], sender, CancellationToken.None); // a live value on a marked battery

        Assert.Equal([rig.PackId], hold.PackIds);
        await rig.Publication.PublishHeldAsync(hold, sender);

        Assert.Equal(2, (await rig.Store.GetActiveVersionAsync(rig.PackId))!.Number); // once
        var published = (await rig.Context.Database!.GetCollection<MongoDB.Bson.BsonDocument>("applicationAuditEvents").Find(MongoDB.Driver.FilterDefinition<MongoDB.Bson.BsonDocument>.Empty).ToListAsync())
            .Where(entry => entry["eventType"].AsString == PublicationService.PublishedEvent).Select(entry => entry.ToString()).ToArray();
        Assert.Contains(published, entry => entry.Contains("with the changes that were waiting", StringComparison.Ordinal));

        // The hold ended with the release: a second save in the same flow publishes at once.
        mass["gramKilogramValue"] = 540;
        await editor.SaveAsync(sender, TargetKind.Model, "model-test", Post(mass), confirmed: true);
        Assert.Equal(3, (await rig.Store.GetActiveVersionAsync(rig.PackId))!.Number);
    }
}
