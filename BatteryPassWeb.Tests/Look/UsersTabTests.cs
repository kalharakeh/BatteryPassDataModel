using BatteryPassWeb.Tests.Dpp;

namespace BatteryPassWeb.Tests.Look;

/// <summary>Step 3e: the Users tab. "Add user" opens a window, the list shows each person's access and clusters and can be
/// searched, a row opens to name and access, clusters, a new password and deleting the account, and the sign-out time is a
/// small card at the end.</summary>
public sealed class UsersTabTests
{
    private static string Page => File.ReadAllText(DppTestFiles.RepoFile("web", "Views", "Admin", "Clusters.cshtml"));

    private static string Tab
    {
        get
        {
            var page = Page;
            var start = page.IndexOf("@if (selectedTab == \"users\")", StringComparison.Ordinal);
            var end = page.IndexOf("@if (selectedTab == \"api-token-management\")", StringComparison.Ordinal);
            Assert.True(start >= 0 && end > start);
            return page[start..end];
        }
    }

    [Fact]
    public void AddUser_ShouldOpenAWindowWithTodaysFields()
    {
        var tab = Tab;
        var window = tab[tab.IndexOf("<dialog id=\"add-user-dialog\"", StringComparison.Ordinal)..tab.IndexOf("</dialog>", StringComparison.Ordinal)];

        Assert.Contains("data-open-dialog=\"add-user-dialog\">Add user</button>", tab, StringComparison.Ordinal);
        Assert.Contains("action=\"/admin/clusters/save-user\"", window, StringComparison.Ordinal);
        foreach (var field in new[] { "name=\"name\"", "name=\"email\"", "name=\"password\"", "name=\"passwordConfirmation\"", "name=\"systemRole\"" })
        {
            Assert.Contains(field, window, StringComparison.Ordinal);
        }

        Assert.Contains("selected=\"@(option.Key.Equals(\"member\", StringComparison.OrdinalIgnoreCase))\"", window, StringComparison.Ordinal);
        Assert.Contains("@await Html.PartialAsync(\"_PasswordReveal\")", window, StringComparison.Ordinal);
    }

    [Fact]
    public void EachUser_ShouldShowAccessAndClustersAndOpenInPlace()
    {
        var tab = Tab;
        var row = tab[tab.IndexOf("<details class=\"bp-row\" data-open-user=\"@user.Email\"", StringComparison.Ordinal)..tab.IndexOf("</details>", StringComparison.Ordinal)];

        Assert.Contains("data-search-text=\"@searchText\"", row, StringComparison.Ordinal);
        Assert.Contains("<span class=\"bp-soft-tag\">@AccessTag(selectedGlobalAccess)</span>", row, StringComparison.Ordinal);
        Assert.Contains("@ClusterName(membership.ClusterId)@(membership.Role == \"clusterAdmin\" ? \" · admin\" : \"\")", row, StringComparison.Ordinal);
        Assert.Contains("<h3>Name and access</h3>", row, StringComparison.Ordinal);
        Assert.Contains("<h3>Clusters</h3>", row, StringComparison.Ordinal);
        Assert.Contains("<h3>New password</h3>", row, StringComparison.Ordinal);
        Assert.Equal(2, row.Split("action=\"/admin/clusters/save-user\"").Length - 1);
        Assert.Contains("action=\"/admin/clusters/delete-user\"", row, StringComparison.Ordinal);
        Assert.Contains("action=\"/admin/clusters/assign-user\"", row, StringComparison.Ordinal);
        Assert.True(row.IndexOf("<div class=\"bp-danger-zone\">", StringComparison.Ordinal) > row.IndexOf("<h3>New password</h3>", StringComparison.Ordinal));
        Assert.Contains("action=\"/admin/clusters/delete-account\"", row, StringComparison.Ordinal);
        Assert.DoesNotContain("<table", tab, StringComparison.Ordinal);
    }

    [Fact]
    public void TheList_ShouldBeSearchable()
    {
        var tab = Tab;
        var script = File.ReadAllText(DppTestFiles.RepoFile("web", "wwwroot", "js", "bp-admin-pages.js"));

        Assert.Contains("<section class=\"bp-card bp-list-card mt-4\" data-list aria-label=\"Users\">", tab, StringComparison.Ordinal);
        Assert.Contains("placeholder=\"Search name, email, access or cluster\" data-list-search", tab, StringComparison.Ordinal);
        Assert.Contains("data-list-no-results hidden>No users match the search.</p>", tab, StringComparison.Ordinal);
        Assert.Contains("document.querySelectorAll('[data-list-search]')", script, StringComparison.Ordinal);
        Assert.Contains("document.querySelectorAll('details[data-open-user]')", script, StringComparison.Ordinal);
        Assert.DoesNotContain("data-user-drawer-toggle", Page, StringComparison.Ordinal);
    }

    [Fact]
    public void SigningOut_ShouldBeASmallCardAtTheEnd()
    {
        var tab = Tab;

        Assert.True(tab.IndexOf("<h2>Signing out</h2>", StringComparison.Ordinal) > tab.IndexOf("</details>", StringComparison.Ordinal));
        Assert.Contains("action=\"/admin/clusters/session-timeout\"", tab, StringComparison.Ordinal);
        Assert.Contains("name=\"sessionTimeoutMinutes\" aria-label=\"Sign out after\"", tab, StringComparison.Ordinal);
    }

    [Fact]
    public void Labels_ShouldBeInSentenceCase()
    {
        var page = Page;

        Assert.Contains("[\"member\"] = \"Cluster member (sees only their clusters' batteries)\"", page, StringComparison.Ordinal);
        Assert.Contains("[\"admin\"] = \"Global admin (the team)\"", page, StringComparison.Ordinal);
        Assert.Contains("[\"clusterAdmin\"] = \"Cluster admin\"", page, StringComparison.Ordinal);
        Assert.Contains("[\"member\"] = \"User\"", page, StringComparison.Ordinal);
        Assert.DoesNotContain("Normal User", page, StringComparison.Ordinal);
        Assert.DoesNotContain("\"Global Admin\";", page, StringComparison.Ordinal);
        Assert.Contains("data-password-reveal", File.ReadAllText(DppTestFiles.RepoFile("web", "Views", "Shared", "_PasswordReveal.cshtml")), StringComparison.Ordinal);
    }
}
