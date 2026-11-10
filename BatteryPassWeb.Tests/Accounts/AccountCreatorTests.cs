using BatteryPassWeb.Controllers;
using BatteryPassWeb.Services;
using BatteryPassWeb.Tests.Dpp;
using Microsoft.AspNetCore.Mvc;

namespace BatteryPassWeb.Tests.Accounts;

/// <summary>
/// Finding R19, the manager's second decision: a cluster admin changes the name or the password only of an account that a
/// cluster admin of one of their clusters created (the account records the cluster it was made in), with no role across
/// the app and only in their clusters. An account without the mark (made by the team, or before this) is never theirs.
/// </summary>
[Collection(MongoCollection.Name)]
public sealed class AccountCreatorTests(MongoFixture mongo)
{
    private static async Task<IReadOnlySet<string>> OfferedAsync(AccountRig rig, string admin)
    {
        var page = Assert.IsType<ViewResult>(await rig.ClusterAdmin(admin).Users(null, CancellationToken.None));
        return Assert.IsAssignableFrom<IReadOnlySet<string>>(page.ViewData[ClusterAdminController.MayChangeAccountKey]);
    }

    private static Task SetPasswordAsync(AccountRig rig, string admin, string email) =>
        AccountRig.Form(rig.ClusterAdmin(admin), ("change", "password"), ("email", email), ("password", AccountRig.Typed), ("passwordConfirmation", AccountRig.Typed))
            .SaveUser(CancellationToken.None);

    [Fact]
    public async Task TwoClicks_ShouldNotTakeOverAnAccountInNoCluster()
    {
        using var rig = await AccountRig.StartAsync(mongo);
        await rig.AccountAsync("waiting@example.test", "Waiting for a cluster", AccessControlService.RoleNormalUser); // the team's, no cluster yet

        await AccountRig.Form(rig.ClusterAdmin(AccountRig.NorthAdmin), ("email", "waiting@example.test"), ("clusterId", "north")).SaveUser(CancellationToken.None);
        await SetPasswordAsync(rig, AccountRig.NorthAdmin, "waiting@example.test");

        Assert.Equal(["north"], await rig.ClustersOfAsync("waiting@example.test"));
        Assert.DoesNotContain("waiting@example.test", await OfferedAsync(rig, AccountRig.NorthAdmin));
        Assert.True(await rig.SignsInAsync("waiting@example.test", AccountRig.Password));
        Assert.False(await rig.SignsInAsync("waiting@example.test", AccountRig.Typed));
    }

    [Fact]
    public async Task AnAccountMadeInAnotherCluster_ShouldNotBecomeTheirs_WhenItLeavesThatCluster()
    {
        using var rig = await AccountRig.StartAsync(mongo);
        await AccountRig.Form(rig.ClusterAdmin(AccountRig.SouthAdmin),
            ("email", "made.south@example.test"), ("password", AccountRig.Password), ("passwordConfirmation", AccountRig.Password), ("clusterId", "south")).SaveUser(CancellationToken.None);
        await AccountRig.Form(rig.ClusterAdmin(AccountRig.NorthAdmin), ("email", "made.south@example.test"), ("clusterId", "north")).SaveUser(CancellationToken.None);
        await AccountRig.Form(rig.ClusterAdmin(AccountRig.SouthAdmin), ("email", "made.south@example.test"), ("clusterId", "south")).DeleteUserMembership(CancellationToken.None);

        await SetPasswordAsync(rig, AccountRig.NorthAdmin, "made.south@example.test");

        Assert.Equal(["north"], await rig.ClustersOfAsync("made.south@example.test"));
        Assert.DoesNotContain("made.south@example.test", await OfferedAsync(rig, AccountRig.NorthAdmin));
        Assert.True(await rig.SignsInAsync("made.south@example.test", AccountRig.Password));
    }

    [Fact]
    public async Task AnAccountTheyMade_ShouldStayTheirs()
    {
        using var rig = await AccountRig.StartAsync(mongo);
        await AccountRig.Form(rig.ClusterAdmin(AccountRig.NorthAdmin),
            ("email", "made.north@example.test"), ("password", AccountRig.Password), ("passwordConfirmation", AccountRig.Password), ("clusterId", "north")).SaveUser(CancellationToken.None);

        await SetPasswordAsync(rig, AccountRig.NorthAdmin, "made.north@example.test");

        Assert.Equal("north", BsonHelpers.GetString((await rig.Clusters.GetUserByEmailAsync("made.north@example.test"))!, ClusterRepository.CreatedInClusterField));
        Assert.Contains("made.north@example.test", await OfferedAsync(rig, AccountRig.NorthAdmin));
        Assert.True(await rig.SignsInAsync("made.north@example.test", AccountRig.Typed));
    }

    [Fact]
    public async Task AnAccountTheTeamMade_ShouldHaveNoMark()
    {
        using var rig = await AccountRig.StartAsync(mongo);
        await AccountRig.Form(rig.TeamMember(), ("email", "team.made@example.test"), ("name", "Team made"), ("systemRole", "member"),
            ("password", AccountRig.Password), ("passwordConfirmation", AccountRig.Password), ("clusterId", "north"), ("role", "member")).SaveUser(CancellationToken.None);

        await SetPasswordAsync(rig, AccountRig.NorthAdmin, "team.made@example.test");

        Assert.Equal(string.Empty, BsonHelpers.GetString((await rig.Clusters.GetUserByEmailAsync("team.made@example.test"))!, ClusterRepository.CreatedInClusterField));
        Assert.True(await rig.SignsInAsync("team.made@example.test", AccountRig.Password));
    }
}
