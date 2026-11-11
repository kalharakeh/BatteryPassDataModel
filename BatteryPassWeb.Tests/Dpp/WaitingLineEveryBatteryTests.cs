using MongoDB.Bson;
using MongoDB.Driver;
using BatteryPassWeb.Dpp.Store;

namespace BatteryPassWeb.Tests.Dpp;

/// <summary>
/// Round four, 5: the team's list names every marked battery, also a predecessor that is still active while its
/// successor is a draft (one row per battery would show only the draft).
/// </summary>
[Collection(MongoCollection.Name)]
public sealed class WaitingLineEveryBatteryTests(MongoFixture mongo)
{
    [Fact]
    public async Task APredecessorWithADraftSuccessor_ShouldBeNamed()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo).WithClustersAsync();
        var packs = rig.Context.Database!.GetCollection<BsonDocument>(DppStore.Packs);
        var successor = (await packs.Find(Builders<BsonDocument>.Filter.Eq("_id", rig.PackId)).SingleAsync()).DeepClone().AsBsonDocument;
        successor["_id"] = "pack-successor";
        successor["SuccessorOf"] = rig.PackId;
        successor["Values"]["uniqueProductIdentifier"]["Value"] = "urn:test:successor";
        await packs.InsertOneAsync(successor); // a draft: no version yet
        await rig.Store.MarkChangesWaitingAsync([rig.PackId], DateTime.UtcNow);

        var page = await BatteryTesting.Pages(rig).TeamListAsync(null);

        Assert.DoesNotContain(page.Rows, row => row.PackId == rig.PackId); // one row per battery shows the draft
        Assert.Equal(rig.PackId, Assert.Single(page.Waiting).PackId);
    }
}
