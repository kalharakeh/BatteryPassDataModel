namespace BatteryPassWeb.Tests;

public sealed class SiteShellLayoutTests
{
    [Fact]
    public void Layout_ShouldUseScaniaInspiredHeaderAndFooterShell()
    {
        var markup = File.ReadAllText(RepoFile("web", "Views", "Shared", "_Layout.cshtml"));

        Assert.Contains("bp-scania-header", markup);
        Assert.Contains("bp-header-top-row", markup);
        Assert.Contains("bp-header-wordmark", markup);
        Assert.Contains("bp-header-wordmark-image", markup);
        Assert.Contains("bp-header-symbol-link", markup);
        Assert.Contains("bp-header-symbol-logo", markup);
        Assert.Contains("bp-header-nav-row", markup);
        Assert.Contains("bp-header-right-group", markup);
        Assert.Contains("bp-header-primary-links", markup);
        Assert.Contains("bp-header-account-links", markup);
        Assert.Contains("bp-header-actions", markup);
        Assert.Contains("bp-footer-extended", markup);
        Assert.Contains("bp-footer-legal", markup);
        Assert.Contains("bp-footer-wordmark", markup);
        Assert.Contains("bp-footer-wordmark-image", markup);
        Assert.Contains("~/images/scania_wordmark_blue_rgb.svg", markup);
        Assert.Contains("~/images/scania_symbol_L_rgb.svg", markup);
        Assert.Contains("Copyright Scania Industrial Batteries 2026 All rights reserved. Scania Industrial Batteries AB, SE-151 87 Stockholm, Sweden. Tel: +46-8-55 38 10 00", markup);
        Assert.DoesNotContain("Scania in region:", markup);
        Assert.DoesNotContain("API help", markup);
        Assert.DoesNotContain(">SCANIA<", markup);
    }

    [Fact]
    public void LayoutHeaderActions_ShouldPinAuthenticatedActionsToRightInRequestedOrder()
    {
        var markup = File.ReadAllText(RepoFile("web", "Views", "Shared", "_Layout.cshtml"));
        var navRow = Slice(markup, "<div class=\"bp-header-nav-row\"", "</nav>");

        Assert.Contains("bp-header-right-group", navRow);
        Assert.Contains("bp-header-primary-links", navRow);
        Assert.Contains("bp-header-account-links", navRow);
        Assert.True(
            navRow.IndexOf("href=\"/registry\"", StringComparison.Ordinal) <
            navRow.IndexOf("href=\"/admin\"", StringComparison.Ordinal),
            "Open Registry should render before Admin in the right-pinned authenticated action group.");
        Assert.True(
            navRow.IndexOf("href=\"/admin\"", StringComparison.Ordinal) <
            navRow.IndexOf("href=\"/help\"", StringComparison.Ordinal),
            "Admin should render before Help in the right-pinned authenticated action group.");
        Assert.True(
            navRow.IndexOf("bp-header-primary-links", StringComparison.Ordinal) <
            navRow.IndexOf("bp-identity", StringComparison.Ordinal),
            "Authenticated identity should render after Open Registry/Admin/Help.");
        Assert.True(
            navRow.IndexOf("bp-identity", StringComparison.Ordinal) <
            navRow.IndexOf("bp-header-account-links", StringComparison.Ordinal),
            "Logout should remain after the logged-in user and at the far right of the group.");
        Assert.Contains("Logout", navRow);
        Assert.Contains("Login", navRow);
    }

    [Fact]
    public void LayoutExtendedFooter_ShouldOnlyShowAppColumns()
    {
        var markup = File.ReadAllText(RepoFile("web", "Views", "Shared", "_Layout.cshtml"));
        var extendedFooter = Slice(markup, "<div class=\"bp-footer-extended\"", "<div class=\"bp-footer-legal\">");

        Assert.Contains("BATTERY PASSPORT", extendedFooter);
        Assert.Contains("TOOLS", extendedFooter);
        Assert.DoesNotContain("bp-footer-about", extendedFooter);
        Assert.DoesNotContain("SCANIA INDUSTRIAL BATTERIES", extendedFooter);
        Assert.DoesNotContain("https://www.scania.com/industrial-batteries/en/home.html", extendedFooter);
        Assert.DoesNotContain("https://www.scania.com/se/sv/home.html", extendedFooter);
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
        Assert.Contains(".bp-scania-header", css);
        Assert.Contains(".bp-footer-wordmark", css);
        Assert.Contains(".bp-footer-wordmark-image", css);
        Assert.Contains(".bp-header-wordmark-image", css);
        Assert.Contains("filter: brightness(0) invert(1);", css);
        Assert.Contains(".bp-footer-extended", css);
        Assert.Contains(".bp-header-nav-row", css);
        Assert.Contains(".bp-header-right-group", css);
        Assert.Contains(".bp-header-account-links", css);
        Assert.Contains("justify-content: flex-end;", css);
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
