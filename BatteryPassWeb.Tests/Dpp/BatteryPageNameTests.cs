using BatteryPassWeb.Dpp.Editing;
using BatteryPassWeb.Dpp.Pages;

namespace BatteryPassWeb.Tests.Dpp;

/// <summary>
/// Redesign R6 (the review of the built R5): the battery page had three names ("Battery page" on the list icons, "Manage
/// this battery" on the detail page, "manage it from its row" after a hand-over); it is "the battery page" everywhere.
/// The team, which had no way back from the detail page, gets the same link to its own battery page.
/// </summary>
[Collection(MongoCollection.Name)]
public sealed class BatteryPageNameTests(MongoFixture mongo)
{
    [Fact]
    public async Task TheDetailPage_ShouldLeadTheTeamToItsBatteryPage_AndTheClusterAdminToTheirs()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo).WithClustersAsync();
        var pages = ClusterBatteryPagesTests.Pages(rig);

        Assert.Equal(BatteryPages.BatteryHref(rig.PackId), await pages.ManageHrefAsync(ChangeSender.Team("team@example.com"), rig.PackId));
        Assert.Equal(ClusterBatteryPages.BatteryHref(rig.PackId), await pages.ManageHrefAsync(ChangeSender.ClusterAdmin("north-admin@example.com", BatteryTesting.North), rig.PackId));
        Assert.Null(await pages.ManageHrefAsync(ChangeSender.Team("team@example.com"), "no-such-pack"));
        Assert.Null(await pages.ManageHrefAsync(null, rig.PackId));
    }

    [Fact]
    public void TheBatteryPage_ShouldHaveOneName()
    {
        var hero = File.ReadAllText(DppTestFiles.RepoFile("web", "Views", "Passport", "_PassportHero.cshtml"));
        var registry = File.ReadAllText(DppTestFiles.RepoFile("web", "Controllers", "RegistryController.cs"));

        Assert.Contains(">Open the battery page<svg", hero, StringComparison.Ordinal);
        Assert.DoesNotContain("Manage this battery", hero, StringComparison.Ordinal);
        Assert.Contains("open its battery page from its row below.", registry, StringComparison.Ordinal);
        Assert.Contains("data-tooltip=\"Battery page\"", File.ReadAllText(DppTestFiles.RepoFile("web", "Views", "Shared", "_RowActions.cshtml")), StringComparison.Ordinal);
    }
}
