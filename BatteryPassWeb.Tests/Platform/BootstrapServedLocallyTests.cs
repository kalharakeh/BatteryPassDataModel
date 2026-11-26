using System.Security.Cryptography;
using System.Text.RegularExpressions;
using BatteryPassWeb.Tests.Dpp;

namespace BatteryPassWeb.Tests.Platform;

/// <summary>
/// Finding L07: Bootstrap 5.3.3 comes from the app itself, byte for byte the files of its official release, and no page
/// loads a script or a style sheet from another host.
/// </summary>
public sealed class BootstrapServedLocallyTests
{
    // SHA-256 of the files in bootstrap-5.3.3-dist.zip (github.com/twbs/bootstrap/releases/tag/v5.3.3), which are also what
    // cdn.jsdelivr.net served for bootstrap@5.3.3 before this change.
    private const string CssHash = "3c8f27e6009ccfd710a905e6dcf12d0ee3c6f2ac7da05b0572d3e0d12e736fc8";
    private const string JsHash = "0833b2e9c3a26c258476c46266e6877fc75218625162e0460be9a3a098a61c6c";

    private static string Library(params string[] parts) => DppTestFiles.RepoFile(new[] { "web", "wwwroot", "lib", "bootstrap" }.Concat(parts).ToArray());

    [Theory]
    [InlineData("css", "bootstrap.min.css", CssHash)]
    [InlineData("js", "bootstrap.bundle.min.js", JsHash)]
    public void BootstrapFiles_ShouldBeTheOfficialRelease_WithLfLineEndings(string folder, string name, string hash)
    {
        var bytes = File.ReadAllBytes(Library(folder, name));

        Assert.Equal(hash, Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant());
        Assert.DoesNotContain((byte)'\r', bytes);
    }

    [Fact]
    public void BootstrapFolder_ShouldCarryTheLicence()
    {
        var licence = File.ReadAllText(Library("LICENSE"));

        Assert.Contains("The MIT License (MIT)", licence, StringComparison.Ordinal);
        Assert.Contains("The Bootstrap Authors", licence, StringComparison.Ordinal);
    }

    [Fact]
    public void Layout_ShouldLoadBootstrapFromTheApp()
    {
        var layout = File.ReadAllText(DppTestFiles.RepoFile("web", "Views", "Shared", "_Layout.cshtml"));

        Assert.Contains("href=\"~/lib/bootstrap/css/bootstrap.min.css\"", layout, StringComparison.Ordinal);
        Assert.Contains("src=\"~/lib/bootstrap/js/bootstrap.bundle.min.js\"", layout, StringComparison.Ordinal);
        Assert.DoesNotContain("jsdelivr", layout, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Views_ShouldLoadNoScriptOrStyleSheetFromAnotherHost()
    {
        var views = Directory.GetFiles(Path.GetDirectoryName(DppTestFiles.RepoFile("web", "Views", "_ViewStart.cshtml"))!, "*.cshtml", SearchOption.AllDirectories);
        var other = new Regex(@"<(script[^>]*\ssrc|link[^>]*\shref)\s*=\s*[""']\s*(https?:)?//", RegexOptions.IgnoreCase);

        Assert.NotEmpty(views);
        foreach (var view in views)
        {
            Assert.False(other.IsMatch(File.ReadAllText(view)), $"{Path.GetFileName(view)} loads something from another host");
        }
    }
}
