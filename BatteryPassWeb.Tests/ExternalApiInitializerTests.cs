using System.Reflection;
using BatteryPassWeb.Services;
using MongoDB.Bson;

namespace BatteryPassWeb.Tests;

public sealed class ExternalApiInitializerTests
{
    [Fact]
    public void EnsureSamplePassport_ShouldNotCloneSeededPassports()
    {
        var source = File.ReadAllText(RepoFile("web", "Services", "ExternalApiInitializer.cs"));

        Assert.Contains("if (existing == null)", source);
        Assert.Contains("return;", source);
        Assert.DoesNotContain("candidates.FirstOrDefault()?.DeepClone().AsBsonDocument", source);
    }

    [Fact]
    public void BuildFallbackSampleDocument_ShouldUseScaniaManufacturer()
    {
        var method = typeof(ExternalApiInitializer).GetMethod(
            "BuildFallbackSampleDocument",
            BindingFlags.NonPublic | BindingFlags.Static);

        Assert.NotNull(method);

        var document = Assert.IsType<BsonDocument>(method.Invoke(null, []));

        Assert.Equal(
            "Scania Industrial Batteries",
            document["app"]["display"]["manufacturerName"].AsString);
    }

    [Fact]
    public void DemoTokens_ShouldIncludeClusterScopedReadWriteAndSignTokensInResetAndHelp()
    {
        var initializer = File.ReadAllText(RepoFile("web", "Services", "ExternalApiInitializer.cs"));
        var repository = File.ReadAllText(RepoFile("web", "Services", "ExternalApiRepository.cs"));
        var productTemplateService = File.ReadAllText(RepoFile("web", "Services", "ProductTemplateService.cs"));
        var helpController = File.ReadAllText(RepoFile("web", "Controllers", "HelpController.cs"));
        var helpModel = File.ReadAllText(RepoFile("web", "Models", "ViewModels", "ExternalApiHelpViewModel.cs"));
        var helpView = File.ReadAllText(RepoFile("web", "Views", "Help", "Index.cshtml"));

        Assert.Contains("SampleApiClusterId = \"demo-cluster\"", initializer);
        Assert.Contains("SampleBatteryFamily", initializer);
        Assert.Contains("SampleBatterySerialNumber", initializer);
        Assert.Contains("SampleSignTokenId", initializer);
        Assert.Contains("SampleSignTokenValue", initializer);
        Assert.Contains("ExternalTokenAccessMode.Sign", initializer);
        Assert.Contains("UpsertFixedTokenAsync", repository);
        Assert.Contains("EnsureFixedApiDemoTokensAsync(cancellationToken);", productTemplateService);
        Assert.Contains("[\"demo-cluster\"]", productTemplateService);
        Assert.Contains("\"demo.user@example.test\"", productTemplateService);
        Assert.Contains("ExternalApiInitializer.SampleBatterySerialNumber", productTemplateService);
        Assert.Contains("[new(\"1.0\", -30), new(\"2.0\", 0)]", productTemplateService);
        Assert.Contains("ExternalApiInitializer.SampleApiClusterId", helpController);
        Assert.Contains("SampleSignTokenId", helpController);
        Assert.Contains("SampleSignToken", helpModel);
        Assert.Contains("Sample validate/sign/publish token", helpView);
        Assert.Contains("const sampleSignToken", helpView);
        Assert.Contains("'sample-sign': sampleSignToken", helpView);
        Assert.Contains("tokenPreset: 'sample-sign'", helpView);
    }

    [Fact]
    public void LandingSearchSample_ShouldUseGeneratedDemoBatteryIdInsteadOfLegacyDid()
    {
        var homeController = File.ReadAllText(RepoFile("web", "Controllers", "HomeController.cs"));
        var landing = File.ReadAllText(RepoFile("web", "Views", "Home", "Index.cshtml"));

        Assert.Contains("BatteryIdService", homeController);
        Assert.Contains("ExternalApiInitializer.CreateSampleBatteryId", homeController);
        Assert.DoesNotContain("sample-customer-north-001", homeController);
        Assert.Contains("sample battery ID", landing);
    }

    [Fact]
    public void LegacySampleDid_ShouldResolveToGeneratedDemoBattery()
    {
        var resolver = File.ReadAllText(RepoFile("web", "Services", "BatteryRouteResolutionService.cs"));

        Assert.Contains("LegacySampleBatteryId", resolver);
        Assert.Contains("CreateSampleBatteryId", resolver);
        Assert.Contains("BatteryIdService", resolver);
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
}
