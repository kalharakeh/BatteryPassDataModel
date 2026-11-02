using BatteryPassWeb.Dpp.Demo;
using BatteryPassWeb.Dpp.Documents;
using BatteryPassWeb.Dpp.Store;
using BatteryPassWeb.Services;
using BatteryPassWeb.Tests.Dpp;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Bson;
using MongoDB.Driver;

namespace BatteryPassWeb.Tests.Demo;

/// <summary>Step 3d-3: clearing removes only demo data and never breaks real data; resetting seeds again (decision 32).</summary>
[Collection(MongoCollection.Name)]
public sealed class DemoClearTests(MongoFixture mongo)
{
    private static FilterDefinition<BsonDocument> Eq(string field, BsonValue value) => Builders<BsonDocument>.Filter.Eq(field, value);

    [Fact]
    public async Task Clearing_ShouldRemoveEveryDemoRecordWithItsVersionsDocumentsAndFiles()
    {
        using var rig = await DemoRig.StartAsync(mongo);
        await rig.Demo.SeedAsync(DemoRig.Who);

        var result = await rig.Demo.ClearAsync(DemoRig.Who);

        foreach (var collection in new[] { DppStore.Families, DppStore.Models, DppStore.Sites, DppStore.Batches, DppStore.Operators, DppStore.Packs, DppStore.Versions, DppStore.HandOvers, DocumentStore.Records, $"{DocumentStore.Bucket}.files", $"{DocumentStore.Bucket}.chunks", "users", "clusterMemberships", "clusters", "apiTokens" })
        {
            Assert.True(await rig.Count(collection) == 0, $"{collection} still has records");
        }

        Assert.Empty(result.Notes);
        Assert.Contains("9 batteries removed", result.Done);
        Assert.Contains("12 accounts removed", result.Done);
    }

    [Fact]
    public async Task Clearing_ShouldKeepTheAuditLogAndLogItself()
    {
        using var rig = await DemoRig.StartAsync(mongo);
        await rig.Demo.SeedAsync(DemoRig.Who);
        var before = await rig.Count("applicationAuditEvents");

        await rig.Demo.ClearAsync(DemoRig.Who);

        Assert.Equal(before + 1, await rig.Count("applicationAuditEvents"));
        var entry = (await rig.All("applicationAuditEvents", Eq("eventType", DemoDataService.ClearedEvent))).Single();
        Assert.Equal(9, entry["metadata"]["batteries"].ToInt32());
    }

    [Fact]
    public async Task ClearingAgain_ShouldSayThereWasNothingToClear()
    {
        using var rig = await DemoRig.StartAsync(mongo);

        var result = await rig.Demo.ClearAsync(DemoRig.Who);

        Assert.Equal(["There was no demo data to clear."], result.Done);
    }

    [Fact]
    public async Task RealDataUsingDemoRecords_ShouldKeepThemWithoutTheirMark()
    {
        using var rig = await DemoRig.StartAsync(mongo);
        await rig.Demo.SeedAsync(DemoRig.Who);
        var store = rig.Store;
        var model = (await rig.All(DppStore.Models, Eq("Name", "Compact 7M"))).Single();
        var site = (await rig.All(DppStore.Sites)).First();
        var demoOperator = (await rig.All(DppStore.Operators)).First();
        await store.InsertAsync(new ProductionBatch { Id = "batch-real", ModelId = model["_id"].AsString, SiteId = site["_id"].AsString, Year = 2027 });
        await store.InsertAsync(new BatteryPack { Id = "pack-real", BatchId = "batch-real", OperatorId = demoOperator["_id"].AsString, ClusterId = "demo-north" });
        await rig.Services.GetRequiredService<ClusterRepository>().UpsertUserAsync("real.person@example.com", "Real person", ["member"], "hash");
        await rig.Services.GetRequiredService<ClusterRepository>().UpsertClusterMembershipAsync("real.person@example.com", "demo-south", "member");

        var result = await rig.Demo.ClearAsync(DemoRig.Who);

        Assert.Contains("Kept Compact 7M: 1 real batches use it, so it is no longer marked as demo data.", result.Notes);
        Assert.Contains($"Kept {site["Name"].AsString}: 1 real batches use it, so it is no longer marked as demo data.", result.Notes);
        Assert.Contains($"Kept {demoOperator["Name"].AsString}: 1 real batteries use it, so it is no longer marked as demo data.", result.Notes);
        Assert.Contains("Kept Compact: 1 real models use it, so it is no longer marked as demo data.", result.Notes);
        Assert.Contains("Kept cluster demo-north: 1 real batteries use it, so it is no longer marked as demo data.", result.Notes);
        Assert.Contains("Kept cluster demo-south: 1 real accounts use it, so it is no longer marked as demo data.", result.Notes);
        Assert.Equal(["pack-real"], (await rig.All(DppStore.Packs)).Select(pack => pack["_id"].AsString));
        Assert.Equal(["Compact 7M"], (await rig.All(DppStore.Models)).Select(record => record["Name"].AsString));
        Assert.All(await rig.All(DppStore.Models), record => Assert.False(record["IsDemo"].AsBoolean));
        Assert.Equal(["demo-north", "demo-south"], (await rig.All("clusters")).Select(cluster => cluster["clusterId"].AsString).Order());
        Assert.All(await rig.All("clusters"), cluster => Assert.False(cluster["isDemo"].AsBoolean));
        Assert.Equal(["real.person@example.com"], (await rig.All("users")).Select(user => user["email"].AsString));
    }

    [Fact]
    public async Task Resetting_ShouldClearThenSeedWithNewBatteries()
    {
        using var rig = await DemoRig.StartAsync(mongo);
        var first = await rig.Demo.SeedAsync(DemoRig.Who);
        var firstPacks = (await rig.All(DppStore.Packs)).Select(pack => pack["_id"].AsString).ToList();

        var result = await rig.Demo.ResetAsync(DemoRig.Who);

        var packs = (await rig.All(DppStore.Packs)).Select(pack => pack["_id"].AsString).ToList();
        Assert.Equal(9, packs.Count);
        Assert.Empty(packs.Intersect(firstPacks));
        Assert.Equal(10, await rig.Count(DppStore.Versions));
        Assert.Equal(7, result.Tokens.Count);
        Assert.Empty(result.Tokens.Select(token => token.Value).Intersect(first.Tokens.Select(token => token.Value)));
        Assert.Contains("9 batteries removed", result.Done);
        Assert.Contains("9 batteries, 6 of them published", result.Done);
    }
}
