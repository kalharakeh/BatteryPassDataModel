using BatteryPassWeb.Controllers;
using BatteryPassWeb.Services;
using BatteryPassWeb.Tests.Dpp;
using Microsoft.AspNetCore.Mvc;

namespace BatteryPassWeb.Tests.Accounts;

/// <summary>
/// Finding R19: a cluster admin never sets the name or the password of an account that has an app-wide role or belongs
/// to a cluster they don't administer, and never touches the fallback admin's address. Each way in has its own test.
/// </summary>
[Collection(MongoCollection.Name)]
public sealed class AccountTakeoverTests(MongoFixture mongo)
{
    private const string AlreadyHasAnAccount = "This person already has an account. They were added to your cluster; their name and password stay as they are.";
    private const string OnlyTheTeam = "Only the team can change this person's name or password.";

    // (a) "Add user" with an existing outside account and a typed password.
    [Theory]
    [InlineData(AccountRig.Msa, "Market Surveillance")]
    [InlineData(AccountRig.SouthAdmin, "South Admin")]
    public async Task AddUser_ShouldOnlyAddTheMembership_WhenTheAddressHasAnAccountOutsideTheCluster(string email, string name)
    {
        using var rig = await AccountRig.StartAsync(mongo);
        var northAdmin = AccountRig.Form(rig.ClusterAdmin(AccountRig.NorthAdmin),
            ("name", "Taken over"), ("email", email), ("password", AccountRig.Typed), ("passwordConfirmation", AccountRig.Typed),
            ("clusterId", "north"), ("role", "member"));

        await northAdmin.SaveUser(CancellationToken.None);

        Assert.True(await rig.SignsInAsync(email, AccountRig.Password));
        Assert.False(await rig.SignsInAsync(email, AccountRig.Typed));
        Assert.Equal(name, await rig.NameOfAsync(email));
        Assert.Contains("north", await rig.ClustersOfAsync(email));
        Assert.Equal(AlreadyHasAnAccount, northAdmin.TempData["StatusMessage"]);
    }

    [Fact]
    public async Task AddUser_ShouldNotRenameAnAccount_WhenTheNameIsLeftEmpty()
    {
        using var rig = await AccountRig.StartAsync(mongo);
        var northAdmin = AccountRig.Form(rig.ClusterAdmin(AccountRig.NorthAdmin), ("email", AccountRig.SouthUser), ("clusterId", "north"));

        await northAdmin.SaveUser(CancellationToken.None);

        Assert.Equal("South User", await rig.NameOfAsync(AccountRig.SouthUser));
    }

    // (b) Added without a password, then the row's "New password" (and "Name"): not offered, and refused when sent anyway.
    [Theory]
    [InlineData(AccountRig.Msa)]
    [InlineData(AccountRig.SouthAdmin)]
    public async Task TheRowsNewPassword_ShouldBeRefused_WhenTheAccountHasAnAppWideRoleOrAnotherCluster(string email)
    {
        using var rig = await AccountRig.StartAsync(mongo);
        await AccountRig.Form(rig.ClusterAdmin(AccountRig.NorthAdmin), ("email", email), ("clusterId", "north")).SaveUser(CancellationToken.None);

        var page = Assert.IsType<ViewResult>(await rig.ClusterAdmin(AccountRig.NorthAdmin).Users(null, CancellationToken.None));
        var mayChange = Assert.IsAssignableFrom<IReadOnlySet<string>>(page.ViewData[ClusterAdminController.MayChangeAccountKey]);
        Assert.DoesNotContain(email, mayChange);
        Assert.Contains(AccountRig.NorthUser, mayChange);

        var password = AccountRig.Form(rig.ClusterAdmin(AccountRig.NorthAdmin),
            ("change", "password"), ("email", email), ("password", AccountRig.Typed), ("passwordConfirmation", AccountRig.Typed), ("clusterId", "north"), ("role", "member"));
        await password.SaveUser(CancellationToken.None);
        var name = AccountRig.Form(rig.ClusterAdmin(AccountRig.NorthAdmin), ("change", "name"), ("email", email), ("name", "Taken over"), ("clusterId", "north"));
        await name.SaveUser(CancellationToken.None);

        Assert.True(await rig.SignsInAsync(email, AccountRig.Password));
        Assert.False(await rig.SignsInAsync(email, AccountRig.Typed));
        Assert.NotEqual("Taken over", await rig.NameOfAsync(email));
        Assert.Equal(OnlyTheTeam, password.TempData["ErrorMessage"]);
        Assert.Equal(OnlyTheTeam, name.TempData["ErrorMessage"]);
    }

