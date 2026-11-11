using System.Text.Json.Nodes;
using BatteryPassWeb.Dpp.Editing;
using BatteryPassWeb.Dpp.Publication;
using BatteryPassWeb.Dpp.Store;

namespace BatteryPassWeb.Tests.Dpp;

/// <summary>
/// Round two, A: a published passport never differs from its saved data without the team seeing it. A change held back
/// because the passport couldn't publish follows by itself once a live value clears the block (A1); the battery page
/// says a change is waiting (A2); every publishing action can say, per passport, what happened (A3).
/// </summary>
[Collection(MongoCollection.Name)]
public sealed class HeldBackChangeTests(MongoFixture mongo)
{
    private const string ModelId = "model-test";
    private const string Cycles = "numberOfFullChargingAndDischargingCycles";

    [Fact]
    public async Task AHeldBackChange_ShouldFollowByItself_WhenALiveValueClearsTheBlock()
    {
        var rig = await HeldBackAsync();

        // The battery system sends its cycles (#83): the passport can publish again, and the waiting mass goes with it.
        var feed = await rig.Pipeline.SaveAsync(ChangeSender.Feed("bms-feed", "cluster-test"), new ChangeSet(TargetKind.Pack, rig.PackId, new Dictionary<string, JsonNode?> { [Cycles] = 14 }, "feed"));

        Assert.True(feed.Saved);
        var active = (await rig.Store.GetActiveVersionAsync(rig.PackId))!;
        Assert.Equal(2, active.Number);
        Assert.Equal("513", Mass(active));
        Assert.Null(await rig.Publication.WaitingAsync(rig.PackId));
    }

    [Fact]
    public async Task AHeldBackChange_ShouldBeSeenWithWhatHoldsItBack()
    {
        var rig = await HeldBackAsync();

        var waiting = await rig.Publication.WaitingAsync(rig.PackId);

        Assert.NotNull(waiting);
        Assert.Equal(1, waiting.Version);
        Assert.Contains(waiting.Blocking, item => item.StartsWith("#83 ", StringComparison.Ordinal));
        var page = (await BatteryTesting.Pages(rig).BatteryAsync(rig.PackId))!;
        Assert.Equal(1, page.Publication.Waiting?.Version);
    }

    [Fact]
    public async Task ALiveValueWithNothingWaiting_ShouldPublishNoNewVersion()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo).WithClustersAsync();

        await rig.Pipeline.SaveAsync(ChangeSender.Feed("bms-feed", "cluster-test"), new ChangeSet(TargetKind.Pack, rig.PackId, new Dictionary<string, JsonNode?> { [Cycles] = 15 }, "feed"));

        Assert.Equal(1, (await rig.Store.GetActiveVersionAsync(rig.PackId))!.Number);
        Assert.Null(await rig.Publication.WaitingAsync(rig.PackId));
    }

    [Fact]
    public async Task TheOutcomes_ShouldSayNewVersionNothingChangedOrHeldBack()
    {
        var rig = await HeldBackAsync();

        var held = Assert.Single(await rig.Publication.OutcomesAsync([(rig.PackId, 1)]));
        Assert.Equal(PublishOutcome.HeldBack, held.Outcome);
        Assert.Contains(held.Blocking, item => item.StartsWith("#83 ", StringComparison.Ordinal));

        await rig.Pipeline.SaveAsync(ChangeSender.Feed("bms-feed", "cluster-test"), new ChangeSet(TargetKind.Pack, rig.PackId, new Dictionary<string, JsonNode?> { [Cycles] = 14 }, "feed"));
        Assert.Equal(PublishOutcome.NewVersion, Assert.Single(await rig.Publication.OutcomesAsync([(rig.PackId, 1)])).Outcome);
        Assert.Equal(PublishOutcome.NothingChanged, Assert.Single(await rig.Publication.OutcomesAsync([(rig.PackId, 2)])).Outcome);
    }

    /// <summary>
    /// The seed pack published while its model said "not made for cycling use" and without cycles (#83); then the model
    /// says yes, and the team changes its mass: the change is saved, and the passport keeps version 1.
    /// </summary>
    private async Task<PublicationRig> HeldBackAsync()
    {
        var rig = await PublicationTesting.RigAsync(mongo).WithClustersAsync();
        await rig.Store.SetModelAnswersAsync(ModelId, true, false, true);
        await rig.Store.SetValuesAsync<BatteryPack>(rig.PackId, new Dictionary<string, BatteryPassWeb.Dpp.Values.AttributeValue>(), [Cycles]);
        Assert.Null((await rig.Publication.PublishAsync(PublicationTesting.Team, rig.PackId)).Problem);
        await rig.Store.SetModelAnswersAsync(ModelId, true, true, true);

        var mass = new JsonObject { ["gramKilogramUnit"] = "kg", ["gramKilogramValue"] = 513 };
        Assert.True((await rig.Pipeline.SaveAsync(PublicationTesting.Team, new ChangeSet(TargetKind.Model, ModelId, new Dictionary<string, JsonNode?> { ["batteryMass"] = mass }, "form"))).Saved);
        Assert.Equal(1, (await rig.Store.GetActiveVersionAsync(rig.PackId))!.Number);
        return rig;
    }

    private static string Mass(PassportVersion version) =>
        version.StaticCore["batteryPassportData"]!["identifiersAndProductData"]!["batteryMass"]!["gramKilogramValue"]!.ToJsonString();
}
