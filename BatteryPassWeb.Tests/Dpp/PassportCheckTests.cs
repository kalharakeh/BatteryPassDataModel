using System.Text.Json.Nodes;
using BatteryPassWeb.Controllers;
using BatteryPassWeb.Dpp.Publication;
using BatteryPassWeb.Dpp.Store;
using BatteryPassWeb.Dpp.Values;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using MongoDB.Bson;
using MongoDB.Driver;

namespace BatteryPassWeb.Tests.Dpp;

/// <summary>
/// Decision 65: one check of all published batteries puts the markers of waiting changes and the active versions right,
/// whatever went wrong, and never publishes. It reads in bulk: no read per battery.
/// </summary>
[Collection(MongoCollection.Name)]
public sealed class PassportCheckTests(MongoFixture mongo)
{
    private const string ModelId = "model-test";

    [Fact]
    public async Task ADifferenceWithNoMarker_ShouldBeMarked_AndNothingPublished()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo);
        await rig.Store.SetValuesAsync<BatteryModel>(ModelId, new Dictionary<string, AttributeValue>
        {
            ["batteryMass"] = new() { Value = new JsonObject { ["gramKilogramUnit"] = "kg", ["gramKilogramValue"] = 513 }, GeneratedAtUtc = DateTime.UtcNow, Source = "form" },
        }); // written straight into the store: no marker

        var result = await rig.Publication.ReconcileAsync("check-test");

        Assert.Equal((1, 1, 0, 0), (result.Checked, result.Marked, result.Cleared, result.Archived));
        Assert.NotNull((await rig.Store.GetAsync<BatteryPack>(rig.PackId))!.ChangesWaitingSinceUtc);
        Assert.Equal(1, (await rig.Store.GetActiveVersionAsync(rig.PackId))!.Number); // never publishes
        var entry = await rig.Context.Database!.GetCollection<BsonDocument>("applicationAuditEvents")
            .Find(Builders<BsonDocument>.Filter.Eq("eventType", PublicationService.CheckedEvent)).SingleAsync();
        Assert.Equal("Checked 1 published battery: 1 now marked as waiting, 0 no longer, 0 old versions archived.", entry["message"].AsString);
        Assert.Single(entry["metadata"]["batteries"].AsBsonArray);
    }

    [Fact]
    public async Task AFalseMarker_ShouldBeCleared()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo);
        await rig.Store.MarkChangesWaitingAsync([rig.PackId], DateTime.UtcNow);

        var result = await rig.Publication.ReconcileAsync("check-test");

        Assert.Equal(1, result.Cleared);
        Assert.Null((await rig.Store.GetAsync<BatteryPack>(rig.PackId))!.ChangesWaitingSinceUtc);
    }

    [Fact]
    public async Task TwoActiveVersions_ShouldBecomeOne_TheHighest()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo);
        var versions = rig.Context.Database!.GetCollection<BsonDocument>(DppStore.Versions);
        var second = (await versions.Find(Builders<BsonDocument>.Filter.Eq("PackId", rig.PackId)).SingleAsync()).DeepClone().AsBsonDocument;
        second["_id"] = "version-second";
        second["Number"] = 2;
        second["DigitalProductPassportId"] = "urn:test:second";
        await versions.InsertOneAsync(second); // a fault between an insert and its archive step

        var result = await rig.Publication.ReconcileAsync("check-test");

        Assert.Equal(1, result.Archived);
        var all = (await rig.Store.ListVersionsAsync(rig.PackId)).OrderBy(version => version.Number).ToArray();
        Assert.Equal([DppStatuses.Archived, DppStatuses.Active], all.Select(version => version.DppStatus));
    }

    [Fact]
    public async Task ARunWithNothingWrong_ShouldWriteNothing()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo);

        var before = await StartCountingAsync(rig, writesOnly: true);
        var result = await rig.Publication.ReconcileAsync("check-test");
        var writes = await StopCountingAsync(rig, before, writesOnly: true);

        Assert.False(result.ChangedAnything);
        Assert.Equal("Checked 1 published battery: nothing to put right.", result.Words);
        Assert.Equal(0, writes);
    }

    [Fact]
    public async Task FiveThousandBatteries_ShouldCostNoReadPerBattery()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo);
        var packs = rig.Context.Database!.GetCollection<BsonDocument>(DppStore.Packs);
        var versions = rig.Context.Database!.GetCollection<BsonDocument>(DppStore.Versions);
        var pack = await packs.Find(Builders<BsonDocument>.Filter.Eq("_id", rig.PackId)).SingleAsync();
        var version = await versions.Find(Builders<BsonDocument>.Filter.Eq("PackId", rig.PackId)).SingleAsync();
        var newPacks = new List<BsonDocument>();
        var newVersions = new List<BsonDocument>();
        for (var i = 0; i < 5000; i++)
        {
            var copy = pack.DeepClone().AsBsonDocument;
            copy.Remove("ItemKey"); // a copy has no key of its own yet, as a pack from before S1 round two
            copy["_id"] = $"pack-check-{i:D5}";
            copy["Values"]["batterySerialNumber"]["Value"] = $"CHECK-{i:D5}";
            copy["Values"]["uniqueProductIdentifier"]["Value"] = $"urn:test:check-battery:{i:D5}";
            newPacks.Add(copy);
            var versionCopy = version.DeepClone().AsBsonDocument;
            versionCopy["_id"] = $"version-check-{i:D5}";
            versionCopy["PackId"] = copy["_id"];
            versionCopy["DigitalProductPassportId"] = $"urn:test:check:{i:D5}";
            versionCopy["StaticCore"]["dppHeader"]["uniqueProductIdentifier"] = $"urn:test:check-battery:{i:D5}"; // the version shows the copy's own IDs
            versionCopy["StaticCore"]["batteryPassportData"]["identifiersAndProductData"]["batterySerialNumber"] = $"CHECK-{i:D5}";
            versionCopy["Number"] = 4;
            newVersions.Add(versionCopy);

            // Three older versions each, archived: read only as number and status (round seven, 2).
            for (var number = 1; number <= 3; number++)
            {
                var older = versionCopy.DeepClone().AsBsonDocument;
                older["_id"] = $"version-check-{i:D5}-{number}";
                older["Number"] = number;
                older["DppStatus"] = DppStatuses.Archived;
                older["DigitalProductPassportId"] = $"urn:test:check:{i:D5}:{number}";
                newVersions.Add(older);
            }
        }

        await packs.InsertManyAsync(newPacks);
        await versions.InsertManyAsync(newVersions);

        var since = await StartProfilingAsync(rig);
        var before = await StartCountingAsync(rig, writesOnly: false);
        var result = await rig.Publication.ReconcileAsync("check-test");
        var used = await StopCountingAsync(rig, before, writesOnly: false);

        Assert.Equal(5001, result.Checked);
        Assert.False(result.ChangedAnything); // the copies equal their versions (same content, passport ID aside)
        Assert.True(used < 60, $"the check of 5,001 batteries used {used} database operations");

        // Of 20,001 versions only the 5,001 active ones are read in full; the others as number and status (round seven, 2).
        Assert.Equal(5001, await FullVersionsReadAsync(rig, since));
    }

    [Fact]
    public async Task CheckAllPassportsNow_ShouldSayWhatItPutRight()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo).WithClustersAsync();
        await rig.Store.MarkChangesWaitingAsync([rig.PackId], DateTime.UtcNow);
        var controller = BatteriesControllerTests.Controller(rig, new Dictionary<string, string>());

        var result = Assert.IsType<RedirectResult>(await controller.CheckAll(rig.Publication, new PassportCheckLeftoversTests.NotStopping()));

        Assert.Equal("/admin/batteries", result.Url);
        Assert.Equal("Checked 1 published battery: 0 now marked as waiting, 1 no longer, 0 old versions archived.", controller.TempData[ProductDataController.StatusKey]);
    }

    [Theory]
    [InlineData(null, true)]
    [InlineData("on", true)]
    [InlineData("off", false)]
    [InlineData("false", false)]
    public void TheSetting_ShouldTurnTheBackgroundCheckOff(string? value, bool on)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["DPP_PASSPORT_CHECK"] = value }).Build();

        Assert.Equal(on, PassportCheckService.IsOn(configuration));
    }

    /// <summary>When the profiling of a test starts, to read back the operations after it.</summary>
    private static Task<DateTime> StartProfilingAsync(PublicationRig rig) => Task.FromResult(DateTime.UtcNow.AddSeconds(-1));

    /// <summary>How many version documents the profiled reads returned without a projection, i.e. in full.</summary>
    private static async Task<long> FullVersionsReadAsync(PublicationRig rig, DateTime since)
    {
        var database = rig.Context.Database!;
        var reads = await database.GetCollection<BsonDocument>("system.profile")
            .Find(Builders<BsonDocument>.Filter.Eq("ns", $"{database.DatabaseNamespace.DatabaseName}.{DppStore.Versions}")
                & Builders<BsonDocument>.Filter.In("op", new[] { "query", "getmore" })
                & Builders<BsonDocument>.Filter.Gte("ts", since))
            .ToListAsync();
        return reads
            .Where(read => !(read.GetValue("command", new BsonDocument()).AsBsonDocument.Contains("projection")
                || read.GetValue("originatingCommand", new BsonDocument()).AsBsonDocument.Contains("projection")))
            .Sum(read => read.GetValue("nreturned", 0).ToInt64());
    }

    /// <summary>MongoDB's profiler on the test's own database (no heartbeats, no other databases).</summary>
    private static async Task<long> StartCountingAsync(PublicationRig rig, bool writesOnly)
    {
        var database = rig.Context.Database!;
        await database.RunCommandAsync<BsonDocument>(new BsonDocument("profile", 2));
        return await ProfiledAsync(database, writesOnly);
    }

    private static async Task<long> StopCountingAsync(PublicationRig rig, long before, bool writesOnly)
    {
        var database = rig.Context.Database!;
        var after = await ProfiledAsync(database, writesOnly);
        await database.RunCommandAsync<BsonDocument>(new BsonDocument("profile", 0));
        return after - before;
    }

    private static Task<long> ProfiledAsync(IMongoDatabase database, bool writesOnly)
    {
        var filter = Builders<BsonDocument>.Filter.Not(Builders<BsonDocument>.Filter.Regex("ns", new BsonRegularExpression(@"\.system\.profile$")))
            & Builders<BsonDocument>.Filter.Ne("command.profile", 2) & Builders<BsonDocument>.Filter.Ne("command.profile", 0);
        if (writesOnly)
        {
            filter &= Builders<BsonDocument>.Filter.In("op", new[] { "insert", "update", "remove" });
        }

        return database.GetCollection<BsonDocument>("system.profile").CountDocumentsAsync(filter);
    }
}
