using BatteryPassWeb.Controllers;
using BatteryPassWeb.Services;
using BatteryPassWeb.Tests.Dpp;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Bson;
using MongoDB.Driver;

namespace BatteryPassWeb.Tests.Accounts;

/// <summary>
/// Finding W7: no path writes a membership for an address with no account (decision 39 closed the team's add-to) or for a
/// cluster that doesn't exist, and nobody can take an address that still has such a membership from before.
/// </summary>
[Collection(MongoCollection.Name)]
public sealed class MembershipChecksTests(MongoFixture mongo)
{
    private static async Task<long> MembershipsAsync(AccountRig rig, string email) =>
        await rig.Database.GetCollection<BsonDocument>("clusterMemberships").CountDocumentsAsync(Builders<BsonDocument>.Filter.Eq("email", email));

    private static ClusterAdminController TeamOnClusterAdminPages(AccountRig rig) =>
        AccountRig.With(new ClusterAdminController(
            rig.Access,
            rig.Services.GetRequiredService<ApplicationAuditService>(),
            rig.Clusters,
            rig.Services.GetRequiredService<ExternalApiRepository>()),
            AccountRig.Person(AccountRig.Team, AccessControlService.RoleAdmin));

    [Fact]
    public async Task TheTeamsAddTo_ShouldRefuseAClusterThatDoesNotExist()
    {
        using var rig = await AccountRig.StartAsync(mongo);
        var team = AccountRig.Form(rig.TeamMember(), ("email", AccountRig.NorthUser), ("clusterId", "nowhere"), ("role", "member"));

        await team.AssignUserMembership(CancellationToken.None);

        Assert.Equal(ClusterRepository.NoSuchCluster, team.TempData["ErrorMessage"]);
        Assert.Equal(["north"], await rig.ClustersOfAsync(AccountRig.NorthUser));
    }

    [Fact]
    public async Task TheTeamsAddTo_ShouldAcceptACluster_TypedInCapitals()
    {
        using var rig = await AccountRig.StartAsync(mongo);

        await AccountRig.Form(rig.TeamMember(), ("email", AccountRig.NorthUser), ("clusterId", "SOUTH"), ("role", "member")).AssignUserMembership(CancellationToken.None);

        Assert.Equal(["north", "south"], await rig.ClustersOfAsync(AccountRig.NorthUser));
    }

    [Fact]
    public async Task TheTeamsUserForm_ShouldMakeNoAccountAndNoMembership_ForAClusterThatDoesNotExist()
    {
        using var rig = await AccountRig.StartAsync(mongo);
        var team = AccountRig.Form(rig.TeamMember(),
            ("email", "new.person@example.test"), ("password", AccountRig.Typed), ("passwordConfirmation", AccountRig.Typed), ("clusterId", "nowhere"), ("role", "member"));

        await team.SaveUser(CancellationToken.None);

        Assert.Equal(ClusterRepository.NoSuchCluster, team.TempData["ErrorMessage"]);
        Assert.Null(await rig.Clusters.GetUserByEmailAsync("new.person@example.test"));
        Assert.Equal(0, await MembershipsAsync(rig, "new.person@example.test"));
    }

    [Fact]
    public async Task TheClusterAdminPages_ShouldRefuseAClusterThatDoesNotExist_EvenForTheTeam()
    {
        using var rig = await AccountRig.StartAsync(mongo);
        var team = AccountRig.Form(TeamOnClusterAdminPages(rig),
            ("email", AccountRig.NorthUser), ("clusterId", "nowhere"), ("role", "member"), ("change", "cluster"));

        await team.SaveUser(CancellationToken.None);

        Assert.Equal(ClusterRepository.NoSuchCluster, team.TempData["ErrorMessage"]);
        Assert.Equal(["north"], await rig.ClustersOfAsync(AccountRig.NorthUser));

        var newAccount = AccountRig.Form(TeamOnClusterAdminPages(rig),
            ("email", "new.person@example.test"), ("clusterId", "nowhere"), ("role", "member"), ("password", AccountRig.Typed), ("passwordConfirmation", AccountRig.Typed));
        await newAccount.SaveUser(CancellationToken.None);

        Assert.Null(await rig.Clusters.GetUserByEmailAsync("new.person@example.test"));
        Assert.Equal(0, await MembershipsAsync(rig, "new.person@example.test"));
    }

    [Fact]
    public async Task AnAddressThatStillHasAMembershipButNoAccount_ShouldNotBeTakenOnTheAccountPage()
    {
        using var rig = await AccountRig.StartAsync(mongo);
        await rig.Clusters.UpsertClusterMembershipAsync("orphan@example.test", "north", AccessControlService.RoleClusterAdmin); // older data
        var page = AccountRig.Form(rig.AccountPage(AccountRig.SouthUser),
            ("email", "orphan@example.test"), ("name", "South User"), ("currentPassword", AccountRig.Password));

        var result = await page.Save(CancellationToken.None);

        Assert.Equal("That email is already used by another account.", AccountProfileViewModelProbe.Model(result).ErrorMessage);
        Assert.NotNull(await rig.Clusters.GetUserByEmailAsync(AccountRig.SouthUser));
        Assert.Equal(["south"], await rig.ClustersOfAsync(AccountRig.SouthUser));
    }

    [Fact]
    public async Task AnAddressWithNoMembershipsAndNoAccount_ShouldStillBeTakenOnTheAccountPage()
    {
        using var rig = await AccountRig.StartAsync(mongo);
        var page = AccountRig.Form(rig.AccountPage(AccountRig.SouthUser),
            ("email", "fresh@example.test"), ("name", "South User"), ("currentPassword", AccountRig.Password));

        await page.Save(CancellationToken.None);

        Assert.NotNull(await rig.Clusters.GetUserByEmailAsync("fresh@example.test"));
        Assert.Equal(["south"], await rig.ClustersOfAsync("fresh@example.test"));
    }
}
