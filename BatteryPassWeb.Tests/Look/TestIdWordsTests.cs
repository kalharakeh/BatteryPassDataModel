using BatteryPassWeb.Dpp.Identifiers;
using BatteryPassWeb.Tests.Dpp;

namespace BatteryPassWeb.Tests.Look;

/// <summary>
/// Decision 82: a site with the demo switched off still makes trial batteries with test IDs, so the pages call them test
/// IDs, never demo IDs, and say what removes them ("Clear demo data", which keeps its name and stays on offer), not
/// "when the demo data is reset".
/// </summary>
public sealed class TestIdWordsTests
{
    private static string Read(params string[] path) => File.ReadAllText(DppTestFiles.RepoFile(path));

    [Fact]
    public void ThePages_ShouldCallThemTestIds_AndSayWhatRemovesThem()
    {
        var index = Read("web", "Views", "BatteryIds", "Index.cshtml");
        var rows = Read("web", "Views", "BatteryIds", "_IdRows.cshtml");
        var release = Read("web", "Views", "BatteryIds", "Release.cshtml");
        var newBattery = Read("web", "Views", "Batteries", "NewBattery.cshtml");
        var controller = Read("web", "Controllers", "BatteryIdsController.cs");
        var guide = Read("web", "Views", "Help", "Guide.cshtml");

        Assert.Contains("Test IDs aren't counted.", index, StringComparison.Ordinal);
        Assert.Contains("<span>Show test IDs</span>", index, StringComparison.Ordinal);
        Assert.Contains("\"Clear demo data\" on the Clusters tab removes them with their batteries.", index, StringComparison.Ordinal);
        Assert.Contains("<span class=\"bp-id-demo\">Test</span>", index, StringComparison.Ordinal);
        Assert.Contains("<span class=\"bp-id-demo\">Test</span>", rows, StringComparison.Ordinal);
        Assert.Contains("\" · test IDs\"", release, StringComparison.Ordinal);
        Assert.Contains("label=\"Test IDs (marked TEST; removed by Clear demo data)\"", newBattery, StringComparison.Ordinal);
        Assert.Contains("\\\"Clear demo data\\\" on the Clusters tab removes them, with the test batteries made from them.", controller, StringComparison.Ordinal);
        Assert.Contains("Test IDs show only with \"Show test IDs\" ticked", guide, StringComparison.Ordinal);
        Assert.Equal("a test battery takes a test ID and a real battery a real ID; pick one of the same kind", BatteryIdPool.SwapNeedsSameKind);

        foreach (var text in new[] { index, rows, release, newBattery, controller })
        {
            Assert.DoesNotContain("Demo IDs", text, StringComparison.Ordinal);
            Assert.DoesNotContain("demo IDs", text, StringComparison.Ordinal);
            Assert.DoesNotContain("demo ID ", text, StringComparison.Ordinal);
            Assert.DoesNotContain(">Demo<", text, StringComparison.Ordinal);
            Assert.DoesNotContain("reset or cleared", text, StringComparison.Ordinal);
            Assert.DoesNotContain("Reset demo data", text, StringComparison.Ordinal); // not there when the demo is off
        }

        // The demo customers' rule is said only where there are demo customers (the demo-off walk-through, decision 82).
        Assert.Contains("@if (Model.Clusters.Any(cluster => cluster.IsDemo)) { <text> A battery with a real ID can't go to a demo customer.</text> }", newBattery, StringComparison.Ordinal);

        Assert.DoesNotContain("Show demo IDs", guide, StringComparison.Ordinal);
        Assert.DoesNotContain("when the demo data is reset or cleared", guide, StringComparison.Ordinal);
    }
}
