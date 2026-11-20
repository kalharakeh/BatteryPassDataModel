using BatteryPassWeb.Dpp.Documents;

namespace BatteryPassWeb.Tests.Dpp;

/// <summary>
/// Finding D18: the QR code opens APP_BASE_URL, the battery ID names DPP_ID_BASE_URL. In Production the two must be the
/// same site, or the app refuses to start; elsewhere it logs one warning.
/// </summary>
public sealed class AddressHostTests
{
    [Theory]
    [InlineData("https://passport.example.test", "https://other.example.test")]
    [InlineData("https://www.example.test", "https://example.test")]
    [InlineData("https://passport.example.test:8443", "https://passport.example.test")]
    [InlineData("http://localhost:5423", "http://localhost:5186")]
    public void HostMismatch_ShouldExplain_WhenTheTwoAddressesAreOnDifferentHosts(string appBaseUrl, string idBaseUrl)
    {
        var message = DppLinks.HostMismatch(appBaseUrl, idBaseUrl);

        Assert.NotNull(message);
        Assert.Contains("APP_BASE_URL", message, StringComparison.Ordinal);
        Assert.Contains("DPP_ID_BASE_URL", message, StringComparison.Ordinal);
        Assert.Contains(new Uri(appBaseUrl).Authority, message, StringComparison.Ordinal);
        Assert.Contains(new Uri(idBaseUrl).Authority, message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("https://passport.example.test", "https://passport.example.test")]
    [InlineData("https://passport.example.test/", "https://PASSPORT.example.test")]
    [InlineData("https://passport.example.test:443", "https://passport.example.test/ids")]
    [InlineData("http://localhost:5423", "http://localhost:5423/")]
    [InlineData("", "https://passport.example.test")]
    [InlineData("https://passport.example.test", "")]
    [InlineData(null, null)]
    public void HostMismatch_ShouldBeNull_WhenTheHostsMatchOrASettingIsMissing(string? appBaseUrl, string? idBaseUrl) =>
        Assert.Null(DppLinks.HostMismatch(appBaseUrl, idBaseUrl));

    [Fact]
    public void TheAppsStart_ShouldRefuseDifferentHostsInProduction_AndWarnElsewhere()
    {
        var program = File.ReadAllText(DppTestFiles.RepoFile("web", "Program.cs")).Replace("\r\n", "\n");

        Assert.Contains(
            "if (DppLinks.HostMismatch(appBaseUrlSetting, idBaseUrlSetting) is { } hostMismatch && builder.Environment.IsProduction())\n{\n    throw new InvalidOperationException(hostMismatch);\n}",
            program,
            StringComparison.Ordinal);
        Assert.Contains("DppLinks.HostMismatch(fallbackOptions.AppBaseUrl, fallbackOptions.IdBaseUrl) is { } addressWarning", program, StringComparison.Ordinal);
        Assert.Contains("app.Logger.LogWarning(addressWarning);", program, StringComparison.Ordinal);
    }
}
