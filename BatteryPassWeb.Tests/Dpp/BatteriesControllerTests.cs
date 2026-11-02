using System.Reflection;
using System.Security.Claims;
using BatteryPassWeb.Controllers;
using BatteryPassWeb.Dpp.Documents;
using BatteryPassWeb.Dpp.Editing;
using BatteryPassWeb.Dpp.Pages;
using BatteryPassWeb.Dpp.Store;
using BatteryPassWeb.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.Primitives;

namespace BatteryPassWeb.Tests.Dpp;

/// <summary>The batteries pages' routes (step 2d-2), as a signed-in team member reaches them, and their views.</summary>
[Collection(MongoCollection.Name)]
public sealed class BatteriesControllerTests(MongoFixture mongo)
{
    private static string View(string name) => File.ReadAllText(DppTestFiles.RepoFile("web", "Views", "Batteries", $"{name}.cshtml"));

    [Fact]
    public void TheBatteriesPages_ShouldBeTheTeamsOnlyUnderTheMenusAddress()
    {
        var type = typeof(BatteriesController);

        Assert.Equal("AdminOnly", type.GetCustomAttribute<AuthorizeAttribute>()?.Policy);
        Assert.Equal("admin/batteries", type.GetCustomAttribute<RouteAttribute>()?.Template);
        Assert.All(
            type.GetMethods().Where(method => method.GetCustomAttribute<HttpPostAttribute>() is not null),
            method => Assert.NotNull(method.GetCustomAttribute<ValidateAntiForgeryTokenAttribute>()));
    }

    [Fact]
    public async Task TheList_ShouldShowEveryBatteryAndFilterBySearch()
    {
        var rig = await PublicationTesting.RigAsync(mongo).WithClustersAsync();

        var all = Assert.IsType<ViewResult>(await Controller(rig).Batteries(null, CancellationToken.None));
        var none = Assert.IsType<ViewResult>(await Controller(rig).Batteries("no such battery", CancellationToken.None));

        Assert.Single(Assert.IsType<BatteriesPage>(all.Model).Rows);
        Assert.Empty(Assert.IsType<BatteriesPage>(none.Model).Rows);
    }

    [Fact]
    public async Task CreatingABattery_ShouldOpenItsPageOrShowTheFormAgainAsTyped()
    {
        var rig = await PublicationTesting.RigAsync(mongo).WithClustersAsync();
        var serial = BatteryTesting.Field("batterySerialNumber");

        var created = await Controller(rig, (serial, "VPC9-2001")).CreateBattery(rig.Seed.Sources.Batch.Id, rig.Seed.Sources.Operator.Id, BatteryTesting.South, CancellationToken.None);
        var refused = await Controller(rig, (serial, " vpc9-2001 ")).CreateBattery(rig.Seed.Sources.Batch.Id, rig.Seed.Sources.Operator.Id, null, CancellationToken.None);

        Assert.StartsWith("/admin/batteries/pack-", Assert.IsType<RedirectResult>(created).Url, StringComparison.Ordinal);
        var again = Assert.IsType<ViewResult>(refused);
        Assert.Equal("NewBattery", again.ViewName);
        var page = Assert.IsType<NewBatteryPage>(again.Model);
        var field = page.Values.Sections.SelectMany(section => section.Fields).Single(item => item.Attribute.Number == 11);
        Assert.Equal(" vpc9-2001 ", field.Text(serial));
        Assert.Equal("Another battery already has this serial number.", field.Refusal);
        Assert.Equal(2, (await rig.Store.ListAsync<BatteryPack>()).Count);
    }

