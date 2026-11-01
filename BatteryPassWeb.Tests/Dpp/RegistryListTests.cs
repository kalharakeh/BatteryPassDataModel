using BatteryPassWeb.Dpp.Access;
using BatteryPassWeb.Dpp.Editing;
using BatteryPassWeb.Dpp.Pages;
using BatteryPassWeb.Dpp.Store;

namespace BatteryPassWeb.Tests.Dpp;

/// <summary>Decision 30: the Registry lists the published batteries each signed-in reader may open.</summary>
public sealed class RegistryListTests
{
    private static BatteryRow Row(string serial, string? cluster, bool published) => new(
        $"pack-{serial}", serial, $"urn:test:{serial}", "Family", "Model", "2026 · Plant", cluster, cluster ?? "No cluster",
        published
            ? new PassportVersion
            {
                Id = $"version-{serial}", PackId = $"pack-{serial}", Number = 1, DigitalProductPassportId = $"urn:test:dpp-{serial}",
                DppStatus = "active", PublishedAtUtc = DateTime.UtcNow, LastUpdatedUtc = DateTime.UtcNow, PublishedBy = "team",
                StaticCore = new System.Text.Json.Nodes.JsonObject(), Proof = null!,
            }
            : null);

    private static readonly BatteriesPage Batteries = new([Row("NORTH-1", "north", true), Row("SOUTH-1", "south", true), Row("NORTH-DRAFT", "north", false)], string.Empty, 3);

    [Theory]
    [InlineData(ReaderRole.Team, "NORTH-1,SOUTH-1", true)]
    [InlineData(ReaderRole.MarketSurveillance, "NORTH-1,SOUTH-1", false)]
    [InlineData(ReaderRole.LiRepair, "NORTH-1,SOUTH-1", false)]
    [InlineData(ReaderRole.ClusterUser, "NORTH-1", true)]
    [InlineData(ReaderRole.ClusterAdmin, "NORTH-1", true)]
    public void EachReader_ShouldSeeThePublishedBatteriesTheyMayOpen(ReaderRole role, string serials, bool clusters)
    {
        var reader = role is ReaderRole.ClusterUser or ReaderRole.ClusterAdmin ? Reader.With("x", role, "north") : Reader.With("x", role);

        var page = RegistryList.Build(reader, Batteries);

        Assert.Equal(serials.Split(','), page.Rows.Select(row => row.SerialNumber));
        Assert.Equal(clusters, page.ShowClusters);
    }

    [Fact]
    public void ASignedInUserWithoutRoleOrCluster_ShouldSeeNothingAndBeToldWhy()
    {
        var page = RegistryList.Build(Reader.With("x", ReaderRole.ClusterUser), Batteries);

        Assert.Empty(page.Rows);
        Assert.Contains("Ask your cluster admin", page.Scope);
    }

    [Fact]
    public void TheRegistry_ShouldOpenEachBatterysDetailPage()
    {
        var view = File.ReadAllText(DppTestFiles.RepoFile("web", "Views", "Registry", "Index.cshtml"));

        Assert.Contains("/dpp/@Uri.EscapeDataString(row.Identifier)/detail", view);
        Assert.DoesNotContain("_BatteryTable", view); // the old model's table is gone from the Registry
        Assert.Contains("Model.ManageHref(row)", view); // step 4a: the cluster admin's "Manage" link
    }

    [Fact]
    public void AClusterAdmin_ShouldGetAManageLinkOnTheirOwnClustersRowsOnly()
    {
        var reader = new Reader(new HashSet<ReaderRole> { ReaderRole.ClusterAdmin, ReaderRole.ClusterUser }, new HashSet<string> { "north", "south" }, "x");
        var editor = ChangeSender.ClusterAdmin("x", "north"); // admin of north, a plain member of south

        var page = RegistryList.Build(reader, Batteries, editor);

        Assert.Equal("/cluster-admin/batteries/pack-NORTH-1", page.ManageHref(page.Rows.Single(row => row.SerialNumber == "NORTH-1")));
        Assert.Null(page.ManageHref(page.Rows.Single(row => row.SerialNumber == "SOUTH-1")));
    }

    [Fact]
    public void EveryoneElse_ShouldGetNoManageLink()
    {
        var user = RegistryList.Build(Reader.With("x", ReaderRole.ClusterUser, "north"), Batteries);
        var team = RegistryList.Build(Reader.With("x", ReaderRole.Team), Batteries, ChangeSender.Team("x"));

        Assert.All(user.Rows, row => Assert.Null(user.ManageHref(row)));
        Assert.All(team.Rows, row => Assert.Null(team.ManageHref(row)));
    }
}
