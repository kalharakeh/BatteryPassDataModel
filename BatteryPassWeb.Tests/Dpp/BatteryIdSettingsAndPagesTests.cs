using System.Security.Claims;
using System.Text.Json.Nodes;
using BatteryPassWeb.Controllers;
using BatteryPassWeb.Dpp.Demo;
using BatteryPassWeb.Dpp.Editing;
using BatteryPassWeb.Dpp.Identifiers;
using BatteryPassWeb.Dpp.Store;
using BatteryPassWeb.Services;
using BatteryPassWeb.Tests.Demo;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.DependencyInjection;

namespace BatteryPassWeb.Tests.Dpp;

/// <summary>
/// Step 6d-2b, after the owner's trial of 6d-2 (9 Nov 2026): missing ID settings no longer fail silently (no real
/// release, no battery from a real released ID, a red notice on the page; demo data still works), and the "Battery IDs"
/// page is three tabs (Releases first, All IDs, Release new IDs) with a page per release that a new release lands on.
/// </summary>
[Collection(MongoCollection.Name)]
public sealed class BatteryIdSettingsAndPagesTests(MongoFixture mongo)
{
    private const string Who = DemoRig.Who;
    private static readonly string[] AllThree = ["DPP_ID_AGENCY", "DPP_ID_COMPANY", "DPP_ID_BASE_URL"];

    private static async Task<(DemoRig Rig, BatteryIdPool Pool, BatteryPack C7)> SeededAsync(MongoFixture mongo, bool dunsIds)
    {
        var rig = await DemoRig.StartAsync(mongo, dunsIds: dunsIds);
        await rig.Demo.SeedAsync(Who);
        var c7 = (await rig.Store.FindPacksBySerialNumberAsync("DEMO-C7M-0001")).Single();
        return (rig, rig.Services.GetRequiredService<BatteryIdPool>(), c7);
    }

    private static NewPack NewBattery(BatteryPack like, string serial) =>
        new(like.BatchId, like.OperatorId, like.ClusterId, new Dictionary<string, JsonNode?> { ["batterySerialNumber"] = serial }, FromPool: true);

