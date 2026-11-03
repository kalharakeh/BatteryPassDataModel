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

        var result = await controller.ResetDemoData();

        Assert.Equal("/admin/clusters?tab=clusters", Assert.IsType<RedirectResult>(result).Url);
        Assert.Equal("Demo data reset: There was no demo data to clear; 2 families, 3 models, 2 sites, 2 operators and 3 batches; 10 batteries, 7 of them published; 12 accounts and 7 API tokens.", controller.TempData["StatusMessage"]);
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

        await controller.ClearDemoData();

        Assert.StartsWith("Demo data cleared: 10 batteries removed; ", (string)controller.TempData["StatusMessage"]!, StringComparison.Ordinal);
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

        await controller.ResetDemoData();

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

    [Fact]
    public void ResetAndClear_ShouldFinishEvenWhenTheTabIsClosed()
    {
        // Closing the tab cancels the request; the demo data must not stop halfway (owner's point 1, redesign R1).
        foreach (var action in new[] { nameof(AdminController.ResetDemoData), nameof(AdminController.ClearDemoData) })
        {
            Assert.DoesNotContain(typeof(AdminController).GetMethod(action)!.GetParameters(), parameter => parameter.ParameterType == typeof(CancellationToken));
        }
    }

    [Fact]
    public void TheDemoButtons_ShouldSayTheyAreWorkingOnceConfirmed()
    {
        var page = File.ReadAllText(DppTestFiles.RepoFile("web", "Views", "Admin", "Clusters.cshtml"));
        var card = page[page.IndexOf("data-demo-data-card", StringComparison.Ordinal)..page.IndexOf("@if (selectedTab == \"users\")", StringComparison.Ordinal)];
        var script = File.ReadAllText(DppTestFiles.RepoFile("web", "wwwroot", "js", "bp-confirm.js")); // the confirm window's own script since R6

        Assert.Equal(2, card.Split("data-confirm-busy=\"").Length - 1);
        Assert.Contains("data-confirm-busy=\"Resetting the demo data… This takes a little over a minute. Keep this page open; it reloads when done.\"", card, StringComparison.Ordinal);
        Assert.Contains("data-confirm-busy=\"Clearing the demo data… This takes about 20 seconds. Keep this page open; it reloads when done.\"", card, StringComparison.Ordinal);
        Assert.Contains("const busy = pendingButton?.dataset.confirmBusy;", script, StringComparison.Ordinal);
        Assert.Contains("confirmSubmit.disabled = true;", script, StringComparison.Ordinal);
        Assert.Contains("if (confirmSubmit?.disabled)", script, StringComparison.Ordinal);
    }

    [Fact]
    public void TheSeed_ShouldRunTheBatteriesSideBySide()
    {
        // On Atlas each database call waits for its round trip (about 1,200 calls: 138 s before this change and 76 s after it, with 45 ms each way). The batteries
        // don't depend on each other, nor do the accounts on the product data, so they run side by side; each battery's
        // own steps stay in order and every save still goes through the save rules (redesign R1, point 1).
        var service = File.ReadAllText(DppTestFiles.RepoFile("web", "Dpp", "Demo", "DemoDataService.cs"));

        Assert.Contains("var accountsSeeded = SeedAccountsAsync(catalogue, accountNotes, cancellationToken);", service, StringComparison.Ordinal);
        Assert.Contains("var outcomes = await Task.WhenAll(catalogue.Packs.Select(demo => SeedPackAsync(", service, StringComparison.Ordinal);
        Assert.Contains("private async Task<PackOutcome> SeedPackAsync(", service, StringComparison.Ordinal);
    }

    private sealed class NoTempDataStorage : ITempDataProvider
    {
        public IDictionary<string, object> LoadTempData(HttpContext context) => new Dictionary<string, object>();

        public void SaveTempData(HttpContext context, IDictionary<string, object> values)
        {
        }
    }
}
