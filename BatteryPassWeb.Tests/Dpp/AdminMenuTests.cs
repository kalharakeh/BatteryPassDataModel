namespace BatteryPassWeb.Tests.Dpp;

/// <summary>The admin menu after 2d-2: the new pages under the existing entries, no temporary or duplicate entries (decision 23).</summary>
public sealed class AdminMenuTests
{
    private static readonly string Menu = File.ReadAllText(DppTestFiles.RepoFile("web", "Views", "Shared", "_AdminTabs.cshtml"));

    [Theory]
    [InlineData("/admin/batteries", "Batteries")]
    [InlineData("/admin/families", "Battery families")]
    [InlineData("/admin/sites-and-operators", "Sites and operators")]
    public void NewPages_ShouldBeInBothTheTabsAndThePhoneMenu(string href, string label)
    {
        Assert.Contains($"href=\"{href}\">{label}</a>", Menu, StringComparison.Ordinal);
        Assert.Contains($"<option value=\"{href}\"", Menu, StringComparison.Ordinal);
    }

    [Fact]
    public void RemovedPages_ShouldHaveNoEntry()
    {
        Assert.DoesNotContain("Editable fields", Menu, StringComparison.Ordinal);
        Assert.DoesNotContain("tab=local-editable-fields", Menu, StringComparison.Ordinal);
        Assert.DoesNotContain("tab=products", Menu, StringComparison.Ordinal);
        Assert.DoesNotContain("Battery cluster assignments", Menu, StringComparison.Ordinal);
        Assert.DoesNotContain("tab=batteries", Menu, StringComparison.Ordinal);
        Assert.DoesNotContain("tab=battery\"", Menu, StringComparison.Ordinal);
    }

    [Fact]
    public void EveryLabel_ShouldAppearOnceInEachMenu()
    {
        foreach (var label in new[] { "Battery families", "Sites and operators", "Batteries", "Users" })
        {
            Assert.Equal(2, Menu.Split($">{label}<").Length - 1); // one tab, one phone-menu option
        }
    }
}
