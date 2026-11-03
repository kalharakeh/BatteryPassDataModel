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
        var list = File.ReadAllText(DppTestFiles.RepoFile("web", "Views", "Batteries", "Batteries.cshtml"));
        var registry = File.ReadAllText(DppTestFiles.RepoFile("web", "Views", "Registry", "Index.cshtml"));

        Assert.Contains("@BatteryPages.ShortId(row.Identifier)</small>@await Html.PartialAsync(\"_CopyIdButton\", row.Identifier)</span>", list, StringComparison.Ordinal);
        Assert.Contains("<small class=\"d-block text-secondary mt-1\">Version @shown.Number · updated @ValueWords.Time(shown.LastUpdatedUtc)</small>", list, StringComparison.Ordinal);
        Assert.Contains("Version @shown.Number · updated @ValueWords.Time(shown.LastUpdatedUtc)</small>", registry, StringComparison.Ordinal); // the same words
        Assert.DoesNotContain("· version {version.Number}", list, StringComparison.Ordinal); // said once, under the badges
    }
}
