using BatteryPassWeb.Configuration;
using BatteryPassWeb.Dpp;
using BatteryPassWeb.Dpp.Access;
using BatteryPassWeb.Dpp.Api;
using BatteryPassWeb.Dpp.Documents;
using BatteryPassWeb.Dpp.Editing;
using BatteryPassWeb.Dpp.Forms;
using BatteryPassWeb.Dpp.Identifiers;
using BatteryPassWeb.Dpp.Pages;
using BatteryPassWeb.Dpp.Publication;
using BatteryPassWeb.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using System.Security.Cryptography;

namespace BatteryPassWeb.Tests.Dpp;

public sealed class DppServiceWiringTests
{
    [Fact]
    public void AddDpp_ShouldResolveEveryServiceWithoutADatabase()
    {
        using var provider = new ServiceCollection()
            .AddSingleton(Options.Create(new BatteryPassOptions { AppBaseUrl = DocumentTesting.BaseUrl }))
            .AddSingleton<MongoContext>()
            .AddSingleton<ApplicationAuditService>()
            .AddSingleton<ClusterRepository>()
            .AddSingleton(new DemoSigningKeyService(ECDsa.Create(ECCurve.NamedCurves.nistP256)))
            .AddDpp()
            .BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });

        Assert.NotNull(provider.GetRequiredService<SavePipeline>());
        Assert.NotNull(provider.GetRequiredService<PackCreationService>());
        Assert.IsType<TestIdentifierScheme>(provider.GetRequiredService<IIdentifierScheme>());
        Assert.Same(TimeProvider.System, provider.GetRequiredService<TimeProvider>());
        Assert.NotNull(provider.GetRequiredService<PassportReader>());
        Assert.NotNull(provider.GetRequiredService<DocumentService>());
        Assert.True(provider.GetRequiredService<DppLinks>().IsConfigured);
        Assert.Same(provider.GetRequiredService<PublicationService>(), Assert.Single(provider.GetServices<IPackChangeListener>()));
        Assert.NotNull(provider.GetRequiredService<RecordEditor>());
        Assert.Same(provider.GetRequiredService<AttributeForms>(), provider.GetRequiredService<RecordEditor>().Forms);
        Assert.NotNull(provider.GetRequiredService<BatteryPages>());
        Assert.NotNull(provider.GetRequiredService<DppReaders>());
        Assert.NotNull(provider.GetRequiredService<ReaderPages>());
        Assert.NotNull(provider.GetRequiredService<PassportApi>());
    }

    [Fact]
    public void Program_ShouldRegisterTheServicesAndCreateTheIndexesAtStartup()
    {
        var program = File.ReadAllText(DppTestFiles.RepoFile("web", "Program.cs"));

        Assert.Contains("builder.Services.AddDpp();", program, StringComparison.Ordinal);
        Assert.Contains("await scope.ServiceProvider.GetRequiredService<DppStore>().EnsureIndexesAsync();", program, StringComparison.Ordinal);
        Assert.Contains("await scope.ServiceProvider.GetRequiredService<DocumentStore>().EnsureIndexesAsync();", program, StringComparison.Ordinal);
        Assert.Contains("APP_BASE_URL is not set", program, StringComparison.Ordinal);
    }
}
