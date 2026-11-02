using System.Reflection;
using System.Security.Claims;
using System.Text.Json;
using BatteryPassWeb.Configuration;
using BatteryPassWeb.Controllers;
using BatteryPassWeb.Dpp.Demo;
using BatteryPassWeb.Dpp.Documents;
using BatteryPassWeb.Dpp.Store;
using BatteryPassWeb.Models.ViewModels;
using BatteryPassWeb.Services;
using BatteryPassWeb.Tests.Dpp;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace BatteryPassWeb.Tests.Demo;

/// <summary>Step 3d-3: the "Demo data" card on the team's admin pages (decision 32).</summary>
[Collection(MongoCollection.Name)]
public sealed class DemoCardTests(MongoFixture mongo)
{
    internal static AdminController Controller(DemoRig rig)
    {
        var http = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Email, DemoRig.Who), new Claim(ClaimTypes.Role, AccessControlService.RoleAdmin)], "test")),
        };
        var services = rig.Services;
        return new AdminController(
            services.GetRequiredService<ClusterRepository>(),
            services.GetRequiredService<ExternalApiRepository>(),
            services.GetRequiredService<ApplicationAuditService>(),
            new ApplicationSettingsService(services.GetRequiredService<MongoContext>(), services.GetRequiredService<IOptions<BatteryPassOptions>>()),
            rig.Store,
            rig.Demo)
        {
            ControllerContext = new ControllerContext { HttpContext = http },
            TempData = new TempDataDictionary(http, new NoTempDataStorage()),
        };
    }

    [Fact]
    public async Task ResettingFromTheCard_ShouldSeedAndShowEveryNewTokenOnce()
    {
        using var rig = await DemoRig.StartAsync(mongo);
        var controller = Controller(rig);

        var result = await controller.ResetDemoData(CancellationToken.None);

        Assert.Equal("/admin/clusters?tab=clusters", Assert.IsType<RedirectResult>(result).Url);
        Assert.Equal("Demo data reset: There was no demo data to clear; 2 families, 3 models, 2 sites, 2 operators and 3 batches; 9 batteries, 6 of them published; 12 accounts and 7 API tokens.", controller.TempData["StatusMessage"]);
        var tokens = JsonSerializer.Deserialize<List<DemoToken>>((string)controller.TempData["GeneratedCredentials"]!)!;
        Assert.Equal(7, tokens.Count);
        Assert.All(tokens, token => Assert.False(string.IsNullOrWhiteSpace(token.Value)));
    }

    [Fact]
    public async Task ClearingFromTheCard_ShouldClearAndSayWhatWasRemoved()
    {
        using var rig = await DemoRig.StartAsync(mongo);
        await rig.Demo.SeedAsync(DemoRig.Who);
        var controller = Controller(rig);

        await controller.ClearDemoData(CancellationToken.None);

        Assert.StartsWith("Demo data cleared: 9 batteries removed; ", (string)controller.TempData["StatusMessage"]!, StringComparison.Ordinal);
        Assert.Null(controller.TempData["GeneratedCredentials"]);
        Assert.Equal(0, await rig.Count(DppStore.Packs));
    }

    [Fact]
    public async Task WithoutTheAppsAddress_ShouldSayWhyAndClearNothing()
    {
        using var rig = await DemoRig.StartAsync(mongo, appBaseUrl: null);
        await rig.Services.GetRequiredService<ClusterRepository>().UpsertClusterAsync("demo-north", "Demo North Operations");
        await rig.Database.GetCollection<MongoDB.Bson.BsonDocument>("clusters").UpdateOneAsync(
            MongoDB.Driver.Builders<MongoDB.Bson.BsonDocument>.Filter.Eq("clusterId", "demo-north"), MongoDB.Driver.Builders<MongoDB.Bson.BsonDocument>.Update.Set("isDemo", true));
        var controller = Controller(rig);

        await controller.ResetDemoData(CancellationToken.None);

        Assert.Equal(DppLinks.NotConfigured, controller.TempData["ErrorMessage"]);
        Assert.Equal(1, await rig.Count("clusters"));
    }

    [Fact]
    public async Task ThePage_ShouldSayWhetherTheDemoDataIsThereAndWhetherAccountsCanBeMade()
    {
        using var rig = await DemoRig.StartAsync(mongo, password: null);
        var controller = Controller(rig);

        var before = Assert.IsType<AdminClusterViewModel>(Assert.IsType<ViewResult>(await controller.Clusters("clusters", null, CancellationToken.None)).Model);
        await rig.Demo.SeedAsync(DemoRig.Who);
        var after = Assert.IsType<AdminClusterViewModel>(Assert.IsType<ViewResult>(await controller.Clusters("clusters", null, CancellationToken.None)).Model);

        Assert.Equal((false, false), (before.HasDemoData, before.DemoAccountsPossible));
        Assert.True(after.HasDemoData);
    }

    [Fact]
    public void TheActions_ShouldBeForAdminsOnlyAndPostedFromThePage()
    {
        Assert.Equal("AdminOnly", typeof(AdminController).GetCustomAttribute<AuthorizeAttribute>()!.Policy);
        foreach (var (action, route) in new[] { (nameof(AdminController.ResetDemoData), "demo-data/reset"), (nameof(AdminController.ClearDemoData), "demo-data/clear") })
        {
            var method = typeof(AdminController).GetMethod(action)!;
            Assert.Equal(route, method.GetCustomAttribute<HttpPostAttribute>()!.Template);
            Assert.NotNull(method.GetCustomAttribute<ValidateAntiForgeryTokenAttribute>());
        }
    }

    [Fact]
    public void TheCard_ShouldAskFirstAndTheWindowShouldShowTheTokensOnce()
    {
        var page = File.ReadAllText(DppTestFiles.RepoFile("web", "Views", "Admin", "Clusters.cshtml"));
        var card = page[page.IndexOf("data-demo-data-card", StringComparison.Ordinal)..page.IndexOf("@if (selectedTab == \"users\")", StringComparison.Ordinal)];
        var window = File.ReadAllText(DppTestFiles.RepoFile("web", "Views", "Shared", "_GeneratedCredentialsModal.cshtml"));

        Assert.Contains("action=\"/admin/demo-data/reset\"", card, StringComparison.Ordinal);
        Assert.Contains("action=\"/admin/demo-data/clear\"", card, StringComparison.Ordinal);
        Assert.Equal(2, card.Split("data-confirm-action").Length - 1);
        Assert.Equal(2, card.Split("@Html.AntiForgeryToken()").Length - 1);
        Assert.Contains("DemoDataService.NoPassword", card, StringComparison.Ordinal);
        Assert.Contains("@await Html.PartialAsync(\"_GeneratedCredentialsModal\", Model.GeneratedCredentials)", page, StringComparison.Ordinal);
        Assert.Contains("Store these credentials now", window, StringComparison.Ordinal);
        Assert.Contains("These tokens are shown only once.", window, StringComparison.Ordinal);
        Assert.Contains("data-generated-token-copy", window, StringComparison.Ordinal);
    }

    private sealed class NoTempDataStorage : ITempDataProvider
    {
        public IDictionary<string, object> LoadTempData(HttpContext context) => new Dictionary<string, object>();

        public void SaveTempData(HttpContext context, IDictionary<string, object> values)
        {
        }
    }
}
