using BatteryPassWeb.Tests.Dpp;

namespace BatteryPassWeb.Tests.Look;

/// <summary>Step 3e: the cluster admins' Users and API tokens pages, on the same layout as the team's tabs and limited to
/// the clusters they run. Saving a name or a password names one of the person's memberships and keeps its role (before,
/// those two forms sent no cluster, so the save was refused without a word).</summary>
public sealed class ClusterAdminPagesTests
{
    private static string Read(params string[] parts) => File.ReadAllText(DppTestFiles.RepoFile(parts));

    private static readonly string Tabs = Read("web", "Views", "Shared", "_ClusterAdminTabs.cshtml");
    private static readonly string Users = Read("web", "Views", "ClusterAdmin", "Users.cshtml");
    private static readonly string Tokens = Read("web", "Views", "ClusterAdmin", "ApiTokens.cshtml");

    [Fact]
    public void Heading_ShouldNameTheClustersAndTheTabs()
    {
        Assert.Contains("<h1>Cluster admin</h1>", Tabs, StringComparison.Ordinal);
        Assert.Contains("<p>Batteries, users and API tokens for @(string.IsNullOrWhiteSpace(scope) ? \"the clusters you run\" : scope).</p>", Tabs, StringComparison.Ordinal); // a Batteries tab since R6
        Assert.Contains("href=\"/cluster-admin/users\">Users</a>", Tabs, StringComparison.Ordinal);
        Assert.Contains("href=\"/cluster-admin/api-tokens\">API tokens</a>", Tabs, StringComparison.Ordinal);
        foreach (var page in new[] { Users, Tokens })
        {
            Assert.Contains("ViewData[\"ClusterAdminScope\"] = Model.Clusters.Count == 1 ? Model.Clusters[0].Name : null;", page, StringComparison.Ordinal);
            Assert.DoesNotContain("<h1>", page, StringComparison.Ordinal);
            Assert.Contains("@await Html.PartialAsync(\"_ConfirmModal\")", page, StringComparison.Ordinal);
            Assert.Contains("<script src=\"~/js/bp-admin-pages.js\" asp-append-version=\"true\"></script>", page, StringComparison.Ordinal);
            Assert.DoesNotContain("<table", page, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void AddUser_ShouldPutThemStraightIntoOneOfYourClusters()
    {
        var window = Users[Users.IndexOf("<dialog id=\"add-user-dialog\"", StringComparison.Ordinal)..Users.IndexOf("</dialog>", StringComparison.Ordinal)];

        Assert.Contains("action=\"/cluster-admin/users/save\"", window, StringComparison.Ordinal);
        Assert.Contains("<select name=\"clusterId\" required>", window, StringComparison.Ordinal);
        Assert.Contains("<select name=\"role\">", window, StringComparison.Ordinal);
        Assert.Contains("[\"member\"] = \"User\"", Users, StringComparison.Ordinal);
        Assert.Contains("[\"clusterAdmin\"] = \"Cluster admin\"", Users, StringComparison.Ordinal);
    }

    [Fact]
    public void SavingANameOrAPassword_ShouldKeepTheirClusterAndRole()
    {
        var row = Users[Users.IndexOf("<details class=\"bp-row\" data-open-user=\"@user.Email\"", StringComparison.Ordinal)..Users.IndexOf("</details>", StringComparison.Ordinal)];
        var keepFields = "<input type=\"hidden\" name=\"clusterId\" value=\"@keep?.ClusterId\" />";

        Assert.Contains("var keep = memberships.FirstOrDefault();", Users, StringComparison.Ordinal);
        Assert.Equal(2, row.Split(keepFields).Length - 1);
        Assert.Equal(2, row.Split("<input type=\"hidden\" name=\"role\" value=\"@keep?.Role\" />").Length - 1);
        Assert.Contains("action=\"/cluster-admin/users/delete-membership\"", row, StringComparison.Ordinal);
        Assert.DoesNotContain("delete-account", Users, StringComparison.Ordinal);
        Assert.Contains("only the team changes their account", row, StringComparison.Ordinal);
    }

    [Fact]
    public void Tokens_ShouldOfferOnlyWhatAClusterTokenDoes()
    {
        var window = Tokens[Tokens.IndexOf("<dialog id=\"create-token-dialog\"", StringComparison.Ordinal)..Tokens.IndexOf("</dialog>", StringComparison.Ordinal)];

        Assert.Contains("action=\"/cluster-admin/api-tokens/create\"", window, StringComparison.Ordinal);
        Assert.Contains("<strong>Read</strong><small>Reads the passports of your cluster's batteries.</small>", window, StringComparison.Ordinal);
        Assert.Contains("<strong>Read + write</strong><small>Also sends their live values.</small>", window, StringComparison.Ordinal);
        Assert.DoesNotContain("globalAccess", window, StringComparison.Ordinal);
        Assert.Contains("checked=\"@(Model.Clusters.Count == 1)\"", window, StringComparison.Ordinal);
        Assert.Contains("@await Html.PartialAsync(\"_ApiTokenList\", new BatteryPassWeb.Models.ViewModels.ApiTokenListViewModel(Model.ApiTokens, \"/cluster-admin/api-tokens\", false, false, \"No API tokens for your clusters yet.\"))", Tokens, StringComparison.Ordinal);
    }

    [Fact]
    public void RemovingATokenHere_ShouldSayItOnlySwitchesItOff()
    {
        var list = Read("web", "Views", "Shared", "_ApiTokenList.cshtml");
        var controller = Read("web", "Controllers", "ClusterAdminController.cs");

        Assert.Contains("@if (Model.CanDelete)", list, StringComparison.Ordinal);
        Assert.Contains(">Deactivate token</button>", list, StringComparison.Ordinal);
        Assert.Contains("Only the team can switch it on again or delete it.", list, StringComparison.Ordinal);
        Assert.Contains("TempData[\"StatusMessage\"] = \"API token deactivated.\";", controller, StringComparison.Ordinal);
        Assert.DoesNotContain("TempData[\"StatusMessage\"] = \"API token deleted.\";", controller, StringComparison.Ordinal);
    }
}
