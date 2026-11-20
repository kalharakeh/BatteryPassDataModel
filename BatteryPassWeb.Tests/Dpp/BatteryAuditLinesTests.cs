using BatteryPassWeb.Dpp.Audit;
using BatteryPassWeb.Dpp.Documents;
using BatteryPassWeb.Dpp.Pages;
using BatteryPassWeb.Services;

namespace BatteryPassWeb.Tests.Dpp;

/// <summary>The batteries pages' events in the audit log: plain sentences, and a link to the battery's page.</summary>
[Collection(MongoCollection.Name)]
public sealed class BatteryAuditLinesTests(MongoFixture mongo)
{
    private const string Who = "team@example.com";

    private static async Task<IReadOnlyList<AuditLine>> LinesAsync(PublicationRig rig)
    {
        var events = await new ApplicationAuditService(rig.Context).ListApplicationAuditEventsAsync(new ApplicationAuditFilter());
        return await new AuditLines(rig.Store, new DocumentStore(rig.Context), AttributeFormsTests.Forms, rig.Context).DescribeAsync(events);
    }

    [Fact]
    public async Task AClusterMove_ShouldSayFromWhereToWhereAndLinkToTheBattery()
    {
        var rig = await PublicationTesting.RigAsync(mongo).WithClustersAsync();
        await BatteryTesting.Pages(rig).SetClusterAsync(Who, rig.PackId, BatteryTesting.South);

        var line = Assert.Single(await LinesAsync(rig), line => line.EventType == BatteryPages.ClusterChangedEvent);

        Assert.Equal($"{Who} moved battery VPC8C05-6788 to another cluster", line.Text);
        Assert.Equal("Customer North → Customer South", line.Change);
        Assert.Contains(line.Facts, fact => fact.Term == "Record" && fact.Href == BatteryPages.BatteryHref(rig.PackId));
    }

    [Fact]
    public async Task ARemovedDraft_ShouldBeNamedByItsSerialNumber()
    {
        var rig = await PublicationTesting.RigAsync(mongo).WithClustersAsync();
        await BatteryTesting.Pages(rig).RemoveDraftAsync(Who, rig.PackId);

        var line = Assert.Single(await LinesAsync(rig), line => line.EventType == ProductDataPages.RemovedEvent);

        Assert.Equal($"{Who} removed battery VPC8C05-6788", line.Text);
    }
}
