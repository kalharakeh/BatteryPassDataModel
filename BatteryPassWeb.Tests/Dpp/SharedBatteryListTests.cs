using BatteryPassWeb.Dpp.Access;
using BatteryPassWeb.Dpp.Editing;
using BatteryPassWeb.Dpp.Pages;

namespace BatteryPassWeb.Tests.Dpp;

/// <summary>
/// Redesign R7, the owner's item 4 ("open registry and batteries page have the same content but behave differently")
/// and choice "One shared list": both pages draw one list, with the same search, columns and row icons. A serial number
/// opens the battery page for the team and its cluster admins, the detail page for everyone else. Each page keeps its own
/// rows and rules: who is listed, who sees clusters and the battery status (#22), the Registry's hand-overs.
/// </summary>
[Collection(MongoCollection.Name)]
public sealed class SharedBatteryListTests(MongoFixture mongo)
{
    private static string View(params string[] parts) => File.ReadAllText(DppTestFiles.RepoFile(["web", "Views", .. parts])).Replace("\r\n", "\n", StringComparison.Ordinal);

    [Fact]
    public async Task ASerialNumber_ShouldOpenTheBatteryPage_ForTheTeamAndItsClusterAdmins_AndTheDetailPageForOthers()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo).WithClustersAsync();
        var batteries = await BatteryTesting.Pages(rig).ListAsync(null);
        var row = Assert.Single(batteries.Rows);

        var team = RegistryList.Build(Reader.With("team", ReaderRole.Team), batteries, ChangeSender.Team("team"));
        var admin = RegistryList.Build(Reader.With("admin", ReaderRole.ClusterAdmin, BatteryTesting.North), batteries, ChangeSender.ClusterAdmin("admin", BatteryTesting.North));
        var member = RegistryList.Build(Reader.With("user", ReaderRole.ClusterUser, BatteryTesting.North), batteries);
        var surveillance = RegistryList.Build(Reader.With("msa", ReaderRole.MarketSurveillance), batteries);

        Assert.Equal(BatteryPages.BatteryHref(row.PackId), team.OpenHref(row));
        Assert.Equal(ClusterBatteryPages.BatteryHref(row.PackId), admin.OpenHref(row));
        Assert.Equal(PassportPages.DetailHref(row.Identifier), member.OpenHref(row));
        Assert.Equal(PassportPages.DetailHref(row.Identifier), surveillance.OpenHref(row));
    }

    [Fact]
    public async Task TheUpdatedColumn_ShouldSayTheVersionAndDayShort_AndInFullOnHover()
    {
        var published = Assert.Single((await BatteryTesting.Pages(await PublicationTesting.PublishedRigAsync(mongo)).ListAsync(null)).Rows);
        var draft = Assert.Single((await BatteryTesting.Pages(await PublicationTesting.RigAsync(mongo)).ListAsync(null)).Rows);
        var shown = (published.Active ?? published.Latest)!;

        Assert.Equal($"v{shown.Number} · {shown.LastUpdatedUtc.ToString("d MMM", System.Globalization.CultureInfo.InvariantCulture)}", BatteryListView.Updated(published));
        Assert.Equal($"Version {shown.Number} · updated {ValueWords.Time(shown.LastUpdatedUtc)}", BatteryListView.UpdatedInFull(published));
        Assert.Null(BatteryListView.Updated(draft));
    }

    [Fact]
    public void TheSearch_ShouldMentionClustersOnlyToThoseWhoMaySearchThem()
    {
        static BatteryListView List(bool clusters) => new("/registry", string.Empty, [], clusters, _ => string.Empty, _ => null!, _ => false);

        Assert.Equal("Serial number, battery ID, passport ID or cluster", List(clusters: true).Placeholder);
        Assert.Equal("Serial number, battery ID or passport ID", List(clusters: false).Placeholder); // R6: no cluster search for them
    }

    [Fact]
    public void BothPages_ShouldDrawTheSharedList_WithTheirOwnRules()
    {
        var team = View("Batteries", "Batteries.cshtml");
        var registry = View("Registry", "Index.cshtml");
        var list = View("Shared", "_BatteryList.cshtml");

        Assert.Contains("new BatteryListView(\"/admin/batteries\", Model.Query, Model.Rows, ShowClusters: true,", team, StringComparison.Ordinal);
        Assert.Contains("CreateHref = \"/admin/batteries/new\",", team, StringComparison.Ordinal);
        Assert.Contains("new BatteryListView(\"/registry\", Model.Query, Model.Rows, Model.ShowClusters,\n        Model.OpenHref, Model.Actions, row => Model.BatteryStatusShown.Contains(row.PackId))", registry, StringComparison.Ordinal);
        Assert.True(registry.IndexOf("id=\"waiting\"", StringComparison.Ordinal) < registry.IndexOf("_BatteryList", StringComparison.Ordinal)); // the hand-overs stay first
        Assert.DoesNotContain("<table", team, StringComparison.Ordinal);
        Assert.DoesNotContain("<table", registry, StringComparison.Ordinal);
        Assert.Contains("<th>Serial number</th>\n                        <th>Model</th>\n                        <th>Batch</th>", list, StringComparison.Ordinal);
        Assert.Contains("<th>Updated</th>", list, StringComparison.Ordinal);
        Assert.Contains("placeholder=\"@Model.Placeholder\"", list, StringComparison.Ordinal);
    }
}