    [Fact]
    public async Task AValueChangeOnAPublishedBattery_ShouldAskFirstThenPublishAVersion()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo).WithClustersAsync();
        var stored = (await rig.Store.GetAsync<BatteryPack>(rig.PackId))!.Values;
        var form = BatteryTesting.ShownForm(BatteryPages.ValueAttributes(published: true), stored);
        form[BatteryTesting.Field("thirdPartyServiceProvider")] = "Other Hosting AB";
        form[ProductDataController.StampField] = PageTesting.Editor(rig).Stamp(BatteryPages.ValueAttributes(published: true), stored, null);

        var asked = Assert.IsType<ViewResult>(await Controller(rig, [.. form]).SaveValues(rig.PackId, confirmed: false, CancellationToken.None));
        Assert.Single(Assert.IsType<BatteryPage>(asked.Model).Values.Confirm);
        Assert.Equal(1, await rig.Store.GetLatestVersionNumberAsync(rig.PackId));

        var saved = await Controller(rig, [.. form]).SaveValues(rig.PackId, confirmed: true, CancellationToken.None);
        Assert.Equal($"{BatteryPages.BatteryHref(rig.PackId)}#values", Assert.IsType<RedirectResult>(saved).Url);
        Assert.Equal(2, await rig.Store.GetLatestVersionNumberAsync(rig.PackId));
    }

    [Fact]
    public async Task LiveValues_ShouldSaveInPlaceAtTheTimeTyped()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo).WithClustersAsync();
        var stored = (await rig.Store.GetAsync<BatteryPack>(rig.PackId))!.Values;
        var form = BatteryTesting.ShownForm(RecordEditor.LiveAttributes, stored);
        form[BatteryTesting.Field("temperatureInformation")] = "18";
        form[ProductDataController.StampField] = PageTesting.Editor(rig).Stamp(RecordEditor.LiveAttributes, stored, null);
        form[BatteriesController.MeasuredAtField] = "2026-10-29T09:15";

        var result = await Controller(rig, [.. form]).SaveLiveValues(rig.PackId, CancellationToken.None);

        Assert.Equal($"{BatteryPages.BatteryHref(rig.PackId)}#live", Assert.IsType<RedirectResult>(result).Url);
        var temperature = (await rig.Store.GetAsync<BatteryPack>(rig.PackId))!.Values["temperatureInformation"];
        Assert.Equal(BatteriesController.MeasuredAt("2026-10-29T09:15"), temperature.GeneratedAtUtc);
        Assert.Equal(1, await rig.Store.GetLatestVersionNumberAsync(rig.PackId));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not a time")]
    public void AMeasurementTimeThatIsBlankOrUnreadable_ShouldMeanNow(string typed) =>
        Assert.Null(BatteriesController.MeasuredAt(typed));

    [Fact]
    public void AMeasurementTime_ShouldBeReadInThePagesTimeZone() =>
        Assert.Equal(
            TimeZoneInfo.ConvertTimeToUtc(new DateTime(2026, 10, 29, 9, 15, 0, DateTimeKind.Unspecified), TimeZoneInfo.Local),
            BatteriesController.MeasuredAt("2026-10-29T09:15"));

    [Fact]
    public async Task PublishingAndMovingTheCluster_ShouldReportInTheirOwnCard()
    {
        var rig = await PublicationTesting.RigAsync(mongo).WithClustersAsync();

        var moved = Controller(rig);
        await moved.SetCluster(rig.PackId, "cluster-gone", CancellationToken.None);
        var published = Controller(rig);
        var result = await published.Publish(rig.PackId, CancellationToken.None);

        Assert.Equal("cluster", moved.TempData[BatteriesController.CardKey]);
        Assert.Equal("The cluster wasn't changed: cluster cluster-gone doesn't exist.", moved.TempData[BatteriesController.CardErrorKey]);
        Assert.Equal($"{BatteryPages.BatteryHref(rig.PackId)}#publication", Assert.IsType<RedirectResult>(result).Url);
        Assert.Equal("Version 1 published.", published.TempData[BatteriesController.CardStatusKey]);
        Assert.Equal(1, await rig.Store.GetLatestVersionNumberAsync(rig.PackId));
    }

    [Fact]
    public async Task AnAccidentReport_ShouldBeAddedOrSayWhatIsMissing()
    {
        var rig = await PublicationTesting.RigAsync(mongo).WithClustersAsync();
        var bytes = DocumentTesting.Pdf("collision");
        var file = new FormFile(new MemoryStream(bytes), 0, bytes.Length, "file", "collision.pdf");

        var empty = Controller(rig);
        await empty.AddAccidentReport(rig.PackId, null, null, null, CancellationToken.None);
        var added = Controller(rig);
        await added.AddAccidentReport(rig.PackId, file, "Collision", "en", CancellationToken.None);

        Assert.Equal("Choose a file first.", empty.TempData[BatteriesController.CardErrorKey]);
        Assert.Equal("collision.pdf added.", added.TempData[BatteriesController.CardStatusKey]);
        Assert.Single(await new DocumentStore(rig.Context).ListAsync(TargetKind.Pack, rig.PackId, DocumentService.Accidents));
    }

    [Fact]
    public async Task RemovingABattery_ShouldWorkOnlyForADraft()
    {
        var published = await PublicationTesting.PublishedRigAsync(mongo).WithClustersAsync();
        var draft = await PublicationTesting.RigAsync(mongo).WithClustersAsync();

        var kept = await Controller(published).Remove(published.PackId, CancellationToken.None);
        var removed = await Controller(draft).Remove(draft.PackId, CancellationToken.None);

        Assert.Equal($"{BatteryPages.BatteryHref(published.PackId)}#remove", Assert.IsType<RedirectResult>(kept).Url);
        Assert.Equal("/admin/batteries", Assert.IsType<RedirectResult>(removed).Url);
        Assert.Null(await draft.Store.GetAsync<BatteryPack>(draft.PackId));
    }

    [Fact]
    public async Task AMissingBattery_ShouldBeNotFound()
    {
        var rig = await PublicationTesting.RigAsync(mongo).WithClustersAsync();

        Assert.IsType<NotFoundResult>(await Controller(rig).Battery("pack-missing", CancellationToken.None));
    }

    [Fact]
    public void TheViews_ShouldUseTheTeamPagesFinishAndTheirParts()
    {
        var list = View("Batteries");
        var battery = DppTestFiles.BatteryPageSource();
        var create = View("NewBattery");
        var editor = File.ReadAllText(DppTestFiles.RepoFile("web", "Views", "ProductData", "_RecordEditor.cshtml"));

        Assert.All(new[] { list, battery, create }, view => Assert.Contains("~/css/bp-product-data.css", view, StringComparison.Ordinal));
        Assert.Contains("PartialAsync(\"_AdminTabs\", \"batteries\")", list, StringComparison.Ordinal);
        Assert.Contains("Passport ID, battery ID, serial or cluster", list, StringComparison.Ordinal);
        foreach (var card in new[] { "publication", "values", "live", "accidents", "versions", "remove" })
        {
            Assert.Contains($"id=\"{card}\"", battery, StringComparison.Ordinal);
        }

        Assert.Contains("~/Views/ProductData/_RecordEditor.cshtml", battery, StringComparison.Ordinal);
        Assert.Contains("No cluster yet", create, StringComparison.Ordinal);
        Assert.Contains("Model.Kind is TargetKind.Model or TargetKind.Site or TargetKind.Operator", editor, StringComparison.Ordinal); // a battery has no name field
        Assert.Contains("~/Views/ProductData/_EditorField.cshtml", editor, StringComparison.Ordinal);
    }

    /// <summary>The controller as a team member reaches it, with this form posted (also used by the status tests, step 4b).</summary>
    internal static BatteriesController Controller(PublicationRig rig, IReadOnlyDictionary<string, string> form) => Controller(rig, form.ToArray());

    private static BatteriesController Controller(PublicationRig rig, params KeyValuePair<string, string>[] form)
    {
        var http = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim(ClaimTypes.Email, "team@example.com"), new Claim(ClaimTypes.Role, AccessControlService.RoleAdmin)],
                "test")),
        };
        http.Request.Form = new FormCollection(form.ToDictionary(entry => entry.Key, entry => new StringValues(entry.Value)));
        return new BatteriesController(BatteryTesting.Pages(rig), PageTesting.Documents(rig), StatusChangeTests.Pages(rig))
        {
            ControllerContext = new ControllerContext { HttpContext = http },
            TempData = new TempDataDictionary(http, new NoTempDataStorage()),
        };
    }

    private static BatteriesController Controller(PublicationRig rig, (string Name, string Value) field) =>
        Controller(rig, new KeyValuePair<string, string>(field.Name, field.Value));

    private sealed class NoTempDataStorage : ITempDataProvider
    {
        public IDictionary<string, object> LoadTempData(HttpContext context) => new Dictionary<string, object>();

        public void SaveTempData(HttpContext context, IDictionary<string, object> values)
        {
        }
    }
}
