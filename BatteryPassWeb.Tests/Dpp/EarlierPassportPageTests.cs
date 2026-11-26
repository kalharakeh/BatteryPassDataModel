using BatteryPassWeb.Dpp.Pages;
using BatteryPassWeb.Dpp.Store;

namespace BatteryPassWeb.Tests.Dpp;

/// <summary>
/// Redesign R7, the owner's item 2 ("why do the old batteries passports cards in the versions have each battery page link
/// on them?", kept but made self-explanatory): the passports list says what an earlier passport's link leads to, and that
/// page says in one line that it is an earlier passport, read only, with the way to the newest. Nothing on it invites an
/// edit any more: its cluster and modules took changes (the page offered both and the app saved them); now neither is
/// offered, and the app refuses both.
/// </summary>
[Collection(MongoCollection.Name)]
public sealed class EarlierPassportPageTests(MongoFixture mongo)
{
    private static string Read(params string[] parts) => File.ReadAllText(DppTestFiles.RepoFile(parts)).Replace("\r\n", "\n", StringComparison.Ordinal);

    [Fact]
    public async Task AnEarlierPassport_ShouldRefuseACluster_AndAModuleList_WhileTheNewestTakesThem()
    {
        var rig = await new SuccessionTests(mongo).InactiveAsync();
        var pages = BatteryTesting.Pages(rig);
        var newest = (await rig.Store.GetAsync<BatteryPack>(rig.PackId))!.Succession!.SuccessorPackId!;
        var before = (await rig.Store.GetAsync<BatteryPack>(rig.PackId))!;

        Assert.Equal(BatteryPages.EarlierPassport, await pages.SetClusterAsync("team@example.com", rig.PackId, BatteryTesting.South));
        Assert.Equal(BatteryPages.EarlierPassport, await pages.SetModulesAsync("team@example.com", rig.PackId, "1; MOD-1; CAN-1"));
        var after = (await rig.Store.GetAsync<BatteryPack>(rig.PackId))!;
        Assert.Equal((before.ClusterId, before.Modules.Count), (after.ClusterId, after.Modules.Count));
        Assert.Null(await pages.SetModulesAsync("team@example.com", newest, "1; MOD-1; CAN-1"));
    }

    [Fact]
    public async Task AnEarlierPassportsPage_ShouldKnowItIsReplaced()
    {
        var rig = await new SuccessionTests(mongo).InactiveAsync();
        var newest = (await rig.Store.GetAsync<BatteryPack>(rig.PackId))!.Succession!.SuccessorPackId!;

        var earlier = (await BatteryTesting.Pages(rig).BatteryAsync(rig.PackId))!;
        var current = (await BatteryTesting.Pages(rig).BatteryAsync(newest))!;

        Assert.True(earlier.Publication.Replaced);
        Assert.True(earlier.Cards.Replaced);
        Assert.False(current.Publication.Replaced);
        Assert.Equal([false, true], earlier.Chain.Select(link => link.IsNewest));
    }

    [Fact]
    public void TheLinksAndThePage_ShouldSayWhatTheyAre_AndOfferNoEdit()
    {
        var chain = Read("web", "Views", "Shared", "_PassportChain.cshtml");
        var page = Read("web", "Views", "Batteries", "Battery.cshtml");
        var modules = Read("web", "Views", "Batteries", "_ModulesCard.cshtml");

        Assert.Contains("@(link.IsNewest ? \"Newest passport's page\" : $\"Passport {link.Number}'s page (read only)\")", chain, StringComparison.Ordinal);
        Assert.Contains("An earlier passport's page shows it as it was when it was replaced, to read only.", chain, StringComparison.Ordinal);
        Assert.Contains("Passport @thisPassport.Number of @Model.Chain.Count, an earlier passport, read only: nothing on it changes. <a href=\"@BatteryPages.BatteryHref(newestPassport.PackId)\">Open the newest passport's page</a>", page, StringComparison.Ordinal);
        Assert.Contains("<div><dt>Cluster @if (!publication.Replaced) {<button type=\"button\" class=\"bp-text-button\" data-open-dialog=\"cluster-dialog\">Change</button>}</dt>", page, StringComparison.Ordinal);
        Assert.Contains("@if (Model.ForTeam && Model.Replaced)", modules, StringComparison.Ordinal);
        Assert.Contains("The module list stays as it was on this earlier passport; change it on the newest one.", modules, StringComparison.Ordinal);
    }
}
