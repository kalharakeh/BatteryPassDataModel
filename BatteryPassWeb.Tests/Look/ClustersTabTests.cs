using BatteryPassWeb.Models.ViewModels;
using BatteryPassWeb.Tests.Demo;
using BatteryPassWeb.Tests.Dpp;
using Microsoft.AspNetCore.Mvc;

namespace BatteryPassWeb.Tests.Look;

/// <summary>Step 3e: the Clusters tab. "Add cluster" opens a window, each cluster is a row that opens in place (rename,
/// its users, delete; force delete in its own window), and the Demo data card comes after the real clusters.</summary>
[Collection(MongoCollection.Name)]
public sealed class ClustersTabTests(MongoFixture mongo)
{
    private static string Page => File.ReadAllText(DppTestFiles.RepoFile("web", "Views", "Admin", "Clusters.cshtml"));

    private static string Tab
    {
        get
        {
            var page = Page;
            var start = page.IndexOf("@if (selectedTab == \"clusters\")", StringComparison.Ordinal);
            var end = page.IndexOf("@if (selectedTab == \"users\")", StringComparison.Ordinal);
            Assert.True(start >= 0 && end > start);
            return page[start..end];
        }
    }

    [Fact]
    public void AddCluster_ShouldOpenAWindowThatKeepsARefusedName()
    {
        var tab = Tab;

        Assert.Contains("<button type=\"button\" class=\"bp-primary-button\" data-open-dialog=\"add-cluster-dialog\">Add cluster</button>", tab, StringComparison.Ordinal);
        Assert.Contains("<dialog id=\"add-cluster-dialog\"", tab, StringComparison.Ordinal);
        Assert.Contains("data-open-on-load=\"@(clusterCreateName.Length > 0 ? \"true\" : \"false\")\"", tab, StringComparison.Ordinal);
        Assert.Contains("action=\"/admin/clusters/create\"", tab, StringComparison.Ordinal);
        Assert.Contains("name=\"name\" value=\"@clusterCreateName\"", tab, StringComparison.Ordinal);
        Assert.Contains("name=\"clusterId\" value=\"@clusterCreateId\"", tab, StringComparison.Ordinal);
        Assert.Contains("formmethod=\"dialog\" formnovalidate", tab, StringComparison.Ordinal);
    }

    [Fact]
    public void EachCluster_ShouldBeARowThatOpensToRenameSeeUsersAndDelete()
    {
        var tab = Tab;
        var row = tab[tab.IndexOf("<details class=\"bp-row\" data-cluster-row", StringComparison.Ordinal)..tab.IndexOf("</details>", StringComparison.Ordinal)];

        Assert.Contains("<span class=\"bp-row-title\"><strong>@cluster.Name</strong><code>@cluster.ClusterId</code></span>", row, StringComparison.Ordinal);
        Assert.Contains("@Count(clusterMemberships.Count, \"user\", \"users\")", row, StringComparison.Ordinal);
        Assert.Contains("@Count(clusterTokenCount, \"token\", \"tokens\")", row, StringComparison.Ordinal);
        Assert.Contains("action=\"/admin/clusters/update\"", row, StringComparison.Ordinal);
        Assert.Contains("<span class=\"bp-soft-tag\">@MembershipRoleLabel(membership.Role)</span>", row, StringComparison.Ordinal);
        Assert.Contains("action=\"/admin/clusters/delete\"", row, StringComparison.Ordinal);
        Assert.Contains("data-confirm-action data-confirm-title=\"Delete cluster\"", row, StringComparison.Ordinal);
        Assert.Contains("<div class=\"bp-danger-zone\">", row, StringComparison.Ordinal);
        Assert.Contains("data-open-dialog=\"@forceDialogId\">Force delete…</button>", row, StringComparison.Ordinal);
        Assert.DoesNotContain("<table", tab, StringComparison.Ordinal);
    }

    [Fact]
    public void ForceDelete_ShouldAskForTheTypedSentenceInItsOwnWindow()
    {
        var tab = Tab;
        var window = tab[tab.IndexOf("<dialog id=\"@forceDialogId\"", StringComparison.Ordinal)..];

        Assert.Contains("action=\"/admin/clusters/force-delete\"", window, StringComparison.Ordinal);
        Assert.Contains("<em>I understand this deletes linked cluster data</em>", window, StringComparison.Ordinal);
        Assert.Contains("name=\"confirmation\" autocomplete=\"off\" required", window, StringComparison.Ordinal);
    }

    [Fact]
    public void DemoData_ShouldComeAfterTheClusters()
    {
        var tab = Tab;

        Assert.True(tab.IndexOf("data-demo-data-card", StringComparison.Ordinal) > tab.IndexOf("data-cluster-row", StringComparison.Ordinal));
        Assert.Contains("data-demo-data-state>@(Model.HasDemoData ? \"Loaded\" : \"Not loaded\")</span>", tab, StringComparison.Ordinal);
    }

    [Fact]
    public void ThePage_ShouldUseTheSharedScriptAndConfirmWindow()
    {
        var page = Page;
        var script = File.ReadAllText(DppTestFiles.RepoFile("web", "wwwroot", "js", "bp-admin-pages.js"));

        Assert.Contains("@await Html.PartialAsync(\"_ConfirmModal\")", page, StringComparison.Ordinal);
        Assert.Contains("<script src=\"~/js/bp-admin-pages.js\" asp-append-version=\"true\"></script>", page, StringComparison.Ordinal);
        Assert.DoesNotContain("data-confirm-modal hidden", page, StringComparison.Ordinal);
        Assert.Contains("document.querySelectorAll('[data-open-dialog]')", script, StringComparison.Ordinal);
        Assert.Contains("document.querySelectorAll('dialog[data-open-on-load=\"true\"]')", script, StringComparison.Ordinal);
        Assert.Contains("document.querySelectorAll('[data-confirm-action]')", script, StringComparison.Ordinal);
        Assert.Contains("data-confirm-modal hidden", File.ReadAllText(DppTestFiles.RepoFile("web", "Views", "Shared", "_ConfirmModal.cshtml")), StringComparison.Ordinal);
    }

    [Fact]
    public async Task TheTab_ShouldKnowEachTokensClusters()
    {
        using var rig = await DemoRig.StartAsync(mongo);
        await rig.Demo.SeedAsync(DemoRig.Who);

        var model = Assert.IsType<AdminClusterViewModel>(Assert.IsType<ViewResult>(await DemoCardTests.Controller(rig).Clusters("clusters", null, CancellationToken.None)).Model);

        Assert.Equal(2, model.ApiTokens.Count(token => !token.GlobalAccess && token.ClusterIds.Contains("demo-north")));
        Assert.Single(model.ApiTokens, token => token.GlobalAccess);
    }
}
