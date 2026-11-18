using System.Net;
using System.Text.RegularExpressions;
using BatteryPassWeb.Dpp.Demo;
using BatteryPassWeb.Tests.Demo;
using BatteryPassWeb.Tests.Dpp;
using Microsoft.Extensions.DependencyInjection;

namespace BatteryPassWeb.Tests.Look;

/// <summary>
/// Findings U14 and U30: a customer's admin's "Admin" opens their batteries first, inside the area's tabs, and the tabs stay
/// on each of its pages; their customer ID, which a hand-over asks for, is shown with a copy button on the area's pages
/// and on the Registry.
/// </summary>
[Collection(MongoCollection.Name)]
public sealed partial class CustomerAdminAreaTests(MongoFixture mongo)
{
    private const string NorthAdmin = "demo.north.admin@example.test";
    private const string NorthUser = "demo.north.user@example.test";

    private static async Task<ShellSite> SiteAsync(MongoFixture mongo)
    {
        var site = await ShellSite.StartAsync(mongo.NewContext());
        await site.Services.GetRequiredService<DemoDataService>().ResetAsync(DemoRig.Who);
        return site;
    }

    [GeneratedRegex("class=\"bp-battery-link[^\"]*\"[^>]*>([^<]+)</a>")]
    private static partial Regex Serial();

    private static string[] Serials(string body) => Serial().Matches(body).Select(match => match.Groups[1].Value).Order(StringComparer.Ordinal).ToArray();

    [Fact]
    public async Task Admin_ShouldOpenOnTheirBatteries_InsideTheTabs()
    {
        await using var site = await SiteAsync(mongo);

        var (status, _, location) = await site.GetAsync("/cluster-admin", NorthAdmin);
        Assert.Equal(HttpStatusCode.Redirect, status);
        Assert.Equal("/cluster-admin/batteries", location);

        var (listStatus, list, _) = await site.GetAsync("/cluster-admin/batteries", NorthAdmin);
        var (_, registry, _) = await site.GetAsync("/registry", NorthAdmin);

        Assert.Equal(HttpStatusCode.OK, listStatus);
        Assert.Contains("<h1>Admin</h1>", list, StringComparison.Ordinal);
        Assert.Contains("<a class=\"bp-tab bp-tab-active\" href=\"/cluster-admin/batteries\">Batteries</a>", list, StringComparison.Ordinal);
        Assert.Contains("href=\"/cluster-admin/users\">Users</a>", list, StringComparison.Ordinal);
        Assert.Contains("aria-current=\"page\">Admin</a>", list, StringComparison.Ordinal);
        Assert.NotEmpty(Serials(list));
        Assert.Equal(Serials(registry), Serials(list)); // the same batteries as their Registry lists
        Assert.Contains("action=\"/cluster-admin/batteries\"", list, StringComparison.Ordinal); // its own search
    }

    [Theory]
    [InlineData("/cluster-admin/batteries")]
    [InlineData("/cluster-admin/users")]
    [InlineData("/cluster-admin/api-tokens")]
    public async Task EachPageOfTheArea_ShouldKeepTheTabs_AndShowTheCustomerId(string path)
    {
        await using var site = await SiteAsync(mongo);

        var (status, body, _) = await site.GetAsync(path, NorthAdmin);

        Assert.Equal(HttpStatusCode.OK, status);
        Assert.Contains("href=\"/cluster-admin/batteries\">Batteries</a>", body, StringComparison.Ordinal);
        Assert.Contains("href=\"/cluster-admin/api-tokens\">API tokens</a>", body, StringComparison.Ordinal);
        Assert.Contains("Your customer ID:", body, StringComparison.Ordinal);
        Assert.Contains("<code>demo-north</code>", body, StringComparison.Ordinal);
        Assert.Contains("data-copy-value=\"demo-north\"", body, StringComparison.Ordinal);
        Assert.DoesNotContain("<option value=\"/registry\">", body, StringComparison.Ordinal); // the phone picker stays in the area too
    }

    [Fact]
    public async Task TheRegistry_ShouldShowACustomersAdminTheirCustomerId_AndNobodyElse()
    {
        await using var site = await SiteAsync(mongo);

        var (_, admin, _) = await site.GetAsync("/registry", NorthAdmin);
        var (_, user, _) = await site.GetAsync("/registry", NorthUser);
        var (_, msa, _) = await site.GetAsync("/registry", "demo.msa@example.test");

        Assert.Contains("Your customer ID:", admin, StringComparison.Ordinal);
        Assert.Contains("<code>demo-north</code>", admin, StringComparison.Ordinal);
        Assert.Contains("data-copy-value=\"demo-north\"", admin, StringComparison.Ordinal);
        Assert.DoesNotContain("Your customer ID", user, StringComparison.Ordinal);
        Assert.DoesNotContain("Your customer ID", msa, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ACustomersUser_ShouldBeKeptOutOfTheArea()
    {
        await using var site = await SiteAsync(mongo);

        var (status, _, location) = await site.GetAsync("/cluster-admin/batteries", NorthUser);

        Assert.Equal(HttpStatusCode.Redirect, status);
        Assert.Equal("/not-allowed", location);
    }

    [Fact]
    public void TheHandOverWindow_ShouldSayWhereTheReceiverFindsTheirId()
    {
        var window = File.ReadAllText(DppTestFiles.RepoFile("web", "Views", "ClusterAdmin", "_HandOverDialog.cshtml"));

        Assert.Contains("Ask the receiving customer for their customer ID: their admins find it at the top of their Admin page.", window, StringComparison.Ordinal);
        Assert.Contains("<span>Receiving customer ID</span>", window, StringComparison.Ordinal);
    }
}
