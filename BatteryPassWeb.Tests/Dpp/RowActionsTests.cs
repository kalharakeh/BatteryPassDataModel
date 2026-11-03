using BatteryPassWeb.Dpp.Access;
using BatteryPassWeb.Dpp.Editing;
using BatteryPassWeb.Dpp.Pages;

namespace BatteryPassWeb.Tests.Dpp;

/// <summary>
/// Redesign R5, owner's item 1: the battery lists' rows end in icons named on hover, like the old app's row actions:
/// the battery page (pencil), the public and detail pages (globe, document) once published, the history (clock) for the
/// team. They replace R3's Open button.
/// </summary>
[Collection(MongoCollection.Name)]
public sealed class RowActionsTests(MongoFixture mongo)
{
    private static string View(params string[] parts) => File.ReadAllText(DppTestFiles.RepoFile(["web", "Views", .. parts]));

    private async Task<(BatteryRow Row, string Key)> PublishedRowAsync()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo).WithClustersAsync();
        var row = (await BatteryTesting.Pages(rig).ListAsync(null)).Rows.Single(candidate => candidate.PackId == rig.PackId);
        return (row, Uri.EscapeDataString(row.Identifier));
    }

    [Fact]
    public async Task APublishedRow_ShouldOpenItsPublicAndDetailPages_AndTheTeamItsHistory()
    {
        var (row, key) = await PublishedRowAsync();

        var team = RowActions.For(row, BatteryPages.BatteryHref(row.PackId), team: true);
        var reader = RowActions.For(row, null, team: false);

        Assert.Equal(BatteryPages.BatteryHref(row.PackId), team.Manage);
        Assert.Equal($"/dpp/{key}", team.Public);
        Assert.Equal($"/dpp/{key}/detail", team.Detail);
        Assert.Equal($"/admin/audit?q={Uri.EscapeDataString(row.PackId)}", team.History);
        Assert.Null(reader.Manage);
        Assert.Null(reader.History); // the audit log is the team's
        Assert.NotNull(reader.Public);
    }

    [Fact]
    public async Task ADraftRow_ShouldHaveNoPublicOrDetailPage()
    {
        var (row, _) = await PublishedRowAsync();

        var draft = RowActions.For(row with { Active = null, Latest = null }, BatteryPages.BatteryHref(row.PackId), team: true);

        Assert.Null(draft.Public);
        Assert.Null(draft.Detail);
        Assert.NotNull(draft.Manage);
    }

    [Fact]
    public async Task TheRegistry_ShouldOpenTheTeamsBatteryPageForTheTeam_AndTheClusterAdminsForItsAdmin()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo).WithClustersAsync();
        var list = await BatteryTesting.Pages(rig).ListAsync(null);
        var row = list.Rows.Single(candidate => candidate.PackId == rig.PackId);

        var team = RegistryList.Build(Reader.With("team@example.com", ReaderRole.Team), list).Actions(row);
        var admin = RegistryList.Build(Reader.With("north-admin@example.com", ReaderRole.ClusterAdmin, BatteryTesting.North), list,
            ChangeSender.ClusterAdmin("north-admin@example.com", BatteryTesting.North)).Actions(row);
        var surveillance = RegistryList.Build(Reader.With("msa@example.com", ReaderRole.MarketSurveillance), list).Actions(row);

        Assert.Equal(BatteryPages.BatteryHref(rig.PackId), team.Manage);
        Assert.NotNull(team.History);
        Assert.Equal(ClusterBatteryPages.BatteryHref(rig.PackId), admin.Manage);
        Assert.Null(admin.History);
        Assert.Null(surveillance.Manage);
        Assert.NotNull(surveillance.Detail);
    }

    [Fact]
    public void BothLists_ShouldEndEachRowInTheIcons_NamedOnHover()
    {
        var icons = View("Shared", "_RowActions.cshtml");
        var registry = View("Registry", "Index.cshtml");
        var team = View("Batteries", "Batteries.cshtml");

        foreach (var (icon, name) in new[] { ("i-pencil", "Battery page"), ("i-globe", "Public page"), ("i-doc", "Detail page"), ("i-history", "History") })
        {
            Assert.Contains($"data-tooltip=\"{name}\"><svg class=\"bp-report-action-icon\" aria-hidden=\"true\"><use href=\"#{icon}\" />", icons, StringComparison.Ordinal);
        }

        Assert.Contains("@await Html.PartialAsync(\"_RowActions\", Model.Actions(row))", registry, StringComparison.Ordinal);
        Assert.Contains("@await Html.PartialAsync(\"_RowActions\", RowActions.For(row, BatteryPages.BatteryHref(row.PackId), team: true))", team, StringComparison.Ordinal);
        Assert.DoesNotContain(">Open<svg", registry, StringComparison.Ordinal); // R3's Open button is gone
        // The icons stay in view at 1280 px: the battery ID shortened in the middle, the batch and cluster wrapping.
        Assert.Contains("@BatteryPages.ShortId(row.Identifier)</small>", team, StringComparison.Ordinal);
        Assert.Contains("@BatteryPages.ShortId(row.Identifier)</small>", registry, StringComparison.Ordinal);
        Assert.Contains(".bp-console-table:has(.bp-actions-column) td:is([data-label=\"Batch\"], [data-label=\"Cluster\"]) {", File.ReadAllText(DppTestFiles.RepoFile("web", "wwwroot", "css", "bp-product-data.css")), StringComparison.Ordinal);
        Assert.Equal(2, team.Split("<span class=\"visually-hidden\">Actions</span>").Length - 1 + registry.Split("<span class=\"visually-hidden\">Actions</span>").Length - 1);
    }
}
