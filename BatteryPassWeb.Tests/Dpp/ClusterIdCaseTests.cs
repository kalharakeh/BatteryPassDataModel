using BatteryPassWeb.Dpp.Access;
using BatteryPassWeb.Dpp.Catalog;
using BatteryPassWeb.Dpp.Editing;
using BatteryPassWeb.Dpp.Store;
using BatteryPassWeb.Services;

namespace BatteryPassWeb.Tests.Dpp;

/// <summary>
/// Finding W13: a cluster ID typed in capitals must not cost the cluster its rights. IDs are written in one form (lower
/// case), and rights compare without regard to capitals, so a record an older version wrote with capitals keeps working.
/// </summary>
[Collection(MongoCollection.Name)]
public sealed class ClusterIdCaseTests(MongoFixture mongo)
{
    private static AttributeDefinition ClusterAdminAttribute =>
        AttributeCatalog.All.First(attribute => attribute.Editors.HasFlag(AttributeEditors.BatteryClusterAdmins));

    [Theory]
    [InlineData("North")]
    [InlineData("NORTH")]
    [InlineData("north")]
    public void AClusterAdmin_ShouldEditABatteryWhoseClusterIsStoredInAnyCapitals(string stored)
    {
        var sender = ChangeSender.ClusterAdmin("admin@example.test", "north");

        Assert.True(EditRights.MayEdit(sender, ClusterAdminAttribute, stored));
    }

    [Fact]
    public void ATokenForACluster_ShouldReadAndSendForABatteryStoredInAnyCapitals()
    {
        var token = new ExternalApiTokenContext { TokenId = "t1", ClusterIds = ["North"] };

        Assert.Contains("North", DppReaders.For(token).ClusterIds);
        Assert.Contains("NORTH", DppReaders.SenderFor(token).ClusterIds);
        Assert.True(AudienceRules.MaySee(DppReaders.For(token), AttributeCatalog.ByNumber(63), ["North"]));
    }

    [Fact]
    public async Task AUserOfACluster_ShouldSeePurchaserDataOfABatteryStoredWithCapitals()
    {
        var clusters = new ClusterRepository(mongo.NewContext());
        await clusters.UpsertUserAsync("user@example.test", "User", ["member"]);
        await clusters.UpsertClusterMembershipAsync("user@example.test", "North", "member");
        var reader = await new DppReaders(clusters).ForAsync(new System.Security.Claims.ClaimsPrincipal(new System.Security.Claims.ClaimsIdentity(
            [new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.Email, "user@example.test"), new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.Role, "member")], "test")));

        Assert.True(AudienceRules.MaySee(reader, AttributeCatalog.ByNumber(63), ["NORTH"]));
    }

    [Fact]
    public async Task MovingABatteryToAClusterTypedInCapitals_ShouldStoreItInLowerCase()
    {
        var (store, _, seed) = await DppStoreTesting.SeededStoreAsync(mongo);

        Assert.True(await store.SetPackClusterAsync(seed.Sources.Pack.Id, "  NORTH "));

        Assert.Equal("north", (await store.GetAsync<BatteryPack>(seed.Sources.Pack.Id))!.ClusterId);
        Assert.True(await store.SetPackClusterAsync(seed.Sources.Pack.Id, null));
        Assert.Null((await store.GetAsync<BatteryPack>(seed.Sources.Pack.Id))!.ClusterId);
    }

    [Fact]
    public async Task CountingAndClearingAClusterBatteries_ShouldFindThoseStoredWithCapitals()
    {
        var (store, context, seed) = await DppStoreTesting.SeededStoreAsync(mongo);
        await context.Database!.GetCollection<MongoDB.Bson.BsonDocument>(DppStore.Packs).UpdateOneAsync(
            MongoDB.Driver.Builders<MongoDB.Bson.BsonDocument>.Filter.Eq("_id", seed.Sources.Pack.Id),
            MongoDB.Driver.Builders<MongoDB.Bson.BsonDocument>.Update.Set("ClusterId", "North"));

        Assert.Equal(1, await store.CountPacksInClusterAsync("north"));
        Assert.Equal(1, await store.ClearClusterAsync("NORTH"));
        Assert.Null((await store.GetAsync<BatteryPack>(seed.Sources.Pack.Id))!.ClusterId);
    }

    [Fact]
    public async Task ATokenMadeForAClusterTypedInCapitals_ShouldKeepTheClusterInLowerCase()
    {
        var tokens = new ExternalApiRepository(mongo.NewContext(), new ExternalApiSecurityService("cluster-id-case-test-key-for-hashes-0"));

        var (document, _) = await tokens.CreateTokenAsync("North token", ExternalTokenAccessMode.Read, ["North ", "NORTH"], false, false, "team");

        Assert.Equal(["north"], document["clusterIds"].AsBsonArray.Select(id => id.AsString));
    }
}
