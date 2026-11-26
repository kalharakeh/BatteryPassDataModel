using System.Text.Json.Nodes;
using BatteryPassWeb.Dpp.Editing;
using BatteryPassWeb.Dpp.Publication;
using BatteryPassWeb.Dpp.Store;
using MongoDB.Bson;
using MongoDB.Driver;

namespace BatteryPassWeb.Tests.Dpp;

/// <summary>
/// Round five, the rule "the marker may be wrongly set, never wrongly unset", shown by fault injection: a failure between
/// the mark and the value write, and between the value write and the publishing, leaves every affected published
/// battery marked. A battery whose version is made active again (undoing a mark) is marked first.
/// </summary>
[Collection(MongoCollection.Name)]
public sealed class MarkerRuleFaultTests(MongoFixture mongo)
{
    private const string ModelId = "model-test";

    private static ChangeSet Mass(int kilograms) =>
        new(TargetKind.Model, ModelId, new Dictionary<string, JsonNode?> { ["batteryMass"] = new JsonObject { ["gramKilogramUnit"] = "kg", ["gramKilogramValue"] = kilograms } }, "form");

    [Fact]
    public async Task AFailureBetweenTheValueWriteAndThePublishing_ShouldLeaveTheBatteryMarked()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo);
        var pipeline = SavePipelineTesting.Pipeline(rig.Store, rig.Context, rig.Clock, new ThrowingListener(), rig.Publication);

        await Assert.ThrowsAsync<InvalidOperationException>(() => pipeline.SaveAsync(PublicationTesting.Team, Mass(513)));

        Assert.Equal("513", (await rig.Store.GetAsync<BatteryModel>(ModelId))!.Values["batteryMass"].Value!["gramKilogramValue"]!.ToJsonString()); // written
        Assert.Equal(1, (await rig.Store.GetActiveVersionAsync(rig.PackId))!.Number); // not published
        Assert.NotNull((await rig.Store.GetAsync<BatteryPack>(rig.PackId))!.ChangesWaitingSinceUtc); // and marked
    }

    [Fact]
    public async Task AFailureBetweenTheMarkAndTheValueWrite_ShouldLeaveTheBatteryMarked()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo);
        var database = rig.Context.Database!;

        // The database refuses the write itself: a model's mass of 600 kg or more breaks its validator.
        await database.RunCommandAsync<BsonDocument>(new BsonDocument
        {
            ["collMod"] = DppStore.Models,
            ["validator"] = new BsonDocument("Values.batteryMass.Value.gramKilogramValue", new BsonDocument("$lt", 600)),
            ["validationLevel"] = "strict",
            ["validationAction"] = "error",
        });
        try
        {
            await Assert.ThrowsAnyAsync<MongoException>(() => rig.Pipeline.SaveAsync(PublicationTesting.Team, Mass(700)));
        }
        finally
        {
            await database.RunCommandAsync<BsonDocument>(new BsonDocument { ["collMod"] = DppStore.Models, ["validator"] = new BsonDocument() });
        }

        Assert.NotEqual("700", (await rig.Store.GetAsync<BatteryModel>(ModelId))!.Values["batteryMass"].Value!["gramKilogramValue"]!.ToJsonString()); // not written
        Assert.NotNull((await rig.Store.GetAsync<BatteryPack>(rig.PackId))!.ChangesWaitingSinceUtc); // marked anyway: wrongly set is allowed
    }

    [Fact]
    public async Task AChangeWhileTheBatteryIsMarked_ShouldMarkIt_AndUndoingTheMarkShouldPublishIt()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo).WithClustersAsync();
        var marks = new PassportMarks(rig.Store, TestDemo.For(rig), new BatteryPassWeb.Services.ApplicationAuditService(rig.Context), rig.Clock, rig.Publication);
        Assert.Null(await marks.MarkInvalidAsync(PublicationTesting.Team, rig.PackId, "Wrong battery"));

        await rig.Pipeline.SaveAsync(PublicationTesting.Team, Mass(513));

        Assert.NotNull((await rig.Store.GetAsync<BatteryPack>(rig.PackId))!.ChangesWaitingSinceUtc); // the pipeline marks marked passports too

        var (_, published) = await marks.UndoWithOutcomeAsync(PublicationTesting.Team, rig.PackId);

        Assert.Equal(PublishOutcome.NewVersion, published!.Outcome);
        Assert.Null((await rig.Store.GetAsync<BatteryPack>(rig.PackId))!.ChangesWaitingSinceUtc);
    }

    [Fact]
    public async Task AFirstPublication_ShouldLeaveNoMarkerWhenNothingCrossedIt()
    {
        var rig = await PublicationTesting.RigAsync(mongo);

        Assert.Null((await rig.Publication.PublishAsync(PublicationTesting.Team, rig.PackId)).Problem);

        Assert.Null((await rig.Store.GetAsync<BatteryPack>(rig.PackId))!.ChangesWaitingSinceUtc);
    }

    /// <summary>A listener before publication that fails once the values are written.</summary>
    private sealed class ThrowingListener : IPackChangeListener
    {
        public Task ValuesSavedAsync(TargetKind target, string targetId, IReadOnlyCollection<string> keys, ChangeSender sender, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("injected after the value write");
    }
}
