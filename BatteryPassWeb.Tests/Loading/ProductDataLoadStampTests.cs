using System.Text.Json.Nodes;
using BatteryPassWeb.Dpp.Editing;
using BatteryPassWeb.Dpp.Store;
using BatteryPassWeb.Tests.Dpp;
using static BatteryPassWeb.Tests.Loading.LoadTesting;

namespace BatteryPassWeb.Tests.Loading;

/// <summary>
/// Package X1c: the fingerprint between the check and Load covers only what the load reads and would change (the
/// workbook, the files, and the product data it plans against). What moves by itself on a live site, such as a battery's
/// live values, a new passport version or another battery of the model, never refuses a load as "changed after the check".
/// </summary>
[Collection(MongoCollection.Name)]
public sealed class ProductDataLoadStampTests(MongoFixture mongo)
{
    private static FilledWorkbook ForTheSeedModel() => FilledWorkbook.Empty()
        .Set(FamilySheet, "family|Name", "Core", "Test battery family")
        .Set(ModelSheet, "model|Name", "Core800", "EXB-NMC-100")
        .Clear(ModelSheet, "model|Name", "Core800s")
        .Clear(ModelSheet, "model|Name", "Core700")
        .SetNumber(ModelSheet, "model|batteryMass|gramKilogramValue", "Core800", 520);

    [Fact]
    public async Task ALoad_ShouldSurviveALiveSaveANewPassportVersionAndAnotherBattery_BetweenCheckAndLoad()
    {
        var rig = Around(await PublicationTesting.PublishedRigAsync(mongo));
        var bytes = ForTheSeedModel().Bytes();
        var check = await rig.Loader.CheckAsync(WorkbookName, bytes, []);
        var team = ChangeSender.Team(Who);

        // A battery feed's live value, and a change of the battery's own that publishes version 2.
        await rig.Rig.Pipeline.SaveAsync(team, new ChangeSet(TargetKind.Pack, rig.Rig.PackId, new Dictionary<string, JsonNode?> { ["stateOfChargeSoC"] = JsonNode.Parse("""{"percentUnit":"%","percentageValue":55}""") }, "feed"));
        await rig.Rig.Pipeline.SaveAsync(team, new ChangeSet(TargetKind.Pack, rig.Rig.PackId, new Dictionary<string, JsonNode?> { ["thirdPartyServiceProvider"] = JsonValue.Create("Scania AB") }, "form"));
        Assert.Equal(2, (await rig.Store.GetActiveVersionAsync(rig.Rig.PackId))?.Number);

        // Another battery of the model, whose serial doesn't fit the model's new format either.
        await rig.Store.InsertAsync(new BatteryPack
        {
            Id = "pack-second",
            BatchId = "batch-test-2026",
            OperatorId = "operator-test",
            Values = new(StringComparer.Ordinal)
            {
                ["batterySerialNumber"] = DppStoreTesting.Value(JsonValue.Create("VPC8C05-77")),
                ["uniqueProductIdentifier"] = DppStoreTesting.Value(JsonValue.Create("urn:test:battery:VPC8C05-77")),
            },
        });

        var result = await rig.Loader.LoadAsync(Who, WorkbookName, bytes, [], check.Stamp);

        Assert.True(result.Loaded, result.Message);
        Assert.Equal("520", (await rig.Store.GetAsync<BatteryModel>("model-test"))!.Values["batteryMass"].Value!["gramKilogramValue"]!.ToJsonString());
    }

    [Fact]
    public async Task AChangeByHandToAValueTheLoadChanges_BetweenCheckAndLoad_ShouldStillRefuseTheLoad()
    {
        var rig = Around(await PublicationTesting.RigAsync(mongo));
        var bytes = ForTheSeedModel().Bytes();
        var check = await rig.Loader.CheckAsync(WorkbookName, bytes, []);
        await rig.Rig.Pipeline.SaveAsync(ChangeSender.Team(Who), new ChangeSet(TargetKind.Model, "model-test", new Dictionary<string, JsonNode?> { ["batteryMass"] = JsonNode.Parse("""{"gramKilogramUnit":"kg","gramKilogramValue":500}""") }, "form"));

        var result = await rig.Loader.LoadAsync(Who, WorkbookName, bytes, [], check.Stamp);

        Assert.False(result.Loaded);
        Assert.Equal(BatteryPassWeb.Dpp.Loading.ProductDataLoader.ChangedSinceCheck, result.Message);
    }

    [Fact]
    public void TheTestWorkbook_ShouldBeTheSameBytesEveryTimeItIsWritten()
    {
        var workbook = ForTheSeedModel();

        var first = workbook.Bytes();
        Thread.Sleep(2100); // a zip entry's time has two seconds' resolution
        var second = workbook.Bytes();

        Assert.Equal(first, second);
    }
}
