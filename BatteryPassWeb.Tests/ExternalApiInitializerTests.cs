using System.Reflection;
using BatteryPassWeb.Services;
using MongoDB.Bson;

namespace BatteryPassWeb.Tests;

public sealed class ExternalApiInitializerTests
{
    [Fact]
    public void EnsureSamplePassport_ShouldUseGeneratedFallbackInsteadOfCloningSeededPassports()
    {
        var source = File.ReadAllText(RepoFile("web", "Services", "ExternalApiInitializer.cs"));

        Assert.Contains("CreateSampleBatteryId(_batteryIdService)", source);
        Assert.Contains("BuildFallbackSampleDocument(sampleBatteryId)", source);
        Assert.Contains("BuildFallbackSampleBattery(sampleBatteryId)", source);
        Assert.DoesNotContain("candidates.FirstOrDefault()?.DeepClone().AsBsonDocument", source);
    }

    [Fact]
    public void BuildFallbackSampleDocument_ShouldUseGeneratedBatteryClusterAndPublicTrust()
    {
        var method = typeof(ExternalApiInitializer).GetMethod(
            "BuildFallbackSampleDocument",
            BindingFlags.NonPublic | BindingFlags.Static);

        Assert.NotNull(method);

        var document = Assert.IsType<BsonDocument>(method.Invoke(null, ["sample-battery-id"]));

        Assert.Equal("sample-battery-id", document["batteryId"].AsString);
        Assert.Equal(ExternalApiInitializer.SampleApiClusterId, document["clusterId"].AsString);
        Assert.True(document["isLatestForBattery"].AsBoolean);
        Assert.Equal(
            "Scania Industrial Batteries",
            document["app"]["display"]["manufacturerName"].AsString);
        Assert.Equal("published", document["registryInfo"]["status"].AsString);
        Assert.True(document["validation"]["isValid"].AsBoolean);
        Assert.Equal("signed", document["trust"]["state"].AsString);
        Assert.False(document["trust"]["isDirty"].AsBoolean);
        Assert.NotEmpty(document["trust"]["latestProof"]["proofValue"].AsString);
    }

    [Fact]
    public void BuildFallbackSampleBattery_ShouldUseGeneratedBatteryClusterAndIdentity()
    {
        var method = typeof(ExternalApiInitializer).GetMethod(
            "BuildFallbackSampleBattery",
            BindingFlags.NonPublic | BindingFlags.Static);

        Assert.NotNull(method);

        var document = Assert.IsType<BsonDocument>(method.Invoke(null, ["sample-battery-id"]));

        Assert.Equal("sample-battery-id", document["batteryId"].AsString);
        Assert.Equal(ExternalApiInitializer.SampleApiClusterId, document["clusterId"].AsString);
        Assert.Equal(ExternalApiInitializer.SampleBatteryFamily, document["identity"]["batteryFamily"].AsString);
        Assert.Equal(ExternalApiInitializer.SampleBatterySerialNumber, document["identity"]["serialNumber"].AsString);
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
        Assert.Contains("Sample create, validate, sign and publish passport token", helpView);
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
        Assert.Contains("public sample passport", landing, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("return Redirect($\"/{Uri.EscapeDataString(query)}\")", homeController);
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
