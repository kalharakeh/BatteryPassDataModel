using BatteryPassWeb.Configuration;
using BatteryPassWeb.Services;
using BatteryPassWeb.Tests.Dpp;

namespace BatteryPassWeb.Tests.Accounts;

/// <summary>
/// Finding R21: in Production the app refuses to start with the fallback admin on; no default password is written in
/// code or settings; outside Production the fallback works only with a password from the environment; a database
/// account with the fallback's address, in any capitals, always wins.
/// </summary>
[Collection(MongoCollection.Name)]
public sealed class FallbackAdminTests(MongoFixture mongo)
{
    private static string Read(params string[] parts) => File.ReadAllText(DppTestFiles.RepoFile(parts));

    [Theory]
    [InlineData(true, true, true)]
    [InlineData(true, false, false)]
    [InlineData(false, true, false)]
    public void Production_ShouldRefuseToStart_WithTheFallbackOn(bool isProduction, bool fallbackOn, bool refused)
    {
        var message = DemoAdminFallbackConfiguration.ProductionRefusal(isProduction, fallbackOn);

        Assert.Equal(refused, message is not null);
        if (refused)
        {
            Assert.Contains("ENABLE_DEMO_ADMIN_FALLBACK", message, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void ProgramCs_ShouldCheckBeforeStarting_AndSayWhenThereIsNoPassword()
    {
        var program = Read("web", "Program.cs");

        Assert.True(program.IndexOf("DemoAdminFallbackConfiguration.ProductionRefusal(", StringComparison.Ordinal)
            < program.IndexOf("var app = builder.Build();", StringComparison.Ordinal));
        Assert.Contains("DemoAdminFallbackConfiguration.NoPasswordWarning", program, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("web", "Program.cs")]
    [InlineData("web", "appsettings.json")]
    [InlineData("web", "appsettings.Development.json")]
    public void NoDefaultPassword_ShouldBeWritten(params string[] file)
    {
        var text = Read(file);

        Assert.DoesNotContain("Password123", text, StringComparison.Ordinal);
        Assert.DoesNotContain("\"DemoAdminPassword\"", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TheFallback_ShouldNotWork_WithoutAPassword()
    {
        using var rig = await AccountRig.StartAsync(mongo, options => options.DemoAdminPassword = string.Empty);

        Assert.False(await rig.SignsInAsync(AccountRig.FallbackEmail, AccountRig.FallbackPassword));
        Assert.Null(await rig.Auth.CreatePrincipalForUserAsync(AccountRig.FallbackEmail));
    }

    [Theory]
    [InlineData(AccountRig.FallbackEmail)]
    [InlineData("FALLBACK.Admin@example.test")]
    public async Task ADatabaseAccountWithTheFallbacksAddress_ShouldAlwaysWin(string typed)
    {
        using var rig = await AccountRig.StartAsync(mongo);
        await rig.AccountAsync("Fallback.Admin@Example.TEST", "Someone", AccessControlService.RoleNormalUser);

        Assert.False(await rig.SignsInAsync(typed, AccountRig.FallbackPassword));
        var own = await rig.Auth.AuthenticateLoginAsync(typed, AccountRig.Password);
        Assert.Equal(LoginAuthenticationStatus.Authenticated, own.Status);
        Assert.False(own.Principal!.IsInRole(AccessControlService.RoleAdmin));
        Assert.False((await rig.Auth.CreatePrincipalForUserAsync(typed))!.IsInRole(AccessControlService.RoleAdmin));
    }
}
