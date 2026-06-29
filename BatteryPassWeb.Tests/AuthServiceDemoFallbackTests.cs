using BatteryPassWeb.Configuration;
using BatteryPassWeb.Services;
using Microsoft.Extensions.Options;

namespace BatteryPassWeb.Tests;

public sealed class AuthServiceDemoFallbackTests
{
    [Fact]
    public async Task AuthenticateLoginAsync_ShouldRejectDemoAdminFallbackUnlessExplicitlyEnabled()
    {
        var service = CreateService(new BatteryPassOptions
        {
            DemoAdminEmail = "admin@example.test",
            DemoAdminPassword = "Password123!",
            EnableDemoAdminFallback = false
        });

        var result = await service.AuthenticateLoginAsync("admin@example.test", "Password123!");

        Assert.Equal(LoginAuthenticationStatus.Invalid, result.Status);
        Assert.Null(result.Principal);
    }

    [Fact]
    public async Task AuthenticateLoginAsync_ShouldAllowDemoAdminFallbackWhenExplicitlyEnabled()
    {
        var service = CreateService(new BatteryPassOptions
        {
            DemoAdminEmail = "admin@example.test",
            DemoAdminPassword = "Password123!",
            EnableDemoAdminFallback = true
        });

        var result = await service.AuthenticateLoginAsync("admin@example.test", "Password123!");

        Assert.Equal(LoginAuthenticationStatus.Authenticated, result.Status);
        Assert.NotNull(result.Principal);
        Assert.True(result.Principal!.IsInRole(AccessControlService.RoleAdmin));
    }

    [Fact]
    public async Task CreatePrincipalForUserAsync_ShouldRejectDemoAdminFallbackUnlessExplicitlyEnabled()
    {
        var service = CreateService(new BatteryPassOptions
        {
            DemoAdminEmail = "admin@example.test",
            DemoAdminPassword = "Password123!",
            EnableDemoAdminFallback = false
        });

        var principal = await service.CreatePrincipalForUserAsync("admin@example.test");

        Assert.Null(principal);
    }

    [Fact]
    public void Configuration_ShouldExposeExplicitDemoAdminFallbackFlag()
    {
        var options = File.ReadAllText(RepoFile("web", "Configuration", "BatteryPassOptions.cs"));
        var program = File.ReadAllText(RepoFile("web", "Program.cs"));
        var appsettings = File.ReadAllText(RepoFile("web", "appsettings.json"));
        var developmentSettings = File.ReadAllText(RepoFile("web", "appsettings.Development.json"));
        var envExample = File.ReadAllText(RepoFile("web", ".env.example"));

        Assert.Contains("EnableDemoAdminFallback", options);
        Assert.Contains("ENABLE_DEMO_ADMIN_FALLBACK", program);
        Assert.Contains("\"EnableDemoAdminFallback\": false", appsettings);
        Assert.Contains("\"EnableDemoAdminFallback\": true", developmentSettings);
        Assert.Contains("ENABLE_DEMO_ADMIN_FALLBACK=true", envExample);
    }

    private static AuthService CreateService(BatteryPassOptions options)
    {
        var configuredOptions = Options.Create(options);
        return new AuthService(new MongoContext(configuredOptions), configuredOptions, new TestEmailSender());
    }

    private static string RepoFile(params string[] parts)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null)
        {
            var candidate = Path.Combine(new[] { directory.FullName }.Concat(parts).ToArray());
            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException($"Could not find repository file: {Path.Combine(parts)}");
    }

    private sealed class TestEmailSender : IEmailSender
    {
        public bool IsConfigured => false;

        public Task SendTemporaryPasswordAsync(
            string recipientEmail,
            string temporaryPassword,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }
}
