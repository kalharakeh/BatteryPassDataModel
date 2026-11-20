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
    [InlineData("web", "Configuration", "BatteryPassOptions.cs")]
    [InlineData("web", ".env.example")]
    public void NoDefaultPassword_ShouldBeWritten(params string[] file)
    {
        var text = Read(file);

        Assert.DoesNotContain("Password123", text, StringComparison.Ordinal);
        Assert.DoesNotContain("\"DemoAdminPassword\"", text, StringComparison.Ordinal);
    }

    [Fact]
    public void TheOldDefault_ShouldBeNowhereInTheRepository()
    {
        var old = "Password" + "123!"; // written in two parts, so this file isn't a hit itself
        var root = Path.GetFullPath(Path.Combine(DppTestFiles.RepoFile("web", "Program.cs"), "..", ".."));
        // The repository's own source folders only, and no dot-folders, build output or local launchers: a checkout's
        // untracked and ignored files are not the repository's.
        var skip = new[] { "bin", "obj", "node_modules" };
        var hits = new[] { "web", "tools", "scripts", "docs", "BatteryPassWeb.Tests" }
            .Select(folder => Path.Combine(root, folder))
            .Where(Directory.Exists)
            .SelectMany(folder => Directory.EnumerateFiles(folder, "*", SearchOption.AllDirectories))
            .Where(path => !path[root.Length..].Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                .Any(part => skip.Contains(part) || (part.StartsWith('.') && part != Path.GetFileName(path))))
            .Where(path => Path.GetFileName(path) is not (".env.local" or ".env"))
            .Where(path => Path.GetExtension(path) is ".cs" or ".json" or ".md" or ".cshtml" or ".js" or ".sh" or ".example" or ".yml" or ".yaml")
            .Where(path => File.ReadAllText(path).Contains(old, StringComparison.Ordinal))
            .Select(path => path[root.Length..])
            .ToList();

        Assert.Empty(hits);
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
