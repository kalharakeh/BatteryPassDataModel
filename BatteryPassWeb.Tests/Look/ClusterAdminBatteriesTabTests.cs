using BatteryPassWeb.Tests.Dpp;

namespace BatteryPassWeb.Tests.Look;

/// <summary>
/// Redesign R6, the owner's choice 1 (3 Nov 2026): cluster admins had no tab for their batteries (the old app had
/// "Managed passports"); a Batteries tab, first, opens the Registry, where their batteries are (decision 30).
/// </summary>
public sealed class ClusterAdminBatteriesTabTests
{
    [Fact]
    public void TheClusterAdminTabs_ShouldStartWithBatteries_OpeningTheRegistry()
    {
        var tabs = File.ReadAllText(DppTestFiles.RepoFile("web", "Views", "Shared", "_ClusterAdminTabs.cshtml"));

        Assert.Contains("<a class=\"@TabClass(\"batteries\")\" href=\"/registry\">Batteries</a>", tabs, StringComparison.Ordinal);
        Assert.Contains("<option value=\"/registry\">Batteries</option>", tabs, StringComparison.Ordinal); // the phone's section picker too
        Assert.True(tabs.IndexOf(">Batteries</a>", StringComparison.Ordinal) < tabs.IndexOf(">Users</a>", StringComparison.Ordinal));
        Assert.Contains("Batteries, users and API tokens for", tabs, StringComparison.Ordinal);
    }
}
