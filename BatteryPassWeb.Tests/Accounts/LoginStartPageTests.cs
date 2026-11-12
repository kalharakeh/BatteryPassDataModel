using BatteryPassWeb.Controllers;
using BatteryPassWeb.Services;
using BatteryPassWeb.Tests.Dpp;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;

namespace BatteryPassWeb.Tests.Accounts;

/// <summary>Finding U31: a signed-in reader who opens the login address goes where signing in would have taken them.</summary>
[Collection(MongoCollection.Name)]
public sealed class LoginStartPageTests(MongoFixture mongo)
{
    [Theory]
    [InlineData(AccessControlService.RoleNormalUser, "/")]
    [InlineData(AccessControlService.RoleMarketSurveillanceAuthority, "/")]
    [InlineData(AccessControlService.RoleClusterAdmin, "/")]
    [InlineData(AccessControlService.RoleAdmin, "/admin")]
    public async Task TheLoginAddress_ShouldLeadASignedInReaderToTheirStartPage(string role, string start)
    {
        using var rig = await AccountRig.StartAsync(mongo);
        var page = AccountRig.With(rig.LoginPage(), AccountRig.Person("someone@example.test", role));

        var result = page.Index(next: null, returnUrl: null, switchAccount: false);

        Assert.Equal(start, Assert.IsType<RedirectResult>(result).Url);
    }

    [Fact]
    public async Task TheFormSignIn_ShouldLeadAReaderToTheirStartPage()
    {
        using var rig = await AccountRig.StartAsync(mongo);
        var http = new DefaultHttpContext
        {
            RequestServices = new ServiceCollection().AddSingleton<IAuthenticationService, AccountRig.RecordingAuthenticationService>().BuildServiceProvider(),
        };
        var api = new AuthApiController(rig.Auth, rig.Services.GetRequiredService<AuthenticationSessionService>())
        {
            ControllerContext = new ControllerContext { HttpContext = http },
        };

        var result = await api.Login(AccountRig.Msa, AccountRig.Password, null, CancellationToken.None);

        Assert.Equal("/", Assert.IsType<RedirectResult>(result).Url);
    }

    [Fact]
    public void TheStartPage_ShouldBeTheTeamsAreaOnlyForTheTeam()
    {
        Assert.Equal("/admin", LoginController.StartPage(AccountRig.Person("team@example.test", AccessControlService.RoleAdmin)));
        Assert.Equal("/", LoginController.StartPage(AccountRig.Person("reader@example.test", AccessControlService.RoleNormalUser)));
    }
}
