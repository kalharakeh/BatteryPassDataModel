using System.Reflection;
using BatteryPassWeb.Configuration;
using BatteryPassWeb.Controllers;
using BatteryPassWeb.Dpp.Loading;
using BatteryPassWeb.Dpp.Store;
using BatteryPassWeb.Services;
using BatteryPassWeb.Tests.Dpp;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;
using static BatteryPassWeb.Tests.Loading.LoadTesting;

namespace BatteryPassWeb.Tests.Loading;

/// <summary>
/// Package X1, point 1 and the manager's check 6: the page is for the team's admins only. A cluster admin and a visitor
/// never reach it (the team's policy), and a demo account is turned away whatever role it holds, for every action.
/// </summary>
[Collection(MongoCollection.Name)]
public sealed class ProductDataLoadControllerTests(MongoFixture mongo)
{
    private static ProductDataLoadController Controller(LoadRig rig, string email, bool demo = false)
    {
        var user = TestAccounts.SignedIn(rig.Rig.Context, email, AccessControlService.RoleAdmin);
        if (demo)
        {
            rig.Rig.Context.Database!.GetCollection<BsonDocument>("users").UpdateOne(
                Builders<BsonDocument>.Filter.Eq("email", ClusterRepository.NormalizeEmail(email)),
                Builders<BsonDocument>.Update.Set(ClusterRepository.DemoMark, true));
        }

        var http = new DefaultHttpContext { User = user };
        return new ProductDataLoadController(rig.Loader, new AccessControlService(new ClusterRepository(rig.Rig.Context)), Options.Create(new BatteryPassOptions()))
        {
            ControllerContext = new ControllerContext { HttpContext = http },
            TempData = new TempDataDictionary(http, new NoTempData()),
        };
    }

    private static FormFile File(string name, byte[] content) => new(new MemoryStream(content), 0, content.Length, "files", name);

