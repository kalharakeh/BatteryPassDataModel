using System.Text.Json.Nodes;
using BatteryPassWeb.Dpp.Store;
using BatteryPassWeb.Tests.Dpp;
using MongoDB.Bson;
using MongoDB.Driver;

namespace BatteryPassWeb.Tests.Demo;

/// <summary>Finding D14: a lithium-ion model's batch has no recycled lead share in the demo data (empty, not 35 %).</summary>
[Collection(MongoCollection.Name)]
public sealed class DemoLeadShareTests(MongoFixture mongo)
{
    private static JsonObject Catalogue => JsonNode.Parse(File.ReadAllText(DppTestFiles.RepoFile("web", "Dpp", "Demo", "demo-catalogue.json")))!.AsObject();

    [Fact]
    public void TheCatalogue_ShouldGiveNoLithiumIonBatchARecycledLeadShare()
    {
        var catalogue = Catalogue;
        var lithium = catalogue["models"]!.AsArray()
            .Where(model => model!["values"]?["batteryChemistry"]?["chemicalCodeValue"]?.GetValue<string>().StartsWith("Li", StringComparison.Ordinal) == true)
            .Select(model => model!["key"]!.GetValue<string>())
            .ToHashSet();

        Assert.NotEmpty(lithium);
        foreach (var batch in catalogue["batches"]!.AsArray().Where(batch => lithium.Contains(batch!["model"]!.GetValue<string>())))
        {
            Assert.False(batch!["values"]!.AsObject().ContainsKey("leadRecycledContentShare"), batch["key"]!.GetValue<string>());
        }
    }

    [Fact]
    public async Task AfterSeeding_NoDemoBatchShouldHaveARecycledLeadShare()
    {
        using var rig = await DemoRig.StartAsync(mongo);
        await rig.Demo.SeedAsync(DemoRig.Who);

        var batches = await rig.All(DppStore.Batches, Builders<BsonDocument>.Filter.Eq("IsDemo", true));

        Assert.NotEmpty(batches);
        Assert.All(batches, batch => Assert.False(batch["Values"].AsBsonDocument.Contains("leadRecycledContentShare")));
    }
}
