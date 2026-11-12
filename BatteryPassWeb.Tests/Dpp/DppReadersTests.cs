using System.Security.Claims;
using BatteryPassWeb.Dpp.Access;
using BatteryPassWeb.Dpp.Catalog;
using BatteryPassWeb.Dpp.Editing;
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
        var reader = await (await ReadersWithAsync("someone@example.com", role)).ForAsync(User("Someone@Example.com", role));

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
        var reader = await (await ReadersWithAsync("someone@example.com", role)).ForAsync(User("someone@example.com", role));

        Assert.Empty(reader.Roles);
        Assert.All(AttributeCatalog.All, attribute =>
            Assert.Equal(attribute.Audience == Audience.Public, AudienceRules.MaySee(reader, attribute, ["cluster-test"])));
    }

    [Fact]
    public async Task Memberships_ShouldMakeAClusterUserOrAdminOfEachCluster()
    {
        var clusters = new ClusterRepository(mongo.NewContext());
        await clusters.UpsertUserAsync("user@example.com", "User", ["clusterAdmin"], "hash");
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
        await clusters.UpsertUserAsync("user@example.com", "User", ["member"], "hash");
        await clusters.UpsertClusterMembershipAsync("user@example.com", "cluster-test", "member");
        var readers = new DppReaders(clusters);
        Assert.Equal(["cluster-test"], (await readers.ForAsync(User("user@example.com", "member"))).ClusterIds);

        await clusters.DeleteClusterMembershipAsync("user@example.com", "cluster-test");

        var after = await readers.ForAsync(User("user@example.com", "member"));
        Assert.Empty(after.ClusterIds);
        Assert.Empty(after.Roles);
    }

    /// <summary>Finding R03, point 6a: a session whose account record is gone has no roles and changes nothing.</summary>
    [Fact]
    public async Task ASessionWithoutAnAccountRecord_ShouldHaveNoRoles()
    {
        var clusters = new ClusterRepository(mongo.NewContext());
        await clusters.UpsertClusterMembershipAsync("gone@example.com", "cluster-test", "clusterAdmin");
        var readers = new DppReaders(clusters);

        Assert.Same(Reader.Anonymous, await readers.ForAsync(User("gone@example.com", "admin")));
        Assert.Null(await readers.EditorForAsync(User("gone@example.com", "admin")));
        Assert.Null(await readers.EditorForAsync(User("gone@example.com", "clusterAdmin")));
    }

    /// <summary>The fallback admin has no account record and keeps the team's reach (findings R21, R03 point 6a).</summary>
    [Fact]
    public async Task TheFallbackAdmin_ShouldKeepTheTeamsRole()
    {
        var readers = Readers();
        var marked = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(ClaimTypes.Email, "fallback@example.com"), new Claim(ClaimTypes.Role, "admin"), new Claim(AuthService.FallbackClaim, "1")], "test"));
        var older = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(ClaimTypes.Email, "fallback@example.com"), new Claim(ClaimTypes.Name, "Demo Administrator"), new Claim(ClaimTypes.Role, "admin")], "test"));

        Assert.Equal([ReaderRole.Team], (await readers.ForAsync(marked)).Roles);
        Assert.Equal([ReaderRole.Team], (await readers.ForAsync(older)).Roles);
        Assert.Equal(SenderKind.Team, (await readers.EditorForAsync(marked))!.Kind);
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

    private async Task<DppReaders> ReadersWithAsync(string email, string role)
    {
        var clusters = new ClusterRepository(mongo.NewContext());
        await clusters.UpsertUserAsync(email, "Someone", [role], "hash");
        return new DppReaders(clusters);
    }

    private static ClaimsPrincipal User(string email, string role) =>
        new(new ClaimsIdentity([new Claim(ClaimTypes.Email, email), new Claim(ClaimTypes.Role, role)], "test"));
}
