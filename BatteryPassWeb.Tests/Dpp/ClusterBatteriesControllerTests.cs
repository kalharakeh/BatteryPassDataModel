using System.Reflection;
using System.Security.Claims;
using BatteryPassWeb.Controllers;
using BatteryPassWeb.Dpp.Access;
using BatteryPassWeb.Dpp.Pages;
using BatteryPassWeb.Dpp.Store;
using BatteryPassWeb.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.Primitives;

namespace BatteryPassWeb.Tests.Dpp;

/// <summary>The cluster admin's battery page as a signed-in user reaches it (step 4a).</summary>
[Collection(MongoCollection.Name)]
public sealed class ClusterBatteriesControllerTests(MongoFixture mongo)
{
    private const string NorthAdmin = "north-admin@example.com";

    private const string SouthAdmin = "south-admin@example.com";

    [Fact]
    public void ThePage_ShouldBeTheClusterAdminsWithEveryPostChecked()
    {
        var type = typeof(ClusterBatteriesController);

        Assert.Equal("ClusterAdminOrAdmin", type.GetCustomAttribute<AuthorizeAttribute>()?.Policy);
        Assert.Equal("cluster-admin/batteries", type.GetCustomAttribute<RouteAttribute>()?.Template);
        Assert.All(
            type.GetMethods().Where(method => method.GetCustomAttribute<HttpPostAttribute>() is not null),
            method => Assert.NotNull(method.GetCustomAttribute<ValidateAntiForgeryTokenAttribute>()));
    }

