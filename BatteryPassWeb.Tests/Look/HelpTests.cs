using BatteryPassWeb.Dpp.Pages;
using BatteryPassWeb.Dpp.Store;
using BatteryPassWeb.Tests.Demo;
using BatteryPassWeb.Tests.Dpp;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Bson;
using MongoDB.Driver;

namespace BatteryPassWeb.Tests.Look;

/// <summary>Step 3e: the Help is one place. Its contents list shows only the reader's parts, the team's workflow is a part
/// of it rather than a page under the admin tabs, the API reference keeps its page inside it, and the API examples use the
/// demo clusters and a demo battery, never a real one.</summary>
[Collection(MongoCollection.Name)]
public sealed class HelpTests(MongoFixture mongo)
{
    private static string Read(params string[] parts) => File.ReadAllText(DppTestFiles.RepoFile(parts));

    private static readonly string Contents = Read("web", "Views", "Shared", "_HelpContents.cshtml");
    private static readonly string Guide = Read("web", "Views", "Help", "Guide.cshtml");
    private static readonly string Api = Read("web", "Views", "Help", "Index.cshtml");

    [Fact]
    public void TheContents_ShouldShowOnlyTheReadersParts()
    {
        Assert.Contains("var isTeam = User.IsInRole(AccessControlService.RoleAdmin);", Contents, StringComparison.Ordinal);
        Assert.Contains("var runsACluster = isTeam || User.IsInRole(AccessControlService.RoleClusterAdmin);", Contents, StringComparison.Ordinal);
        Assert.True(Contents.IndexOf("@if (runsACluster)", StringComparison.Ordinal) < Contents.IndexOf("Part(\"cluster\")", StringComparison.Ordinal));
        Assert.True(Contents.IndexOf("@if (isTeam)", StringComparison.Ordinal) < Contents.IndexOf("Part(\"workflow\")", StringComparison.Ordinal));
        Assert.Contains("@if (signedIn && !runsACluster)", Contents, StringComparison.Ordinal);
        Assert.Contains("<a href=\"/help/api\" aria-current=\"@(onGuide ? null : \"page\")\">API reference and workbench</a>", Contents, StringComparison.Ordinal);

        // The page itself shows the same parts under the same conditions.
        Assert.True(Guide.IndexOf("@if (runsACluster)", StringComparison.Ordinal) < Guide.IndexOf("id=\"cluster\"", StringComparison.Ordinal));
        Assert.True(Guide.IndexOf("@if (isTeam)", StringComparison.Ordinal) < Guide.IndexOf("id=\"workflow\"", StringComparison.Ordinal));
        Assert.True(Guide.IndexOf("@if (signedIn && !runsACluster)", StringComparison.Ordinal) < Guide.IndexOf("id=\"access\"", StringComparison.Ordinal));
    }

    [Fact]
    public void Help_ShouldNoLongerSitUnderTheAdminTabs()
    {
        foreach (var page in new[] { Guide, Api })
        {
            Assert.Contains("@await Html.PartialAsync(\"_HelpContents\"", page, StringComparison.Ordinal);
            Assert.Contains("<div class=\"bp-help-layout mt-4\">", page, StringComparison.Ordinal);
            Assert.Contains("<h1>Help</h1>", page, StringComparison.Ordinal);
            Assert.DoesNotContain("_AdminTabs", page, StringComparison.Ordinal);
            Assert.DoesNotContain("_HelpTabs", page, StringComparison.Ordinal);
        }

        Assert.Throws<FileNotFoundException>(() => DppTestFiles.RepoFile("web", "Views", "Admin", "Help.cshtml"));
        Assert.Throws<FileNotFoundException>(() => DppTestFiles.RepoFile("web", "Views", "Shared", "_HelpTabs.cshtml"));
        Assert.Contains("public IActionResult Team() => Redirect(\"/help#workflow\");", Read("web", "Controllers", "HelpController.cs"), StringComparison.Ordinal);
        Assert.Contains("public IActionResult Help() => Redirect(\"/help/team\");", Read("web", "Controllers", "AdminController.cs"), StringComparison.Ordinal);
    }

    [Fact]
    public void TheTeamsPart_ShouldWalkFromProductDataToAPublishedPassport()
    {
        var workflow = Guide[Guide.IndexOf("id=\"workflow\"", StringComparison.Ordinal)..Guide.IndexOf("id=\"words\"", StringComparison.Ordinal)];

        Assert.Contains("<a href=\"/admin/families\">Set up product data</a>", workflow, StringComparison.Ordinal);
        Assert.Contains("<a href=\"/admin/batteries/new\">Create the battery</a>", workflow, StringComparison.Ordinal);
        Assert.Contains("Only \"required by Feb 2027\" blocks publishing", workflow, StringComparison.Ordinal);
        Assert.Contains("checks the passport against the official schema, signs it and publishes it", workflow, StringComparison.Ordinal);
        Assert.Contains("<dt>Archived version</dt>", Guide, StringComparison.Ordinal);
        foreach (var gone in new[] { "Validate passport", "Sign passport", "Dirty", "Evidence readiness", "Trust &amp; conformance" })
        {
            Assert.DoesNotContain(gone, Guide, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void TheClusterAdminsPart_ShouldSayHowToAddPeopleAndTokens()
    {
        var part = Guide[Guide.IndexOf("id=\"cluster\"", StringComparison.Ordinal)..Guide.IndexOf("@if (isTeam)", StringComparison.Ordinal)];

        Assert.Contains("<a href=\"/cluster-admin/users\">Add your people</a>", part, StringComparison.Ordinal);
        Assert.Contains("<a href=\"/cluster-admin/api-tokens\">Give your systems a token</a>", part, StringComparison.Ordinal);
        Assert.Contains("it is shown once", part, StringComparison.Ordinal);
        Assert.Contains("\"Manage\" opens a battery's page", part, StringComparison.Ordinal); // step 4a
    }

    [Fact]
    public void TheApiExamples_ShouldUseTheDemoClusters()
    {
        Assert.DoesNotContain("demo-cluster", Api, StringComparison.Ordinal);
        Assert.DoesNotContain("Demo API Cluster", Api, StringComparison.Ordinal);
        Assert.Contains("GET @Model.BasePath/clusters/demo-north/batteries", Api, StringComparison.Ordinal);
        Assert.Contains("\"clusterId\": \"demo-north\",", Api, StringComparison.Ordinal);
        Assert.Contains("No published demo battery to show. Use one of your batteries' IDs.", Api, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TheExampleBattery_ShouldBeAPublishedDemoBatteryOrNone()
    {
        using var rig = await DemoRig.StartAsync(mongo);
        var pages = rig.Services.GetRequiredService<BatteryPages>();

        Assert.Equal(string.Empty, await pages.ExampleBatteryIdAsync());

        await rig.Demo.SeedAsync(DemoRig.Who);
        var example = await pages.ExampleBatteryIdAsync();
        var published = (await pages.ListAsync(null)).Rows.Where(row => !row.IsDraft).Select(row => row.Identifier).ToList();
        Assert.Contains(example, published);

        // Batteries without the demo mark (real ones) are never shown.
        await rig.Database.GetCollection<BsonDocument>(DppStore.Packs).UpdateManyAsync(FilterDefinition<BsonDocument>.Empty, Builders<BsonDocument>.Update.Set("IsDemo", false));
        Assert.Equal(string.Empty, await pages.ExampleBatteryIdAsync());
    }
}
