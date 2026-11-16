using BatteryPassWeb.Controllers;
using BatteryPassWeb.Models.ViewModels;
using BatteryPassWeb.Services;
using BatteryPassWeb.Tests.Dpp;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Bson;
using MongoDB.Driver;

namespace BatteryPassWeb.Tests.Accounts;

/// <summary>
/// Finding W4: after 10 wrong passwords for one address within 15 minutes the address can't sign in for 15 minutes, whoever
/// tries and from wherever; the page says what it says for a wrong password plus "try again later", for an address with no
/// account too. A sign-in or a new password clears the count. It is kept in the database.
/// </summary>
[Collection(MongoCollection.Name)]
public sealed class SignInLockoutTests(MongoFixture mongo)
{
    private static async Task WrongPasswordsAsync(AccountRig rig, string email, int times)
    {
        for (var attempt = 0; attempt < times; attempt++)
        {
            Assert.False(await rig.SignsInAsync(email, "not-the-password-" + attempt));
        }
    }

    [Fact]
    public async Task TenWrongPasswords_ShouldLockTheAccount_AndTheRightOneIsRefusedMeanwhile()
    {
        using var rig = await AccountRig.StartAsync(mongo);
        await WrongPasswordsAsync(rig, AccountRig.NorthUser, 10);

        var result = await rig.Auth.AuthenticateLoginAsync(AccountRig.NorthUser, AccountRig.Password);

        Assert.Equal(LoginAuthenticationStatus.Locked, result.Status);
        Assert.Null(result.Principal);
    }

    [Fact]
    public async Task NineWrongPasswords_ShouldNotLockTheAccount()
    {
        using var rig = await AccountRig.StartAsync(mongo);
        await WrongPasswordsAsync(rig, AccountRig.NorthUser, 9);

        Assert.True(await rig.SignsInAsync(AccountRig.NorthUser, AccountRig.Password));
    }

    [Fact]
    public async Task TheLock_ShouldHoldWhateverCapitalsTheAddressIsTypedIn()
    {
        using var rig = await AccountRig.StartAsync(mongo);
        await WrongPasswordsAsync(rig, "North.User@Example.test", 5);
        await WrongPasswordsAsync(rig, "  north.user@example.test ", 5);

        Assert.False(await rig.SignsInAsync(AccountRig.NorthUser, AccountRig.Password));
    }

    [Fact]
    public async Task TheLock_ShouldEndAfterFifteenMinutes_AndNotBefore()
    {
        var clock = new MutableTimeProvider(DateTimeOffset.UtcNow);
        using var rig = await AccountRig.StartAsync(mongo, clock: clock);
        await WrongPasswordsAsync(rig, AccountRig.NorthUser, 10);

        clock.Now = clock.Now.AddMinutes(14);
        Assert.False(await rig.SignsInAsync(AccountRig.NorthUser, AccountRig.Password));

        clock.Now = clock.Now.AddMinutes(2);
        Assert.True(await rig.SignsInAsync(AccountRig.NorthUser, AccountRig.Password));
    }

    [Fact]
    public async Task WrongPasswordsMoreThanFifteenMinutesApart_ShouldNotAddUp()
    {
        var clock = new MutableTimeProvider(DateTimeOffset.UtcNow);
        using var rig = await AccountRig.StartAsync(mongo, clock: clock);
        await WrongPasswordsAsync(rig, AccountRig.NorthUser, 6);
        clock.Now = clock.Now.AddMinutes(16);
        await WrongPasswordsAsync(rig, AccountRig.NorthUser, 6);

        Assert.True(await rig.SignsInAsync(AccountRig.NorthUser, AccountRig.Password));
    }

    [Fact]
    public async Task ASignIn_ShouldClearTheCount()
    {
        using var rig = await AccountRig.StartAsync(mongo);
        await WrongPasswordsAsync(rig, AccountRig.NorthUser, 9);
        Assert.True(await rig.SignsInAsync(AccountRig.NorthUser, AccountRig.Password));

        await WrongPasswordsAsync(rig, AccountRig.NorthUser, 9);

        Assert.True(await rig.SignsInAsync(AccountRig.NorthUser, AccountRig.Password));
    }

