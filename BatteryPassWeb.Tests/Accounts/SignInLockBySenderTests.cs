using System.Net;
using BatteryPassWeb.Controllers;
using BatteryPassWeb.Models.ViewModels;
using BatteryPassWeb.Services;
using BatteryPassWeb.Tests.Dpp;
using Microsoft.AspNetCore.Mvc;

namespace BatteryPassWeb.Tests.Accounts;

/// <summary>
/// H1 round two, point 1 (the manager's rule): guessing is slowed for the guesser, never for the owner of the account.
/// Wrong passwords are counted per account and per sender: ten from one sender lock that sender out of that account, and
/// nobody else. Fifty from all senders together stop a password alone from signing in from a sender that never signed in
/// to the account before. A temporary password from "Forgot password" is never refused because of a lock, and the Account
/// page is limited only by its own session's sender. Every refusal says the same words. All through the pages.
/// </summary>
[Collection(MongoCollection.Name)]
public sealed class SignInLockBySenderTests(MongoFixture mongo)
{
    private const string Refused = "Invalid email or password. Try again later.";
    private const string Owner = AccountRig.Team;

    private static T From<T>(T controller, string ip) where T : Controller
    {
        controller.HttpContext.Connection.RemoteIpAddress = IPAddress.Parse(ip);
        return controller;
    }

    /// <summary>The sign-in page's answer: "signed in", "change temporary password" or the words it shows.</summary>
    private static async Task<string> SignInAsync(AccountRig rig, string ip, string email, string password)
    {
        var result = await From(rig.LoginPage(), ip).Index(new LoginViewModel { Email = email, Password = password }, CancellationToken.None);
        return result switch
        {
            RedirectResult => "signed in",
            ViewResult { ViewName: "ChangeTemporaryPassword" } => "change temporary password",
            ViewResult view => ((LoginViewModel)view.Model!).ErrorMessage,
            _ => result.GetType().Name,
        };
    }

    private static async Task WrongAsync(AccountRig rig, string ip, string email, int times)
    {
        for (var attempt = 0; attempt < times; attempt++)
        {
            Assert.Equal(Refused, await SignInAsync(rig, ip, email, $"not-the-password-{attempt}"));
        }
    }

    [Fact]
    public async Task TenWrongFromOneSender_ShouldLockOutThatSender_ButTheOwnerSignsInFromAnother()
    {
        using var rig = await AccountRig.StartAsync(mongo);
        await WrongAsync(rig, "203.0.113.7", Owner, 10);

        Assert.Equal(Refused, await SignInAsync(rig, "203.0.113.7", Owner, AccountRig.Password));
        Assert.Equal("signed in", await SignInAsync(rig, "198.51.100.20", Owner, AccountRig.Password));
    }

    [Fact]
    public async Task EveryRefusal_ShouldSayTheSameWords()
    {
        using var rig = await AccountRig.StartAsync(mongo);

        Assert.Equal(Refused, await SignInAsync(rig, "203.0.113.7", Owner, "wrong-password-1"));
        Assert.Equal(Refused, await SignInAsync(rig, "203.0.113.7", "nobody@example.test", "wrong-password-1"));
        await WrongAsync(rig, "203.0.113.8", Owner, 10);
        Assert.Equal(Refused, await SignInAsync(rig, "203.0.113.8", Owner, AccountRig.Password));
    }

    [Fact]
    public async Task FiftyWrongFromManySenders_ShouldStopAPasswordAloneFromNewSenders_ButNotFromOneThatSignedInBefore()
    {
        using var rig = await AccountRig.StartAsync(mongo);
        Assert.Equal("signed in", await SignInAsync(rig, "198.51.100.20", Owner, AccountRig.Password)); // the owner's usual place
        for (var sender = 1; sender <= 5; sender++)
        {
            await WrongAsync(rig, $"203.0.113.{sender}", Owner, 10);
        }

        Assert.Equal(Refused, await SignInAsync(rig, "192.0.2.99", Owner, AccountRig.Password));
        Assert.Equal("signed in", await SignInAsync(rig, "198.51.100.20", Owner, AccountRig.Password));
        Assert.Equal("signed in", await SignInAsync(rig, "192.0.2.99", AccountRig.Team2, AccountRig.Password)); // another account is untouched
    }

    [Fact]
    public async Task FortyNineWrongFromManySenders_ShouldStillLetANewSenderIn()
    {
        using var rig = await AccountRig.StartAsync(mongo);
        for (var sender = 1; sender <= 7; sender++)
        {
            await WrongAsync(rig, $"203.0.113.{sender}", Owner, 7);
        }

        Assert.Equal("signed in", await SignInAsync(rig, "192.0.2.99", Owner, AccountRig.Password));
    }

