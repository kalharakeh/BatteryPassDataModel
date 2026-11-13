using BatteryPassWeb.Dpp.Documents;
using BatteryPassWeb.Dpp.Pages;
using BatteryPassWeb.Dpp.Store;
using BatteryPassWeb.Services;

namespace BatteryPassWeb.Tests.Dpp;

/// <summary>
/// Finding R02, round two: one condition for the EU registry section, the list's count and the CSV (an active passport,
/// outside the demo), and the app's clock for the record's day.
/// </summary>
[Collection(MongoCollection.Name)]
public sealed class EuRegistryScopeTests(MongoFixture mongo)
{
    [Fact]
    public async Task APassportNoLongerActive_ShouldHaveNoSection_NotCount_AndNotBeRecorded()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo).WithClustersAsync();
        Assert.Null(await PassportMarkTests.Service(rig).MarkInvalidAsync(PublicationTesting.Team, rig.PackId, "Declared values don't match"));
        var pages = BatteryTesting.Pages(rig);

        Assert.Null((await pages.BatteryAsync(rig.PackId))!.Registry); // published once, but not active: like the count and the CSV
        Assert.Equal(0, (await pages.TeamListAsync(null)).NotInTheRegistry);
        Assert.Empty(await pages.NotInTheRegistryAsync());
        Assert.Equal("Only a battery with an active passport, outside the demo, is recorded in the EU registry.", await pages.RecordRegistryAsync("team@example.com", rig.PackId, "2026-10-01", "EU-1"));
    }

    [Fact]
    public async Task TheRecord_ShouldUseTheAppsClock()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo).WithClustersAsync();
        var pages = Pages(rig); // the rig's clock shows 29 Oct 2026, 08:00

        Assert.Equal("Give the day it was registered: today or a day before.", await pages.RecordRegistryAsync("team@example.com", rig.PackId, "2026-10-30", "EU-1"));
        Assert.Null(await pages.RecordRegistryAsync("team@example.com", rig.PackId, "2026-10-29", "EU-1"));

        Assert.Equal(DppStoreTesting.Now.UtcDateTime, (await rig.Store.GetAsync<BatteryPack>(rig.PackId))!.Registry!.RecordedAtUtc);
        Assert.Equal("2026-10-29", (await pages.BatteryAsync(rig.PackId))!.Registry!.Today);
    }

    private static BatteryPages Pages(PublicationRig rig) => new(
        rig.Store,
        PageTesting.Editor(rig),
        BatteryTesting.Creation(rig),
        rig.Publication,
        new DocumentStore(rig.Context),
        DocumentTesting.Links,
        new ClusterRepository(rig.Context),
        new ApplicationAuditService(rig.Context),
        StatusChangeTests.Pages(rig),
        clock: rig.Clock);
}
