using BatteryPassWeb.Tests.Dpp;

namespace BatteryPassWeb.Tests.Look;

/// <summary>
/// Redesign R7, the owner's item 3 ("can we fit each battery row on the list on one line?") and choice "Copy icon only":
/// at 1280 px every row of the shared list is one line. Its two statuses sit side by side, told apart by their shape (a
/// square battery tag, a round passport pill) and named for screen readers; a long serial number (165 px), model, batch or
/// cluster (180 px) ends in "…" with the whole text on hover; the row icons are 32 px; the cells are a little narrower than
/// other console tables (site.css sets 14 px with !important, unchanged); the last icon's name opens to its left. A phone
/// keeps its cards.
/// </summary>
public sealed class OneLineListTests
{
    private static string Read(params string[] parts) => File.ReadAllText(DppTestFiles.RepoFile(parts)).Replace("\r\n", "\n", StringComparison.Ordinal);

    [Fact]
    public void TheList_ShouldClipLongTextWithTheWholeOfItOnHover()
    {
        var list = Read("web", "Views", "Shared", "_BatteryList.cshtml");

        Assert.Contains("bp-mobile-card-table bp-one-line-list\">", list, StringComparison.Ordinal);
        Assert.Contains("<a href=\"@Model.OpenHref(row)\" class=\"bp-battery-link bp-one-line-clip is-serial\" title=\"@row.SerialNumber · battery ID @row.Identifier\">@row.SerialNumber</a>", list, StringComparison.Ordinal);
        Assert.Contains("<td data-label=\"Model\"><span class=\"bp-one-line-clip\" title=\"@row.ModelName · @row.FamilyName family\">@row.ModelName</span></td>", list, StringComparison.Ordinal);
        Assert.Contains("<td data-label=\"Batch\"><span class=\"bp-one-line-clip\" title=\"@row.BatchLabel\">@row.BatchLabel</span></td>", list, StringComparison.Ordinal);
        Assert.Contains("<span class=\"bp-one-line-clip\" title=\"@row.ClusterLabel\">@row.ClusterLabel</span>", list, StringComparison.Ordinal);
    }

    [Fact]
    public void TheStyles_ShouldKeepEachRowOnOneLine_FromTabletWidthUp()
    {
        var css = Read("web", "wwwroot", "css", "bp-product-data.css");
        var oneLine = css[css.IndexOf("/* Every row on one line from tablet width up", StringComparison.Ordinal)..];

        Assert.StartsWith("/* Every row on one line from tablet width up", oneLine, StringComparison.Ordinal);
        Assert.Contains("@media (min-width: 768px) {", oneLine, StringComparison.Ordinal);
        Assert.Contains("    padding-inline: 7px !important;", oneLine, StringComparison.Ordinal);
        Assert.Contains("  .bp-console-table.bp-one-line-list td,\n  .bp-console-table.bp-one-line-list td:is([data-label=\"Batch\"], [data-label=\"Cluster\"]) {\n    white-space: nowrap;", oneLine, StringComparison.Ordinal);
        Assert.Contains("  .bp-one-line-list .bp-one-line-clip {\n    display: inline-block;\n    max-width: 180px;", oneLine, StringComparison.Ordinal);
        // Any serial number fits at 1280 px: 165 px (the demo's are about 140) with 32 px icons, still easy to tap.
        Assert.Contains("  .bp-one-line-list .bp-one-line-clip.is-serial {\n    max-width: 165px;", oneLine, StringComparison.Ordinal);
        Assert.Contains("  .bp-one-line-list .bp-report-action {\n    inline-size: 2rem;\n    block-size: 2rem;\n    min-inline-size: 2rem;\n    min-block-size: 2rem;", oneLine, StringComparison.Ordinal);
        Assert.Contains("  .bp-one-line-list .bp-badge-kind {\n    position: absolute;", oneLine, StringComparison.Ordinal); // named for screen readers
        Assert.Contains(".bp-one-line-list .bp-icon-action-row > .bp-report-action:last-child::after {\n  right: 0;\n  left: auto;", oneLine, StringComparison.Ordinal);
    }
}
