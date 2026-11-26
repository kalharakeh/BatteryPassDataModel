using System.Text.Json.Nodes;
using BatteryPassWeb.Dpp.Editing;
using BatteryPassWeb.Dpp.Identifiers;
using BatteryPassWeb.Dpp.Store;
using BatteryPassWeb.Tests.Demo;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Bson;
using MongoDB.Driver;

namespace BatteryPassWeb.Tests.Dpp;

/// <summary>
/// U16, the way round it: changing a draft's battery ID may only take an ID of the same kind (demo to demo, real to real),
/// so a real ID can't reach demo product data by a swap and then be published.
/// </summary>
[Collection(MongoCollection.Name)]
public sealed class BatteryIdSwapKindTests(MongoFixture mongo)
{
    private const string Who = DemoRig.Who;

    private static async Task<(DemoRig Rig, BatteryIdPool Pool, BatteryPack DemoDraft)> SeededAsync(MongoFixture mongo)
    {
        var rig = await DemoRig.StartAsync(mongo, dunsIds: true);
        await rig.Demo.SeedAsync(Who);
        var draft = (await rig.Store.FindPacksBySerialNumberAsync("DEMO-C7M-0002")).Single(); // a draft with a demo ID on the demo batch
        return (rig, rig.Services.GetRequiredService<BatteryIdPool>(), draft);
    }

    [Fact]
    public async Task ADemoDraft_ShouldRefuseARealId_AndSoNeverPublishItOnDemoData()
    {
        var (rig, pool, draft) = await SeededAsync(mongo);
        using var _ = rig;
        await pool.ReleaseAsync(Who, new ReleaseRequest("PO 1", null, [], "REAL-0001"));
        var before = (await rig.Store.GetAsync<BatteryPack>(draft.Id))!.Values["uniqueProductIdentifier"].Value!.GetValue<string>();

        var refused = await pool.SwapAsync(Who, draft.Id, "REAL-0001");

        Assert.Equal(BatteryIdPool.SwapNeedsSameKind, refused);
        var after = (await rig.Store.GetAsync<BatteryPack>(draft.Id))!;
        Assert.Equal(before, after.Values["uniqueProductIdentifier"].Value!.GetValue<string>());
        Assert.True(after.IsDemo);
        Assert.Equal(ReleasedIdStatuses.Available, (await pool.GetAsync("REAL-0001"))!.Status); // not taken
    }

    [Fact]
    public async Task ADemoDraft_ShouldTakeAnotherDemoId()
    {
        var (rig, pool, draft) = await SeededAsync(mongo);
        using var _ = rig;

        Assert.Null(await pool.SwapAsync(Who, draft.Id, "DEMO-SPARE-0001"));

        var swapped = (await rig.Store.GetAsync<BatteryPack>(draft.Id))!;
        Assert.Equal("DEMO-SPARE-0001", swapped.Values["batterySerialNumber"].Value!.GetValue<string>());
        Assert.True(swapped.IsDemo);
    }

    [Fact]
    public async Task ARealDraft_ShouldRefuseADemoId_ButTakeAnotherRealOne()
    {
        var (rig, pool, demoDraft) = await SeededAsync(mongo);
        using var _ = rig;
        var batch = (await rig.Store.GetAsync<ProductionBatch>(demoDraft.BatchId))!;
        foreach (var (collection, id) in new[] { (DppStore.Batches, batch.Id), (DppStore.Models, batch.ModelId), (DppStore.Operators, demoDraft.OperatorId) })
        {
            await rig.Database.GetCollection<BsonDocument>(collection).UpdateOneAsync(Builders<BsonDocument>.Filter.Eq("_id", id), Builders<BsonDocument>.Update.Set("IsDemo", false));
        }

        await pool.ReleaseAsync(Who, new ReleaseRequest("PO 2", null, [], "REAL-0002\nREAL-0003"));
        var real = (await rig.Services.GetRequiredService<PackCreationService>().CreateAsync(
            ChangeSender.Team(Who),
            new NewPack(demoDraft.BatchId, demoDraft.OperatorId, null, new Dictionary<string, JsonNode?> { ["batterySerialNumber"] = "REAL-0002" }, FromPool: true))).Pack!; // no demo customer for a real ID (M1 round two D)

        Assert.Equal(BatteryIdPool.SwapNeedsSameKind, await pool.SwapAsync(Who, real.Id, "DEMO-SPARE-0001"));
        Assert.Null(await pool.SwapAsync(Who, real.Id, "REAL-0003"));
    }
}