    [Fact]
    public async Task TheRowsNewPassword_ShouldWork_ForAUserOnlyInTheAdminsClusters()
    {
        using var rig = await AccountRig.StartAsync(mongo);
        var northAdmin = AccountRig.Form(rig.ClusterAdmin(AccountRig.NorthAdmin),
            ("change", "password"), ("email", AccountRig.NorthUser), ("password", AccountRig.Typed), ("passwordConfirmation", AccountRig.Typed));

        await northAdmin.SaveUser(CancellationToken.None);
        await AccountRig.Form(rig.ClusterAdmin(AccountRig.NorthAdmin), ("change", "name"), ("email", AccountRig.NorthUser), ("name", "North Person")).SaveUser(CancellationToken.None);

        Assert.True(await rig.SignsInAsync(AccountRig.NorthUser, AccountRig.Typed));
        Assert.Equal("North Person", await rig.NameOfAsync(AccountRig.NorthUser));
    }

    [Fact]
    public async Task AddUser_ShouldCreateTheAccount_WhenTheAddressHasNone()
    {
        using var rig = await AccountRig.StartAsync(mongo);
        await AccountRig.Form(rig.ClusterAdmin(AccountRig.NorthAdmin),
            ("name", "New Person"), ("email", "new.person@example.test"), ("password", AccountRig.Typed), ("passwordConfirmation", AccountRig.Typed),
            ("clusterId", "north"), ("role", "member")).SaveUser(CancellationToken.None);

        Assert.True(await rig.SignsInAsync("new.person@example.test", AccountRig.Typed));
        Assert.Equal("New Person", await rig.NameOfAsync("new.person@example.test"));
        Assert.Equal(["north"], await rig.ClustersOfAsync("new.person@example.test"));
    }

    // (c) An account made beforehand by a cluster admin, then given a role by the team.
    [Fact]
    public async Task TheTeamGivingARole_ShouldNeedANewPassword_WhenTheAccountHadNone()
    {
        using var rig = await AccountRig.StartAsync(mongo);
        const string email = "future.msa@example.test";
        await AccountRig.Form(rig.ClusterAdmin(AccountRig.NorthAdmin),
            ("email", email), ("password", AccountRig.Typed), ("passwordConfirmation", AccountRig.Typed), ("clusterId", "north")).SaveUser(CancellationToken.None);

        var withoutPassword = AccountRig.Form(rig.TeamMember(), ("change", "access"), ("email", email), ("name", "Authority"), ("systemRole", AccessControlService.RoleMarketSurveillanceAuthority));
        await withoutPassword.SaveUser(CancellationToken.None);

        Assert.Equal([AccessControlService.RoleNormalUser], await rig.RolesOfAsync(email));
        Assert.Equal(AccessControlService.NewPasswordForAppWideRole, withoutPassword.TempData["ErrorMessage"]);

        await AccountRig.Form(rig.TeamMember(), ("change", "access"), ("email", email), ("name", "Authority"), ("systemRole", AccessControlService.RoleMarketSurveillanceAuthority),
            ("password", "the-teams-new-password-1"), ("passwordConfirmation", "the-teams-new-password-1")).SaveUser(CancellationToken.None);

        Assert.Equal([AccessControlService.RoleMarketSurveillanceAuthority], await rig.RolesOfAsync(email));
        Assert.False(await rig.SignsInAsync(email, AccountRig.Typed));
        Assert.True(await rig.SignsInAsync(email, "the-teams-new-password-1"));
    }

