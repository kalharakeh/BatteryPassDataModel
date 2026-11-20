using BatteryPassWeb.Dpp.Access;
using BatteryPassWeb.Dpp.Editing;
using BatteryPassWeb.Dpp.Pages;
using BatteryPassWeb.Dpp.Store;
using BatteryPassWeb.Services;
using MongoDB.Bson;
using MongoDB.Driver;

namespace BatteryPassWeb.Tests.Dpp;

/// <summary>
/// H1 round two, point 5: cluster IDs compare like the rights check (case-blind, nothing more): the "Manage" link for an
/// old battery stored with capitals; deleting one cluster never reaches another whose ID the database's collation calls
/// equal ("south" in full-width letters and "south"; the review named "straße", which this MongoDB keeps apart); and a token for a cluster that no longer exists answers as an unknown token.
/// </summary>
[Collection(MongoCollection.Name)]
public sealed class ClusterIdRoundTwoTests(MongoFixture mongo)
{
    [Fact]
    public void TheManageLink_ShouldBeThereForABatteryStoredWithCapitals()
    {
        var page = new RegistryPage([], string.Empty, false, string.Empty)
        {
            ManagedClusterIds = RegistryList.Build(Reader.Anonymous, new BatteriesPage([], string.Empty, 0), ChangeSender.ClusterAdmin("admin@example.test", "north")).ManagedClusterIds,
        };

        Assert.Contains("North", page.ManagedClusterIds);
    }

    private static async Task<(DppStore Store, IMongoCollection<BsonDocument> Packs, string PackId)> LookAlikeClusterAsync(MongoFixture mongo)
    {
        var (store, context, seed) = await DppStoreTesting.SeededStoreAsync(mongo);
        var packs = context.Database!.GetCollection<BsonDocument>(DppStore.Packs);
        await packs.UpdateOneAsync(Builders<BsonDocument>.Filter.Eq("_id", seed.Sources.Pack.Id), Builders<BsonDocument>.Update.Set("ClusterId", "ｓｏｕｔｈ"));
        return (store, packs, seed.Sources.Pack.Id);
    }

    [Fact]
    public async Task CountingAndClearing_ShouldNotReachAClusterTheCollationOnlyCallsEqual()
    {
        var (store, packs, packId) = await LookAlikeClusterAsync(mongo);

        Assert.Equal(0, await store.CountPacksInClusterAsync("south"));
        Assert.Equal(0, await store.ClearClusterAsync("south"));
        Assert.Equal("ｓｏｕｔｈ", (await packs.Find(Builders<BsonDocument>.Filter.Eq("_id", packId)).SingleAsync())["ClusterId"].AsString);

        Assert.Equal(1, await store.CountPacksInClusterAsync("ＳＯＵＴＨ"));
    }

    [Fact]
    public async Task CancellingHandOvers_ShouldNotReachAClusterTheCollationOnlyCallsEqual()
    {
        var (store, _, seed) = await DppStoreTesting.SeededStoreAsync(mongo);
        var handOver = new HandOver { Id = "handover-1", PackId = seed.Sources.Pack.Id, FromClusterId = "north", ToClusterId = "ｓｏｕｔｈ", State = HandOverStates.Waiting, AskedBy = "a@example.test", AskedAtUtc = DateTime.UtcNow };
        await store.InsertAsync(handOver);

        Assert.Equal(0, await store.CancelWaitingHandOversOfClusterAsync("south", "team", DateTime.UtcNow));
        Assert.Equal(HandOverStates.Waiting, (await store.GetAsync<HandOver>("handover-1"))!.State);
        Assert.Equal(1, await store.CancelWaitingHandOversOfClusterAsync("ＳＯＵＴＨ", "team", DateTime.UtcNow));
    }

    [Fact]
    public async Task ATokenForAClusterThatNoLongerExists_ShouldAnswerAsAnUnknownToken()
    {
        var context = mongo.NewContext();
        var clusters = new ClusterRepository(context);
        await clusters.UpsertClusterAsync("north", "Customer North");
        var tokens = new ExternalApiRepository(context, new ExternalApiSecurityService("cluster-gone-test-key-for-the-hashes-0"));
        var (_, gone) = await tokens.CreateTokenAsync("Gone", ExternalTokenAccessMode.Read, ["gone"], false, false, "team");
        var (_, both) = await tokens.CreateTokenAsync("Both", ExternalTokenAccessMode.Read, ["gone", "north"], false, false, "team");
        var (_, team) = await tokens.CreateTokenAsync("Team", ExternalTokenAccessMode.Read, [], true, true, "team");

        var answer = await tokens.ValidateTokenAsync(gone, ExternalTokenRequirement.Read);
        Assert.False(answer.Success);
        Assert.Equal("Token is invalid.", answer.Message);

        var narrowed = await tokens.ValidateTokenAsync(both, ExternalTokenRequirement.Read);
        Assert.True(narrowed.Success);
        Assert.Equal(["north"], narrowed.Context!.ClusterIds);

        Assert.True((await tokens.ValidateTokenAsync(team, ExternalTokenRequirement.Read)).Success);
    }

    [Fact]
    public async Task MakingAClusterWithTheIdOfAnOldOne_ShouldRevokeTheOldOnesLeftoverTokens()
    {
        using var rig = await BatteryPassWeb.Tests.Accounts.AccountRig.StartAsync(mongo);
        var tokens = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<ExternalApiRepository>(rig.Services);
        var (_, leftover) = await tokens.CreateTokenAsync("Left over from before W8", ExternalTokenAccessMode.Read, ["gone"], false, false, "team");

        await BatteryPassWeb.Tests.Accounts.AccountRig.Form(rig.TeamMember(), ("name", "A new customer"), ("clusterId", "Gone")).CreateCluster(CancellationToken.None);

        Assert.NotNull(await rig.Clusters.GetClusterByIdAsync("gone"));
        Assert.False((await tokens.ValidateTokenAsync(leftover, ExternalTokenRequirement.Read)).Success);
    }
}
