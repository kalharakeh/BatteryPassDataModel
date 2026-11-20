using System.Net;
using BatteryPassWeb.Controllers;
using BatteryPassWeb.Models.ViewModels;
using BatteryPassWeb.Services;
using BatteryPassWeb.Tests.Dpp;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Bson;
using MongoDB.Driver;

namespace BatteryPassWeb.Tests.Accounts;

/// <summary>
/// FU3: an account with no password yet (the first team account before it is claimed) costs a sign-in as long as an address
/// with no account, so nobody can tell it is unclaimed by the time the answer takes; and the temporary password that
/// "Forgot password" mails signs the person in even while the account is locked, while wrong passwords stay refused.
/// </summary>
[Collection(MongoCollection.Name)]
public sealed class FirstAdminSignInTests(MongoFixture mongo)
{
    private const string Refused = "Invalid email or password. Try again later.";
    private const string Unclaimed = "unclaimed.team@example.test";

    private static async Task<string> SignInAsync(AccountRig rig, string ip, string email, string password)
    {
        var page = rig.LoginPage();
        page.HttpContext.Connection.RemoteIpAddress = IPAddress.Parse(ip);
        var result = await page.Index(new LoginViewModel { Email = email, Password = password }, CancellationToken.None);
        return result switch
        {
            RedirectResult => "signed in",
            ViewResult { ViewName: "ChangeTemporaryPassword" } => "change temporary password",
            ViewResult view => ((LoginViewModel)view.Model!).ErrorMessage,
            _ => result.GetType().Name,
        };
    }

    [Fact]
    public async Task AnAccountWithNoPasswordYet_ShouldRunTheDummyPasswordCheckAsAnAddressWithNoAccountDoes()
    {
        using var rig = await AccountRig.StartAsync(mongo);
        await rig.AccountAsync(Unclaimed, "Unclaimed", AccessControlService.RoleAdmin);
        await rig.Database.GetCollection<BsonDocument>("users").UpdateOneAsync(
            Builders<BsonDocument>.Filter.Eq("email", Unclaimed),
            Builders<BsonDocument>.Update.Unset("passwordHash"));

        var start = rig.Auth.DummyChecks;
        Assert.Equal(Refused, await SignInAsync(rig, "203.0.113.50", "nobody@example.test", "some-guess-1"));
        var noAccount = rig.Auth.DummyChecks - start;
        Assert.Equal(Refused, await SignInAsync(rig, "203.0.113.50", Unclaimed, "some-guess-1"));
        var noPassword = rig.Auth.DummyChecks - start - noAccount;
        Assert.Equal(Refused, await SignInAsync(rig, "203.0.113.50", AccountRig.Team, "some-guess-1")); // an account with a password does its own check
        var withPassword = rig.Auth.DummyChecks - start - noAccount - noPassword;

        Assert.Equal(2, noAccount); // the temporary password's step and the password's step
        Assert.Equal(2, noPassword); // as an address with no account
        Assert.Equal(1, withPassword); // only the temporary password's step; its password step is a real check
    }

    [Fact]
    public async Task WhileTheAccountIsLocked_ThePersonsMailedTemporaryPasswordSignsThemIn_AndWrongPasswordsAreStillRefused()
    {
        using var rig = await AccountRig.StartAsync(mongo);
        for (var attempt = 0; attempt < 10; attempt++)
        {
            Assert.Equal(Refused, await SignInAsync(rig, "203.0.113.7", AccountRig.Team, $"not-the-password-{attempt}"));
        }

        Assert.Equal(Refused, await SignInAsync(rig, "203.0.113.7", AccountRig.Team, AccountRig.Password)); // locked: even the right password
        var forgot = rig.LoginPage();
        forgot.HttpContext.Connection.RemoteIpAddress = IPAddress.Parse("203.0.113.7");
        await forgot.ForgotPassword(AccountRig.Team, CancellationToken.None);
        var temporary = rig.Mail.LastTemporaryPassword;
        Assert.False(string.IsNullOrEmpty(temporary));

        Assert.Equal(Refused, await SignInAsync(rig, "203.0.113.7", AccountRig.Team, "another-wrong-guess"));
        Assert.Equal(Refused, await SignInAsync(rig, "203.0.113.7", AccountRig.Team, AccountRig.Password));
        Assert.Equal("change temporary password", await SignInAsync(rig, "203.0.113.7", AccountRig.Team, temporary));
    }

    [Fact]
    public async Task EveryWrongSignIn_ShouldCostTheSameNumberOfPasswordChecks_WhateverTheAddress()
    {
        using var rig = await AccountRig.StartAsync(mongo);
        await rig.AccountAsync(Unclaimed, "Unclaimed", AccessControlService.RoleAdmin);
        await rig.Database.GetCollection<BsonDocument>("users").UpdateOneAsync(
            Builders<BsonDocument>.Filter.Eq("email", Unclaimed),
            Builders<BsonDocument>.Update.Unset("passwordHash"));
        var forgot = rig.LoginPage();
        forgot.HttpContext.Connection.RemoteIpAddress = IPAddress.Parse("203.0.113.60");
        await forgot.ForgotPassword(AccountRig.Team2, CancellationToken.None); // an active "Forgot password" request for Team2

        async Task<long> CostAsync(string email)
        {
            var before = rig.Auth.PasswordChecks;
            Assert.Equal(Refused, await SignInAsync(rig, "203.0.113.50", email, "some-guess-1"));
            return rig.Auth.PasswordChecks - before;
        }

        var unknown = await CostAsync("nobody@example.test");
        var noPassword = await CostAsync(Unclaimed);
        var withPassword = await CostAsync(AccountRig.Team);
        var activeRequest = await CostAsync(AccountRig.Team2);

        Assert.Equal([unknown, unknown, unknown, unknown], [unknown, noPassword, withPassword, activeRequest]);
        Assert.Equal(2, unknown); // the temporary password's step and the password's step
    }
}
