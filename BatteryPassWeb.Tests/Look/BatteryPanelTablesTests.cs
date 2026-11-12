using BatteryPassWeb.Tests.Dpp;

namespace BatteryPassWeb.Tests.Look;

/// <summary>
/// Redesign R6 (the review of the built R5): on the battery pages the Modules and Versions tables were 174–210 px wider
/// than the panel beside the side menu at 1280 px (site.css gives console tables a 1080 px minimum), so their right-hand
/// columns hid behind a sideways scroll. The panel's tables drop that minimum, Versions shortens the passport ID in the
/// middle (full on hover and in its copy button) and shows the status as the Passport badge, like the other lists.
/// </summary>
public sealed class BatteryPanelTablesTests
{
    private static string Read(params string[] parts) => File.ReadAllText(DppTestFiles.RepoFile(parts)).Replace("\r\n", "\n", StringComparison.Ordinal);

    [Fact]
    public void ThePanelsTables_ShouldNotKeepTheConsoleMinimumWidth() =>
        Assert.Contains(".bp-bpanel .bp-console-table {\n  min-width: 0;\n}\n\n.bp-bpanel .bp-console-table td[data-label=\"Published\"] {\n  white-space: normal;\n}", Read("web", "wwwroot", "css", "bp-product-data.css"), StringComparison.Ordinal);

    [Fact]
    public void Versions_ShouldShowThePassportIdWholeAndTheBadge()
    {
        var versions = Read("web", "Views", "Batteries", "_VersionsCard.cshtml");

        Assert.Contains("title=\"@row.Version.DigitalProductPassportId\">@row.Version.DigitalProductPassportId</a>@await Html.PartialAsync(\"_CopyIdButton\", row.Version.DigitalProductPassportId)", versions, StringComparison.Ordinal);
        Assert.Contains("@await Html.PartialAsync(\"_StatusBadge\", StatusBadge.Passport(row.Version.DppStatus))", versions, StringComparison.Ordinal);
    }
}
