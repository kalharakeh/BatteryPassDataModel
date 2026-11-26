using BatteryPassWeb.Dpp.Demo;
using BatteryPassWeb.Dpp.Editing;
using BatteryPassWeb.Dpp.Identifiers;
using BatteryPassWeb.Dpp.Store;
using BatteryPassWeb.Tests.Dpp;
using Microsoft.Extensions.DependencyInjection;
using static BatteryPassWeb.Tests.Dpp.SmallLetterSerialTesting;

namespace BatteryPassWeb.Tests.Demo;

/// <summary>
/// Decision 82: trial batteries are made on real families with test IDs, and "Clear demo data" removes every test ID and
/// test battery too, also those on real product data. Its confirmation says how many of them go with it, and nothing about
/// them when there are none. What it removes doesn't change, and it stays on offer when the demo is switched off.
/// </summary>
[Collection(MongoCollection.Name)]
public sealed class ClearDemoDataSaysWhatElseGoesTests(MongoFixture mongo)
{
    private const string Plain = "Remove the demo data? Real data stays as it is, and a demo record that real data uses is kept.";

    [Theory]
    [InlineData(0, 0, Plain)]
    [InlineData(3, 1, Plain + " Test IDs and test batteries go too, also those on real product data: 3 test IDs and 1 test battery on real product data.")]
    [InlineData(1, 2, Plain + " Test IDs and test batteries go too, also those on real product data: 1 test ID and 2 test batteries on real product data.")]
    [InlineData(4, 0, Plain + " Test IDs go too: 4 test IDs.")]
    [InlineData(1, 0, Plain + " Test IDs go too: 1 test ID.")]
    [InlineData(0, 1, Plain + " Test batteries go too, also those on real product data: 1 test battery on real product data.")]
    public void TheConfirmation_ShouldSayHowManyTestIdsAndTestBatteriesGo_AndNothingWhenThereAreNone(int testIds, int testBatteries, string message) =>
        Assert.Equal(message, DemoDataService.ClearConfirmation(new TestDataOutsideTheDemo(testIds, testBatteries)));

    [Fact]
    public async Task TheDemosOwnIdsAndBatteries_ShouldNotBeCounted()
    {
        using var rig = await DemoRig.StartAsync(mongo, dunsIds: true);
        Assert.Equal(new TestDataOutsideTheDemo(0, 0), await rig.Demo.TestDataOutsideTheDemoAsync()); // an empty site

        await rig.Demo.SeedAsync(Who);

        Assert.Equal(new TestDataOutsideTheDemo(0, 0), await rig.Demo.TestDataOutsideTheDemoAsync());
    }

    [Fact]
    public async Task TestIdsAndTestBatteriesOnRealProductData_ShouldBeCounted_AndClearingRemovesThemAsBefore()
    {
        var (rig, pool, c7) = await SeededAsync(mongo); // the Compact 7M's batch, model and operator are real product data
        using var __ = rig;
        var before = await rig.Demo.TestDataOutsideTheDemoAsync(); // this rig's shortcut leaves the demo's own Compact 7M batteries on a real batch
        Assert.Equal(0, before.TestIds);
        var creation = rig.Services.GetRequiredService<PackCreationService>();
        Assert.True((await pool.ReleaseAsync(Who, new ReleaseRequest("Trial", null, [], "TRY-0001\nTRY-0002\nvpc8c05s-90003", IsDemo: true))).Done);
        Assert.True((await pool.ReleaseAsync(Who, List("PO real", "VPC8C05s-00001"))).Done);
        var trial = (await creation.CreateAsync(ChangeSender.Team(Who), NewBattery(c7, "TRY-0001"))).Pack!;
        var real = (await creation.CreateAsync(ChangeSender.Team(Who), NewBattery(c7, "VPC8C05s-00001"))).Pack!;
        await pool.VoidAsync(Who, "TRY-0002", "Label damaged"); // a voided test ID goes too

        var counted = await rig.Demo.TestDataOutsideTheDemoAsync();

        Assert.Equal(new TestDataOutsideTheDemo(3, before.TestBatteries + 1), counted);

        // What clearing removes is what it removed before: every test ID and test battery, never a real one.
        await rig.Demo.ClearAsync(Who);
        Assert.Null(await rig.Store.GetAsync<BatteryPack>(trial.Id));
        Assert.NotNull(await rig.Store.GetAsync<BatteryPack>(real.Id));
        Assert.Equal(["VPC8C05s-00001"], (await pool.ListAsync(new ReleasedIdFilter(Demo: true, Voided: true))).Select(item => item.SerialNumber));
        Assert.Equal(new TestDataOutsideTheDemo(0, 0), await rig.Demo.TestDataOutsideTheDemoAsync());
    }

    [Fact]
    public async Task WithTheDemoSwitchedOff_ClearingShouldStayOnOffer_AndStillCount()
    {
        var (on, pool, c7) = await SeededAsync(mongo);
        using var _ = on;
        var before = await on.Demo.TestDataOutsideTheDemoAsync();
        await pool.ReleaseAsync(Who, new ReleaseRequest("Trial", null, [], "TRY-0001", IsDemo: true));
        await on.Services.GetRequiredService<PackCreationService>().CreateAsync(ChangeSender.Team(Who), NewBattery(c7, "TRY-0001"));
        using var off = DemoRig.Over(on, demoData: false);

        Assert.False(off.Demo.Enabled);
        Assert.Equal(new TestDataOutsideTheDemo(1, before.TestBatteries + 1), await off.Demo.TestDataOutsideTheDemoAsync());

        // The card: the reset is offered only while the demo is on; the clear form stands outside that test, with its counts.
        var view = File.ReadAllText(DppTestFiles.RepoFile("web", "Views", "Admin", "Clusters.cshtml"));
        var reset = view.IndexOf("action=\"/admin/demo-data/reset\"", StringComparison.Ordinal);
        var clear = view.IndexOf("action=\"/admin/demo-data/clear\"", StringComparison.Ordinal);
        var onlyWhenOn = view.LastIndexOf("@if (demoDataEnabled)", reset, StringComparison.Ordinal);
        Assert.True(onlyWhenOn > 0 && view.IndexOf('}', reset) < clear);
        Assert.Contains("data-confirm-message=\"@DemoDataService.ClearConfirmation(testData)\"", view[clear..], StringComparison.Ordinal);
        Assert.Contains("TestDataOutsideTheDemoAsync()", view, StringComparison.Ordinal);
        Assert.DoesNotContain("data-confirm-message=\"Remove the demo data? Real data stays as it is, and a demo record that real data uses is kept.\"", view, StringComparison.Ordinal);
    }
}
