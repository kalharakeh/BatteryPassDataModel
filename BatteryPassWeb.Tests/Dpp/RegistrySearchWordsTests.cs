namespace BatteryPassWeb.Tests.Dpp;

/// <summary>
/// Redesign R6 (the review of the built R5): the Registry's search said only "N batteries." with no way back to the whole
/// list; it now says what it matched and offers "Show all", like the team's list. An older passport version linked its
/// current version twice (an icon by the badges and a line under them); the line stays.
/// </summary>
public sealed class RegistrySearchWordsTests
{
    private static string View(params string[] parts) => File.ReadAllText(DppTestFiles.RepoFile(["web", "Views", .. parts])).Replace("\r\n", "\n", StringComparison.Ordinal);

    [Fact]
    public void TheRegistrysSearch_ShouldSayWhatItMatched_AndOfferShowAll()
    {
        var registry = View("Shared", "_BatteryList.cshtml"); // the Registry's list is the shared list since R7

        Assert.Contains("$\"{Words.Count(Model.Rows.Count, \"battery\", \"batteries\")} match “{Model.Query}”.\"", registry, StringComparison.Ordinal);
        Assert.Contains("$\"No battery matches “{Model.Query}”.\"", registry, StringComparison.Ordinal);
        Assert.Equal(2, registry.Split("<a href=\"@Model.Action\">Show all</a>").Length - 1);
        Assert.Contains("new BatteryListView(\"/registry\", Model.Query", View("Registry", "Index.cshtml"), StringComparison.Ordinal);
    }

    [Fact]
    public void AnOlderVersion_ShouldLinkItsCurrentVersionOnce()
    {
        var hero = View("Passport", "_PassportHero.cshtml");

        Assert.Single(hero.Split(">Open the current version</a>").Skip(1));
        Assert.DoesNotContain("aria-label=\"Open the current version\"", hero, StringComparison.Ordinal);
    }
}
