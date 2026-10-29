using System.Security.Claims;
using BatteryPassWeb.Dpp.Access;
using BatteryPassWeb.Dpp.Catalog;
using BatteryPassWeb.Services;

namespace BatteryPassWeb.Tests.Dpp;

/// <summary>Sign-ins and API tokens as readers (design 5.2): the app-wide role, and the clusters from the memberships.</summary>
[Collection(MongoCollection.Name)]
public sealed class DppReadersTests(MongoFixture mongo)
{
    [Fact]
    public async Task NotSignedIn_ShouldBeAnonymous()
    {
        var reader = await Readers().ForAsync(new ClaimsPrincipal(new ClaimsIdentity()));

        Assert.Same(Reader.Anonymous, reader);
    }

    [Theory]
    [InlineData("admin", ReaderRole.Team)]
    [InlineData("notifiedBody", ReaderRole.NotifiedBody)]
    [InlineData("marketSurveillanceAuthority", ReaderRole.MarketSurveillance)]
    [InlineData("commission", ReaderRole.Commission)]
    [InlineData("liRepair", ReaderRole.LiRepair)]
    [InlineData("liRecycling", ReaderRole.LiRecycling)]
    [InlineData("liTreatment", ReaderRole.LiTreatment)]
    public async Task AnAppWideRole_ShouldBeTheReadersRole(string role, ReaderRole expected)
    {
        var reader = await Readers().ForAsync(User("Someone@Example.com", role));

        Assert.Equal([expected], reader.Roles);
        Assert.Empty(reader.ClusterIds);
        Assert.Equal("someone@example.com", reader.Who);
    }

    [Theory]
    [InlineData("legitimateInterest")]
    [InlineData("member")]
    [InlineData("viewer")]
    public async Task AnyOtherRoleWithoutMemberships_ShouldSeePublicDataOnly(string role)
    {
        var reader = await Readers().ForAsync(User("someone@example.com", role));

        Assert.Empty(reader.Roles);
        Assert.All(AttributeCatalog.All, attribute =>
            Assert.Equal(attribute.Audience == Audience.Public, AudienceRules.MaySee(reader, attribute, ["cluster-test"])));
    }

    [Fact]
    public async Task Memberships_ShouldMakeAClusterUserOrAdminOfEachCluster()
    {
        var clusters = new ClusterRepository(mongo.NewContext());
        await clusters.UpsertClusterMembershipAsync("user@example.com", "Cluster-North", "member");
        await clusters.UpsertClusterMembershipAsync("user@example.com", "cluster-south", "clusterAdmin");

        var reader = await new DppReaders(clusters).ForAsync(User("user@example.com", "clusterAdmin"));

        Assert.Equal([ReaderRole.ClusterUser, ReaderRole.ClusterAdmin], reader.Roles.Order());
        Assert.Equal(["cluster-north", "cluster-south"], reader.ClusterIds.Order());
        Assert.True(AudienceRules.MaySee(reader, AttributeCatalog.ByNumber(63), ["cluster-north"]));
        Assert.False(AudienceRules.MaySee(reader, AttributeCatalog.ByNumber(63), ["cluster-west"]));
    }

    [Fact]
    public async Task ARemovedMembership_ShouldCountAtOnce()
    {
        var clusters = new ClusterRepository(mongo.NewContext());
        await clusters.UpsertClusterMembershipAsync("user@example.com", "cluster-test", "member");
        var readers = new DppReaders(clusters);
        Assert.Equal(["cluster-test"], (await readers.ForAsync(User("user@example.com", "member"))).ClusterIds);

        await clusters.DeleteClusterMembershipAsync("user@example.com", "cluster-test");

        var after = await readers.ForAsync(User("user@example.com", "member"));
        Assert.Empty(after.ClusterIds);
        Assert.Empty(after.Roles);
    }

    [Fact]
    public void AClusterToken_ShouldReadLikeAClusterUserOfItsClusters()
    {
        var reader = DppReaders.For(new ExternalApiTokenContext { TokenId = "token-1", ClusterIds = ["Cluster-North", "cluster-south"] });

        Assert.Equal([ReaderRole.ClusterUser], reader.Roles);
        Assert.Equal(["cluster-north", "cluster-south"], reader.ClusterIds.Order());
        Assert.Equal("api-token:token-1", reader.Who);
    }

    [Fact]
    public void AGlobalToken_ShouldReadLikeTheTeam()
    {
        var reader = DppReaders.For(new ExternalApiTokenContext { TokenId = "token-2", GlobalAccess = true });

        Assert.Equal([ReaderRole.Team], reader.Roles);
    }

    private DppReaders Readers() => new(new ClusterRepository(mongo.NewContext()));

    private static ClaimsPrincipal User(string email, string role) =>
        new(new ClaimsIdentity([new Claim(ClaimTypes.Email, email), new Claim(ClaimTypes.Role, role)], "test"));
}
