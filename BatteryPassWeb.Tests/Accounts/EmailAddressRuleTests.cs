using BatteryPassWeb.Controllers;
using BatteryPassWeb.Services;
using BatteryPassWeb.Tests.Dpp;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Bson;
using MongoDB.Driver;

namespace BatteryPassWeb.Tests.Accounts;

/// <summary>Finding W6: one rule for every typed e-mail address, applied before anything is stored, logged or mailed.</summary>
[Collection(MongoCollection.Name)]
public sealed class EmailAddressRuleTests(MongoFixture mongo)
{
    public static TheoryData<string> Refused => new()
    {
        "api-token:x",
        "a@b.test\nBcc: x@y.test",
        "a@b.test\r\n",
        "a@b.test" + (char)0x2028 + "x@y.test",
        "a@b.test, c@d.test",
        "a@b.test;c@d.test",
        "a@@b.test",
        "@b.test",
        "a@",
        "a b@c.test",
        "<a@b.test>",
        "a\tb@c.test",
        "a@b.test\0",
    };

    [Theory]
    [MemberData(nameof(Refused))]
    public void TheRule_ShouldRefuse(string typed)
    {
        Assert.Equal(EmailAddressRule.Words, EmailAddressRule.Problem(typed));
    }

    [Theory]
    [InlineData("name@example.com")]
    [InlineData("  North.User@Example.test ")]
    [InlineData("first.last+tag@sub.example.test")]
    [InlineData("a@localhost")]
    [InlineData("")]
    public void TheRule_ShouldAccept(string typed)
    {
        Assert.Null(EmailAddressRule.Problem(typed));
    }

    [Fact]
    public void TheRule_ShouldRefuseMoreThan254Characters_AndAcceptExactly254()
    {
        var at254 = new string('a', 254 - "@x.test".Length) + "@x.test";
        Assert.Equal(254, at254.Length);

        Assert.Null(EmailAddressRule.Problem(at254));
        Assert.Equal(EmailAddressRule.Words, EmailAddressRule.Problem("a" + at254));
    }

    [Fact]
    public void TheLookUpCheck_ShouldOnlyLookAtLengthAndControlCharacters_SoOldAddressesStillWork()
    {
        Assert.True(EmailAddressRule.IsClean("old address without an at"));
        Assert.False(EmailAddressRule.IsClean("a@b.test\n"));
        Assert.False(EmailAddressRule.IsClean(new string('a', 255)));
    }

    private static string Bad => "a@b.test\nBcc: x@y.test";

    private static async Task<long> CountAsync(AccountRig rig, string collection, FilterDefinition<BsonDocument>? filter = null) =>
        await rig.Database.GetCollection<BsonDocument>(collection).CountDocumentsAsync(filter ?? FilterDefinition<BsonDocument>.Empty);

    [Fact]
    public async Task TheAccountPage_ShouldRefuseAnAddressWithALineBreak_AndChangeNothing()
    {
        using var rig = await AccountRig.StartAsync(mongo);
        var page = AccountRig.Form(rig.AccountPage(AccountRig.NorthUser),
            ("email", Bad), ("name", "North User"), ("currentPassword", AccountRig.Password));

        var result = await page.Save(CancellationToken.None);

        Assert.Equal(EmailAddressRule.Words, ((AccountProfileViewModelProbe.Model(result)).ErrorMessage));
        Assert.NotNull(await rig.Clusters.GetUserByEmailAsync(AccountRig.NorthUser));
        Assert.Equal(0, await CountAsync(rig, "applicationAuditEvents", Builders<BsonDocument>.Filter.Eq("eventType", "account.profile.updated")));
    }

    [Fact]
    public async Task TheAccountPage_ShouldRefuseAnAddressLikeAnActorName()
    {
        using var rig = await AccountRig.StartAsync(mongo);
        var page = AccountRig.Form(rig.AccountPage(AccountRig.NorthUser),
            ("email", "api-token:x"), ("name", "North User"), ("currentPassword", AccountRig.Password));

        await page.Save(CancellationToken.None);

        Assert.NotNull(await rig.Clusters.GetUserByEmailAsync(AccountRig.NorthUser));
        Assert.Null(await rig.Clusters.GetUserByEmailAsync("api-token:x"));
    }

