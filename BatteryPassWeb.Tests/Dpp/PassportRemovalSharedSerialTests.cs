using BatteryPassWeb.Dpp.Documents;
using BatteryPassWeb.Dpp.Editing;
using BatteryPassWeb.Dpp.Store;
using BatteryPassWeb.Dpp.Uploads;
using BatteryPassWeb.Services;
using Microsoft.Extensions.Logging;
using MongoDB.Bson;
using MongoDB.Driver;

namespace BatteryPassWeb.Tests.Dpp;

/// <summary>
/// Finding R08, round four: a serial number another battery shares isn't the removed battery's to clean up; two removals
/// or finishes at once write one plan and one audit entry; an upload while a removal is stopped logs nothing of the battery
/// and the finish takes any line with its pack.
/// </summary>
[Collection(MongoCollection.Name)]
public sealed class PassportRemovalSharedSerialTests(MongoFixture mongo)
{
    private static readonly ChangeSender Team = PublicationTesting.Team;

    [Fact]
    public async Task ASerialAnotherBatteryHas_ShouldStayTheOthers()
    {
        var rig = await MarkedRigAsync();
        var serial = UploadServiceTests.Serial(rig);
        var database = rig.Context.Database!;
        var packs = database.GetCollection<BsonDocument>(DppStore.Packs);
        var twin = (await packs.Find(Builders<BsonDocument>.Filter.Eq("_id", rig.PackId)).SingleAsync()).DeepClone().AsBsonDocument;
        twin["_id"] = "pack-twin";
        twin["Values"]["uniqueProductIdentifier"]["Value"] = "urn:test:twin-battery";
        twin.Remove("Mark");
        await packs.InsertOneAsync(twin); // a live battery with the same serial number
        var uploads = database.GetCollection<BsonDocument>(DppStore.Uploads);
        await uploads.InsertOneAsync(new BsonDocument { ["_id"] = "upload-ambiguous", ["AtUtc"] = DateTime.UtcNow, ["Who"] = "x", ["SentAs"] = "Team", ["Channel"] = "api", ["Format"] = "native", ["Batteries"] = new BsonArray { new BsonDocument { ["Entry"] = 1, ["NamedAs"] = serial, ["PackId"] = BsonNull.Value, ["Outcome"] = "refused" } } });
        var events = database.GetCollection<BsonDocument>("applicationAuditEvents");
        await events.InsertOneAsync(new BsonDocument { ["eventType"] = BatteryPassWeb.Dpp.Publication.PublicationService.CheckedEvent, ["entityType"] = "dpp-passports", ["entityId"] = "all", ["message"] = "Checked.", ["metadata"] = new BsonDocument { ["batteries"] = new BsonArray { serial } } });

        Assert.Null(await PassportRemovalTests.Removal(rig).RemoveAsync(Team, rig.PackId, Identifier(rig)));

        Assert.DoesNotContain(serial, (await rig.Store.GetAsync<RemovedPassport>(rig.PackId))!.Names);
        Assert.Null(await rig.Store.FindRemovedAsync(serial)); // the serial still names the other battery
        Assert.Equal(1, await uploads.CountDocumentsAsync(Builders<BsonDocument>.Filter.Eq("_id", "upload-ambiguous")));
        var check = await events.Find(Builders<BsonDocument>.Filter.Eq("message", "Checked.")).SingleAsync();
        Assert.Equal([serial], check["metadata"]["batteries"].AsBsonArray.Select(item => item.AsString));
    }

    [Fact]
    public async Task TwoRemovalsAtOnce_ShouldWriteOnePlan_AndOneAuditEntry()
    {
        var rig = await MarkedRigAsync();
        var first = PassportRemovalTests.Removal(rig);
        var second = PassportRemovalTests.Removal(rig);

        await Task.WhenAll(
            Task.Run(() => first.RemoveAsync(Team, rig.PackId, Identifier(rig))),
            Task.Run(() => second.RemoveAsync(Team, rig.PackId, Identifier(rig))));
        await Task.WhenAll(
            Task.Run(() => first.FinishAsync(rig.PackId)),
            Task.Run(() => second.FinishAsync(rig.PackId)));

        var entries = await rig.Context.Database!.GetCollection<BsonDocument>("applicationAuditEvents")
            .Find(Builders<BsonDocument>.Filter.Eq("eventType", PassportRemoval.RemovedEvent)).ToListAsync();
        Assert.Equal(PassportRemoval.RemovalEntryId(rig.PackId), Assert.Single(entries)["_id"].AsString);
        Assert.False(await rig.Store.IsAnythingLeftAsync(rig.PackId));
    }

