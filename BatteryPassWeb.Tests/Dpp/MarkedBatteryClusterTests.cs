using BatteryPassWeb.Dpp.Access;
using BatteryPassWeb.Dpp.Editing;
using BatteryPassWeb.Dpp.Store;

namespace BatteryPassWeb.Tests.Dpp;

/// <summary>
/// Redesign R7, task 5 (the page review's gap, the owner's "Yes, fix it"): a battery's cluster counted only while its
/// passport was active, so once the passport was marked invalid or for deletion its own cluster lost the accident page
/// ("Not available") and the purchaser data, though the team kept them. The cluster now counts while the battery's newest
/// passport is marked, as live values already do (PassportReader): the owning cluster reads it exactly as when it was
/// active. A replaced passport, another cluster and the public stay as they were.
/// </summary>
[Collection(MongoCollection.Name)]
public sealed class MarkedBatteryClusterTests(MongoFixture mongo)
{
    private static readonly Reader NorthAdmin = Reader.With("north-admin", ReaderRole.ClusterAdmin, BatteryTesting.North);
    private static readonly Reader NorthUser = Reader.With("north-user", ReaderRole.ClusterUser, BatteryTesting.North);
    private static readonly Reader SouthAdmin = Reader.With("south-admin", ReaderRole.ClusterAdmin, BatteryTesting.South);

    private async Task<PublicationRig> MarkedAsync(bool forDeletion)
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo).WithClustersAsync();
        if (forDeletion)
        {
            await StatusChangeTests.Service(rig).ChangeAsync(PublicationTesting.Team, rig.PackId, new StatusChange(StatusEvent.Waste, await StatusChangeTests.CurrentAsync(rig)));
            Assert.Null(await PassportMarkTests.Service(rig).MarkForDeletionAsync(PublicationTesting.Team, rig.PackId));
        }
        else
        {
            Assert.Null(await PassportMarkTests.Service(rig).MarkInvalidAsync(PublicationTesting.Team, rig.PackId, "Demo: wrong figures"));
        }

        return rig;
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AMarkedBatterysCluster_ShouldOpenItsAccidentPage_AsWhenItWasActive(bool forDeletion)
    {
        var rig = await MarkedAsync(forDeletion);
        var documents = PageTesting.Documents(rig);

        Assert.NotNull(await documents.AccidentReportsAsync(NorthAdmin, rig.PackId));
        Assert.NotNull(await documents.AccidentReportsAsync(NorthUser, rig.PackId));
        Assert.Equal([BatteryTesting.North], await rig.Store.FindPublishedClusterIdsUsingAsync(packId: rig.PackId));
    }

    [Fact]
    public async Task AnotherCluster_AndThePublic_ShouldStillNotOpenIt()
    {
        var rig = await MarkedAsync(forDeletion: false);
        var documents = PageTesting.Documents(rig);

        Assert.Null(await documents.AccidentReportsAsync(SouthAdmin, rig.PackId));
        Assert.Null(await documents.AccidentReportsAsync(Reader.Anonymous, rig.PackId)); // nothing new becomes public
    }

    [Fact]
    public async Task AMarkedBatterysDetailPage_ShouldShowItsClusterThePurchaserData_AndThePublicNoMore()
    {
        var rig = await MarkedAsync(forDeletion: false);
        var id = (await rig.Store.GetLatestVersionAsync(rig.PackId))!.DigitalProductPassportId;
        var pages = PassportPagesTests.Pages(rig);

        var member = (await pages.DetailAsync(NorthUser, id))!;
        var anyone = (await pages.SummaryAsync(Reader.Anonymous, id))!;

        Assert.Contains(member.Audiences, section => section.Key == "purchaser");
        Assert.Empty(anyone.Audiences);
    }

    [Fact]
    public async Task AReplacedPassport_ShouldStillNotCountForItsCluster()
    {
        var rig = await new SuccessionTests(mongo).InactiveAsync();
        await rig.Store.SetPackClusterAsync(rig.PackId, BatteryTesting.North);

        Assert.Equal(DppStatuses.Inactive, await rig.Store.PassportStatusAsync(rig.PackId));
        Assert.Empty(await rig.Store.FindPublishedClusterIdsUsingAsync(packId: rig.PackId));
        Assert.Null(await PageTesting.Documents(rig).AccidentReportsAsync(NorthAdmin, rig.PackId));
    }
}