    [Fact]
    public async Task ATemporaryPasswordTypedOnTheSignInPage_ShouldWorkWhileTheAccountIsUnderAttack_AndClearTheCounts()
    {
        using var rig = await AccountRig.StartAsync(mongo);
        for (var sender = 1; sender <= 6; sender++)
        {
            await WrongAsync(rig, $"203.0.113.{sender}", Owner, 10);
        }

        var forgot = From(rig.LoginPage(), "203.0.113.1"); // the locked sender itself, and a new one for the account
        await forgot.ForgotPassword(Owner, CancellationToken.None);
        var temporary = rig.Mail.LastTemporaryPassword;
        Assert.False(string.IsNullOrEmpty(temporary));

        Assert.Equal("change temporary password", await SignInAsync(rig, "203.0.113.1", Owner, temporary));
        var change = await From(rig.LoginPage(), "203.0.113.1").ChangeTemporaryPassword(
            new TemporaryPasswordChangeViewModel { Email = Owner, TemporaryPassword = temporary, Password = "a-brand-new-password-1", PasswordConfirmation = "a-brand-new-password-1" },
            CancellationToken.None);
        Assert.IsType<RedirectResult>(change);

        Assert.Equal("signed in", await SignInAsync(rig, "203.0.113.1", Owner, "a-brand-new-password-1"));
        Assert.Equal("signed in", await SignInAsync(rig, "192.0.2.99", Owner, "a-brand-new-password-1"));
    }

    [Fact]
    public async Task TheAccountPage_ShouldWorkForASignedInOwner_WhateverOtherSendersDid()
    {
        using var rig = await AccountRig.StartAsync(mongo);
        for (var sender = 1; sender <= 6; sender++)
        {
            await WrongAsync(rig, $"203.0.113.{sender}", Owner, 10);
        }

        var page = From(AccountRig.Form(rig.AccountPage(Owner, AccessControlService.RoleAdmin),
            ("email", Owner), ("name", "Team"), ("currentPassword", AccountRig.Password), ("password", "a-brand-new-password-1"), ("passwordConfirmation", "a-brand-new-password-1")), "198.51.100.20");

        var result = await page.Save(CancellationToken.None);

        Assert.Equal(string.Empty, AccountProfileViewModelProbe.Model(result).ErrorMessage);
        Assert.True(await rig.SignsInAsync(Owner, "a-brand-new-password-1"));
    }

    [Fact]
    public async Task TheAccountPage_ShouldBeLimitedForItsOwnSender()
    {
        using var rig = await AccountRig.StartAsync(mongo);
        AccountController Page(string current) => From(AccountRig.Form(rig.AccountPage(Owner, AccessControlService.RoleAdmin),
            ("email", Owner), ("name", "Team"), ("currentPassword", current), ("password", "a-brand-new-password-1"), ("passwordConfirmation", "a-brand-new-password-1")), "198.51.100.20");

        for (var attempt = 0; attempt < 10; attempt++)
        {
            Assert.Equal(AuthService.CurrentPasswordWrong, AccountProfileViewModelProbe.Model(await Page($"wrong-current-{attempt}").Save(CancellationToken.None)).ErrorMessage);
        }

        Assert.Equal(AuthService.CurrentPasswordWrong, AccountProfileViewModelProbe.Model(await Page(AccountRig.Password).Save(CancellationToken.None)).ErrorMessage);
        Assert.Equal("signed in", await SignInAsync(rig, "192.0.2.99", Owner, AccountRig.Password)); // the sign-in page from elsewhere isn't touched
    }

    [Fact]
    public async Task ASuccessfulSignIn_ShouldClearOnlyItsOwnSendersCount()
    {
        using var rig = await AccountRig.StartAsync(mongo);
        await WrongAsync(rig, "203.0.113.7", Owner, 9);
        await WrongAsync(rig, "203.0.113.8", Owner, 9);
        Assert.Equal("signed in", await SignInAsync(rig, "203.0.113.8", Owner, AccountRig.Password));

        await WrongAsync(rig, "203.0.113.7", Owner, 1);

        Assert.Equal(Refused, await SignInAsync(rig, "203.0.113.7", Owner, AccountRig.Password));
        Assert.Equal("signed in", await SignInAsync(rig, "203.0.113.8", Owner, AccountRig.Password));
    }

    [Fact]
    public void EveryPasswordCheck_ShouldPassTheSender_AsTheRateLimiterTakesIt()
    {
        string Read(params string[] parts) => File.ReadAllText(DppTestFiles.RepoFile(parts));

        Assert.Contains("AuthenticateLoginAsync(model.Email, model.Password, Sender(HttpContext)", Read("web", "Controllers", "LoginController.cs"), StringComparison.Ordinal);
        Assert.Contains("LoginController.Sender(HttpContext)", Read("web", "Controllers", "AuthApiController.cs"), StringComparison.Ordinal);
        Assert.Contains("IsCurrentPasswordAsync(currentEmail, currentPassword, LoginController.Sender(HttpContext)", Read("web", "Controllers", "AccountController.cs"), StringComparison.Ordinal);
        Assert.Contains("context.Connection.RemoteIpAddress?.ToString() ?? \"unknown\"", Read("web", "Program.cs"), StringComparison.Ordinal);
        Assert.Contains("context.Connection.RemoteIpAddress?.ToString() ?? \"unknown\"", Read("web", "Controllers", "LoginController.cs"), StringComparison.Ordinal);
    }
}
