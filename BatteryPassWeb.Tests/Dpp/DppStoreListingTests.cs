using BatteryPassWeb.Dpp.Documents;
using BatteryPassWeb.Dpp.Store;

namespace BatteryPassWeb.Tests.Dpp;

[Collection(MongoCollection.Name)]
public sealed class DppStoreListingTests(MongoFixture mongo)
{
    [Fact]
    public async Task Lists_ShouldReturnTheRecordsOfEachKind()
    {
        var (store, _, _) = await DppStoreTesting.SeededStoreAsync(mongo);
        await store.InsertAsync(new BatteryModel { Id = "model-a", FamilyId = "family-test", Name = "AAA-1" });
        await store.InsertAsync(new BatteryModel { Id = "model-other", FamilyId = "family-other", Name = "OTHER" });
        await store.InsertAsync(new ProductionBatch { Id = "batch-2027", ModelId = "model-test", SiteId = "site-test", Year = 2027 });

        Assert.Equal(["family-test"], (await store.ListAsync<BatteryFamily>()).Select(f => f.Id));
        Assert.Equal(["model-a", "model-test"], (await store.ListModelsAsync("family-test")).Select(m => m.Id)); // by name
        Assert.Equal(["batch-2027", "batch-test-2026"], (await store.ListBatchesAsync("model-test")).Select(b => b.Id)); // newest first
        Assert.Equal(["site-test"], (await store.ListAsync<ManufacturingSite>()).Select(s => s.Id));
        Assert.Equal(["operator-test"], (await store.ListAsync<EconomicOperator>()).Select(o => o.Id));
    }

    [Fact]
    public async Task GetMany_ShouldLeaveOutIdsThatDontExist()
    {
        var (store, _, _) = await DppStoreTesting.SeededStoreAsync(mongo);

        var models = await store.GetManyAsync<BatteryModel>(["model-test", "no-such-model", "model-test"]);

        Assert.Equal(["model-test"], models.Select(m => m.Id));
        Assert.Empty(await store.GetManyAsync<BatteryModel>([]));
    }

    [Fact]
    public async Task PackCounts_ShouldCountPublishedPacksApart()
    {
        var rig = await PublicationTesting.RigAsync(mongo);

        var draft = await rig.Store.CountPacksByBatchAsync(["batch-test-2026", "batch-empty"]);
        await rig.Publication.PublishAsync(PublicationTesting.Team, rig.PackId);
        var published = await rig.Store.CountPacksByBatchAsync(["batch-test-2026"]);

        Assert.Equal((1, 0), draft["batch-test-2026"]);
        Assert.Equal((0, 0), draft["batch-empty"]);
        Assert.Equal((1, 1), published["batch-test-2026"]);
    }

    [Fact]
    public async Task ActiveVersions_ShouldListOnlyPublishedPacks()
    {
        var rig = await PublicationTesting.RigAsync(mongo);
        Assert.Empty(await rig.Store.ListActiveVersionsAsync([rig.PackId]));

        await rig.Publication.PublishAsync(PublicationTesting.Team, rig.PackId);

        var version = Assert.Single(await rig.Store.ListActiveVersionsAsync([rig.PackId, "no-such-pack"]));
        Assert.Equal(1, version.Number);
    }

    [Fact]
    public async Task Rename_ShouldChangeTheNameOnly()
    {
        var (store, _, _) = await DppStoreTesting.SeededStoreAsync(mongo);

        Assert.True(await store.RenameAsync<ManufacturingSite>("site-test", "  Plant 2  "));
        Assert.False(await store.RenameAsync<BatteryFamily>("no-such-family", "x"));
        await Assert.ThrowsAsync<ArgumentException>(() => store.RenameAsync<ProductionBatch>("batch-test-2026", "x"));

        var site = (await store.GetAsync<ManufacturingSite>("site-test"))!;
        Assert.Equal("Plant 2", site.Name);
        Assert.Equal(3, site.Values.Count);
    }

    [Fact]
    public async Task FamilyPicture_ShouldBeStoredAndReadBack()
    {
        var (store, context, _) = await DppStoreTesting.SeededStoreAsync(mongo);
        var documents = new DocumentStore(context);
        var fileId = await documents.UploadFileAsync("family.png", DocumentTesting.Png());

        Assert.True(await store.SetFamilyPictureAsync("family-test", fileId, "image/png"));

        var family = (await store.GetAsync<BatteryFamily>("family-test"))!;
        Assert.Equal("image/png", family.PictureContentType);
        Assert.Equal(DocumentTesting.Png(), await documents.DownloadFileAsync(family.PictureFileId!));
        Assert.False(await store.SetFamilyPictureAsync("no-such-family", fileId, "image/png"));
    }
}
