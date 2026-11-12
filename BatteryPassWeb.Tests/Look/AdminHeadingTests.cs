using BatteryPassWeb.Tests.Dpp;

namespace BatteryPassWeb.Tests.Look;

/// <summary>Step 3e: one admin heading that stays true, the tabs in the order of the work, Help in the top bar only, and
/// the calmer finish in the team pages' stylesheet (decision 27; site.css is not changed).</summary>
public sealed class AdminHeadingTests
{
    private static string Read(params string[] parts) => File.ReadAllText(DppTestFiles.RepoFile(parts));

    private static readonly string Tabs = Read("web", "Views", "Shared", "_AdminTabs.cshtml");

    private static readonly string[] AdminPages =
    [
        "Admin/AuditLog.cshtml",
        "Admin/Clusters.cshtml",
        "Batteries/Batteries.cshtml",
        "ProductData/Families.cshtml",
        "ProductData/SitesAndOperators.cshtml",
    ];

    [Fact]
    public void Heading_ShouldBeSharedByEveryAdminTab()
    {
        Assert.Contains("<h1>Administration</h1>", Tabs, StringComparison.Ordinal);
        Assert.Contains("<p>Batteries, product data, clusters, users and API tokens.</p>", Tabs, StringComparison.Ordinal);

        foreach (var page in AdminPages)
        {
            var markup = Read(["web", "Views", .. page.Split('/')]);
            Assert.Contains("_AdminTabs", markup, StringComparison.Ordinal);
            Assert.DoesNotContain(">Administration</h1>", markup, StringComparison.Ordinal);
            Assert.DoesNotContain("passport snapshots", markup, StringComparison.Ordinal);
            Assert.DoesNotContain("Manage batteries", markup, StringComparison.Ordinal);
            Assert.DoesNotContain("Manage battery families", markup, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void Tabs_ShouldFollowTheWork()
    {
        var order = new[]
        {
            "href=\"/admin/batteries\">Batteries</a>",
            "href=\"/admin/families\">Battery families</a>",
            "href=\"/admin/sites-and-operators\">Sites and operators</a>",
            "href=\"/admin/clusters?tab=clusters\">Clusters</a>",
            "href=\"/admin/clusters?tab=users\">Users</a>",
            "href=\"/admin/clusters?tab=api-token-management\">API tokens</a>",
            "href=\"/admin/audit\">Audit log</a>",
        };

        var positions = order.Select(tab => Tabs.IndexOf(tab, StringComparison.Ordinal)).ToList();
        Assert.DoesNotContain(-1, positions);
        Assert.Equal(positions.Order(), positions);
        Assert.DoesNotContain("Registered clusters", Tabs, StringComparison.Ordinal);
        Assert.DoesNotContain("API Token Management", Tabs, StringComparison.Ordinal);
    }

    [Fact]
    public void Help_ShouldBeInTheTopBarOnly()
    {
        Assert.DoesNotContain("/help", Tabs, StringComparison.Ordinal);
        Assert.DoesNotContain(">Help<", Tabs, StringComparison.Ordinal);
        Assert.Contains("<a href=\"/help\">Help</a>", Read("web", "Views", "Shared", "_Layout.cshtml"), StringComparison.Ordinal);
    }

    [Fact]
    public void Finish_ShouldLiveInTheTeamStylesheet()
    {
        var finish = Read("web", "wwwroot", "css", "bp-product-data.css");
        var site = Read("web", "wwwroot", "css", "site.css");

        Assert.Contains("/* ==== Admin, cluster admin and help pages (step 3e)", finish, StringComparison.Ordinal);
        foreach (var rule in new[] { ".bp-admin-heading h1", ".bp-tab-intro", ".bp-soft-tag", ".bp-row > summary", ".bp-row-panel", ".bp-danger-zone", ".bp-help-layout" })
        {
            Assert.Contains(rule, finish, StringComparison.Ordinal);
            Assert.DoesNotContain(rule, site, StringComparison.Ordinal);
        }

        Assert.Contains("~/css/bp-product-data.css", Read("web", "Views", "Admin", "Clusters.cshtml"), StringComparison.Ordinal);
    }
}
