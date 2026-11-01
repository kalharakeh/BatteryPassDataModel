using System.Security.Claims;
using BatteryPassWeb.Dpp.Access;
using BatteryPassWeb.Dpp.Editing;
using BatteryPassWeb.Services;

namespace BatteryPassWeb.Tests.Dpp;

/// <summary>
/// A sign-in as the sender of a change (design 6.1, step 4a): the team, or a cluster admin of only the clusters where
/// the membership says cluster admin. Reading treats every membership alike; editing doesn't.
/// </summary>
[Collection(MongoCollection.Name)]
public sealed class ClusterAdminSenderTests(MongoFixture mongo)
{
    [Fact]
    public async Task TheTeam_ShouldEditAsTheTeam()
    {
        var sender = await Readers().EditorForAsync(User("Admin@Example.com", "admin"));

        Assert.NotNull(sender);
        Assert.Equal(SenderKind.Team, sender.Kind);
        Assert.Equal("admin@example.com", sender.Who);
    }

    [Fact]
    public async Task AClusterAdmin_ShouldEditOnlyTheClustersTheyAreAdminOf()
    {
        var clusters = new ClusterRepository(mongo.NewContext());
        await clusters.UpsertClusterMembershipAsync("both@example.com", "Cluster-North", "clusterAdmin");
        await clusters.UpsertClusterMembershipAsync("both@example.com", "cluster-south", "member");

        var sender = await new DppReaders(clusters).EditorForAsync(User("both@example.com", "clusterAdmin"));

        Assert.NotNull(sender);
        Assert.Equal(SenderKind.ClusterAdmin, sender.Kind);
        Assert.Equal(["cluster-north"], sender.ClusterIds);
    }

    [Fact]
    public async Task APlainMember_ShouldEditNothing()
    {
        var clusters = new ClusterRepository(mongo.NewContext());
        await clusters.UpsertClusterMembershipAsync("member@example.com", "cluster-north", "member");

        Assert.Null(await new DppReaders(clusters).EditorForAsync(User("member@example.com", "member")));
    }

    [Theory]
    [InlineData("marketSurveillanceAuthority")]
    [InlineData("liRepair")]
    [InlineData("commission")]
    public async Task AnAppWideReader_ShouldEditNothing(string role)
    {
        Assert.Null(await Readers().EditorForAsync(User("reader@example.com", role)));
    }

    [Fact]
    public async Task NotSignedIn_ShouldEditNothing()
    {
        Assert.Null(await Readers().EditorForAsync(new ClaimsPrincipal(new ClaimsIdentity())));
    }

    [Fact]
    public async Task ARemovedAdminMembership_ShouldCountAtOnce()
    {
        var clusters = new ClusterRepository(mongo.NewContext());
        await clusters.UpsertClusterMembershipAsync("admin@example.com", "cluster-north", "clusterAdmin");
        var readers = new DppReaders(clusters);
        Assert.NotNull(await readers.EditorForAsync(User("admin@example.com", "clusterAdmin")));

        await clusters.DeleteClusterMembershipAsync("admin@example.com", "cluster-north");

        Assert.Null(await readers.EditorForAsync(User("admin@example.com", "clusterAdmin")));
    }

    private DppReaders Readers() => new(new ClusterRepository(mongo.NewContext()));

    private static ClaimsPrincipal User(string email, string role) =>
        new(new ClaimsIdentity([new Claim(ClaimTypes.Email, email), new Claim(ClaimTypes.Role, role)], "test"));
}
