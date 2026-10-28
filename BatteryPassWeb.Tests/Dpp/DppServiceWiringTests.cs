using BatteryPassWeb.Configuration;
using BatteryPassWeb.Dpp;
using BatteryPassWeb.Dpp.Editing;
using BatteryPassWeb.Dpp.Identifiers;
using BatteryPassWeb.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace BatteryPassWeb.Tests.Dpp;

public sealed class DppServiceWiringTests
{
    [Fact]
    public void AddDpp_ShouldResolveEveryServiceWithoutADatabase()
    {
        using var provider = new ServiceCollection()
            .AddSingleton(Options.Create(new BatteryPassOptions()))
            .AddSingleton<MongoContext>()
            .AddSingleton<ApplicationAuditService>()
            .AddDpp()
            .BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });

        Assert.NotNull(provider.GetRequiredService<SavePipeline>());
        Assert.NotNull(provider.GetRequiredService<PackCreationService>());
        Assert.IsType<TestIdentifierScheme>(provider.GetRequiredService<IIdentifierScheme>());
        Assert.Same(TimeProvider.System, provider.GetRequiredService<TimeProvider>());
    }

    [Fact]
    public void Program_ShouldRegisterTheServicesAndCreateTheIndexesAtStartup()
    {
        var program = File.ReadAllText(DppTestFiles.RepoFile("web", "Program.cs"));

        Assert.Contains("builder.Services.AddDpp();", program, StringComparison.Ordinal);
        Assert.Contains("await scope.ServiceProvider.GetRequiredService<DppStore>().EnsureIndexesAsync();", program, StringComparison.Ordinal);
    }
}