    [Fact]
    public void ThePage_ShouldBeForTheTeamOnly_AndEveryPostProtectedAgainstForgery()
    {
        var type = typeof(ProductDataLoadController);
        Assert.Equal("AdminOnly", type.GetCustomAttribute<AuthorizeAttribute>()?.Policy);
        var actions = type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly).Where(action => action.ReturnType == typeof(Task<IActionResult>)).ToArray();
        Assert.Equal(4, actions.Length);
        Assert.DoesNotContain(actions, action => action.GetCustomAttribute<AllowAnonymousAttribute>() is not null);
        Assert.All(actions.Where(action => action.GetCustomAttribute<HttpPostAttribute>() is not null), action =>
        {
            Assert.NotNull(action.GetCustomAttribute<ValidateAntiForgeryTokenAttribute>());
            Assert.NotNull(action.GetCustomAttribute<RequestSizeLimitAttribute>());
        });
    }

    [Fact]
    public async Task ADemoAccount_ShouldBeTurnedAway_FromEveryAction_AndNothingSaved()
    {
        var rig = await EmptyAsync(mongo);
        var controller = Controller(rig, "demo.team@example.test", demo: true);
        var workbook = File(WorkbookName, Core().Bytes());

        Assert.IsType<ForbidResult>(await controller.Index(CancellationToken.None));
        Assert.IsType<ForbidResult>(await controller.Workbook(CancellationToken.None));
        Assert.IsType<ForbidResult>(await controller.Check(workbook, [], CancellationToken.None));
        Assert.IsType<ForbidResult>(await controller.Load(workbook, [], "any", CancellationToken.None));
        Assert.Empty(await rig.Store.ListAsync<BatteryFamily>());
    }

    [Fact]
    public async Task TheTeam_ShouldGetTheEmptyWorkbook_AsItWasSent()
    {
        var rig = await EmptyAsync(mongo);

        var file = Assert.IsType<FileContentResult>(await Controller(rig, Who).Workbook(CancellationToken.None));

        Assert.Equal((LoadTemplate.FileName, BatteryPassWeb.Dpp.Identifiers.XlsxWriter.ContentType), (file.FileDownloadName, file.ContentType));
        Assert.Equal(LoadTemplate.Bytes(), file.FileContents);
    }

    [Fact]
    public async Task TheTeam_ShouldCheckThenLoad_AndLandOnTheFamily()
    {
        var rig = await EmptyAsync(mongo);
        var bytes = Core().Bytes();
        var files = CoreFiles().Select(file => (IFormFile)File(file.FileName, file.Content)).ToList();

        var checkedPage = Assert.IsType<ViewResult>(await Controller(rig, Who).Check(File(WorkbookName, bytes), files, CancellationToken.None));
        var check = Assert.IsType<ProductDataLoadPage>(checkedPage.Model).Check!;
        Assert.True(check.CanLoad);

        var controller = Controller(rig, Who);
        var loaded = Assert.IsType<RedirectResult>(await controller.Load(File(WorkbookName, bytes), CoreFiles().Select(file => (IFormFile)File(file.FileName, file.Content)).ToList(), check.Stamp, CancellationToken.None));

        var family = Assert.Single(await rig.Store.ListAsync<BatteryFamily>());
        Assert.Equal($"/admin/families/{family.Id}", loaded.Url);
        Assert.StartsWith("Product data loaded: 9 records made", (string)controller.TempData[ProductDataController.StatusKey]!, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ACheckFromThePagesScript_ShouldGetTheCheckAlone()
    {
        var rig = await EmptyAsync(mongo);
        var controller = Controller(rig, Who);
        controller.Request.Headers[ProductDataLoadController.InPlaceHeader] = "fetch";

        var result = Assert.IsType<PartialViewResult>(await controller.Check(File(WorkbookName, Core().Bytes()), [], CancellationToken.None));

        Assert.Equal("_Check", result.ViewName);
    }

    [Fact]
    public async Task ALoadWithoutAWorkbook_ShouldSaySo()
    {
        var rig = await EmptyAsync(mongo);

        var page = Assert.IsType<ViewResult>(await Controller(rig, Who).Load(null, [], null, CancellationToken.None));

        Assert.Equal(ProductDataLoadController.ChooseFirst, Assert.IsType<ProductDataLoadPage>(page.Model).Error);
    }

    [Fact]
    public void TheAppsRequestLimit_ShouldGiveTheLoadItsOwnFigureAndSentence()
    {
        var program = System.IO.File.ReadAllText(DppTestFiles.RepoFile("web", "Program.cs"));

        Assert.Contains("LoadLimits.AppliesTo(request)", program, StringComparison.Ordinal);
        Assert.Contains("return BatteryPassWeb.Dpp.Loading.LoadLimits.MaxRequestBytes;", program, StringComparison.Ordinal);
        Assert.Contains("LoadLimits.TooLarge", program, StringComparison.Ordinal);
        Assert.True(LoadLimits.AppliesTo(new DefaultHttpContext { Request = { Method = "POST", Path = "/admin/product-data-load/check" } }.Request));
        Assert.False(LoadLimits.AppliesTo(new DefaultHttpContext { Request = { Method = "POST", Path = "/admin/families" } }.Request));
        Assert.Equal(100L * 1024 * 1024, LoadLimits.MaxRequestBytes);
    }

    [Theory]
    [InlineData("Families.cshtml")]
    [InlineData("SitesAndOperators.cshtml")]
    public void TheProductDataPages_ShouldLinkToThePage(string view)
    {
        var source = System.IO.File.ReadAllText(DppTestFiles.RepoFile("web", "Views", "ProductData", view));

        Assert.Contains("<a href=\"/admin/product-data-load\"", source, StringComparison.Ordinal);
        Assert.Contains(">Load product data from Excel</a>", source, StringComparison.Ordinal);
    }

    private sealed class NoTempData : ITempDataProvider
    {
        public IDictionary<string, object> LoadTempData(HttpContext context) => new Dictionary<string, object>();

        public void SaveTempData(HttpContext context, IDictionary<string, object> values)
        {
        }
    }
}
