using System.Security.Claims;
using System.Text;
using BatteryPassWeb.Controllers;
using BatteryPassWeb.Dpp.Access;
using BatteryPassWeb.Dpp.Pages;
using BatteryPassWeb.Dpp.Store;
using BatteryPassWeb.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.DependencyInjection;

namespace BatteryPassWeb.Tests.Dpp;

/// <summary>
/// The product owner's step-5 test (4 Nov 2026). A battery page's one-time message ("Not checked: the file is for battery
/// DEMO-C7M-0001, not this one.") followed them to another battery and another account: it carried no battery, the pages
/// only peeked at it, so it was never used up, and the browser's TempData cookie outlives a sign-out. A window that is
/// taller than the screen scrolls inside itself; the API help's calls fit their column.
/// </summary>
[Collection(MongoCollection.Name)]
public sealed class Step5OwnerTestFixesTests(MongoFixture mongo)
{
    private const string NorthAdmin = "north-admin@example.com";
    private const string Refused = "Not checked: the file is for battery DEMO-C7M-0001, not this one.";

    [Fact]
    public void ABatterysMessage_ShouldShowOnceOnItsOwnPage_AndNeverOnAnotherBattery()
    {
        var cookie = new MemoryTempData();
        Posted(cookie, "pack-x");

        var other = Request(cookie, "pack-y");
        Assert.Null(other[BatteriesController.CardErrorKey]);
        Assert.Null(other[UploadPages.CheckKey]);
        Assert.Empty(cookie.Values); // dropped, not kept for later

        Posted(cookie, "pack-x");
        var own = Request(cookie, "pack-x");
        Assert.Equal(("upload", Refused, "check-1"), (own[BatteriesController.CardKey], own[BatteriesController.CardErrorKey], own[UploadPages.CheckKey]));
        Assert.Empty(cookie.Values); // used up
        Assert.Null(Request(cookie, "pack-x")[BatteriesController.CardErrorKey]);
    }

    [Fact]
    public void AMessageOfThisRequest_ShouldStayWhenAnOlderOneIsTaken()
    {
        var cookie = new MemoryTempData();
        Posted(cookie, "pack-x");
        var tempData = new TempDataDictionary(new DefaultHttpContext(), cookie);
        var viewData = new ViewDataDictionary(new EmptyModelMetadataProvider(), new ModelStateDictionary())
        {
            [BatteriesController.CardKey] = "live",
            [BatteriesController.CardErrorKey] = "1 value not saved; it's marked below.",
        };

        CardMessages.Take(tempData, viewData, "pack-x");

        Assert.Equal(("live", "1 value not saved; it's marked below."), (viewData[BatteriesController.CardKey], viewData[BatteriesController.CardErrorKey]));
    }

    [Fact]
    public async Task TheClusterAdminsRefusedFile_ShouldShowOnceOnTheirBattery_AndNotFollowThemToAnother()
    {
        var rig = await RigAsync();
        var cookie = new MemoryTempData();
        var file = Encoding.UTF8.GetBytes(UploadServiceTests.File("""{ "batterySerialNumber": "DEMO-C7M-0001", "values": { "stateOfChargeSoC": 40 } }"""));

        var checking = Controller(rig, cookie);
        await checking.CheckUpload(rig.PackId, new FormFile(new MemoryStream(file), 0, file.Length, "file", "upload-file-DEMO-C7M-0001.json"), CancellationToken.None);
        checking.TempData.Save();
        var first = Controller(rig, cookie);
        await first.Battery(rig.PackId, CancellationToken.None);
        first.TempData.Save();
        var again = Controller(rig, cookie);
        await again.Battery(rig.PackId, CancellationToken.None);

        Assert.StartsWith("Not checked: ", Assert.IsType<string>(first.ViewData[BatteriesController.CardErrorKey]), StringComparison.Ordinal);
        Assert.Null(again.ViewData[BatteriesController.CardErrorKey]);
        Assert.Empty(cookie.Values);

        // Another battery's message (the owner's DEMO-C13M-0001) never shows on this one.
        var elsewhere = new TempDataDictionary(new DefaultHttpContext(), cookie);
        CardMessages.Put(elsewhere, "pack-elsewhere", BatteriesController.CardKey, "upload");
        CardMessages.Put(elsewhere, "pack-elsewhere", BatteriesController.CardErrorKey, Refused);
        elsewhere.Save();
        var here = Controller(rig, cookie);
        await here.Battery(rig.PackId, CancellationToken.None);
        here.TempData.Save();

        Assert.Null(here.ViewData[BatteriesController.CardErrorKey]);
        Assert.Empty(cookie.Values);
    }

