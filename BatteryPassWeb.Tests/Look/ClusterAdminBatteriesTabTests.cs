using BatteryPassWeb.Tests.Dpp;

namespace BatteryPassWeb.Tests.Look;

/// <summary>
/// Redesign R6, the owner's choice 1 (3 Nov 2026): cluster admins had no tab for their batteries (the old app had
/// "Managed passports"); a Batteries tab, first. Since finding U14 it opens the area's own list of their batteries, so
/// the tabs stay (it used to open the Registry, where they were gone).
/// </summary>
public sealed class ClusterAdminBatteriesTabTests
{
    [Fact]
    public void TheClusterAdminTabs_ShouldStartWithBatteries_InsideTheArea()
    {
        var tabs = File.ReadAllText(DppTestFiles.RepoFile("web", "Views", "Shared", "_ClusterAdminTabs.cshtml"));

        Assert.Contains("<a class=\"@TabClass(\"batteries\")\" href=\"/cluster-admin/batteries\">Batteries</a>", tabs, StringComparison.Ordinal);
        Assert.Contains("<option value=\"/cluster-admin/batteries\" selected=\"@IsSelected(\"batteries\")\">Batteries</option>", tabs, StringComparison.Ordinal); // the phone's section picker too
        Assert.DoesNotContain("/registry", tabs, StringComparison.Ordinal);
        Assert.True(tabs.IndexOf(">Batteries</a>", StringComparison.Ordinal) < tabs.IndexOf(">Users</a>", StringComparison.Ordinal));
        Assert.Contains("Batteries, users and API tokens for", tabs, StringComparison.Ordinal);
    }
}
