using System.Net;
using BatteryPassWeb.Controllers;
using BatteryPassWeb.Models.ViewModels;
using BatteryPassWeb.Services;
using BatteryPassWeb.Tests.Dpp;
using Microsoft.AspNetCore.Mvc;

namespace BatteryPassWeb.Tests.Accounts;

/// <summary>
/// H1 round two, point 2: look-alike spellings of an address (an invisible character, full-width letters, "ß" for "ss")
/// that the account look-up still finds count against that account, so they don't get ten fresh tries each. And no new
/// address may hold an invisible or direction-changing character.
/// </summary>
[Collection(MongoCollection.Name)]
public sealed class SignInLookAlikeTests(MongoFixture mongo)
{
    private const string Real = "glass.team@example.test";

    // Written as character codes: an editor or a tool may turn an escape into the invisible character itself.
    private static readonly string ZeroWidth = "gla" + (char)0x200B + "ss.team@example.test";
    private static readonly string FullWidth = "ｇｌａｓｓ.team@example.test";
    private static readonly string SharpS = "gla" + (char)0x00DF + ".team@example.test";
    private static readonly string RightToLeft = "glass" + (char)0x202E + ".team@example.test";

    /// <summary>The spellings MongoDB's look-up (collation "en", strength 2) treats as the real address; "ß" is not one of them.</summary>
    public static TheoryData<string> LookAlikes => new() { ZeroWidth, FullWidth };

    private static async Task<string> SignInAsync(AccountRig rig, string email, string password)
    {
        var page = rig.LoginPage();
        page.HttpContext.Connection.RemoteIpAddress = IPAddress.Parse("203.0.113.7");
        var result = await page.Index(new LoginViewModel { Email = email, Password = password }, CancellationToken.None);
        return result is RedirectResult ? "signed in" : ((LoginViewModel)((ViewResult)result).Model!).ErrorMessage;
    }

    [Theory]
    [MemberData(nameof(LookAlikes))]
    public async Task TheLookUp_ShouldFindTheRealAccount_ForThisSpelling(string spelling)
    {
        using var rig = await AccountRig.StartAsync(mongo);
        await rig.AccountAsync(Real, "Glass Team", AccessControlService.RoleNormalUser);

        var found = await rig.Clusters.GetUserByEmailAsync(spelling);

        Assert.Equal(Real, found?["email"].AsString);
    }

    [Fact]
    public async Task SharpS_ShouldNotReachTheAccountAtAll()
    {
        using var rig = await AccountRig.StartAsync(mongo);
        await rig.AccountAsync(Real, "Glass Team", AccessControlService.RoleNormalUser);

        Assert.Null(await rig.Clusters.GetUserByEmailAsync(SharpS));
        Assert.Equal(SignInAttempts.Words, await SignInAsync(rig, SharpS, AccountRig.Password));
    }

    [Fact]
    public async Task WrongPasswordsUnderEverySpelling_ShouldCountAgainstTheOneAccount()
    {
        using var rig = await AccountRig.StartAsync(mongo);
        await rig.AccountAsync(Real, "Glass Team", AccessControlService.RoleNormalUser);

        foreach (var spelling in new[] { ZeroWidth, FullWidth, "GLASS.TEAM@EXAMPLE.TEST" })
        {
            for (var attempt = 0; attempt < 3; attempt++)
            {
                Assert.Equal(SignInAttempts.Words, await SignInAsync(rig, spelling, $"wrong-password-{attempt}"));
            }
        }

        Assert.Equal(SignInAttempts.Words, await SignInAsync(rig, Real, "wrong-password-9")); // the tenth, under the real spelling

        Assert.Equal(SignInAttempts.Words, await SignInAsync(rig, Real, AccountRig.Password));
    }

    [Fact]
    public void TheEmailRule_ShouldRefuseInvisibleAndDirectionChangingCharacters_ForANewAddress()
    {
        Assert.Equal(EmailAddressRule.Words, EmailAddressRule.Problem(ZeroWidth));
        Assert.Equal(EmailAddressRule.Words, EmailAddressRule.Problem(RightToLeft));
        Assert.Equal(EmailAddressRule.Words, EmailAddressRule.Problem("glass" + (char)0xFEFF + ".team@example.test"));
        Assert.Null(EmailAddressRule.Problem(Real));
    }

    [Fact]
    public async Task TheTeamsUserForm_ShouldNotMakeAnAccountThatLooksExactlyLikeAnother()
    {
        using var rig = await AccountRig.StartAsync(mongo);
        var team = AccountRig.Form(rig.TeamMember(),
            ("email", "team" + (char)0x200B + "@example.test"), ("password", AccountRig.Typed), ("passwordConfirmation", AccountRig.Typed));

        var before = (await rig.Clusters.ListUsersAsync()).Count;

        await team.SaveUser(CancellationToken.None);

        Assert.NotNull(team.TempData["ErrorMessage"]); // the look-up finds the team's account, or the rule refuses the address
        Assert.Equal(before, (await rig.Clusters.ListUsersAsync()).Count);
    }

    [Fact]
    public async Task TheClusterAdminsUserForm_ShouldRefuseAnAddressWithADirectionChangingCharacter()
    {
        using var rig = await AccountRig.StartAsync(mongo);
        var admin = AccountRig.Form(rig.ClusterAdmin(AccountRig.NorthAdmin),
            ("email", RightToLeft), ("password", AccountRig.Typed), ("passwordConfirmation", AccountRig.Typed), ("clusterId", "north"));

        await admin.SaveUser(CancellationToken.None);

        Assert.Equal(EmailAddressRule.Words, admin.TempData["ErrorMessage"]);
        Assert.Null(await rig.Clusters.GetUserByEmailAsync(RightToLeft));
    }
}
