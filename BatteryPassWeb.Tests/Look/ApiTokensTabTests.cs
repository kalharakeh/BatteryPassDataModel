using BatteryPassWeb.Services;
using BatteryPassWeb.Tests.Demo;
using BatteryPassWeb.Tests.Dpp;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Bson;

namespace BatteryPassWeb.Tests.Look;

/// <summary>Step 3e: the API tokens tab. "Create token" opens a window that says what each permission does, the list is
/// shared with the cluster admins' page, names wrap with the ID under them, a row opens for regenerate, deactivate and
/// delete, and the two bulk buttons sit under "More". The demo tokens are named after their cluster once.</summary>
[Collection(MongoCollection.Name)]
public sealed class ApiTokensTabTests(MongoFixture mongo)
{
    private static string Read(params string[] parts) => File.ReadAllText(DppTestFiles.RepoFile(parts));

    private static string Tab
    {
        get
        {
            var page = Read("web", "Views", "Admin", "Clusters.cshtml");
            var start = page.IndexOf("@if (selectedTab == \"api-token-management\")", StringComparison.Ordinal);
            var end = page.IndexOf("@await Html.PartialAsync(\"_ConfirmModal\")", StringComparison.Ordinal);
            Assert.True(start >= 0 && end > start);
            return page[start..end];
        }
    }

    private static string List => Read("web", "Views", "Shared", "_ApiTokenList.cshtml");

    [Fact]
    public void CreateToken_ShouldSayWhatEachPermissionDoes()
    {
        var tab = Tab;
        var window = tab[tab.IndexOf("<dialog id=\"create-token-dialog\"", StringComparison.Ordinal)..tab.IndexOf("</dialog>", StringComparison.Ordinal)];

        Assert.Contains("data-open-dialog=\"create-token-dialog\">Create token</button>", tab, StringComparison.Ordinal);
        Assert.Contains("action=\"/admin/api/tokens/create\"", window, StringComparison.Ordinal);
        Assert.Contains("name=\"accessMode\" value=\"read\" checked /><strong>Read</strong><small>Reads the passports of its batteries.</small>", window, StringComparison.Ordinal);
        Assert.Contains("name=\"accessMode\" value=\"readWrite\" /><strong>Read + write</strong><small>Also sends live values. For all batteries, it also creates batteries.</small>", window, StringComparison.Ordinal);
        Assert.Contains("name=\"accessMode\" value=\"sign\" /><strong>Passport lifecycle</strong><small>Reads, and for all batteries publishes passports.</small>", window, StringComparison.Ordinal);
        Assert.Contains("name=\"accessMode\" value=\"readWriteSign\" /><strong>Full access</strong><small>All of the above.</small>", window, StringComparison.Ordinal);
        Assert.Contains("name=\"globalAccess\" value=\"off\"", window, StringComparison.Ordinal);
        Assert.Contains("name=\"globalAccess\" value=\"on\"", window, StringComparison.Ordinal);
        Assert.Contains("name=\"clusterIds\" value=\"@cluster.ClusterId\"", window, StringComparison.Ordinal);
        Assert.DoesNotContain("Full access and Passport Lifecycle create and publish batteries", tab, StringComparison.Ordinal);
    }

    [Fact]
    public void TheList_ShouldBeSharedWithTheClusterAdminsPage()
    {
        Assert.Contains("@await Html.PartialAsync(\"_ApiTokenList\", new BatteryPassWeb.Models.ViewModels.ApiTokenListViewModel(Model.ApiTokens, \"/admin/api/tokens\", true, \"No API tokens yet.\"))", Tab, StringComparison.Ordinal);
        Assert.DoesNotContain("<table", Tab, StringComparison.Ordinal);
    }

    [Fact]
    public void EachToken_ShouldWrapItsNameAndOpenForItsActions()
    {
        var list = List;
        var row = list[list.IndexOf("<details class=\"bp-row\" data-token-row", StringComparison.Ordinal)..];

        Assert.Contains("<strong>@name</strong>\n                    <code>@token.TokenId</code>", row.Replace("\r\n", "\n"), StringComparison.Ordinal);
        Assert.Contains("<span class=\"bp-soft-tag\">@access</span>", row, StringComparison.Ordinal);
        Assert.Contains("<span class=\"bp-soft-tag is-off\">Inactive</span>", row, StringComparison.Ordinal);
        Assert.Contains("\"Never\"", row, StringComparison.Ordinal);
        Assert.Contains("@await Html.PartialAsync(\"_CopyIdButton\", token.TokenId)", row, StringComparison.Ordinal);
        Assert.Contains("action=\"@Model.ActionBase/regenerate\"", row, StringComparison.Ordinal);
        Assert.Contains("data-confirm-title=\"Regenerate token\"", row, StringComparison.Ordinal);
        Assert.Contains("@if (Model.CanSetActive)", row, StringComparison.Ordinal);
        Assert.Contains("action=\"@Model.ActionBase/set-active\"", row, StringComparison.Ordinal);
        Assert.True(row.IndexOf("<div class=\"bp-danger-zone\">", StringComparison.Ordinal) > row.IndexOf("set-active", StringComparison.Ordinal));
        Assert.Contains("action=\"@Model.ActionBase/delete\"", row, StringComparison.Ordinal);
        Assert.Contains("data-confirm-title=\"Delete API token\"", row, StringComparison.Ordinal);
        Assert.Contains("placeholder=\"Search name, ID, permission or cluster\" data-list-search", list, StringComparison.Ordinal);
        Assert.Contains("\"sign\" => \"Passport lifecycle\"", list, StringComparison.Ordinal);
        Assert.DoesNotContain("Passport Lifecycle", list, StringComparison.Ordinal);
    }

    [Fact]
    public void TheBulkButtons_ShouldWaitUnderMore()
    {
        var tab = Tab;
        var more = tab[tab.IndexOf("<details class=\"bp-card bp-console-form-card mt-4 p-3\" data-token-bulk-actions>", StringComparison.Ordinal)..];

        Assert.Contains("<summary class=\"fw-semibold\">More: tokens for every cluster at once</summary>", more, StringComparison.Ordinal);
        Assert.Contains("action=\"/admin/api/tokens/generate-per-cluster\"", more, StringComparison.Ordinal);
        Assert.Contains("action=\"/admin/api/tokens/cleanup-generated\"", more, StringComparison.Ordinal);
        Assert.Contains("data-confirm-title=\"Remove unused generated tokens\"", more, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TheDemoTokens_ShouldNameTheirClusterOnce()
    {
        using var rig = await DemoRig.StartAsync(mongo);
        await rig.Demo.SeedAsync(DemoRig.Who);

        var names = (await rig.Services.GetRequiredService<ExternalApiRepository>().ListTokensAsync()).Select(token => token["name"].AsString).ToList();

        Assert.Contains("Demo North Operations (read)", names);
        Assert.Contains("Demo North Operations (read and write live values)", names);
        Assert.Contains("Demo team (full access)", names);
        Assert.DoesNotContain(names, name => name.StartsWith("Demo Demo", StringComparison.Ordinal));
    }
}
