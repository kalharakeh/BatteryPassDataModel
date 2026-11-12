using System.Security.Cryptography;
using BatteryPassWeb.Dpp.Documents;
using BatteryPassWeb.Services;

namespace BatteryPassWeb.Tests.Dpp;

/// <summary>
/// C3 round two, point 2 (the manager's decision 61): in Production the app doesn't start without its signing key or
/// with an address that isn't a whole http(s) address, and the address mismatch says what differs.
/// </summary>
public sealed class ProductionStartTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Production_ShouldNeedTheSigningKey(string? setting)
    {
        var problem = DemoSigningKeyService.ProductionKeyProblem(production: true, setting);

        Assert.NotNull(problem);
        Assert.Contains(DemoSigningKeyService.PrivateKeySetting, problem, StringComparison.Ordinal);
        Assert.Null(DemoSigningKeyService.ProductionKeyProblem(production: false, setting)); // elsewhere as before
    }

    [Fact]
    public void Production_ShouldStart_WithAReadableKey_AndNeverPrintIt()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var setting = Convert.ToBase64String(key.ExportPkcs8PrivateKey());
        using var otherCurve = ECDsa.Create(ECCurve.NamedCurves.nistP384);
        var wrongCurve = Convert.ToBase64String(otherCurve.ExportPkcs8PrivateKey());

        Assert.Null(DemoSigningKeyService.ProductionKeyProblem(production: true, setting));
        foreach (var unreadable in new[] { "not a key", wrongCurve, setting[..40] })
        {
            var problem = DemoSigningKeyService.ProductionKeyProblem(production: true, unreadable);
            Assert.NotNull(problem);
            Assert.DoesNotContain(unreadable, problem, StringComparison.Ordinal);
        }
    }

    [Theory]
    [InlineData(null, "https://www.example.test", "APP_BASE_URL")]
    [InlineData("www.example.test", "https://www.example.test", "APP_BASE_URL")]
    [InlineData("ftp://www.example.test", "https://www.example.test", "APP_BASE_URL")]
    [InlineData("https://www.example.test", "", "DPP_ID_BASE_URL")]
    [InlineData("https://www.example.test", "/dpp", "DPP_ID_BASE_URL")]
    [InlineData("https://www.example.test", "mailto:ids@example.test", "DPP_ID_BASE_URL")]
    public void Production_ShouldNeedTwoWholeHttpAddresses(string? appBaseUrl, string? idBaseUrl, string named)
    {
        var problem = DppLinks.ProductionAddressProblem(production: true, appBaseUrl, idBaseUrl);

        Assert.NotNull(problem);
        Assert.StartsWith(named, problem, StringComparison.Ordinal);
        Assert.Contains("http or https", problem, StringComparison.Ordinal);
        Assert.Null(DppLinks.ProductionAddressProblem(production: false, appBaseUrl, idBaseUrl));
    }

    [Fact]
    public void Production_ShouldStart_WithTwoWholeAddresses() =>
        Assert.Null(DppLinks.ProductionAddressProblem(production: true, "https://www.example.test", "https://www.example.test/"));

    [Fact]
    public void TheMismatch_ShouldSayWhatDiffers()
    {
        var scheme = DppLinks.HostMismatch("http://www.example.test", "https://www.example.test")!;
        var host = DppLinks.HostMismatch("https://www.example.test", "https://example.test")!;
        var port = DppLinks.HostMismatch("https://www.example.test:8443", "https://www.example.test")!;

        Assert.Contains("http://www.example.test", scheme, StringComparison.Ordinal);
        Assert.Contains("https://www.example.test", scheme, StringComparison.Ordinal);
        Assert.Contains("the scheme differs (http against https)", scheme, StringComparison.Ordinal);
        Assert.DoesNotContain("the host differs", scheme, StringComparison.Ordinal);
        Assert.DoesNotContain("the port differs", scheme, StringComparison.Ordinal);
        Assert.Contains("the host differs (www.example.test against example.test)", host, StringComparison.Ordinal);
        Assert.Contains("the port differs (8443 against 443)", port, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("https://www.example.test.", "https://www.example.test")]
    [InlineData("https://www.example.test", "https://WWW.example.test./")]
    public void AHostWithATrailingDot_ShouldBeTheSameHost(string appBaseUrl, string idBaseUrl)
    {
        Assert.Null(DppLinks.HostMismatch(appBaseUrl, idBaseUrl));
        using var keys = new DemoSigningKeyService(ECDsa.Create(ECCurve.NamedCurves.nistP256), idBaseUrl);
        Assert.Equal("did:web:www.example.test", keys.Issuer);
    }

    [Fact]
    public void TheAppsStart_ShouldCheckKeyAndAddressesInProduction_BeforeTheMismatch()
    {
        var program = File.ReadAllText(DppTestFiles.RepoFile("web", "Program.cs")).Replace("\r\n", "\n");

        var key = program.IndexOf("DemoSigningKeyService.ProductionKeyProblem(builder.Environment.IsProduction(), Environment.GetEnvironmentVariable(DemoSigningKeyService.PrivateKeySetting)) is { } missingSigningKey", StringComparison.Ordinal);
        var addresses = program.IndexOf("DppLinks.ProductionAddressProblem(builder.Environment.IsProduction(), appBaseUrlSetting, idBaseUrlSetting) is { } addressProblem", StringComparison.Ordinal);
        var mismatch = program.IndexOf("DppLinks.HostMismatch(appBaseUrlSetting, idBaseUrlSetting)", StringComparison.Ordinal);

        Assert.True(key > 0, "the key check is missing");
        Assert.True(addresses > 0 && addresses < mismatch, "the address check must come before the mismatch");
        Assert.Contains("throw new InvalidOperationException(missingSigningKey);", program, StringComparison.Ordinal);
        Assert.Contains("throw new InvalidOperationException(addressProblem);", program, StringComparison.Ordinal);
    }
}
