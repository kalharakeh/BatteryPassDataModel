using BatteryPassWeb.Models.ViewModels;
using BatteryPassWeb.Services;
using BatteryPassWeb.Tests.Dpp;
using Microsoft.AspNetCore.Mvc;

namespace BatteryPassWeb.Tests.Accounts;

/// <summary>
/// Finding S06: wherever a person types a new password it has at least 12 characters; passwords that exist keep working;
/// the account page asks for the current password before it changes the password or the e-mail.
/// </summary>
[Collection(MongoCollection.Name)]
public sealed class PasswordRuleTests(MongoFixture mongo)
{
    private const string Short = "elevenchars";

    [Theory]
    [InlineData("a", false)]
    [InlineData(Short, false)]
    [InlineData("twelve chars", true)]
    [InlineData("a much longer password", true)]
    public void TheRule_ShouldBeTwelveCharacters(string password, bool accepted) =>
        Assert.Equal(accepted, AuthService.NewPasswordProblem(password) is null);

    [Fact]
    public async Task TheTeamsForms_ShouldRefuseAShortPassword()
    {
        using var rig = await AccountRig.StartAsync(mongo);
        var add = AccountRig.Form(rig.TeamMember(), ("email", "new@example.test"), ("password", Short), ("passwordConfirmation", Short));
        await add.SaveUser(CancellationToken.None);
        var row = AccountRig.Form(rig.TeamMember(), ("change", "password"), ("email", AccountRig.NorthUser), ("password", Short), ("passwordConfirmation", Short));
        await row.SaveUser(CancellationToken.None);
        var access = AccountRig.Form(rig.TeamMember(), ("change", "access"), ("email", AccountRig.NorthUser), ("systemRole", "commission"), ("password", Short), ("passwordConfirmation", Short));
        await access.SaveUser(CancellationToken.None);

        Assert.Null(await rig.Clusters.GetUserByEmailAsync("new@example.test"));
        Assert.True(await rig.SignsInAsync(AccountRig.NorthUser, AccountRig.Password));
        Assert.Equal([AccessControlService.RoleNormalUser], await rig.RolesOfAsync(AccountRig.NorthUser));
        Assert.All([add, row, access], controller => Assert.Equal(AuthService.PasswordTooShort, controller.TempData["ErrorMessage"]));
    }

    [Fact]
    public async Task AClusterAdminsForms_ShouldRefuseAShortPassword()
    {
        using var rig = await AccountRig.StartAsync(mongo);
        var add = AccountRig.Form(rig.ClusterAdmin(AccountRig.NorthAdmin), ("email", "new@example.test"), ("password", Short), ("passwordConfirmation", Short), ("clusterId", "north"));
        await add.SaveUser(CancellationToken.None);
        var row = AccountRig.Form(rig.ClusterAdmin(AccountRig.NorthAdmin), ("change", "password"), ("email", AccountRig.NorthUser), ("password", Short), ("passwordConfirmation", Short));
        await row.SaveUser(CancellationToken.None);

        Assert.Null(await rig.Clusters.GetUserByEmailAsync("new@example.test"));
        Assert.True(await rig.SignsInAsync(AccountRig.NorthUser, AccountRig.Password));
        Assert.All([add, row], controller => Assert.Equal(AuthService.PasswordTooShort, controller.TempData["ErrorMessage"]));
    }

    [Fact]
    public async Task AShortPasswordThatExists_ShouldStillSignIn()
    {
        using var rig = await AccountRig.StartAsync(mongo);
        await rig.AccountAsync("old.short@example.test", "Old", AccessControlService.RoleNormalUser, password: "abc");

        Assert.True(await rig.SignsInAsync("old.short@example.test", "abc"));
    }

    private static string? ErrorOf(IActionResult result) =>
        Assert.IsType<AccountProfileViewModel>(Assert.IsType<ViewResult>(result).Model).ErrorMessage is { Length: > 0 } error ? error : null;

    [Theory]
    [InlineData("", AuthService.CurrentPasswordNeeded)]
    [InlineData("not-my-password", AuthService.CurrentPasswordWrong)]
    public async Task TheAccountPage_ShouldAskForTheCurrentPassword_BeforeANewOne(string current, string error)
    {
        using var rig = await AccountRig.StartAsync(mongo);
        var page = AccountRig.Form(rig.AccountPage(AccountRig.NorthUser),
            ("email", AccountRig.NorthUser), ("name", "North User"), ("currentPassword", current), ("password", AccountRig.Typed), ("passwordConfirmation", AccountRig.Typed));

        Assert.Equal(error, ErrorOf(await page.Save(CancellationToken.None)));
        Assert.True(await rig.SignsInAsync(AccountRig.NorthUser, AccountRig.Password));
    }

