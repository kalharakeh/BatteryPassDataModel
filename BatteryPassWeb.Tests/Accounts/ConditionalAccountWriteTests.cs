using BatteryPassWeb.Services;
using BatteryPassWeb.Tests.Dpp;

namespace BatteryPassWeb.Tests.Accounts;

/// <summary>
/// Finding R19, the manager's fourth point: a cluster admin's name or password save is written only while the rule still
/// holds, so a save in flight when the team gives the account a role (and a new password) changes nothing.
/// </summary>
[Collection(MongoCollection.Name)]
public sealed class ConditionalAccountWriteTests(MongoFixture mongo)
{
    private static readonly HashSet<string> North = new(StringComparer.OrdinalIgnoreCase) { "north" };

    [Fact]
    public async Task ASaveInFlight_ShouldChangeNothing_WhenTheTeamGaveTheAccountARoleMeanwhile()
    {
        using var rig = await AccountRig.StartAsync(mongo);
        // The cluster admin's rights were checked; then the team gives the account a role and its own new password.
        await rig.Clusters.UpsertUserAsync(AccountRig.NorthUser, "North User", [AccessControlService.RoleMarketSurveillanceAuthority], BCrypt.Net.BCrypt.HashPassword("the-teams-password-1", 4));

        var written = await rig.Clusters.ChangeIfAClusterAdminMayAsync(AccountRig.NorthUser, North, "Taken over", BCrypt.Net.BCrypt.HashPassword(AccountRig.Typed, 4));

        Assert.False(written);
        Assert.True(await rig.SignsInAsync(AccountRig.NorthUser, "the-teams-password-1"));
        Assert.Equal("North User", await rig.NameOfAsync(AccountRig.NorthUser));
    }

    [Fact]
    public async Task ASaveInFlight_ShouldChangeNothing_ForAnAccountWithoutTheirMark()
    {
        using var rig = await AccountRig.StartAsync(mongo);

        Assert.False(await rig.Clusters.ChangeIfAClusterAdminMayAsync(AccountRig.SouthUser, North, "Taken over", null));
        Assert.Equal("South User", await rig.NameOfAsync(AccountRig.SouthUser));
    }

    [Fact]
    public async Task ASave_ShouldBeWritten_WhileTheRuleHolds()
    {
        using var rig = await AccountRig.StartAsync(mongo);

        Assert.True(await rig.Clusters.ChangeIfAClusterAdminMayAsync(AccountRig.NorthUser, North, null, BCrypt.Net.BCrypt.HashPassword(AccountRig.Typed, 4)));
        Assert.True(await rig.Clusters.ChangeIfAClusterAdminMayAsync(AccountRig.NorthUser, North, "North Person", null));

        Assert.True(await rig.SignsInAsync(AccountRig.NorthUser, AccountRig.Typed));
        Assert.Equal("North Person", await rig.NameOfAsync(AccountRig.NorthUser));
    }

    [Fact]
    public void TheClusterAdminsNameAndPasswordSaves_ShouldUseTheConditionalWrite()
    {
        var controller = File.ReadAllText(DppTestFiles.RepoFile("web", "Controllers", "ClusterAdminController.cs"));
        var save = controller[controller.IndexOf("public async Task<IActionResult> SaveUser", StringComparison.Ordinal)..controller.IndexOf("private Task AuditUserSavedAsync", StringComparison.Ordinal)];

        Assert.Equal(2, save.Split("ChangeIfAClusterAdminMayAsync(").Length - 1);
        Assert.DoesNotContain("SetPasswordHashAsync", save, StringComparison.Ordinal);
        Assert.DoesNotContain("UpdateUserProfileAsync", save, StringComparison.Ordinal);
    }
}
