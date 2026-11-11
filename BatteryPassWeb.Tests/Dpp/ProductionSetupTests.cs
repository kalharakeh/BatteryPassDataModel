using BatteryPassWeb.Configuration;
using BatteryPassWeb.Services;
using Microsoft.Extensions.Configuration;

namespace BatteryPassWeb.Tests.Dpp;

/// <summary>
/// C3 round three, point 1: the repository's own Production set-up still starts after decision 61. The example settings
/// and the portable notes name the signing key and the battery IDs' address, and an address set but empty counts as
/// not set, so the next one is used.
/// </summary>
public sealed class ProductionSetupTests
{
    [Fact]
    public void TheExampleSettings_ShouldNameTheSigningKeyAndTheIdAddress_AndNoUnusedIssuerLines()
    {
        var example = File.ReadAllText(DppTestFiles.RepoFile("web", ".env.example")).Replace("\r\n", "\n");
        var lines = example.Split('\n');

        Assert.Contains($"{DemoSigningKeyService.PrivateKeySetting}=", lines);
        Assert.Contains("DPP_ID_BASE_URL=", lines);
        Assert.Contains(lines, line => line.StartsWith('#') && line.Contains("ExportPkcs8PrivateKey", StringComparison.Ordinal)); // how to make a key
        Assert.DoesNotContain("DID_ISSUER", example, StringComparison.Ordinal);
    }

    [Fact]
    public void ThePortableNotes_ShouldNameTheSigningKeyAndTheIdAddress()
    {
        var script = File.ReadAllText(DppTestFiles.RepoFile("scripts", "publish-portable-win-x64.ps1"));
        var guide = File.ReadAllText(DppTestFiles.RepoFile("docs", "portable-publish-and-deploy.md"));

        Assert.Contains("ASPNETCORE_ENVIRONMENT=Production", script, StringComparison.Ordinal); // why they are required
        foreach (var text in new[] { script, guide })
        {
            Assert.Contains(DemoSigningKeyService.PrivateKeySetting, text, StringComparison.Ordinal);
            Assert.Contains("DPP_ID_BASE_URL", text, StringComparison.Ordinal);
        }
    }

    [Theory]
    [InlineData("", "https://www.example.test", "https://www.example.test")]
    [InlineData("   ", "https://www.example.test", "https://www.example.test")]
    [InlineData(null, "https://www.example.test", "https://www.example.test")]
    [InlineData("https://app.example.test", "https://www.example.test", "https://app.example.test")]
    [InlineData("", "", "https://config.example.test")]
    public void AnEmptyAppBaseUrl_ShouldCountAsNotSet(string? appBaseUrl, string? appUrl, string expected)
    {
        var environment = new Dictionary<string, string?> { ["APP_BASE_URL"] = appBaseUrl, ["APP_URL"] = appUrl };
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["BatteryPass:AppUrl"] = "https://config.example.test" }).Build();

        Assert.Equal(expected, SiteAddressSettings.AppBaseUrl(name => environment.GetValueOrDefault(name), configuration));
    }

    [Fact]
    public void AnEmptyIdAddress_ShouldCountAsNotSet()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["BatteryPass:IdBaseUrl"] = "https://ids.example.test" }).Build();

        Assert.Equal("https://ids.example.test", SiteAddressSettings.IdBaseUrl(_ => "", configuration));
        Assert.Null(SiteAddressSettings.IdBaseUrl(_ => " ", new ConfigurationBuilder().Build()));
    }

    [Fact]
    public void TheAppsStart_ShouldReadBothAddressesThisWay()
    {
        var program = File.ReadAllText(DppTestFiles.RepoFile("web", "Program.cs"));

        Assert.Contains("var appBaseUrlSetting = SiteAddressSettings.AppBaseUrl(Environment.GetEnvironmentVariable, builder.Configuration);", program, StringComparison.Ordinal);
        Assert.Contains("var idBaseUrlSetting = SiteAddressSettings.IdBaseUrl(Environment.GetEnvironmentVariable, builder.Configuration);", program, StringComparison.Ordinal);
        Assert.DoesNotContain("Environment.GetEnvironmentVariable(\"APP_BASE_URL\")", program, StringComparison.Ordinal);
        Assert.DoesNotContain("Environment.GetEnvironmentVariable(\"DPP_ID_BASE_URL\")", program, StringComparison.Ordinal);
    }
}