    [Fact]
    public async Task TheTeamRenamingAnAuthority_ShouldNotNeedANewPassword()
    {
        using var rig = await AccountRig.StartAsync(mongo);
        await AccountRig.Form(rig.TeamMember(), ("change", "access"), ("email", AccountRig.Msa), ("name", "Renamed"), ("systemRole", AccessControlService.RoleMarketSurveillanceAuthority)).SaveUser(CancellationToken.None);

        Assert.Equal("Renamed", await rig.NameOfAsync(AccountRig.Msa));
        Assert.True(await rig.SignsInAsync(AccountRig.Msa, AccountRig.Password));
    }

    // (d) The fallback admin's address, in any capitals.
    [Theory]
    [InlineData(AccountRig.FallbackEmail)]
    [InlineData("Fallback.Admin@Example.TEST")]
    public async Task TheFallbackAdminsAddress_ShouldBeRefused_ForAClusterAdmin(string email)
    {
        using var rig = await AccountRig.StartAsync(mongo);
        var northAdmin = AccountRig.Form(rig.ClusterAdmin(AccountRig.NorthAdmin),
            ("email", email), ("password", AccountRig.Typed), ("passwordConfirmation", AccountRig.Typed), ("clusterId", "north"));

        await northAdmin.SaveUser(CancellationToken.None);

        Assert.Null(await rig.Clusters.GetUserByEmailAsync(AccountRig.FallbackEmail));
        Assert.Empty(await rig.ClustersOfAsync(AccountRig.FallbackEmail));
        Assert.Equal(AccessControlService.KeptForTheTeam, northAdmin.TempData["ErrorMessage"]);
        Assert.True(await rig.SignsInAsync(AccountRig.FallbackEmail, AccountRig.FallbackPassword));
    }

    [Fact]
    public async Task TheAccountPage_ShouldNotTakeTheFallbackAdminsAddress()
    {
        using var rig = await AccountRig.StartAsync(mongo);
        var page = AccountRig.Form(rig.AccountPage(AccountRig.NorthUser),
            ("email", "Fallback.Admin@Example.test"), ("name", "North User"), ("currentPassword", AccountRig.Password));

        var result = Assert.IsType<Microsoft.AspNetCore.Mvc.ViewResult>(await page.Save(CancellationToken.None));

        Assert.Equal(AccessControlService.AddressKeptForTheTeam, Assert.IsType<BatteryPassWeb.Models.ViewModels.AccountProfileViewModel>(result.Model).ErrorMessage);
        Assert.Null(await rig.Clusters.GetUserByEmailAsync(AccountRig.FallbackEmail));
        Assert.True(await rig.SignsInAsync(AccountRig.FallbackEmail, AccountRig.FallbackPassword));
    }

    [Fact]
    public async Task ATeamAccount_ShouldStayOutOfAClusterAdminsReach()
    {
        using var rig = await AccountRig.StartAsync(mongo);
        var northAdmin = AccountRig.Form(rig.ClusterAdmin(AccountRig.NorthAdmin),
            ("email", AccountRig.Team), ("password", AccountRig.Typed), ("passwordConfirmation", AccountRig.Typed), ("clusterId", "north"));

        await northAdmin.SaveUser(CancellationToken.None);

        Assert.True(await rig.SignsInAsync(AccountRig.Team, AccountRig.Password));
        Assert.Equal([AccessControlService.RoleAdmin], await rig.RolesOfAsync(AccountRig.Team));
        Assert.Empty(await rig.ClustersOfAsync(AccountRig.Team));
    }

    [Fact]
    public void TheUsersPage_ShouldOfferNameAndPasswordOnlyWhereTheRuleAllows()
    {
        var page = File.ReadAllText(DppTestFiles.RepoFile("web", "Views", "ClusterAdmin", "Users.cshtml"));

        Assert.Contains("ClusterAdminController.MayChangeAccountKey", page, StringComparison.Ordinal);
        Assert.Contains("name=\"change\" value=\"name\"", page, StringComparison.Ordinal);
        Assert.Contains("name=\"change\" value=\"password\"", page, StringComparison.Ordinal);
        Assert.Contains(OnlyTheTeam, page, StringComparison.Ordinal);
        Assert.DoesNotContain("Typing the email of an existing user adds them to it.", page, StringComparison.Ordinal);
    }
}