    [Fact]
    public async Task AnAddressWithNoAccount_ShouldLockTheSameWay_SoTheLockSaysNothingAboutAccounts()
    {
        using var rig = await AccountRig.StartAsync(mongo);
        await WrongPasswordsAsync(rig, "nobody@example.test", 10);

        var result = await rig.Auth.AuthenticateLoginAsync("nobody@example.test", "anything-at-all-1");

        Assert.Equal(LoginAuthenticationStatus.Locked, result.Status);
    }

    [Fact]
    public async Task TheLoginPage_ShouldSaySameWordsForALockedAccountWithTheRightPassword_AndForNoAccount()
    {
        using var rig = await AccountRig.StartAsync(mongo);
        await WrongPasswordsAsync(rig, AccountRig.NorthUser, 10);
        await WrongPasswordsAsync(rig, "nobody@example.test", 10);

        async Task<string> SaysAsync(string email, string password)
        {
            var page = (ViewResult)await rig.LoginPage().Index(new LoginViewModel { Email = email, Password = password }, CancellationToken.None);
            return ((LoginViewModel)page.Model!).ErrorMessage;
        }

        var locked = await SaysAsync(AccountRig.NorthUser, AccountRig.Password);
        Assert.Equal("Invalid email or password. Try again later.", locked);
        Assert.Equal(locked, await SaysAsync("nobody@example.test", "x"));
        Assert.Equal("Invalid email or password.", await SaysAsync(AccountRig.SouthUser, "wrong"));
    }

    [Fact]
    public async Task TheCount_ShouldBeKeptInTheDatabase_SoAnotherAppInstanceSeesIt()
    {
        using var rig = await AccountRig.StartAsync(mongo);
        await WrongPasswordsAsync(rig, AccountRig.NorthUser, 10);

        var other = ActivatorUtilities.CreateInstance<AuthService>(rig.Services);

        Assert.Equal(LoginAuthenticationStatus.Locked, (await other.AuthenticateLoginAsync(AccountRig.NorthUser, AccountRig.Password)).Status);
        Assert.True(await rig.Database.GetCollection<BsonDocument>(SignInAttempts.Collection).CountDocumentsAsync(FilterDefinition<BsonDocument>.Empty) > 0);
    }

    [Fact]
    public async Task AReset_ShouldClearTheLock_AndWorksWhileLocked()
    {
        using var rig = await AccountRig.StartAsync(mongo);
        await WrongPasswordsAsync(rig, AccountRig.NorthUser, 10);
        await rig.Auth.CreatePasswordResetAsync(AccountRig.NorthUser, "127.0.0.1", "https://example.test");

        var changed = await rig.Auth.ConsumeTemporaryPasswordAsync(AccountRig.NorthUser, rig.Mail.LastTemporaryPassword, "a-brand-new-password-1");

        Assert.True(changed);
        Assert.True(await rig.SignsInAsync(AccountRig.NorthUser, "a-brand-new-password-1"));
    }

    [Fact]
    public async Task ANewPasswordFromTheTeamOrAClusterAdmin_ShouldClearTheLock()
    {
        using var rig = await AccountRig.StartAsync(mongo);
        await WrongPasswordsAsync(rig, AccountRig.NorthUser, 10);

        await AccountRig.Form(rig.ClusterAdmin(AccountRig.NorthAdmin),
            ("email", AccountRig.NorthUser), ("change", "password"), ("password", AccountRig.Typed), ("passwordConfirmation", AccountRig.Typed)).SaveUser(CancellationToken.None);

        Assert.True(await rig.SignsInAsync(AccountRig.NorthUser, AccountRig.Typed));

        await WrongPasswordsAsync(rig, AccountRig.SouthUser, 10);
        await AccountRig.Form(rig.TeamMember(),
            ("email", AccountRig.SouthUser), ("change", "password"), ("password", AccountRig.Typed), ("passwordConfirmation", AccountRig.Typed)).SaveUser(CancellationToken.None);

        Assert.True(await rig.SignsInAsync(AccountRig.SouthUser, AccountRig.Typed));
    }

    [Fact]
    public async Task TheFallbackAdmin_ShouldBeLockedLikeAnyOtherAddress()
    {
        using var rig = await AccountRig.StartAsync(mongo);
        await WrongPasswordsAsync(rig, AccountRig.FallbackEmail, 10);

        Assert.False(await rig.SignsInAsync(AccountRig.FallbackEmail, AccountRig.FallbackPassword));
    }
}
