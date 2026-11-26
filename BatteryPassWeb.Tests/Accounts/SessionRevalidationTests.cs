using System.Security.Claims;
using BatteryPassWeb.Services;
using BatteryPassWeb.Tests.Dpp;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace BatteryPassWeb.Tests.Accounts;

/// <summary>
/// Finding S02: an open session is checked against the account on each request. Deleting the account, lowering its access,
/// removing its cluster or changing its password (by someone else) takes its rights away; gaining access gives it;
/// sessions from before the update keep working.
/// </summary>
[Collection(MongoCollection.Name)]
public sealed class SessionRevalidationTests(MongoFixture mongo)
{
    /// <summary>One request with this session cookie: the principal the request goes on with, or null when it was signed out.</summary>
    private static async Task<ClaimsPrincipal?> NextRequestAsync(AccountRig rig, ClaimsPrincipal session)
    {
        var http = new DefaultHttpContext
        {
            RequestServices = new ServiceCollection().AddSingleton<IAuthenticationService, AccountRig.RecordingAuthenticationService>().BuildServiceProvider(),
        };
        var scheme = new AuthenticationScheme(CookieAuthenticationDefaults.AuthenticationScheme, null, typeof(CookieAuthenticationHandler));
        var now = DateTimeOffset.UtcNow;
        var ticket = new AuthenticationTicket(session, new AuthenticationProperties { IssuedUtc = now, ExpiresUtc = now.AddHours(3) }, scheme.Name);
        var context = new CookieValidatePrincipalContext(http, scheme, new CookieAuthenticationOptions(), ticket);

        await rig.Services.GetRequiredService<AuthenticationSessionService>().ValidatePrincipalAsync(context);

        return context.Principal;
    }

    private static async Task<ClaimsPrincipal> SignInAsync(AccountRig rig, string email, string password = AccountRig.Password)
    {
        var result = await rig.Auth.AuthenticateLoginAsync(email, password);
        Assert.Equal(LoginAuthenticationStatus.Authenticated, result.Status);
        return result.Principal!;
    }

    [Fact]
    public async Task AnUnchangedAccount_ShouldKeepItsSession()
    {
        using var rig = await AccountRig.StartAsync(mongo);
        var session = await SignInAsync(rig, AccountRig.NorthAdmin);

        var next = await NextRequestAsync(rig, session);

        Assert.NotNull(next);
        Assert.True(next.IsInRole(AccessControlService.RoleClusterAdmin));
    }

    [Fact]
    public async Task ADeletedAccount_ShouldLoseItsSession()
    {
        using var rig = await AccountRig.StartAsync(mongo);
        var session = await SignInAsync(rig, AccountRig.Team2);

        await AccountRig.Form(rig.TeamMember(), ("email", AccountRig.Team2)).DeleteAccount(CancellationToken.None);

        Assert.Null(await NextRequestAsync(rig, session));
    }

    [Fact]
    public async Task ALoweredTeamAccount_ShouldLoseTheTeamsRights()
    {
        using var rig = await AccountRig.StartAsync(mongo);
        var session = await SignInAsync(rig, AccountRig.Team2);

        await AccountRig.Form(rig.TeamMember(), ("change", "access"), ("email", AccountRig.Team2), ("name", "Team Two"), ("systemRole", "member")).SaveUser(CancellationToken.None);
        var next = await NextRequestAsync(rig, session);

        Assert.False(next?.IsInRole(AccessControlService.RoleAdmin) ?? false);
    }

    [Fact]
    public async Task AClusterAdminRemovedFromTheirCluster_ShouldLoseTheClusterAdminRole()
    {
        using var rig = await AccountRig.StartAsync(mongo);
        var session = await SignInAsync(rig, AccountRig.SouthAdmin);

        await rig.Clusters.DeleteClusterMembershipAsync(AccountRig.SouthAdmin, "south");
        var next = await NextRequestAsync(rig, session);

        Assert.False(next?.IsInRole(AccessControlService.RoleClusterAdmin) ?? false);
    }

