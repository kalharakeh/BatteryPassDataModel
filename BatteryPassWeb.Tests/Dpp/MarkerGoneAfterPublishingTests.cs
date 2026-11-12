using System.Text.Json.Nodes;
using BatteryPassWeb.Dpp.Editing;
using BatteryPassWeb.Dpp.Publication;
using BatteryPassWeb.Dpp.Store;
using BatteryPassWeb.Dpp.Values;
using BatteryPassWeb.Services;

namespace BatteryPassWeb.Tests.Dpp;

/// <summary>
/// Round seven, 7: after the team publishes a waiting change, through "Publish the waiting changes" or by undoing a mark,
/// with nothing else crossing, the battery's marker is gone when the action returns. Both start from a marker set by a
/// held-back model edit through the save pipeline. The app already did this; the tests keep it so.
/// </summary>
[Collection(MongoCollection.Name)]
public sealed class MarkerGoneAfterPublishingTests(MongoFixture mongo)
{
    private const string ModelId = "model-test";
    private const string Cycles = "numberOfFullChargingAndDischargingCycles";

    private static ChangeSet Mass(int kilograms) =>
        new(TargetKind.Model, ModelId, new Dictionary<string, JsonNode?> { ["batteryMass"] = new JsonObject { ["gramKilogramUnit"] = "kg", ["gramKilogramValue"] = kilograms } }, "form");

    [Fact]
    public async Task PublishTheWaitingChanges_ShouldLeaveNoMarker()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo).WithClustersAsync();
        var cycles = (await rig.Store.GetAsync<BatteryPack>(rig.PackId))!.Values[Cycles];
        await rig.Store.SetValuesAsync<BatteryPack>(rig.PackId, new Dictionary<string, AttributeValue>(), [Cycles]); // #83 gone
        await rig.Pipeline.SaveAsync(PublicationTesting.Team, Mass(513)); // held back: marked
        Assert.NotNull((await rig.Store.GetAsync<BatteryPack>(rig.PackId))!.ChangesWaitingSinceUtc);
        await rig.Store.SetValuesAsync<BatteryPack>(rig.PackId, new Dictionary<string, AttributeValue> { [Cycles] = cycles }); // #83 back

        var outcome = await rig.Publication.PublishWaitingAsync(PublicationTesting.Team, rig.PackId);

        Assert.Equal(PublishOutcome.NewVersion, outcome!.Outcome);
        Assert.Null((await rig.Store.GetAsync<BatteryPack>(rig.PackId))!.ChangesWaitingSinceUtc);
    }

    [Fact]
    public async Task UndoingAMark_ShouldLeaveNoMarker()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo).WithClustersAsync();
        var marks = new PassportMarks(rig.Store, TestDemo.For(rig), new ApplicationAuditService(rig.Context), rig.Clock, rig.Publication);
        Assert.Null(await marks.MarkInvalidAsync(PublicationTesting.Team, rig.PackId, "Wrong battery"));
        await rig.Pipeline.SaveAsync(PublicationTesting.Team, Mass(513)); // saved while marked: marked
        Assert.NotNull((await rig.Store.GetAsync<BatteryPack>(rig.PackId))!.ChangesWaitingSinceUtc);

        var (problem, published) = await marks.UndoWithOutcomeAsync(PublicationTesting.Team, rig.PackId);

        Assert.Null(problem);
        Assert.Equal(PublishOutcome.NewVersion, published!.Outcome);
        Assert.Null((await rig.Store.GetAsync<BatteryPack>(rig.PackId))!.ChangesWaitingSinceUtc);
    }
}
