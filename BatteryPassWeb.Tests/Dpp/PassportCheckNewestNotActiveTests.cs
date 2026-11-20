using BatteryPassWeb.Dpp.Store;
using MongoDB.Bson;
using MongoDB.Driver;

namespace BatteryPassWeb.Tests.Dpp;

/// <summary>
/// Round seven, 1: a battery whose newest version is no longer active (invalid here) while a lower one is still active,
/// as a mark left it before round six. The check archives the active versions below the newest and sets no marker.
/// </summary>
[Collection(MongoCollection.Name)]
public sealed class PassportCheckNewestNotActiveTests(MongoFixture mongo)
{
    [Theory]
    [InlineData(DppStatuses.Invalid)]
    [InlineData(DppStatuses.MarkedForDeletion)]
    [InlineData(DppStatuses.Inactive)]
    public async Task ALowerActiveVersion_ShouldBeArchived_AndTheBatteryNotMarked(string newestStatus)
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo);
        var versions = rig.Context.Database!.GetCollection<BsonDocument>(DppStore.Versions);
        var second = (await versions.Find(Builders<BsonDocument>.Filter.Eq("PackId", rig.PackId)).SingleAsync()).DeepClone().AsBsonDocument;
        second["_id"] = "version-second";
        second["Number"] = 2;
        second["DigitalProductPassportId"] = "urn:test:second";
        second["DppStatus"] = newestStatus; // the mark acted only on the highest version
        await versions.InsertOneAsync(second);

        var result = await rig.Publication.ReconcileAsync("check-test");

        Assert.Equal((1, 0, 0), (result.Archived, result.Marked, result.Cleared));
        Assert.Null(await rig.Store.GetActiveVersionAsync(rig.PackId)); // no lower version is active
        Assert.Equal(newestStatus, await rig.Store.PassportStatusAsync(rig.PackId)); // readers serve the newest version's state
        Assert.Null((await rig.Store.GetAsync<BatteryPack>(rig.PackId))!.ChangesWaitingSinceUtc);
    }
}
