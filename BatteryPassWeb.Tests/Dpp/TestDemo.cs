using BatteryPassWeb.Dpp.Access;
using BatteryPassWeb.Dpp.Store;
using BatteryPassWeb.Services;

namespace BatteryPassWeb.Tests.Dpp;

/// <summary>The demo boundary (finding R03) on a test database, for services the tests build by hand.</summary>
internal static class TestDemo
{
    public static DemoBoundary For(DppStore store, MongoContext context) => new(store, new ClusterRepository(context));

    public static DemoBoundary For(PublicationRig rig) => For(rig.Store, rig.Context);

    /// <summary>Sets the demo mark on one record of a collection.</summary>
    public static Task MarkAsync(PublicationRig rig, string collection, string id) =>
        rig.Context.Database!.GetCollection<MongoDB.Bson.BsonDocument>(collection).UpdateOneAsync(
            MongoDB.Driver.Builders<MongoDB.Bson.BsonDocument>.Filter.Eq("_id", id),
            MongoDB.Driver.Builders<MongoDB.Bson.BsonDocument>.Update.Set("IsDemo", true));

    /// <summary>
    /// The seed battery inside the demo (<see cref="DemoScope.Inside"/>): its pack, batch, model and operator marked demo,
    /// and its cluster, when it has one, a demo cluster.
    /// </summary>
    public static async Task MakeInsideAsync(PublicationRig rig, bool cluster = true, bool productData = true)
    {
        var sources = rig.Seed.Sources;
        await MarkAsync(rig, DppStore.Packs, rig.PackId);
        if (productData)
        {
            await MarkAsync(rig, DppStore.Batches, sources.Batch.Id);
            await MarkAsync(rig, DppStore.Models, sources.Model.Id);
            await MarkAsync(rig, DppStore.Operators, sources.Operator.Id);
        }

        if (cluster && (await rig.Store.GetAsync<BatteryPack>(rig.PackId))?.ClusterId is { } clusterId)
        {
            await new ClusterRepository(rig.Context).UpsertClusterAsync(clusterId, "Demo cluster");
            await rig.Context.Database!.GetCollection<MongoDB.Bson.BsonDocument>("clusters").UpdateOneAsync(
                MongoDB.Driver.Builders<MongoDB.Bson.BsonDocument>.Filter.Eq("clusterId", ClusterRepository.NormalizeClusterId(clusterId)),
                MongoDB.Driver.Builders<MongoDB.Bson.BsonDocument>.Update.Set(ClusterRepository.DemoMark, true));
        }
    }
}
