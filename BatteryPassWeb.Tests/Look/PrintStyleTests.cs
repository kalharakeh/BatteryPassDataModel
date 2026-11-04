using BatteryPassWeb.Tests.Dpp;

namespace BatteryPassWeb.Tests.Look;

/// <summary>
/// Step 6a-2, the passport pages on paper (the owner's answers P1–P5 and the A4 previews approved on 4 Nov 2026): the public
/// summary and the detail page load bp-print.css for print only; on paper every tab prints open, the live values are a
/// table of each value's latest reading, and the QR code, the page's address and when it was printed head the first page.
/// The print-only parts are hidden on screen, and site.css keeps no print rules.
/// </summary>
public sealed class PrintStyleTests
{
    private const string PrintLink = "<link rel=\"stylesheet\" href=\"~/css/bp-print.css\" media=\"print\" asp-append-version=\"true\" />";

    private static string Read(params string[] path) => File.ReadAllText(DppTestFiles.RepoFile(["web", .. path])).Replace("\r\n", "\n");

    private static string Print() => Read("wwwroot", "css", "bp-print.css");

    [Fact]
    public void OnlyTheTwoPassportPages_ShouldLoadThePrintStyle_ForPrintOnly()
    {
        Assert.Contains(PrintLink, Read("Views", "Passport", "Summary.cshtml"), StringComparison.Ordinal);
        Assert.Contains(PrintLink, Read("Views", "Passport", "Detail.cshtml"), StringComparison.Ordinal);

        var others = Directory.GetFiles(Path.GetDirectoryName(Path.GetDirectoryName(DppTestFiles.RepoFile("web", "Views", "Shared", "_Layout.cshtml")))!, "*.cshtml", SearchOption.AllDirectories)
            .Where(path => File.ReadAllText(path).Contains("bp-print.css", StringComparison.Ordinal))
            .Select(Path.GetFileName)
            .OrderBy(name => name, StringComparer.Ordinal);
        Assert.Equal(["Detail.cshtml", "Summary.cshtml"], others);
    }

    [Fact]
    public void ThePrintOnlyParts_ShouldBeHiddenOnScreen()
    {
        var hero = Read("Views", "Passport", "_PassportHero.cshtml");
        var live = Read("Views", "Passport", "_LiveHistory.cshtml");

        Assert.Contains("<p class=\"bp-print-address d-none d-print-block\">", hero, StringComparison.Ordinal);
        Assert.Contains("@if (Links.IsConfigured)", hero, StringComparison.Ordinal);
        Assert.Contains("<table class=\"bp-print-live d-none d-print-table\">", live, StringComparison.Ordinal);
        Assert.Contains("<th>Value</th><th>Latest reading</th><th>When and from where</th>", live, StringComparison.Ordinal);
        Assert.Equal(2, live.Split("<p class=\"bp-help-text d-print-none\">").Length - 1);
    }

    [Fact]
    public void OnPaper_TheScreenOnlyPartsGo_AndEveryTabPrints()
    {
        var print = Print();

        Assert.Contains("@page {\n  size: A4;", print, StringComparison.Ordinal);
        foreach (var hidden in new[] { "header.bp-header,", "footer.bp-footer,", ".bp-hero-ctas,", "#passport-tabs,", "[data-mark-invalid],", ".bp-copy-id-button,", ".bp-status-help-button,", ".bp-live-charts,", ".bp-live-all," })
        {
            Assert.Contains(hidden, print, StringComparison.Ordinal);
        }

        Assert.Contains(".tab-content > .tab-pane {\n  display: block !important;\n  opacity: 1 !important;\n}", print, StringComparison.Ordinal);
    }

    [Fact]
    public void OnPaper_TheWideCards_ShouldStayInsideThePage()
    {
        // On screen site.css stretches them to 100vw with translateX(-50%); on paper 100vw is the sheet, which cut their left side.
        Assert.Contains(".bp-report-hero-card,\n.bp-report-wide-section {\n  width: auto !important;\n  margin-inline: 0 !important;\n  transform: none !important;\n}", Print(), StringComparison.Ordinal);
    }

    [Fact]
    public void OnPaper_ADocumentLink_ShouldShowItsAddress() =>
        Assert.Contains(".bp-field-grid dd a[target=\"_blank\"]::after {\n  content: \" (\" attr(href) \")\";", Print(), StringComparison.Ordinal);

    [Fact]
    public void SiteCss_ShouldKeepNoPrintRules()
    {
        Assert.DoesNotContain("@media print", Read("wwwroot", "css", "site.css"), StringComparison.Ordinal);
        Assert.DoesNotContain("bp-print.css", Read("Views", "Shared", "_Layout.cshtml"), StringComparison.Ordinal);
    }
}
