using BatteryPassWeb.Dpp.Access;
using BatteryPassWeb.Dpp.Editing;
using BatteryPassWeb.Dpp.Pages;

namespace BatteryPassWeb.Tests.Dpp;

/// <summary>
/// Redesign R3 on the lists: both statuses on every row (the battery's only for its readers), an Open button where the
/// cluster admin's small "Manage" link was (owner's point 11), and "Waiting for you" standing out (point 12).
/// </summary>
[Collection(MongoCollection.Name)]
public sealed class RegistryBadgesTests(MongoFixture mongo)
{
    private static string View(params string[] parts) => File.ReadAllText(DppTestFiles.RepoFile(["web", "Views", .. parts]));

    [Fact]
    public async Task EachRow_ShouldCarryItsBatteryStatusForItsReadersOnly()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo).WithClustersAsync();
        var list = await BatteryTesting.Pages(rig).ListAsync(null);

        var itsCluster = RegistryList.Build(Reader.With("user", ReaderRole.ClusterUser, BatteryTesting.North), list);
        var surveillance = RegistryList.Build(Reader.With("msa@example.com", ReaderRole.MarketSurveillance), list);
        var recycler = RegistryList.Build(Reader.With("recycler@example.com", ReaderRole.LiRecycling), list);

        Assert.Equal("original", list.Rows.Single(row => row.PackId == rig.PackId).BatteryStatus);
        Assert.Contains(rig.PackId, itsCluster.BatteryStatusShown);
        Assert.Contains(rig.PackId, surveillance.BatteryStatusShown);
        Assert.Single(recycler.Rows); // listed, as every published battery is for them (decision 30) …
        Assert.Empty(recycler.BatteryStatusShown); // … but #22 is purchaser data
    }

    [Fact]
    public async Task TheTopBar_ShouldCountTheHandOversWaitingForAClusterAdmin()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo).WithClustersAsync();
        var service = HandOverTests.Service(rig);
        var southAdmin = ChangeSender.ClusterAdmin("south-admin@example.com", BatteryTesting.South);

        Assert.Equal(0, await service.WaitingCountAsync(southAdmin));
        await service.AskAsync(ChangeSender.ClusterAdmin("north-admin@example.com", BatteryTesting.North), rig.PackId, BatteryTesting.South, confirm: true);

        Assert.Equal(1, await service.WaitingCountAsync(southAdmin));
        Assert.Equal(0, await service.WaitingCountAsync(ChangeSender.Team("team@example.com")));
        Assert.Equal(2, View("Shared", "_Layout.cshtml").Split("Open Registry<partial name=\"_WaitingCount\" /></a>").Length - 1);
        Assert.Contains("<span class=\"bp-nav-count\"", View("Shared", "_WaitingCount.cshtml"), StringComparison.Ordinal);
    }

    [Fact]
    public void TheRegistry_ShouldShowBothBadgesAnOpenButtonAndAWaitingCardThatStandsOut()
    {
        var registry = View("Registry", "Index.cshtml");
        var waiting = registry[registry.IndexOf("id=\"waiting\"", StringComparison.Ordinal)..registry.IndexOf("</section>", StringComparison.Ordinal)];

        Assert.Contains("bp-wait mt-4\" id=\"waiting\"", registry, StringComparison.Ordinal);
        Assert.Contains("<use href=\"#i-bell\" />", waiting, StringComparison.Ordinal);
        Assert.True(waiting.IndexOf("value=\"true\"", StringComparison.Ordinal) < waiting.IndexOf("value=\"false\"", StringComparison.Ordinal)); // Accept first
        // The shared list (R7) draws both badges; the Registry says whose battery status (#22) the reader may see.
        var list = View("Shared", "_BatteryList.cshtml");
        Assert.Contains("row => Model.BatteryStatusShown.Contains(row.PackId)", registry, StringComparison.Ordinal);
        Assert.Contains("@if (Model.ShowsBatteryStatus(row) && StatusBadge.Battery(row.BatteryStatus) is { } battery)", list, StringComparison.Ordinal);
        Assert.Contains("StatusBadge.Passport(row.IsDraft ? null : (row.Active ?? row.Latest)!.DppStatus)", list, StringComparison.Ordinal);
        Assert.Contains("_RowActions", list, StringComparison.Ordinal); // the Open button became the row's icons (R5)
        Assert.DoesNotContain(">Manage</a>", list, StringComparison.Ordinal);
    }
}