    [Fact]
    public async Task TheMarker_ShouldKeepThePlanItWasWrittenWith()
    {
        var rig = await MarkedRigAsync();
        await Assert.ThrowsAsync<InvalidOperationException>(() => Removal(rig, new FailAfter("markers")).RemoveAsync(Team, rig.PackId, Identifier(rig)));
        var written = (await rig.Store.GetAsync<RemovedPassport>(rig.PackId))!;

        await rig.Store.AddRemovalMarkersAsync([new RemovedPassport { Id = rig.PackId, BatteryId = "another plan", Ids = [], RemovedAtUtc = DateTime.UtcNow, RemovedBy = "late@example.com" }]);

        var kept = (await rig.Store.GetAsync<RemovedPassport>(rig.PackId))!;
        Assert.Equal((written.BatteryId, written.RemovedBy), (kept.BatteryId, kept.RemovedBy));
        Assert.Equal(written.Ids, kept.Ids);
    }

    [Fact]
    public async Task AnUploadWhileARemovalIsStopped_ShouldLogNothingOfTheBattery_AndTheFinishShouldTakeAnyLineWithItsPack()
    {
        var rig = await MarkedRigAsync();
        await Assert.ThrowsAsync<InvalidOperationException>(() => Removal(rig, new FailAfter("audit entry")).RemoveAsync(Team, rig.PackId, Identifier(rig)));

        var report = await UploadServiceTests.Run(rig, Team, UploadServiceTests.File(UploadServiceTests.Entry(Identifier(rig), """{ "stateOfChargeSoC": 41 }""")));
        var line = Assert.Single(report.Batteries);
        Assert.Equal((UploadService.Refused, UploadService.GoneRefusal, (string?)null), (line.Outcome, line.Problem, line.PackId));
        Assert.Empty(line.Values);
        var uploads = rig.Context.Database!.GetCollection<BsonDocument>(DppStore.Uploads);
        Assert.Equal(0, await uploads.CountDocumentsAsync(Builders<BsonDocument>.Filter.Eq("Batteries.PackId", rig.PackId)));

        // A line with the pack that the plan doesn't know of (logged by an older version, say).
        await uploads.InsertOneAsync(new BsonDocument { ["_id"] = "upload-late", ["AtUtc"] = DateTime.UtcNow, ["Who"] = "x", ["SentAs"] = "Team", ["Channel"] = "page", ["Format"] = "native", ["Batteries"] = new BsonArray { new BsonDocument { ["Entry"] = 1, ["NamedAs"] = "x", ["PackId"] = rig.PackId, ["Outcome"] = "saved" } } });
        await PassportRemovalTests.Removal(rig).FinishAsync(rig.PackId);

        Assert.Equal(0, await uploads.CountDocumentsAsync(Builders<BsonDocument>.Filter.Eq("_id", "upload-late")));
        Assert.Equal(0, await uploads.CountDocumentsAsync(Builders<BsonDocument>.Filter.Eq("Batteries.NamedAs", Identifier(rig))));
        Assert.False(await rig.Store.IsAnythingLeftAsync(rig.PackId));
    }

    private async Task<PublicationRig> MarkedRigAsync()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo).WithClustersAsync();
        await StatusChangeTests.Service(rig).ChangeAsync(Team, rig.PackId, new StatusChange(StatusEvent.Waste, await StatusChangeTests.CurrentAsync(rig)));
        Assert.Null(await PassportMarkTests.Service(rig).MarkForDeletionAsync(Team, rig.PackId, "recycled"));
        return rig;
    }

    private static string Identifier(PublicationRig rig) => rig.Seed.Sources.Pack.Values["uniqueProductIdentifier"].Value!.GetValue<string>();

    private static PassportRemoval Removal(PublicationRig rig, ILogger<PassportRemoval> logger) =>
        new(rig.Store, TestDemo.For(rig), new DocumentStore(rig.Context), new ApplicationAuditService(rig.Context), rig.Clock, logger);

    /// <summary>Fails the removal right after one step.</summary>
    private sealed class FailAfter(string step) : ILogger<PassportRemoval>
    {
        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (logLevel == LogLevel.Debug && formatter(state, exception).EndsWith($": {step} done.", StringComparison.Ordinal))
            {
                throw new InvalidOperationException($"Injected failure after {step}.");
            }
        }
    }
}
