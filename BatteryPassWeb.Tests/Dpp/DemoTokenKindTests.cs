using BatteryPassWeb.Dpp.Demo;
using BatteryPassWeb.Services;
using MongoDB.Bson;
using MongoDB.Driver;

namespace BatteryPassWeb.Tests.Dpp;

/// <summary>
/// Finding R03, the manager's point 5: a token counts as demo when it is marked or when every cluster it is for is a demo
/// cluster, so a token the team or the start-up made for a demo cluster (and one regenerated from it) stays inside the demo;
/// the mark is written in the token's own insert.
/// </summary>
[Collection(MongoCollection.Name)]
public sealed class DemoTokenKindTests(MongoFixture mongo)
{
    private const string DemoCluster = "demo-x";
    private const string Key = "token-kind-test-key-for-the-token-hashes-0";

    private static async Task<MongoContext> ContextAsync(MongoFixture mongo)
    {
        var context = mongo.NewContext();
        var clusters = new ClusterRepository(context);
        await clusters.UpsertClusterAsync(DemoCluster, "Demo X");
        await clusters.UpsertClusterAsync("north", "Real north");
        await context.Database!.GetCollection<BsonDocument>("clusters").UpdateOneAsync(
            Builders<BsonDocument>.Filter.Eq("clusterId", DemoCluster), Builders<BsonDocument>.Update.Set(ClusterRepository.DemoMark, true));
        return context;
    }

    [Theory]
    [InlineData(new[] { DemoCluster }, false, true)]
    [InlineData(new[] { DemoCluster, "north" }, false, false)]
    [InlineData(new string[0], true, false)] // a global token isn't for clusters: only its mark makes it demo
    public async Task AToken_ShouldBeDemo_WhenEveryClusterItIsForIsADemoCluster(string[] clusterIds, bool global, bool demo)
    {
        var repository = new ExternalApiRepository(await ContextAsync(mongo), new ExternalApiSecurityService(Key));
        var (_, value) = await repository.CreateTokenAsync("made by the team", ExternalTokenAccessMode.Read, clusterIds, false, global, "team@example.test");

        var validated = await repository.ValidateTokenAsync(value, ExternalTokenRequirement.Read);

        Assert.True(validated.Success);
        Assert.Equal(demo, validated.Context!.IsDemo);
    }

    [Fact]
    public async Task ATokenForADemoCluster_ShouldBeRefused_WhenTheDemoIsSwitchedOff()
    {
        var context = await ContextAsync(mongo);
        var (_, value) = await new ExternalApiRepository(context, new ExternalApiSecurityService(Key)).CreateTokenAsync("start-up token", ExternalTokenAccessMode.Read, [DemoCluster], false, false, "system");

        var refused = await new ExternalApiRepository(context, new ExternalApiSecurityService(Key), new DemoDataSwitch(false)).ValidateTokenAsync(value, ExternalTokenRequirement.Read);

        Assert.False(refused.Success);
        Assert.Equal(DemoDataSwitch.TokenOff, refused.Message);
    }

    [Fact]
    public async Task ARegeneratedTokenForADemoCluster_ShouldStayDemo_AndBeRefusedWhenTheDemoIsSwitchedOff()
    {
        var context = await ContextAsync(mongo);
        var repository = new ExternalApiRepository(context, new ExternalApiSecurityService(Key));
        var (document, _) = await repository.CreateTokenAsync("start-up token", ExternalTokenAccessMode.ReadWrite, [DemoCluster], false, false, "system", autoClusterId: DemoCluster);

        var regenerated = (await repository.RegenerateTokenAsync(document["tokenId"].AsString, "demo.admin@example.test"))!;

        Assert.True((await repository.ValidateTokenAsync(regenerated, ExternalTokenRequirement.Read)).Context!.IsDemo);
        Assert.False((await new ExternalApiRepository(context, new ExternalApiSecurityService(Key), new DemoDataSwitch(false)).ValidateTokenAsync(regenerated, ExternalTokenRequirement.Read)).Success);
    }

    [Fact]
    public async Task TheDemoMark_ShouldBeWrittenWithTheToken()
    {
        var repository = new ExternalApiRepository(await ContextAsync(mongo), new ExternalApiSecurityService(Key));

        var (document, _) = await repository.CreateTokenAsync("Demo team (full access)", ExternalTokenAccessMode.Lifecycle, [], false, true, "demo", isDemo: true);

        Assert.True(document[ClusterRepository.DemoMark].AsBoolean);
    }
}
