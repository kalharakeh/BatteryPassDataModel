using BatteryPassWeb.Dpp.Access;
using BatteryPassWeb.Dpp.Editing;
using BatteryPassWeb.Dpp.Store;
using MongoDB.Bson;
using MongoDB.Driver;

namespace BatteryPassWeb.Tests.Dpp;

/// <summary>
/// Finding R03 on the pages: a demo account is a visitor on a real battery's passport page (no restricted parts, no
/// "Mark as invalid"), and a demo cluster admin doesn't manage a real battery even in their cluster; on a demo battery
/// both keep their role.
/// </summary>
[Collection(MongoCollection.Name)]
public sealed class DemoPagesTests(MongoFixture mongo)
{
    private static Reader DemoMsa => Reader.With("demo.msa@example.test", ReaderRole.MarketSurveillance) with { DemoOnly = true };

    private static Task MakeDemoAsync(PublicationRig rig) => TestDemo.MakeInsideAsync(rig);

    // The manager's two mixed cases: a demo-ID battery on real product data, and a demo battery in a real cluster.
    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public async Task OnAMixedBattery_ADemoAuthorityShouldGetTheVisitorsPage(bool demoCluster, bool demoProductData)
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo).WithClustersAsync();
        await TestDemo.MakeInsideAsync(rig, cluster: demoCluster, productData: demoProductData);
        var pages = PassportPagesTests.Pages(rig);

        var demo = (await pages.DetailAsync(DemoMsa, PassportPagesTests.Identifier(rig)))!;

        Assert.False(demo.MayMarkInvalid);
        Assert.False(demo.ReaderSeesMore);
        Assert.Null(await ClusterBatteryPagesTests.Pages(rig).BatteryAsync(
            ChangeSender.ClusterAdmin("demo.admin@example.test", (await rig.Store.GetAsync<BatteryPack>(rig.PackId))!.ClusterId!) with { DemoOnly = true }, rig.PackId));
    }

    [Fact]
    public async Task OnARealBattery_ADemoAuthorityShouldGetTheVisitorsPage()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo).WithClustersAsync();
        var pages = PassportPagesTests.Pages(rig);
        var id = PassportPagesTests.Identifier(rig);

        var demo = (await pages.DetailAsync(DemoMsa, id))!;
        var visitor = (await pages.DetailAsync(Reader.Anonymous, id))!;
        var real = (await pages.DetailAsync(Reader.With("msa@example.test", ReaderRole.MarketSurveillance), id))!;

        Assert.False(demo.MayMarkInvalid);
        Assert.False(demo.ReaderSeesMore);
        Assert.Null(demo.Modules);
        Assert.Equal(visitor.Audiences.Select(audience => audience.Title), demo.Audiences.Select(audience => audience.Title));
        Assert.True(real.MayMarkInvalid);
        Assert.True(real.ReaderSeesMore);
    }

    [Fact]
    public async Task OnADemoBattery_ADemoAuthorityShouldKeepItsRole()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo).WithClustersAsync();
        await MakeDemoAsync(rig);

        var demo = (await PassportPagesTests.Pages(rig).DetailAsync(DemoMsa, PassportPagesTests.Identifier(rig)))!;

        Assert.True(demo.MayMarkInvalid);
        Assert.True(demo.ReaderSeesMore);
    }

    [Fact]
    public async Task ADemoClusterAdmin_ShouldNotManageARealBattery_EvenInTheirCluster()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo).WithClustersAsync();
        var pages = ClusterBatteryPagesTests.Pages(rig);
        var demoAdmin = ChangeSender.ClusterAdmin("demo.north.admin@example.test", BatteryTesting.North) with { DemoOnly = true };

        Assert.Null(await pages.BatteryAsync(demoAdmin, rig.PackId));
        Assert.NotNull(await pages.BatteryAsync(ChangeSender.ClusterAdmin("north.admin@example.test", BatteryTesting.North), rig.PackId));

        await MakeDemoAsync(rig);
        Assert.NotNull(await pages.BatteryAsync(demoAdmin, rig.PackId));
    }
}
