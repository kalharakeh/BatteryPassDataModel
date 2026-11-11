using BatteryPassWeb.Dpp.Editing;
using BatteryPassWeb.Dpp.Store;
using BatteryPassWeb.Services;
using MongoDB.Bson;
using MongoDB.Driver;

namespace BatteryPassWeb.Tests.Dpp;

/// <summary>
/// Round six, 2: until the check has run, a fault can leave two active versions. Readers are served only the highest
/// active one as active; marking a passport and making it inactive archive the lower active versions first.
/// </summary>
[Collection(MongoCollection.Name)]
public sealed class OneActiveVersionTests(MongoFixture mongo)
{
    [Fact]
    public async Task AnOlderActiveVersion_ShouldBeServedAsArchived()
    {
        var rig = await TwoActiveAsync();
        var older = (await rig.Store.ListVersionsAsync(rig.PackId)).OrderBy(version => version.Number).ToArray();

        Assert.Equal([DppStatuses.Archived, DppStatuses.Active], older.Select(version => version.DppStatus));
        Assert.Equal(DppStatuses.Archived, (await rig.Store.GetVersionByPassportIdAsync(older[0].DigitalProductPassportId))!.DppStatus);
        Assert.Equal(DppStatuses.Active, (await rig.Store.GetVersionByPassportIdAsync(older[1].DigitalProductPassportId))!.DppStatus);
    }

    [Fact]
    public async Task AMark_ShouldArchiveTheLowerActiveVersionsFirst()
    {
        var rig = await TwoActiveAsync();
        var marks = new PassportMarks(rig.Store, TestDemo.For(rig), new ApplicationAuditService(rig.Context), rig.Clock, rig.Publication);

        Assert.Null(await marks.MarkInvalidAsync(PublicationTesting.Team, rig.PackId, "Wrong battery"));

        Assert.Equal([DppStatuses.Archived, DppStatuses.Invalid], await StoredStatusesAsync(rig));
    }

    [Fact]
    public async Task MakingAPassportInactive_ShouldArchiveTheLowerActiveVersionsFirst()
    {
        var rig = await TwoActiveAsync();

        await rig.Publication.InactivateAsync(PublicationTesting.Team, rig.PackId, "test");

        Assert.Equal([DppStatuses.Archived, DppStatuses.Inactive], await StoredStatusesAsync(rig));
    }

    /// <summary>The seed published, and a second active version left by a fault before its archive step.</summary>
    private async Task<PublicationRig> TwoActiveAsync()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo).WithClustersAsync();
        var versions = rig.Context.Database!.GetCollection<BsonDocument>(DppStore.Versions);
        var second = (await versions.Find(Builders<BsonDocument>.Filter.Eq("PackId", rig.PackId)).SingleAsync()).DeepClone().AsBsonDocument;
        second["_id"] = "version-second";
        second["Number"] = 2;
        second["DigitalProductPassportId"] = "urn:test:second";
        await versions.InsertOneAsync(second);
        return rig;
    }

    /// <summary>The statuses as stored, not as served.</summary>
    private static async Task<string[]> StoredStatusesAsync(PublicationRig rig) =>
        (await rig.Context.Database!.GetCollection<BsonDocument>(DppStore.Versions)
            .Find(Builders<BsonDocument>.Filter.Eq("PackId", rig.PackId)).SortBy(version => version["Number"]).ToListAsync())
            .Select(version => version["DppStatus"].AsString)
            .ToArray();
}