    [Fact]
    public async Task TheTeamsUserForm_ShouldRefuseAnAddressWithALineBreak_AndMakeNoAccount()
    {
        using var rig = await AccountRig.StartAsync(mongo);
        var team = AccountRig.Form(rig.TeamMember(),
            ("email", Bad), ("password", AccountRig.Typed), ("passwordConfirmation", AccountRig.Typed));

        await team.SaveUser(CancellationToken.None);

        Assert.Equal(EmailAddressRule.Words, team.TempData["ErrorMessage"]);
        Assert.Equal(0, await CountAsync(rig, "users", Builders<BsonDocument>.Filter.Regex("email", "Bcc")));
    }

    [Fact]
    public async Task TheTeamsAddTo_ShouldRefuseAnAddressWithALineBreak()
    {
        using var rig = await AccountRig.StartAsync(mongo);
        var team = AccountRig.Form(rig.TeamMember(), ("email", Bad), ("clusterId", "north"), ("role", "member"));

        await team.AssignUserMembership(CancellationToken.None);

        Assert.Equal(EmailAddressRule.Words, team.TempData["ErrorMessage"]);
        Assert.Equal(0, await CountAsync(rig, "clusterMemberships", Builders<BsonDocument>.Filter.Regex("email", "Bcc")));
    }

    [Fact]
    public async Task TheClusterAdminsUserForm_ShouldRefuseAnAddressWithALineBreak_AndMakeNoAccountOrMembership()
    {
        using var rig = await AccountRig.StartAsync(mongo);
        var admin = AccountRig.Form(rig.ClusterAdmin(AccountRig.NorthAdmin),
            ("email", Bad), ("password", AccountRig.Typed), ("passwordConfirmation", AccountRig.Typed), ("clusterId", "north"));

        await admin.SaveUser(CancellationToken.None);

        Assert.Equal(EmailAddressRule.Words, admin.TempData["ErrorMessage"]);
        Assert.Equal(0, await CountAsync(rig, "users", Builders<BsonDocument>.Filter.Regex("email", "Bcc")));
        Assert.Equal(0, await CountAsync(rig, "clusterMemberships", Builders<BsonDocument>.Filter.Regex("email", "Bcc")));
    }

    [Fact]
    public async Task ForgotPassword_ShouldStoreLogAndMailNothingForARefusedAddress_AndAnswerAsAlways()
    {
        using var rig = await AccountRig.StartAsync(mongo);
        var login = rig.LoginPage();

        await login.ForgotPassword(Bad, CancellationToken.None);

        Assert.Equal("If an account exists, a temporary password has been sent.", login.TempData["ForgotPasswordMessage"]);
        Assert.Equal(0, await CountAsync(rig, "passwordResetRequests"));
        Assert.Equal(0, await CountAsync(rig, "applicationAuditEvents", Builders<BsonDocument>.Filter.Eq("eventType", "security.passwordReset.requested")));
        Assert.Equal(string.Empty, rig.Mail.LastTemporaryPassword);
    }

    [Fact]
    public async Task SignIn_ShouldRefuseAnAddressWithALineBreak_AndKeepNoCountForIt()
    {
        using var rig = await AccountRig.StartAsync(mongo);

        var result = await rig.Auth.AuthenticateLoginAsync(Bad, "whatever-password-1");

        Assert.Equal(LoginAuthenticationStatus.Invalid, result.Status);
        Assert.Equal(0, await CountAsync(rig, SignInAttempts.Collection));
    }

    [Fact]
    public async Task AnOldAccountWithAnOddStoredAddress_ShouldStillSignInAndSaveItsName()
    {
        using var rig = await AccountRig.StartAsync(mongo);
        await rig.Database.GetCollection<BsonDocument>("users").InsertOneAsync(new BsonDocument
        {
            ["email"] = "old odd address",
            ["name"] = "Old Account",
            ["roles"] = new BsonArray { AccessControlService.RoleNormalUser },
            ["passwordHash"] = BCrypt.Net.BCrypt.HashPassword(AccountRig.Password, 4),
        });

        Assert.True(await rig.SignsInAsync("old odd address", AccountRig.Password));

        var page = AccountRig.Form(rig.AccountPage("old odd address"), ("email", "old odd address"), ("name", "Renamed"));
        await page.Save(CancellationToken.None);

        Assert.Equal("Renamed", await rig.NameOfAsync("old odd address"));
    }
}

/// <summary>Reads the model of the account page's answer.</summary>
internal static class AccountProfileViewModelProbe
{
    public static BatteryPassWeb.Models.ViewModels.AccountProfileViewModel Model(IActionResult result) =>
        (BatteryPassWeb.Models.ViewModels.AccountProfileViewModel)((ViewResult)result).Model!;
}
