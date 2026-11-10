using System.Text.Json.Nodes;
using BatteryPassWeb.Dpp.Access;
using BatteryPassWeb.Dpp.Editing;
using BatteryPassWeb.Dpp.Store;
using BatteryPassWeb.Services;
using MongoDB.Bson;
using MongoDB.Driver;

namespace BatteryPassWeb.Tests.Dpp;

/// <summary>
/// Finding R03 in the editing services: a demo sender (a demo account or token) changes no real battery: no value, no
/// note, no status, no mark, no publication, no hand-over; and no battery moves between a demo and a real cluster.
/// </summary>
[Collection(MongoCollection.Name)]
public sealed class DemoEditingTests(MongoFixture mongo)
{
    private static ChangeSender DemoTeam => ChangeSender.Team("api-token:demo") with { DemoOnly = true };

    private static ChangeSender DemoNorthAdmin => ChangeSender.ClusterAdmin("demo.north.admin@example.test", BatteryTesting.North) with { DemoOnly = true };

    [Fact]
    public async Task ADemoSender_ShouldChangeNothingOnARealBattery()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo).WithClustersAsync();
        var before = await rig.Store.GetLatestVersionNumberAsync(rig.PackId);

        var save = await rig.Pipeline.SaveAsync(DemoTeam, new ChangeSet(TargetKind.Pack, rig.PackId, new Dictionary<string, JsonNode?> { ["batteryMass"] = JsonNode.Parse("123") }, "api"));
        var note = await StatusChangeTests.Service(rig).AddNoteAsync(DemoNorthAdmin, rig.PackId, "a note");
        var mark = await PassportMarkTests.Service(rig).MarkInvalidAsync(ChangeSender.MarketSurveillance("demo.msa@example.test") with { DemoOnly = true }, rig.PackId, "a reason");
        var publish = await rig.Publication.PublishAsync(DemoTeam, rig.PackId);
        var handOver = await HandOverTests.Service(rig).AskAsync(DemoNorthAdmin, rig.PackId, BatteryTesting.South, confirm: true);

        Assert.False(save.Saved);
        Assert.All(save.Keys, key => Assert.Equal(DemoScope.RealBattery, key.Message));
        Assert.Equal(StatusChangeService.NotYours, note);
        Assert.Equal(DemoScope.RealBattery, mark);
        Assert.Equal(DemoScope.RealBattery, publish.Problem);
        Assert.Equal(HandOverService.NotYours, handOver.Problem);
        Assert.Equal(DppStatuses.Active, await rig.Store.PassportStatusAsync(rig.PackId));
        Assert.Equal(before, await rig.Store.GetLatestVersionNumberAsync(rig.PackId));
    }

    // The manager's point 2: MayMove binds demo senders only.
    [Fact]
    public async Task ADemoAdmin_ShouldHandOverOnlyIntoADemoCluster_AndARealAdminAsBefore()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo).WithClustersAsync();
        await TestDemo.MakeInsideAsync(rig);
        var demoAdmin = ChangeSender.ClusterAdmin("demo.admin@example.test", BatteryTesting.North) with { DemoOnly = true };

        var intoReal = await HandOverTests.Service(rig).AskAsync(demoAdmin, rig.PackId, BatteryTesting.South, confirm: true);
        var byReal = await HandOverTests.Service(rig).AskAsync(ChangeSender.ClusterAdmin("north.admin@example.test", BatteryTesting.North), rig.PackId, BatteryTesting.South, confirm: true);

        Assert.Equal(HandOverService.NoSuchCluster, intoReal.Problem); // as if the cluster didn't exist (point 4)
        Assert.Null(byReal.Problem);
    }
}