    [Fact]
    public void BothBatteryPages_ShouldDrawTheSameCardsAndOnlyTheTeamsTheTeamOnlyForms()
    {
        var clusterAdmin = File.ReadAllText(DppTestFiles.RepoFile("web", "Views", "ClusterAdmin", "Battery.cshtml"));
        var team = File.ReadAllText(DppTestFiles.RepoFile("web", "Views", "Batteries", "Battery.cshtml"));

        Assert.All(DppTestFiles.BatteryCardViews, card =>
        {
            Assert.Contains($"~/Views/Batteries/{card}.cshtml\", Model.Cards", clusterAdmin, StringComparison.Ordinal);
            Assert.Contains($"~/Views/Batteries/{card}.cshtml\", Model.Cards", team, StringComparison.Ordinal);
        });
        foreach (var teamOnly in new[] { "_RecordEditor", "/publish", "/cluster", "/remove", "/audit" })
        {
            Assert.DoesNotContain(teamOnly, clusterAdmin, StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task TheBatterysClusterAdmin_ShouldSeeItsPage()
    {
        var rig = await RigAsync();

        var view = Assert.IsType<ViewResult>(await Controller(rig, NorthAdmin, "clusterAdmin").Battery(rig.PackId, CancellationToken.None));

        Assert.Equal(rig.PackId, Assert.IsType<ClusterBatteryPage>(view.Model).Pack.Id);
    }

    [Fact]
    public async Task AnotherClustersAdmin_ShouldFindNothing()
    {
        var rig = await RigAsync();

        Assert.IsType<NotFoundResult>(await Controller(rig, SouthAdmin, "clusterAdmin").Battery(rig.PackId, CancellationToken.None));
    }

    [Fact]
    public async Task TheTeam_ShouldBeSentToItsOwnBatteryPage()
    {
        var rig = await RigAsync();

        var redirect = Assert.IsType<RedirectResult>(await Controller(rig, "team@example.com", "admin").Battery(rig.PackId, CancellationToken.None));

        Assert.Equal(BatteryPages.BatteryHref(rig.PackId), redirect.Url);
    }

    [Fact]
    public async Task LiveValues_ShouldBeSavedAndThePageOpenAtTheirCard()
    {
        var rig = await RigAsync();
        var form = await LiveFormAsync(rig, "temperatureInformation", "21");
        var controller = Controller(rig, NorthAdmin, "clusterAdmin", form);

        var redirect = Assert.IsType<RedirectResult>(await controller.SaveLiveValues(rig.PackId, CancellationToken.None));

        Assert.Equal($"{ClusterBatteryPages.BatteryHref(rig.PackId)}#live", redirect.Url);
        Assert.Equal("live", controller.TempData[BatteriesController.CardKey]);
        var temperature = (await rig.Store.GetAsync<BatteryPack>(rig.PackId))!.Values["temperatureInformation"];
        Assert.Equal(21, temperature.Value!["celsiusValue"]!.GetValue<int>());
        Assert.Equal(NorthAdmin, temperature.Who);
    }

    [Fact]
    public async Task ARefusedLiveValue_ShouldShowThePageAgainAsTyped()
    {
        var rig = await RigAsync();
        var controller = Controller(rig, NorthAdmin, "clusterAdmin", await LiveFormAsync(rig, "stateOfChargeSoC", "140"));

        var view = Assert.IsType<ViewResult>(await controller.SaveLiveValues(rig.PackId, CancellationToken.None));

        Assert.Equal("live", view.ViewData[BatteriesController.CardKey]);
        var field = Assert.IsType<ClusterBatteryPage>(view.Model).Live.Sections.SelectMany(section => section.Fields).Single(item => item.Attribute.Number == 63);
        Assert.Equal("140", field.Text(BatteryTesting.Field("stateOfChargeSoC")));
        Assert.NotNull(field.Refusal);
    }

    [Fact]
    public async Task AnotherClustersAdmin_ShouldNotSaveLiveValues()
    {
        var rig = await RigAsync();
        var controller = Controller(rig, SouthAdmin, "clusterAdmin", await LiveFormAsync(rig, "stateOfChargeSoC", "12"));

        Assert.IsType<NotFoundResult>(await controller.SaveLiveValues(rig.PackId, CancellationToken.None));
    }

    [Fact]
    public async Task AnAccidentReport_ShouldBeAddedAndThePageOpenAtTheReports()
    {
        var rig = await RigAsync();
        var controller = Controller(rig, NorthAdmin, "clusterAdmin");

        var redirect = Assert.IsType<RedirectResult>(await controller.AddAccidentReport(rig.PackId, Upload("fall.pdf", DocumentTesting.Pdf("fall")), "Dropped", null, CancellationToken.None));

        Assert.Equal($"{ClusterBatteryPages.BatteryHref(rig.PackId)}#accidents", redirect.Url);
        Assert.Equal("fall.pdf added.", controller.TempData[BatteriesController.CardStatusKey]);
    }

    [Fact]
    public async Task NoFile_ShouldBeSaidOnTheReportsCard()
    {
        var rig = await RigAsync();
        var controller = Controller(rig, NorthAdmin, "clusterAdmin");

        Assert.IsType<RedirectResult>(await controller.AddAccidentReport(rig.PackId, null, null, null, CancellationToken.None));

        Assert.Equal("Choose a file first.", controller.TempData[BatteriesController.CardErrorKey]);
    }

    [Fact]
    public async Task AnotherClustersAdmin_ShouldNotAddAReport()
    {
        var rig = await RigAsync();

        Assert.IsType<NotFoundResult>(await Controller(rig, SouthAdmin, "clusterAdmin").AddAccidentReport(rig.PackId, Upload("fall.pdf", DocumentTesting.Pdf()), null, null, CancellationToken.None));
    }

    [Fact]
    public async Task TheDetailPage_ShouldOfferManageToTheBatterysClusterAdminOnly()
    {
        var rig = await RigAsync();
        await new ClusterRepository(rig.Context).UpsertClusterMembershipAsync("north-user@example.com", BatteryTesting.North, "member");

        Assert.Equal(ClusterBatteryPages.BatteryHref(rig.PackId), (await DetailAsync(rig, NorthAdmin, "clusterAdmin")).ViewData[ClusterBatteryPages.ManageHrefKey]);
        Assert.Null((await DetailAsync(rig, SouthAdmin, "clusterAdmin")).ViewData[ClusterBatteryPages.ManageHrefKey]);
        Assert.Null((await DetailAsync(rig, "north-user@example.com", "member")).ViewData[ClusterBatteryPages.ManageHrefKey]);
        Assert.Equal("ManageHref", ClusterBatteryPages.ManageHrefKey); // the hero names no cluster, so it reads the key as text
        Assert.Contains("ViewData[\"ManageHref\"] is string manageHref", File.ReadAllText(DppTestFiles.RepoFile("web", "Views", "Passport", "_PassportHero.cshtml")), StringComparison.Ordinal);
    }

    private static async Task<ViewResult> DetailAsync(PublicationRig rig, string email, string role)
    {
        var controller = new PassportController(new DppReaders(new ClusterRepository(rig.Context)), PassportPagesTests.Pages(rig))
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Email, email), new Claim(ClaimTypes.Role, role)], "test")),
                },
            },
        };
        return Assert.IsType<ViewResult>(await controller.Detail(PassportPagesTests.Identifier(rig), CancellationToken.None, manage: ClusterBatteryPagesTests.Pages(rig)));
    }

    [Fact]
    public async Task AMarkedBattery_ShouldOpenReadOnly_AndRefuseEveryPost()
    {
        // Redesign R6b: the page opens read-only for a passport marked invalid; every change is still refused.
        var rig = await RigAsync();
        Assert.Null(await PassportMarkTests.Service(rig).MarkInvalidAsync(PublicationTesting.Team, rig.PackId, "Demo: wrong figures"));
        var live = await LiveFormAsync(rig, "temperatureInformation", "21");

        var page = Assert.IsType<ClusterBatteryPage>(Assert.IsType<ViewResult>(await Controller(rig, NorthAdmin, "clusterAdmin").Battery(rig.PackId, CancellationToken.None)).Model);
        Assert.True(page.ReadOnly);
        Assert.Equal(ClusterBatteryPages.BatteryHref(rig.PackId), (await DetailAsync(rig, NorthAdmin, "clusterAdmin")).ViewData[ClusterBatteryPages.ManageHrefKey]); // the detail page leads to it
        Assert.Null((await DetailAsync(rig, SouthAdmin, "clusterAdmin")).ViewData[ClusterBatteryPages.ManageHrefKey]);
        Assert.IsType<NotFoundResult>(await Controller(rig, NorthAdmin, "clusterAdmin", live).SaveLiveValues(rig.PackId, CancellationToken.None));
        Assert.IsType<NotFoundResult>(await Controller(rig, NorthAdmin, "clusterAdmin").AddAccidentReport(rig.PackId, Upload("report.pdf", "%PDF-1.4 report"u8.ToArray()), "Report", "en", CancellationToken.None));
        Assert.IsType<NotFoundResult>(await Controller(rig, NorthAdmin, "clusterAdmin").ChangeStatus(rig.PackId, "Repaired", "2026-11-03", "A repair.", CancellationToken.None));
        Assert.IsType<NotFoundResult>(await Controller(rig, NorthAdmin, "clusterAdmin").AddHistoryNote(rig.PackId, "A note.", CancellationToken.None));
        Assert.IsType<NotFoundResult>(await Controller(rig, NorthAdmin, "clusterAdmin").OtherOperator(rig.PackId, "Other", "Somewhere", null, false, CancellationToken.None));
        await Controller(rig, NorthAdmin, "clusterAdmin").HandOver(rig.PackId, BatteryTesting.South, BatteryTesting.South, HandOverTests.Service(rig), CancellationToken.None);
        Assert.Null(await rig.Store.FindWaitingHandOverAsync(rig.PackId));
        Assert.NotEqual(21, (await rig.Store.GetAsync<BatteryPack>(rig.PackId))!.Values["temperatureInformation"].Value!["celsiusValue"]!.GetValue<int>());
    }

    private async Task<PublicationRig> RigAsync()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo).WithClustersAsync();
        var clusters = new ClusterRepository(rig.Context);
        await clusters.UpsertClusterMembershipAsync(NorthAdmin, BatteryTesting.North, "clusterAdmin");
        await clusters.UpsertClusterMembershipAsync(SouthAdmin, BatteryTesting.South, "clusterAdmin");
        return rig;
    }

    /// <summary>The live form as the page shows it, with its stamp, and one field typed.</summary>
    private static async Task<Dictionary<string, string>> LiveFormAsync(PublicationRig rig, string property, string typed)
    {
        var stored = (await rig.Store.GetAsync<BatteryPack>(rig.PackId))!.Values;
        var form = BatteryTesting.ShownForm(RecordEditor.LiveAttributes, stored);
        form[BatteryTesting.Field(property)] = typed;
        form[ProductDataController.StampField] = PageTesting.Editor(rig).Stamp(RecordEditor.LiveAttributes, stored, null);
        return form;
    }

    private static ClusterBatteriesController Controller(PublicationRig rig, string email, string role, IReadOnlyDictionary<string, string>? form = null)
    {
        var http = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Email, email), new Claim(ClaimTypes.Role, role)], "test")),
        };
        http.Request.Form = new FormCollection((form ?? new Dictionary<string, string>()).ToDictionary(entry => entry.Key, entry => new StringValues(entry.Value)));
        return new ClusterBatteriesController(ClusterBatteryPagesTests.Pages(rig), new DppReaders(new ClusterRepository(rig.Context)))
        {
            ControllerContext = new ControllerContext { HttpContext = http },
            TempData = new TempDataDictionary(http, new NoTempDataStorage()),
        };
    }

    private static IFormFile Upload(string name, byte[] content) =>
        new FormFile(new MemoryStream(content), 0, content.Length, "file", name);

    private sealed class NoTempDataStorage : ITempDataProvider
    {
        public IDictionary<string, object> LoadTempData(HttpContext context) => new Dictionary<string, object>();

        public void SaveTempData(HttpContext context, IDictionary<string, object> values)
        {
        }
    }
}