    [Fact]
    public async Task TheAccountPage_ShouldAskForTheCurrentPassword_BeforeANewEmail()
    {
        using var rig = await AccountRig.StartAsync(mongo);
        var page = AccountRig.Form(rig.AccountPage(AccountRig.NorthUser), ("email", "elsewhere@example.test"), ("name", "North User"));

        Assert.Equal(AuthService.CurrentPasswordNeeded, ErrorOf(await page.Save(CancellationToken.None)));
        Assert.NotNull(await rig.Clusters.GetUserByEmailAsync(AccountRig.NorthUser));
        Assert.Null(await rig.Clusters.GetUserByEmailAsync("elsewhere@example.test"));
    }

    [Fact]
    public async Task TheAccountPage_ShouldRefuseAShortPassword_AndTakeAGoodOne()
    {
        using var rig = await AccountRig.StartAsync(mongo);
        var tooShort = AccountRig.Form(rig.AccountPage(AccountRig.NorthUser),
            ("email", AccountRig.NorthUser), ("name", "North User"), ("currentPassword", AccountRig.Password), ("password", Short), ("passwordConfirmation", Short));
        Assert.Equal(AuthService.PasswordTooShort, ErrorOf(await tooShort.Save(CancellationToken.None)));

        var good = AccountRig.Form(rig.AccountPage(AccountRig.NorthUser),
            ("email", AccountRig.NorthUser), ("name", "North User"), ("currentPassword", AccountRig.Password), ("password", AccountRig.Typed), ("passwordConfirmation", AccountRig.Typed));
        Assert.Null(ErrorOf(await good.Save(CancellationToken.None)));
        Assert.True(await rig.SignsInAsync(AccountRig.NorthUser, AccountRig.Typed));
    }

    [Fact]
    public async Task TheAccountPage_ShouldChangeTheNameWithoutAPassword()
    {
        using var rig = await AccountRig.StartAsync(mongo);
        var page = AccountRig.Form(rig.AccountPage(AccountRig.NorthUser), ("email", AccountRig.NorthUser), ("name", "Renamed"));

        Assert.Null(ErrorOf(await page.Save(CancellationToken.None)));
        Assert.Equal("Renamed", await rig.NameOfAsync(AccountRig.NorthUser));
    }

    [Fact]
    public async Task ChangingATemporaryPassword_ShouldRefuseAShortOne()
    {
        using var rig = await AccountRig.StartAsync(mongo);
        await rig.Auth.CreatePasswordResetAsync(AccountRig.NorthUser, "127.0.0.1", "http://localhost");
        var temporary = rig.Mail.LastTemporaryPassword;
        var page = rig.LoginPage();

        var result = await page.ChangeTemporaryPassword(new TemporaryPasswordChangeViewModel
        {
            Email = AccountRig.NorthUser,
            TemporaryPassword = temporary,
            Password = Short,
            PasswordConfirmation = Short,
        }, CancellationToken.None);

        Assert.Equal(AuthService.PasswordTooShort, Assert.IsType<TemporaryPasswordChangeViewModel>(Assert.IsType<ViewResult>(result).Model).ErrorMessage);
        Assert.False(await rig.Auth.ConsumeTemporaryPasswordAsync(AccountRig.NorthUser, temporary, Short));
        Assert.True(await rig.Auth.ConsumeTemporaryPasswordAsync(AccountRig.NorthUser, temporary, AccountRig.Typed));
    }

    [Fact]
    public void EveryNewPasswordField_ShouldSayTheRule()
    {
        var pages = new[]
        {
            DppTestFiles.RepoFile("web", "Views", "Admin", "Clusters.cshtml"),
            DppTestFiles.RepoFile("web", "Views", "ClusterAdmin", "Users.cshtml"),
            DppTestFiles.RepoFile("web", "Views", "Account", "Index.cshtml"),
            DppTestFiles.RepoFile("web", "Views", "Login", "ChangeTemporaryPassword.cshtml"),
        };

        foreach (var page in pages)
        {
            var text = File.ReadAllText(page);
            Assert.Contains("At least 12 characters.", text, StringComparison.Ordinal);
            Assert.DoesNotContain("minlength=\"8\"", text, StringComparison.Ordinal);
        }

        Assert.Contains("name=\"currentPassword\"", File.ReadAllText(pages[2]), StringComparison.Ordinal);
    }
}
