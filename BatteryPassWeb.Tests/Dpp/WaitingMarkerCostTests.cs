using System.Text.Json.Nodes;
using BatteryPassWeb.Dpp.Editing;
using BatteryPassWeb.Dpp.Store;
using MongoDB.Bson;
using MongoDB.Driver;

namespace BatteryPassWeb.Tests.Dpp;

/// <summary>
/// Round three, 1: the team's battery list makes no database call and builds no passport per battery; it reads each
/// battery's stored marker of a waiting change. A live save on a battery with nothing waiting costs one extra read.
/// Operations are counted with the database's own counters (the Mongo tests run one at a time).
/// </summary>
[Collection(MongoCollection.Name)]
public sealed class WaitingMarkerCostTests(MongoFixture mongo)
{
    private const int Batteries = 5000;

    [Fact]
    public async Task TheTeamsList_ShouldNotPayPerBattery_AndShouldNameTheMarkedOnes()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo).WithClustersAsync();
        var packs = rig.Context.Database!.GetCollection<BsonDocument>(DppStore.Packs);
        var versions = rig.Context.Database!.GetCollection<BsonDocument>(DppStore.Versions);
        var pack = await packs.Find(Builders<BsonDocument>.Filter.Eq("_id", rig.PackId)).SingleAsync();
        var version = await versions.Find(Builders<BsonDocument>.Filter.Eq("PackId", rig.PackId)).SingleAsync();
        var newPacks = new List<BsonDocument>();
        var newVersions = new List<BsonDocument>();
        for (var i = 0; i < Batteries; i++)
        {
            var id = $"pack-load-{i:D5}";
            var copy = pack.DeepClone().AsBsonDocument;
            copy["_id"] = id;
            copy["Values"]["batterySerialNumber"]["Value"] = $"LOAD-{i:D5}";
            copy["Values"]["uniqueProductIdentifier"]["Value"] = $"urn:test:load:{i:D5}";
            if (i == 42)
            {
                copy["ChangesWaitingSinceUtc"] = DateTime.UtcNow;
            }

            newPacks.Add(copy);
            var versionCopy = version.DeepClone().AsBsonDocument;
            versionCopy["_id"] = $"version-load-{i:D5}";
            versionCopy["PackId"] = id;
            versionCopy["DigitalProductPassportId"] = $"urn:test:load:{i:D5}:v1";
            newVersions.Add(versionCopy);
        }

        await packs.InsertManyAsync(newPacks);
        await versions.InsertManyAsync(newVersions);
        var pages = BatteryTesting.Pages(rig);

        var before = await StartCountingAsync(rig);
        var page = await pages.TeamListAsync(null);
        var searched = await pages.TeamListAsync("LOAD-00001");
        var used = await StopCountingAsync(rig, before);

        Assert.Equal(Batteries + 1, page.Rows.Count);
        Assert.Equal("LOAD-00042", Assert.Single(page.Waiting).SerialNumber);
        Assert.Equal("LOAD-00042", Assert.Single(searched.Waiting).SerialNumber); // outside the search: never hidden by it
        Assert.True(used < 60, $"two openings of the list used {used} database operations");
    }

    [Fact]
    public async Task ALiveSaveWithNothingWaiting_ShouldCostOneExtraRead()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo).WithClustersAsync();
        var sender = ChangeSender.Feed("bms-feed", "cluster-test");

        var before = await StartCountingAsync(rig);
        await rig.Publication.ValuesSavedAsync(TargetKind.Pack, rig.PackId, ["stateOfChargeSoC"], sender, CancellationToken.None);
        var used = await StopCountingAsync(rig, before);

        // The active version, #4's new time, and the battery's marker (3): one more than before the waiting changes (2).
        // Only this test's database is counted, commands included, so no heartbeat can break it; one of slack (round five).
        Assert.True(used <= 4, $"a live save with nothing waiting used {used} database operations");
        Assert.True(used >= 2, $"the profiler saw only {used} operations: it isn't counting"); // the active version and #4 at least
    }

    [Fact]
    public async Task AHeldBackChange_ShouldSetTheMarker_AndItsPublicationClearIt()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo).WithClustersAsync();
        await rig.Store.SetModelAnswersAsync("model-test", null, null, null);
        var mass = new JsonObject { ["gramKilogramUnit"] = "kg", ["gramKilogramValue"] = 513 };
        await rig.Pipeline.SaveAsync(PublicationTesting.Team, new ChangeSet(TargetKind.Model, "model-test", new Dictionary<string, JsonNode?> { ["batteryMass"] = mass }, "form"));

        Assert.NotNull((await rig.Store.GetAsync<BatteryPack>(rig.PackId))!.ChangesWaitingSinceUtc);

        await PageTesting.Pages(rig).SetModelAnswersAsync("team@example.com", (await rig.Store.GetAsync<BatteryModel>("model-test"))!, new BatteryPassWeb.Dpp.Catalog.ModelAnswers(true, true, true), confirmed: true);

        Assert.Equal(2, (await rig.Store.GetActiveVersionAsync(rig.PackId))!.Number);
        Assert.Null((await rig.Store.GetAsync<BatteryPack>(rig.PackId))!.ChangesWaitingSinceUtc);
    }

    [Fact]
    public async Task TheBatterysPage_ShouldPutAWrongMarkerRight()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo).WithClustersAsync();
        await rig.Store.MarkChangesWaitingAsync([rig.PackId], DateTime.UtcNow); // wrong: nothing waits

        var page = (await BatteryTesting.Pages(rig).BatteryAsync(rig.PackId))!;

        Assert.Null(page.Publication.Waiting);
        Assert.Null((await rig.Store.GetAsync<BatteryPack>(rig.PackId))!.ChangesWaitingSinceUtc);
    }

    /// <summary>
    /// Starts MongoDB's profiler on this test's database: every operation on it, commands included (a count or an
    /// aggregate per battery would show), and nothing from other databases or the driver's heartbeats (round five).
    /// </summary>
    private static async Task<long> StartCountingAsync(PublicationRig rig)
    {
        var database = rig.Context.Database!;
        await database.RunCommandAsync<BsonDocument>(new BsonDocument("profile", 2));
        return await ProfiledAsync(database);
    }

    /// <summary>The operations profiled since <paramref name="before"/>; the profiler is switched off again.</summary>
    private static async Task<long> StopCountingAsync(PublicationRig rig, long before)
    {
        var database = rig.Context.Database!;
        var after = await ProfiledAsync(database);
        await database.RunCommandAsync<BsonDocument>(new BsonDocument("profile", 0));
        return after - before;
    }

    private static Task<long> ProfiledAsync(IMongoDatabase database) =>
        database.GetCollection<BsonDocument>("system.profile").CountDocumentsAsync(
            Builders<BsonDocument>.Filter.Not(Builders<BsonDocument>.Filter.Regex("ns", new BsonRegularExpression(@"\.system\.profile$")))
            & Builders<BsonDocument>.Filter.Ne("command.profile", 2) & Builders<BsonDocument>.Filter.Ne("command.profile", 0));
}
