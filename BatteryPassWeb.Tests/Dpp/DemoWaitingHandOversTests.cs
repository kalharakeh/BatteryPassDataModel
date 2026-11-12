using System.Text.Json.Nodes;
using BatteryPassWeb.Dpp.Editing;
using BatteryPassWeb.Dpp.Store;
using BatteryPassWeb.Dpp.Values;
using BatteryPassWeb.Services;
using MongoDB.Bson;
using MongoDB.Driver;

namespace BatteryPassWeb.Tests.Dpp;

/// <summary>
/// Finding R03, the manager's point 3: a demo cluster admin's "Waiting for you" list and its count in the top bar hold only
/// hand-overs of batteries inside the demo; a real battery handed into a demo cluster isn't there for them.
/// </summary>
[Collection(MongoCollection.Name)]
public sealed class DemoWaitingHandOversTests(MongoFixture mongo)
{
    private const string DemoCluster = "demo-x";

    private static ChangeSender DemoReceiver => ChangeSender.ClusterAdmin("demo.x.admin@example.test", DemoCluster) with { DemoOnly = true };

    private static async Task<PublicationRig> RigAsync(MongoFixture mongo)
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo).WithClustersAsync();
        await new ClusterRepository(rig.Context).UpsertClusterAsync(DemoCluster, "Demo X");
        await rig.Context.Database!.GetCollection<BsonDocument>("clusters").UpdateOneAsync(
            Builders<BsonDocument>.Filter.Eq("clusterId", DemoCluster), Builders<BsonDocument>.Update.Set(ClusterRepository.DemoMark, true));
        return rig;
    }

    [Fact]
    public async Task ARealBatteryHandedIntoADemoCluster_ShouldNotWaitForADemoAdmin()
    {
        var rig = await RigAsync(mongo);
        // Outside the demo, with a test ID: a battery with a real ID isn't handed into a demo customer at all (M1 round two D).
        await rig.Store.SetValuesAsync<BatteryPack>(rig.PackId, new Dictionary<string, AttributeValue> { ["uniqueProductIdentifier"] = new() { Value = JsonValue.Create("https://www.sib-scania-eubp.eu/25S/UN778474481-TEST-0001")!, GeneratedAtUtc = rig.Clock.Now.UtcDateTime, Source = "system" } });
        var service = HandOverTests.Service(rig);
        Assert.Null((await service.AskAsync(ChangeSender.ClusterAdmin("north.admin@example.test", BatteryTesting.North), rig.PackId, DemoCluster, confirm: true)).Problem);

        Assert.Equal(0, await service.WaitingCountAsync(DemoReceiver));
        Assert.Empty(await service.WaitingForAsync(DemoReceiver));
        Assert.Equal(1, await service.WaitingCountAsync(ChangeSender.ClusterAdmin("real.x.admin@example.test", DemoCluster)));
    }

    [Fact]
    public async Task ABatteryInsideTheDemo_ShouldWaitForADemoAdmin()
    {
        var rig = await RigAsync(mongo);
        await TestDemo.MakeInsideAsync(rig);
        var service = HandOverTests.Service(rig);
        var demoSender = ChangeSender.ClusterAdmin("demo.north.admin@example.test", BatteryTesting.North) with { DemoOnly = true };
        Assert.Null((await service.AskAsync(demoSender, rig.PackId, DemoCluster, confirm: true)).Problem);

        Assert.Equal(1, await service.WaitingCountAsync(DemoReceiver));
        Assert.Single(await service.WaitingForAsync(DemoReceiver));
    }
}