    [Fact]
    public async Task AnAccountMadeClusterAdmin_ShouldGetTheRoleWithoutSigningInAgain()
    {
        using var rig = await AccountRig.StartAsync(mongo);
        var session = await SignInAsync(rig, AccountRig.NorthUser);

        await rig.Clusters.UpsertClusterMembershipAsync(AccountRig.NorthUser, "north", AccessControlService.RoleClusterAdmin);
        var next = await NextRequestAsync(rig, session);

        Assert.NotNull(next);
        Assert.True(next.IsInRole(AccessControlService.RoleClusterAdmin));
    }

    [Theory]
    [InlineData("team")]
    [InlineData("clusterAdmin")]
    [InlineData("reset")]
    public async Task APasswordChangedBySomeoneElseOrAReset_ShouldEndTheSession(string by)
    {
        using var rig = await AccountRig.StartAsync(mongo);
        var session = await SignInAsync(rig, AccountRig.NorthUser);

        switch (by)
        {
            case "team":
                await AccountRig.Form(rig.TeamMember(), ("change", "password"), ("email", AccountRig.NorthUser), ("password", AccountRig.Typed), ("passwordConfirmation", AccountRig.Typed)).SaveUser(CancellationToken.None);
                break;
            case "clusterAdmin":
                await AccountRig.Form(rig.ClusterAdmin(AccountRig.NorthAdmin), ("change", "password"), ("email", AccountRig.NorthUser), ("password", AccountRig.Typed), ("passwordConfirmation", AccountRig.Typed)).SaveUser(CancellationToken.None);
                break;
            default:
                await rig.Auth.CreatePasswordResetAsync(AccountRig.NorthUser, "127.0.0.1", "http://localhost");
                Assert.True(await rig.Auth.ConsumeTemporaryPasswordAsync(AccountRig.NorthUser, rig.Mail.LastTemporaryPassword, AccountRig.Typed));
                break;
        }

        Assert.True(await rig.SignsInAsync(AccountRig.NorthUser, AccountRig.Typed));
        Assert.Null(await NextRequestAsync(rig, session));
    }

    [Fact]
    public async Task YourOwnPasswordChange_ShouldKeepThisSession_AndEndTheOthers()
    {
        using var rig = await AccountRig.StartAsync(mongo);
        var otherBrowser = await SignInAsync(rig, AccountRig.NorthUser);
        var page = AccountRig.Form(rig.AccountPage(AccountRig.NorthUser),
            ("email", AccountRig.NorthUser), ("name", "North User"), ("currentPassword", AccountRig.Password), ("password", AccountRig.Typed), ("passwordConfirmation", AccountRig.Typed));
        page.HttpContext.User = await SignInAsync(rig, AccountRig.NorthUser);

        await page.Save(CancellationToken.None);

        Assert.NotNull(await NextRequestAsync(rig, page.HttpContext.User));
        Assert.Null(await NextRequestAsync(rig, otherBrowser));
    }

    [Fact]
    public async Task ASessionFromBeforeTheUpdate_ShouldKeepWorking()
    {
        using var rig = await AccountRig.StartAsync(mongo);
        var oldCookie = AccountRig.Person(AccountRig.NorthAdmin, AccessControlService.RoleNormalUser, AccessControlService.RoleClusterAdmin);

        var next = await NextRequestAsync(rig, oldCookie);

        Assert.NotNull(next);
        Assert.True(next.IsInRole(AccessControlService.RoleClusterAdmin));
    }

    [Fact]
    public async Task TheFallbackAdminsSession_ShouldLastUntilADatabaseAccountTakesItsAddress()
    {
        using var rig = await AccountRig.StartAsync(mongo);
        var session = await SignInAsync(rig, AccountRig.FallbackEmail, AccountRig.FallbackPassword);
        Assert.NotNull(await NextRequestAsync(rig, session));

        await rig.AccountAsync("Fallback.Admin@Example.test", "Someone", AccessControlService.RoleNormalUser);

        Assert.Null(await NextRequestAsync(rig, session));
    }

    [Fact]
    public async Task TheFallbackAdminsSession_ShouldEnd_WhenTheFallbackIsSwitchedOff()
    {
        ClaimsPrincipal session;
        using (var on = await AccountRig.StartAsync(mongo))
        {
            session = await SignInAsync(on, AccountRig.FallbackEmail, AccountRig.FallbackPassword);
        }

        using var off = await AccountRig.StartAsync(mongo, options => options.EnableDemoAdminFallback = false);

        Assert.Null(await NextRequestAsync(off, session));
    }
}
