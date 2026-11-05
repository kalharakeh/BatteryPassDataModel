using BatteryPassWeb.Tests.Dpp;

namespace BatteryPassWeb.Tests.Look;

/// <summary>
/// Step 6a, tap targets (WCAG 2.5.8, at least 24 px): on the team's battery page at 375 px the header's "Change" button
/// (19 px) and the Batch and Economic operator links (21 px) sat too close to each other and to the toolbar to pass the
/// rule's spacing exception (R6b phone pass, step 5's review). The text buttons and the header facts' links get a box at
/// least 24 px tall, in bp-product-data.css (site.css unchanged).
/// </summary>
public sealed class TapTargetTests
{
    private static string Css() =>
        File.ReadAllText(DppTestFiles.RepoFile("web", "wwwroot", "css", "bp-product-data.css")).Replace("\r\n", "\n");

    [Fact]
    public void TheTextButtons_ShouldBeAtLeast24PxTall() =>
        Assert.Contains(".bp-text-button {\n  display: inline-flex;\n  align-items: center;\n  min-height: 24px;", Css(), StringComparison.Ordinal);

    [Fact]
    public void TheHeaderFactsLinks_ShouldBeAtLeast24PxTall() =>
        Assert.Contains(".bp-bh-facts dd a {\n  display: inline-flex;\n  align-items: center;\n  min-height: 24px;\n}", Css(), StringComparison.Ordinal);

    [Fact]
    public void TheHeadersChangeButton_ShouldNotPushTheClusterValueDown() =>
        Assert.Contains(".bp-bh-facts dt .bp-text-button {\n  position: relative;\n  z-index: 1;\n  margin-block: -4px;", Css(), StringComparison.Ordinal); // drawn above the value it overhangs (step 6c)
}
