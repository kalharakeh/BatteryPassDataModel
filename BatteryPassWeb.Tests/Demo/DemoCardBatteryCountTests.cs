using BatteryPassWeb.Dpp.Demo;
using BatteryPassWeb.Tests.Dpp;

namespace BatteryPassWeb.Tests.Demo;

/// <summary>
/// The demo card on the Clusters page said "six batteries" by hand. It gives the demo's real battery count, taken from the
/// demo data: 8 batteries (the reset message counts 10, because a battery placed on the market again has a passport more).
/// </summary>
public sealed class DemoCardBatteryCountTests
{
    [Fact]
    public void TheDemoCard_ShouldGiveTheDemosRealBatteryCount()
    {
        var view = File.ReadAllText(DppTestFiles.RepoFile("web", "Views", "Admin", "Clusters.cshtml"));

        Assert.Equal(8, DemoDataService.BatteryCount);
        Assert.DoesNotContain("six batteries", view, StringComparison.Ordinal);
        Assert.Contains("families with @Model.DemoBatteryCount batteries in several states", view, StringComparison.Ordinal);
        Assert.Contains("DemoBatteryCount = BatteryPassWeb.Dpp.Demo.DemoDataService.BatteryCount,", File.ReadAllText(DppTestFiles.RepoFile("web", "Controllers", "AdminController.cs")), StringComparison.Ordinal);
    }
}
