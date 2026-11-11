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

        var before = await OperationsAsync(rig);
        var page = await pages.TeamListAsync(null);
        var searched = await pages.TeamListAsync("LOAD-00001");
        var used = await OperationsAsync(rig) - before;

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

        var before = await OperationsAsync(rig);
        await rig.Publication.ValuesSavedAsync(TargetKind.Pack, rig.PackId, ["stateOfChargeSoC"], sender, CancellationToken.None);
        var used = await OperationsAsync(rig) - before;

        // The active version, #4's new time, and the battery's marker: one more than before the waiting changes (2). The
        // count includes commands, and the one serverStatus call between the readings (round four, 7).
        Assert.True(used <= 4, $"a live save with nothing waiting used {used} database operations");
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
    /// The database's count of operations so far: finds, more results, inserts, updates, deletes and commands (a count or
    /// an aggregate per battery would show there; round four, 7).
    /// </summary>
    private static async Task<long> OperationsAsync(PublicationRig rig)
    {
        var status = await rig.Context.Database!.Client.GetDatabase("admin").RunCommandAsync<BsonDocument>(new BsonDocument("serverStatus", 1));
        var counters = status["opcounters"].AsBsonDocument;
        return new[] { "query", "getmore", "insert", "update", "delete", "command" }.Sum(name => counters[name].ToInt64());
    }
}
