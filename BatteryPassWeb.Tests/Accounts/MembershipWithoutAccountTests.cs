using BatteryPassWeb.Controllers;
using BatteryPassWeb.Services;
using BatteryPassWeb.Tests.Dpp;

namespace BatteryPassWeb.Tests.Accounts;

/// <summary>
/// Finding R19, the manager's second decision: a cluster admin can't make an account for an address that already has a
/// membership in a cluster they don't administer (the new account would inherit it), and the team's "Add to" in a row
/// needs an account.
/// </summary>
[Collection(MongoCollection.Name)]
public sealed class MembershipWithoutAccountTests(MongoFixture mongo)
{
    [Fact]
    public async Task AClusterAdmin_ShouldNotMakeAnAccount_OverAMembershipInAnotherCluster()
    {
        using var rig = await AccountRig.StartAsync(mongo);
        await rig.Clusters.UpsertClusterMembershipAsync("ghost@example.test", "south", AccessControlService.RoleClusterAdmin);
        var northAdmin = AccountRig.Form(rig.ClusterAdmin(AccountRig.NorthAdmin),
            ("email", "Ghost@Example.test"), ("password", AccountRig.Typed), ("passwordConfirmation", AccountRig.Typed), ("clusterId", "north"));

        await northAdmin.SaveUser(CancellationToken.None);

        Assert.Equal(AccessControlService.CantChangeThisAccount, northAdmin.TempData["ErrorMessage"]);
        Assert.Null(await rig.Clusters.GetUserByEmailAsync("ghost@example.test"));
        Assert.Equal(["south"], await rig.ClustersOfAsync("ghost@example.test"));
    }

    [Fact]
    public async Task AClusterAdmin_ShouldMakeAnAccount_OverAMembershipInTheirOwnCluster()
    {
        using var rig = await AccountRig.StartAsync(mongo);
        await rig.Clusters.UpsertClusterMembershipAsync("ghost@example.test", "north", AccessControlService.RoleNormalUser);

        await AccountRig.Form(rig.ClusterAdmin(AccountRig.NorthAdmin),
            ("email", "ghost@example.test"), ("password", AccountRig.Typed), ("passwordConfirmation", AccountRig.Typed), ("clusterId", "north")).SaveUser(CancellationToken.None);

        Assert.True(await rig.SignsInAsync("ghost@example.test", AccountRig.Typed));
    }

    [Fact]
    public async Task TheTeamsAddTo_ShouldNeedAnAccount()
    {
        using var rig = await AccountRig.StartAsync(mongo);
        var team = AccountRig.Form(rig.TeamMember(), ("email", "nobody@example.test"), ("clusterId", "north"), ("role", "clusterAdmin"));

        await team.AssignUserMembership(CancellationToken.None);

        Assert.Equal(AdminController.NoSuchAccount, team.TempData["ErrorMessage"]);
        Assert.Empty(await rig.ClustersOfAsync("nobody@example.test"));
    }
}