    private static BatteryIdsController Controller(DemoRig rig)
    {
        var http = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Email, Who), new Claim(ClaimTypes.Role, AccessControlService.RoleAdmin)], "test")),
        };
        return new BatteryIdsController(rig.Services.GetRequiredService<BatteryIdPool>(), rig.Store)
        {
            ControllerContext = new ControllerContext { HttpContext = http },
            TempData = new TempDataDictionary(http, new NoTempDataStorage()),
        };
    }

    [Fact]
    public async Task MissingSettings_ShouldRefuseARealRelease_NamingThem_WhileTheDemoDataStillWorks()
    {
        var (rig, pool, _) = await SeededAsync(mongo, dunsIds: false);
        using var __ = rig;

        var refused = await pool.ReleaseAsync(Who, new ReleaseRequest("PO 1", null, [], "REAL-0001"));

        Assert.Equal(AllThree, pool.MissingSettings);
        Assert.False(refused.Done);
        Assert.Equal(IdentifierForms.SettingsMissing(AllThree), refused.Problem);
        Assert.Contains("aren't set, so batteries would get test IDs", refused.Problem, StringComparison.Ordinal);
        Assert.Single(await pool.ReleasesAsync(), release => release.IsDemo && release.Reference == DemoDataService.DemoReference); // the demo seeded anyway
        Assert.Empty(await pool.ListAsync(new ReleasedIdFilter(Query: "REAL")));
    }

    [Fact]
    public async Task MissingSettings_ShouldRefuseABatteryFromARealReleasedId_ButNotFromADemoSpare()
    {
        var (rig, pool, c7) = await SeededAsync(mongo, dunsIds: false);
        using var __ = rig;
        // An ID released while the settings were set, before they went missing.
        await rig.Store.ReleasedIds.InsertOneAsync(new ReleasedBatteryId
        {
            Id = "OLD0001",
            SerialNumber = "OLD-0001",
            Identifier = "https://www.sib-scania-eubp.eu/25S/UN778474481OLD0001",
            ReleaseId = "old-release",
            Reference = "PO old",
            Status = ReleasedIdStatuses.Available,
            ReleasedBy = Who,
            ReleasedAtUtc = DateTime.UtcNow,
        });
        var creation = rig.Services.GetRequiredService<PackCreationService>();

        var real = await creation.CreateAsync(ChangeSender.Team(Who), NewBattery(c7, "OLD-0001"));
        var demo = await creation.CreateAsync(ChangeSender.Team(Who), NewBattery(c7, DemoDataService.SpareSerials[0]));

        Assert.Equal(IdentifierForms.SettingsMissing(AllThree), real.Problem);
        Assert.Equal(ReleasedIdStatuses.Available, (await pool.GetAsync("OLD-0001"))!.Status); // nothing was taken
        Assert.Null(demo.Problem);
        Assert.NotNull(demo.Pack);
    }

    [Fact]
    public async Task ThePage_ShouldOpenOnReleases_AndShowTheNotice_WhenTheSettingsAreMissing()
    {
        var (rig, _, _) = await SeededAsync(mongo, dunsIds: false);
        using var __ = rig;

        var page = Assert.IsType<BatteryIdsPage>(Assert.IsType<ViewResult>(await Controller(rig).Index(null, null, null, null, null, false, CancellationToken.None)).Model);

        Assert.Equal(BatteryIdsTabs.Releases, page.Tab);
        Assert.Equal(AllThree, page.MissingSettings);
        Assert.Contains(page.Releases, release => release.IsDemo);
        Assert.All(page.Counts.Values, count => Assert.Equal(0, count)); // the summary counts real IDs only
    }

    [Fact]
    public async Task AllIds_ShouldHideDemoIds_UnlessAskedFor_AndTheSummaryCountRealIdsOnly()
    {
        var (rig, pool, _) = await SeededAsync(mongo, dunsIds: true);
        using var __ = rig;
        await pool.ReleaseAsync(Who, new ReleaseRequest("PO 2", null, [], "REAL-0001\nREAL-0002"));
        var controller = Controller(rig);

        var real = Assert.IsType<BatteryIdsPage>(Assert.IsType<ViewResult>(await controller.Index(BatteryIdsTabs.Ids, null, null, null, null, false, CancellationToken.None)).Model);
        var withDemo = Assert.IsType<BatteryIdsPage>(Assert.IsType<ViewResult>(await controller.Index(BatteryIdsTabs.Ids, null, null, null, null, true, CancellationToken.None)).Model);

        Assert.Equal(["REAL-0001", "REAL-0002"], real.Items.Select(item => item.SerialNumber).Order());
        Assert.Contains(withDemo.Items, item => item.IsDemo);
        Assert.Empty(real.MissingSettings);
        Assert.Equal(2, real.Counts.GetValueOrDefault(ReleasedIdStatuses.Available)); // the demo spares aren't counted
        Assert.Equal(0, real.Counts.GetValueOrDefault(ReleasedIdStatuses.Published));
    }

    [Fact]
    public async Task ARelease_ShouldLandOnItsOwnPage_WithItsIdsAndItsExport()
    {
        var (rig, pool, _) = await SeededAsync(mongo, dunsIds: true);
        using var __ = rig;
        var controller = Controller(rig);

        var done = Assert.IsType<RedirectResult>(await controller.ReleaseIds("PO 7, line 1", null, ["LBL-0001"], ["LBL-0003"], ["3"], null, CancellationToken.None));
        var release = (await pool.ReleasesAsync()).Single(summary => summary.Reference == "PO 7, line 1");
        var page = Assert.IsType<ReleasePage>(Assert.IsType<ViewResult>(await controller.Release(release.ReleaseId, true, CancellationToken.None)).Model);
        var export = Assert.IsType<FileContentResult>(await controller.Export(null, release.ReleaseId, null, null, false, CancellationToken.None));

        Assert.Equal($"/admin/battery-ids/releases/{Uri.EscapeDataString(release.ReleaseId)}?released=true", done.Url);
        Assert.Equal("Released 3 battery IDs. Export them to Excel for the labels.", controller.TempData[ProductDataController.StatusKey]);
        Assert.True(page.JustReleased);
        Assert.Equal(["LBL-0001", "LBL-0002", "LBL-0003"], page.Items.Select(item => item.SerialNumber).Order());
        Assert.StartsWith("battery-ids-PO-7--line-1-", export.FileDownloadName, StringComparison.Ordinal);
        Assert.IsType<NotFoundResult>(await controller.Release("no-such-release", false, CancellationToken.None));
    }

    [Fact]
    public async Task ARefusedRelease_ShouldStayOnTheReleaseTab_WithItsReasons()
    {
        var (rig, _, _) = await SeededAsync(mongo, dunsIds: true);
        using var __ = rig;
        var controller = Controller(rig);

        var view = Assert.IsType<ViewResult>(await controller.ReleaseIds("PO 8", null, [], [], [], "TEST-0001\n---", CancellationToken.None));

        Assert.Equal(nameof(BatteryIdsController.Index), view.ViewName);
        Assert.Equal(StatusCodes.Status422UnprocessableEntity, controller.Response.StatusCode);
        var page = Assert.IsType<BatteryIdsPage>(view.Model);
        Assert.Equal(BatteryIdsTabs.New, page.Tab);
        Assert.Equal(2, page.Refusal!.Problems.Count);
        Assert.Equal("TEST-0001\n---", page.Form.List);
    }

    [Fact]
    public void TheViews_ShouldBeTheOwnersLayout()
    {
        var index = File.ReadAllText(DppTestFiles.RepoFile("web", "Views", "BatteryIds", "Index.cshtml"));
        var rows = File.ReadAllText(DppTestFiles.RepoFile("web", "Views", "BatteryIds", "_IdRows.cshtml"));
        var release = File.ReadAllText(DppTestFiles.RepoFile("web", "Views", "BatteryIds", "Release.cshtml"));

        Assert.Contains("data-id-settings-missing", index, StringComparison.Ordinal);
        Assert.Contains("<fieldset disabled=\"@blocked\" class=\"bp-release-fields\">", index, StringComparison.Ordinal); // no release while blocked
        Assert.Contains("Show demo IDs", index, StringComparison.Ordinal);
        Assert.Contains("Release new IDs", index, StringComparison.Ordinal);
        Assert.Contains("<details class=\"bp-row-menu\">", rows, StringComparison.Ordinal); // Void… in a small menu,
        Assert.DoesNotContain("is-danger", rows, StringComparison.Ordinal); // not a red link on every row
        Assert.Contains("Export to Excel", release, StringComparison.Ordinal);
        Assert.Contains("data-void-unused", release, StringComparison.Ordinal);
    }

    private sealed class NoTempDataStorage : ITempDataProvider
    {
        public IDictionary<string, object> LoadTempData(HttpContext context) => new Dictionary<string, object>();

        public void SaveTempData(HttpContext context, IDictionary<string, object> values)
        {
        }
    }
}
