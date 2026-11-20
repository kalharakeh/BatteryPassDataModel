using BatteryPassWeb.Dpp.Access;
using BatteryPassWeb.Dpp.Pages;

namespace BatteryPassWeb.Tests.Dpp;

/// <summary>
/// Redesign R6 (the review of the built R5): the Registry's search found batteries by their cluster's ID or name for every
/// reader, so market surveillance or a recycler could type a customer's name and learn which batteries are theirs, though
/// the Cluster column is hidden from them. Only readers who may see clusters (the team, the cluster's members) find
/// batteries by cluster now; the old app refused it the same way ("Cluster search is not available for this role").
/// </summary>
[Collection(MongoCollection.Name)]
public sealed class RegistryClusterSearchTests(MongoFixture mongo)
{
    [Fact]
    public async Task AReaderWhoMayNotSeeClusters_ShouldNotFindBatteriesByACluster()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo).WithClustersAsync();
        var pages = BatteryTesting.Pages(rig);
        var surveillance = Reader.With("msa@example.com", ReaderRole.MarketSurveillance);
        var member = Reader.With("user", ReaderRole.ClusterUser, BatteryTesting.North);

        var bySurveillance = await pages.ListOnePerBatteryAsync(BatteryTesting.North, row => RegistryList.MayList(surveillance, row), RegistryList.ShowsClusters(surveillance));
        var byMember = await pages.ListOnePerBatteryAsync(BatteryTesting.North, row => RegistryList.MayList(member, row), RegistryList.ShowsClusters(member));
        var bySerial = await pages.ListOnePerBatteryAsync("VPC8C05", row => RegistryList.MayList(surveillance, row), RegistryList.ShowsClusters(surveillance));

        Assert.False(RegistryList.ShowsClusters(surveillance));
        Assert.Empty(bySurveillance.Rows);
        Assert.Single(byMember.Rows); // the cluster's own members still find their batteries by it
        Assert.Single(bySerial.Rows); // and everyone still finds a battery by its serial number
    }

    [Fact]
    public void TheRegistry_ShouldSearchClustersOnlyForThoseWhoSeeThem() =>
        Assert.Contains("batteries.ListOnePerBatteryAsync(q, row => RegistryList.MayList(reader, row), RegistryList.ShowsClusters(reader), cancellationToken)",
            File.ReadAllText(DppTestFiles.RepoFile("web", "Controllers", "RegistryController.cs")), StringComparison.Ordinal);
}