    [Fact]
    public void SigningInOrOut_ShouldForgetTheLastUsersMessages()
    {
        var cookie = new MemoryTempData();
        Posted(cookie, "pack-x");
        var http = new DefaultHttpContext
        {
            RequestServices = new ServiceCollection().AddSingleton<ITempDataProvider>(cookie).AddSingleton<ITempDataDictionaryFactory, TempDataDictionaryFactory>().BuildServiceProvider(),
        };

        CardMessages.Forget(http);

        Assert.Empty(cookie.Values);
        var program = File.ReadAllText(DppTestFiles.RepoFile("web", "Program.cs"));
        Assert.Contains("options.Events.OnSigningIn = context => CardMessages.ForgetAsync(context.HttpContext);", program, StringComparison.Ordinal);
        Assert.Contains("options.Events.OnSigningOut = context => CardMessages.ForgetAsync(context.HttpContext);", program, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AnotherUsersKeptCheck_ShouldNotShow()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo).WithClustersAsync();
        var check = await UploadServiceTests.Service(rig).CheckBatteryFileAsync(PublicationTesting.Team, rig.PackId, "readings.json", UploadCheckTests.Native(UploadServiceTests.Serial(rig), 41));
        var pack = (await rig.Store.GetAsync<BatteryPack>(rig.PackId))!;
        var pages = UploadCheckTests.Pages(rig);

        Assert.Null((await pages.CardAsync(pack, "/admin/batteries/x", forTeam: true, check.CheckId, null, viewer: "someone.else@example.com")).Check);
        Assert.NotNull((await pages.CardAsync(pack, "/admin/batteries/x", forTeam: true, check.CheckId, null, viewer: PublicationTesting.Team.Who)).Check);
    }

    [Theory]
    [InlineData("Batteries", "Battery.cshtml")]
    [InlineData("Batteries", "_CardMessages.cshtml")]
    [InlineData("Batteries", "_ModulesCard.cshtml")]
    [InlineData("ClusterAdmin", "Battery.cshtml")]
    public void ThePages_ShouldReadACardMessageOnlyFromTheirRequest(string folder, string view)
    {
        var text = File.ReadAllText(DppTestFiles.RepoFile("web", "Views", folder, view));

        Assert.DoesNotContain("TempData.Peek(BatteriesController.Card", text, StringComparison.Ordinal);
        Assert.DoesNotContain("TempData.Peek(BatteryPassWeb.Controllers.BatteriesController.Card", text, StringComparison.Ordinal);
        Assert.DoesNotContain("TempData[BatteriesController.Card", text, StringComparison.Ordinal);
    }

    [Fact]
    public void AWindow_ShouldScrollInsideItself_AndTheApiCallsFitTheirColumn()
    {
        var css = File.ReadAllText(DppTestFiles.RepoFile("web", "wwwroot", "css", "bp-product-data.css")).Replace("\r\n", "\n");
        var help = File.ReadAllText(DppTestFiles.RepoFile("web", "wwwroot", "css", "bp-api-help.css")).Replace("\r\n", "\n");

        Assert.Contains("dialog.bp-dialog {\n  position: fixed;\n  inset: 0;\n  margin: auto;\n  height: fit-content;\n  max-height: calc(100vh - 32px);\n  max-height: calc(100dvh - 32px);\n  overflow-y: auto;\n  overscroll-behavior: contain;\n}", css, StringComparison.Ordinal);
        Assert.Contains(".bp-api-help-endpoint-table {\n  min-width: 0;\n}", help, StringComparison.Ordinal);
        Assert.Contains(".bp-api-help-endpoint-table .bp-help-endpoint-cell code {\n  overflow: visible;\n  text-overflow: clip;\n  white-space: normal;\n  overflow-wrap: anywhere;\n}", help, StringComparison.Ordinal);
    }

    private static void Posted(MemoryTempData cookie, string packId)
    {
        var tempData = new TempDataDictionary(new DefaultHttpContext(), cookie);
        CardMessages.Put(tempData, packId, BatteriesController.CardKey, "upload");
        CardMessages.Put(tempData, packId, BatteriesController.CardErrorKey, Refused);
        CardMessages.Put(tempData, packId, UploadPages.CheckKey, "check-1");
        tempData.Save();
    }

    private static ViewDataDictionary Request(MemoryTempData cookie, string packId)
    {
        var tempData = new TempDataDictionary(new DefaultHttpContext(), cookie);
        var viewData = new ViewDataDictionary(new EmptyModelMetadataProvider(), new ModelStateDictionary());
        CardMessages.Take(tempData, viewData, packId);
        tempData.Save();
        return viewData;
    }

    private async Task<PublicationRig> RigAsync()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo).WithClustersAsync();
        await new ClusterRepository(rig.Context).UpsertClusterMembershipAsync(NorthAdmin, BatteryTesting.North, "clusterAdmin");
        return rig;
    }

    private static ClusterBatteriesController Controller(PublicationRig rig, MemoryTempData cookie)
    {
        var http = new DefaultHttpContext
        {
            User = TestAccounts.SignedIn(rig.Context, NorthAdmin, "clusterAdmin"),
        };
        return new ClusterBatteriesController(ClusterBatteryPagesTests.Pages(rig), new DppReaders(new ClusterRepository(rig.Context)), UploadCheckTests.Pages(rig))
        {
            ControllerContext = new ControllerContext { HttpContext = http },
            TempData = new TempDataDictionary(http, cookie),
        };
    }

    /// <summary>The browser's TempData cookie: what one request saves, the next loads.</summary>
    private sealed class MemoryTempData : ITempDataProvider
    {
        public Dictionary<string, object> Values { get; private set; } = [];

        public IDictionary<string, object> LoadTempData(HttpContext context) => new Dictionary<string, object>(Values);

        public void SaveTempData(HttpContext context, IDictionary<string, object> values) => Values = new Dictionary<string, object>(values);
    }
}
