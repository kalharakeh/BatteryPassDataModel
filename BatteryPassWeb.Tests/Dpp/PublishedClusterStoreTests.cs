namespace BatteryPassWeb.Tests.Dpp;

/// <summary>Which clusters a model, batch or pack value is shown to: only those owning a published pack (J-6).</summary>
[Collection(MongoCollection.Name)]
public sealed class PublishedClusterStoreTests(MongoFixture mongo)
{
    [Fact]
    public async Task DraftPack_ShouldGiveItsClusterNothing()
    {
        var rig = await PublicationTesting.RigAsync(mongo);

        Assert.Empty(await rig.Store.FindPublishedClusterIdsUsingAsync(packId: rig.PackId));
        Assert.Empty(await rig.Store.FindPublishedClusterIdsUsingAsync(modelId: "model-test"));
        Assert.Empty(await rig.Store.FindPublishedClusterIdsUsingAsync(batchId: "batch-test-2026"));
    }

    [Fact]
    public async Task PublishedPack_ShouldGiveItsClusterTheModelBatchAndPack()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo);

        Assert.Equal(["cluster-test"], await rig.Store.FindPublishedClusterIdsUsingAsync(packId: rig.PackId));
        Assert.Equal(["cluster-test"], await rig.Store.FindPublishedClusterIdsUsingAsync(modelId: "model-test"));
        Assert.Equal(["cluster-test"], await rig.Store.FindPublishedClusterIdsUsingAsync(batchId: "batch-test-2026"));
        Assert.Empty(await rig.Store.FindPublishedClusterIdsUsingAsync(modelId: "no-such-model"));
    }

    [Fact]
    public async Task APublishedPack_ShouldCountWithOrWithoutACluster()
    {
        var draft = await PublicationTesting.RigAsync(mongo);
        var published = await PublicationTesting.PublishedRigAsync(mongo);
        await published.Store.SetPackClusterAsync(published.PackId, null);

        Assert.False(await draft.Store.HasPublishedPackUsingAsync(packId: draft.PackId));
        Assert.False(await draft.Store.HasPublishedPackUsingAsync(modelId: "model-test"));
        Assert.True(await published.Store.HasPublishedPackUsingAsync(packId: published.PackId));
        Assert.True(await published.Store.HasPublishedPackUsingAsync(modelId: "model-test"));
        Assert.True(await published.Store.HasPublishedPackUsingAsync(batchId: "batch-test-2026"));
        Assert.Empty(await published.Store.FindPublishedClusterIdsUsingAsync(packId: published.PackId));
    }
}
