using BatteryPassWeb.Tests.Dpp;

namespace BatteryPassWeb.Tests.Look;

/// <summary>
/// Redesign R6, the owner's choice 3 (3 Nov 2026): the team's list lost the old list's copy button for the full battery
/// ID and its "updated" date; both come back, the date as on the Registry ("Version 3 · updated 2 Nov 2026, 19:50").
/// </summary>
public sealed class TeamListRowTests
{
    [Fact]
    public void EachRow_ShouldCopyTheFullBatteryId_AndSayWhenItWasUpdated()
    {
        // R7 (the owner's choice "Copy icon only"): the shared list keeps the copy button and says the version and update
        // in its own short column, "v2 · 3 Nov", in full on hover.
        var list = File.ReadAllText(DppTestFiles.RepoFile("web", "Views", "Shared", "_BatteryList.cshtml"));

        Assert.Contains("@row.SerialNumber</a>@await Html.PartialAsync(\"_CopyIdButton\", row.Identifier)</span>", list, StringComparison.Ordinal);
        Assert.Contains("<td data-label=\"Updated\" title=\"@BatteryListView.UpdatedInFull(row)\">@(BatteryListView.Updated(row) ?? \"—\")</td>", list, StringComparison.Ordinal);
        Assert.DoesNotContain("· version {version.Number}", list, StringComparison.Ordinal);
    }
}
