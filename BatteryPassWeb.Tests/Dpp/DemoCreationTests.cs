using System.Text.Json.Nodes;
using BatteryPassWeb.Dpp.Access;
using BatteryPassWeb.Dpp.Demo;
using BatteryPassWeb.Dpp.Editing;
using BatteryPassWeb.Dpp.Identifiers;
using BatteryPassWeb.Dpp.Store;
using BatteryPassWeb.Tests.Demo;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Bson;
using MongoDB.Driver;

namespace BatteryPassWeb.Tests.Dpp;

/// <summary>
/// Finding R03, the manager's (c): a demo sender (the demo team token) makes only demo batteries. A real ID is refused
/// before the battery is stored, and a picked released ID is available again.
/// </summary>
[Collection(MongoCollection.Name)]
public sealed class DemoCreationTests(MongoFixture mongo)
{
    private static ChangeSender DemoTeam => ChangeSender.Team("api-token:demo") with { DemoOnly = true };

    /// <summary>The demo data, with DEMO-C7M-0001's batch, model and operator made real data (so a real ID may sit on them).</summary>
    private static async Task<(DemoRig Rig, BatteryPack Like)> SeededAsync(MongoFixture mongo, bool realProductData = true)
    {
        var rig = await DemoRig.StartAsync(mongo, dunsIds: true);
        await rig.Demo.SeedAsync(DemoRig.Who);
        var like = (await rig.Store.FindPacksBySerialNumberAsync("DEMO-C7M-0001")).Single();
        if (!realProductData)
        {
            return (rig, like);
        }

        var batch = (await rig.Store.GetAsync<ProductionBatch>(like.BatchId))!;
        foreach (var (collection, id) in new[] { (DppStore.Batches, batch.Id), (DppStore.Models, batch.ModelId), (DppStore.Operators, like.OperatorId) })
        {
            await rig.Database.GetCollection<BsonDocument>(collection).UpdateOneAsync(Builders<BsonDocument>.Filter.Eq("_id", id), Builders<BsonDocument>.Update.Set("IsDemo", false));
        }

        return (rig, like);
    }

    private static NewPack Request(BatteryPack like, string serial, bool fromPool) =>
        new(like.BatchId, like.OperatorId, like.ClusterId, new Dictionary<string, JsonNode?> { ["batterySerialNumber"] = serial }, FromPool: fromPool);

    [Fact]
    public async Task ARealReleasedId_ShouldBeRefusedToADemoSender_AndStayAvailable()
    {
        var (rig, like) = await SeededAsync(mongo);
        using var _ = rig;
        var pool = rig.Services.GetRequiredService<BatteryIdPool>();
        await pool.ReleaseAsync(DemoRig.Who, new ReleaseRequest("PO 1", null, [], "VPC-12221"));

        var result = await rig.Services.GetRequiredService<PackCreationService>().CreateAsync(DemoTeam, Request(like, "VPC-12221", fromPool: true));

        Assert.Null(result.Pack);
        Assert.Equal(DemoScope.RealBattery, result.Problem);
        Assert.Equal(ReleasedIdStatuses.Available, (await pool.GetAsync("VPC-12221"))!.Status);
        Assert.Empty(await rig.Store.FindPacksBySerialNumberAsync("VPC-12221"));
        Assert.NotNull((await rig.Services.GetRequiredService<PackCreationService>().CreateAsync(ChangeSender.Team(DemoRig.Who), Request(like, "VPC-12221", fromPool: true))).Pack);
    }

    [Fact]
    public async Task ARealIdFromATypedSerialNumber_ShouldBeRefusedToADemoSender()
    {
        var (rig, like) = await SeededAsync(mongo);
        using var _ = rig;

        var result = await rig.Services.GetRequiredService<PackCreationService>().CreateAsync(DemoTeam, Request(like, "REAL-0001", fromPool: false));

        Assert.Null(result.Pack);
        Assert.Equal(DemoScope.RealBattery, result.Problem);
        Assert.Empty(await rig.Store.FindPacksBySerialNumberAsync("REAL-0001"));
    }

    [Fact]
    public async Task ADemoSparesId_ShouldMakeADemoBatteryForADemoSender()
    {
        var (rig, like) = await SeededAsync(mongo, realProductData: false); // inside the demo needs demo product data too
        using var _ = rig;

        var result = await rig.Services.GetRequiredService<PackCreationService>().CreateAsync(DemoTeam, Request(like, DemoDataService.SpareSerials[0], fromPool: true));

        Assert.Null(result.Problem);
        Assert.True(result.Pack!.IsDemo);
    }
}
