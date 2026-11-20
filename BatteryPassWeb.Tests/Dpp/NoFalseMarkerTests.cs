using System.Text.Json.Nodes;
using BatteryPassWeb.Dpp.Editing;
using BatteryPassWeb.Dpp.Store;
using BatteryPassWeb.Dpp.Values;

namespace BatteryPassWeb.Tests.Dpp;

/// <summary>
/// Round four, 4: a battery is marked only when its saved data differs from its published passport; a live save that
/// finds nothing differs clears a wrong marker; the team's (and the API's) publish keeps the marker right.
/// </summary>
[Collection(MongoCollection.Name)]
public sealed class NoFalseMarkerTests(MongoFixture mongo)
{
    private const string ModelId = "model-test";

    [Fact]
    public async Task ABlockedChangeThePassportDoesntShow_ShouldMarkNothing()
    {
        // The battery re-states its own capacity fade (#59), so the model's #59 isn't in its passport.
        var rig = await PublicationTesting.PublishedRigAsync(mongo);
        await rig.Store.SetModelAnswersAsync(ModelId, null, null, null); // can't publish

        await rig.Pipeline.SaveAsync(PublicationTesting.Team, new ChangeSet(TargetKind.Model, ModelId, new Dictionary<string, JsonNode?> { ["capacityFade"] = new JsonObject { ["percentUnit"] = "%", ["percentageValue"] = 1 } }, "form"));

        Assert.Null((await rig.Store.GetAsync<BatteryPack>(rig.PackId))!.ChangesWaitingSinceUtc);
    }

    [Fact]
    public async Task ALiveSaveThatFindsNothingDiffers_ShouldClearAWrongMarker()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo).WithClustersAsync();
        await rig.Store.MarkChangesWaitingAsync([rig.PackId], DateTime.UtcNow); // wrong: nothing waits

        await rig.Pipeline.SaveAsync(ChangeSender.Feed("bms-feed", "cluster-test"), new ChangeSet(TargetKind.Pack, rig.PackId, new Dictionary<string, JsonNode?> { ["stateOfChargeSoC"] = 51 }, "feed"));

        Assert.Null((await rig.Store.GetAsync<BatteryPack>(rig.PackId))!.ChangesWaitingSinceUtc);
    }

    [Fact]
    public async Task TheTeamsPublishOfAPublishedBattery_ShouldMarkAWaitingChange_AndClearItOncePublished()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo);
        await rig.Store.SetModelAnswersAsync(ModelId, null, null, null);
        await rig.Store.SetValuesAsync<BatteryModel>(ModelId, new Dictionary<string, AttributeValue>
        {
            ["batteryMass"] = new() { Value = new JsonObject { ["gramKilogramUnit"] = "kg", ["gramKilogramValue"] = 513 }, GeneratedAtUtc = DateTime.UtcNow, Source = "form" },
        }); // saved behind the save path's back

        Assert.NotNull((await rig.Publication.PublishAsync(PublicationTesting.Team, rig.PackId)).Problem);
        Assert.NotNull((await rig.Store.GetAsync<BatteryPack>(rig.PackId))!.ChangesWaitingSinceUtc);

        await rig.Store.SetModelAnswersAsync(ModelId, true, true, true);
        Assert.Null((await rig.Publication.PublishAsync(PublicationTesting.Team, rig.PackId)).Problem);
        Assert.Null((await rig.Store.GetAsync<BatteryPack>(rig.PackId))!.ChangesWaitingSinceUtc);
    }
}
