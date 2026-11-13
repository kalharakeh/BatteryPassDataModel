namespace BatteryPassWeb.Tests;

public sealed class SiteShellLayoutTests
{
    [Fact]
    public void Layout_ShouldUseScaniaInspiredHeaderAndFooterShell()
    {
        var markup = File.ReadAllText(RepoFile("web", "Views", "Shared", "_Layout.cshtml"));

        // One bar since finding L04: the wordmark and the app's name; the Scania symbol's second row is gone.
        Assert.Contains("class=\"bp-topbar", markup);
        Assert.Contains("bp-topbar-brand", markup);
        Assert.Contains("bp-topbar-wordmark", markup);
        Assert.Contains("bp-topbar-product", markup);
        Assert.Contains("bp-topbar-nav", markup);
        Assert.Contains("bp-footer-legal", markup);
        Assert.Contains("bp-footer-wordmark", markup);
        Assert.Contains("bp-footer-wordmark-image", markup);
        Assert.Contains("~/images/scania_wordmark_blue_rgb.svg", markup);
        Assert.DoesNotContain("bp-header-nav-row", markup);
        Assert.DoesNotContain("bp-identity", markup);
        Assert.Contains("Copyright Scania Industrial Batteries 2026 All rights reserved. Scania Industrial Batteries AB, SE-151 87 Stockholm, Sweden. Tel: +46-8-55 38 10 00", markup);
        Assert.DoesNotContain("Scania in region:", markup);
        Assert.DoesNotContain("API help", markup);
        Assert.DoesNotContain(">SCANIA<", markup);
    }

    [Fact]
    public void LayoutHeader_ShouldPutTheLinksBeforeTheAccount()
    {
        var markup = File.ReadAllText(RepoFile("web", "Views", "Shared", "_Layout.cshtml"));
        var header = Slice(markup, "<header class=\"bp-topbar", "</header>");

        Assert.True(
            header.IndexOf("bp-topbar-nav", StringComparison.Ordinal) < header.IndexOf("data-account-menu", StringComparison.Ordinal),
            "The main links come before the account menu.");
        Assert.True(
            header.IndexOf(">Your account</a>", StringComparison.Ordinal) < header.IndexOf(">Log out</button>", StringComparison.Ordinal),
            "Log out is the account menu's last entry.");
        Assert.Contains(">Log in</a>", header);
    }

    [Fact]
    public void TheFooter_ShouldNotRepeatTheMenu()
    {
        var markup = File.ReadAllText(RepoFile("web", "Views", "Shared", "_Layout.cshtml"));
        var footer = Slice(markup, "<footer class=\"bp-footer\">", "</footer>");

        // Finding L05: the footer's second menu repeated the bar's entries under other names; the Help links the API reference.
        Assert.DoesNotContain("bp-footer-extended", footer);
        Assert.DoesNotContain("Search passport", footer);
        Assert.DoesNotContain("Open registry", footer);
        Assert.DoesNotContain("Admin workspace", footer);
        Assert.DoesNotContain("Developer help", footer);
        Assert.DoesNotContain("Account access", footer);
        Assert.Contains("bp-footer-legal", footer);
        Assert.Contains("href=\"/help/api\"", File.ReadAllText(RepoFile("web", "Views", "Help", "Guide.cshtml")));
    }

    [Fact]
    public void LayoutFooterLegalLinks_ShouldPointToScaniaSites()
    {
        var markup = File.ReadAllText(RepoFile("web", "Views", "Shared", "_Layout.cshtml"));
        var linkRow = Slice(markup, "<div class=\"bp-footer-link-row\"", "</div>");

        Assert.Contains("https://www.scania.com/industrial-batteries/en/home.html", linkRow);
        Assert.Contains(">Scania Industrial Batteries<", linkRow);
        Assert.Contains("https://www.scania.com/se/sv/home.html", linkRow);
        Assert.Contains(">Scania<", linkRow);
        Assert.DoesNotContain(">Home<", linkRow);
        Assert.DoesNotContain(">Registry<", linkRow);
        Assert.DoesNotContain(">Help<", linkRow);
    }

    [Fact]
    public void SiteCss_ShouldExposeScaniaPaletteAndShellStyles()
    {
        var css = File.ReadAllText(RepoFile("web", "wwwroot", "css", "site.css"));

        Assert.Contains("--bp-scania-blue: #041e42;", css);
        Assert.Contains("--bp-scania-blue-border: #0f3263;", css);
        Assert.Contains("--bp-primary: #2a6ecf;", css);
        Assert.Contains(".bp-topbar {", css);
        Assert.Contains(".bp-footer-wordmark", css);
        Assert.Contains(".bp-footer-wordmark-image", css);
        Assert.Contains(".bp-topbar-wordmark", css);
        Assert.Contains("filter: brightness(0) invert(1);", css);
        Assert.Contains(".bp-account-panel", css);
        Assert.Contains("grid-template-columns: repeat(2, minmax(0, 1fr));", css);
    }

    [Fact]
    public void StaticAssets_ShouldIncludeOfficialScaniaSvgFiles()
    {
        Assert.True(File.Exists(RepoFile("web", "wwwroot", "images", "scania_wordmark_blue_rgb.svg")));
        Assert.True(File.Exists(RepoFile("web", "wwwroot", "images", "scania_symbol_L_rgb.svg")));
    }

    private static string Slice(string source, string start, string end)
    {
        var startIndex = source.IndexOf(start, StringComparison.Ordinal);
        Assert.True(startIndex >= 0, $"Start marker should exist: {start}");
        var endIndex = source.IndexOf(end, startIndex, StringComparison.Ordinal);
        Assert.True(endIndex > startIndex, $"End marker should exist after start marker: {end}");
        return source[startIndex..(endIndex + end.Length)];
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
