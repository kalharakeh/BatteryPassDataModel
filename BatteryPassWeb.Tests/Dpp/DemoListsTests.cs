using BatteryPassWeb.Dpp.Access;
using BatteryPassWeb.Dpp.Pages;
using BatteryPassWeb.Dpp.Store;
using BatteryPassWeb.Services;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Bson;
using MongoDB.Driver;

namespace BatteryPassWeb.Tests.Dpp;

/// <summary>
/// Finding R03 in the lists: a demo reader meets only demo batteries in the Registry and the API's cluster list, and a
/// demo token only demo batches; the demo mark comes from the stored pack and batch.
/// </summary>
[Collection(MongoCollection.Name)]
public sealed class DemoListsTests(MongoFixture mongo)
{
    private static Reader DemoMsa => Reader.With("demo.msa@example.test", ReaderRole.MarketSurveillance) with { DemoOnly = true };

    private static Task MarkAsync(PublicationRig rig, string collection, string id) =>
        rig.Context.Database!.GetCollection<BsonDocument>(collection).UpdateOneAsync(Builders<BsonDocument>.Filter.Eq("_id", id), Builders<BsonDocument>.Update.Set("IsDemo", true));

    [Fact]
    public async Task TheRegistry_ShouldShowADemoReaderOnlyDemoBatteries()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo).WithClustersAsync();
        var pages = BatteryTesting.Pages(rig);

        var before = await pages.ListAsync(null);
        Assert.False(Assert.Single(before.Rows).IsDemo);
        Assert.Empty(RegistryList.Build(DemoMsa, before).Rows);
        Assert.Single(RegistryList.Build(Reader.With("msa@example.test", ReaderRole.MarketSurveillance), before).Rows);

        await MarkAsync(rig, DppStore.Packs, rig.PackId);
        var after = await pages.ListAsync(null);
        Assert.True(Assert.Single(after.Rows).IsDemo);
        Assert.Single(RegistryList.Build(DemoMsa, after).Rows);
    }

    [Fact]
    public async Task TheApisClusterList_ShouldShowADemoReaderOnlyDemoBatteries()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo).WithClustersAsync();
        var cluster = (await rig.Store.GetAsync<BatteryPack>(rig.PackId))!.ClusterId!;
        var api = PassportApiTests.Api(rig);
        var demoTeam = Reader.With("api-token:demo", ReaderRole.Team) with { DemoOnly = true };

        Assert.Empty(await api.BatteriesAsync(demoTeam, cluster));
        Assert.Single(await api.BatteriesAsync(Reader.With("api-token:real", ReaderRole.Team), cluster));
    }

    [Fact]
    public async Task TheApisBatches_ShouldShowADemoTokenOnlyDemoBatches()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo).WithClustersAsync();
        var repository = DppApiControllerTests.Repository(rig);
        var (document, demoToken) = await repository.CreateTokenAsync("Demo team", ExternalTokenAccessMode.Lifecycle, [], false, true, "test");
        await repository.MarkDemoAsync(document["_id"]);
        var (_, realToken) = await repository.CreateTokenAsync("Team", ExternalTokenAccessMode.Lifecycle, [], false, true, "test");

        int Count(IActionResult result) => DppApiControllerTests.Json(Assert.IsAssignableFrom<ObjectResult>(result)).GetProperty("data").GetProperty("batches").GetArrayLength();

        Assert.Equal(0, Count(await DppApiControllerTests.Controller(rig, demoToken).Batches(CancellationToken.None)));
        var real = Count(await DppApiControllerTests.Controller(rig, realToken).Batches(CancellationToken.None));
        Assert.True(real > 0);

        await MarkAsync(rig, DppStore.Batches, rig.Seed.Sources.Batch.Id);
        Assert.Equal(1, Count(await DppApiControllerTests.Controller(rig, demoToken).Batches(CancellationToken.None)));
    }
}
