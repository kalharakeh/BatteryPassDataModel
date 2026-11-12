using System.Reflection;
using System.Security.Claims;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using BatteryPassWeb.Controllers;
using BatteryPassWeb.Dpp.Access;
using BatteryPassWeb.Dpp.Catalog;
using BatteryPassWeb.Dpp.Editing;
using BatteryPassWeb.Dpp.Pages;
using BatteryPassWeb.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace BatteryPassWeb.Tests.Dpp;

/// <summary>
/// The passport pages, the QR code and the home page's search (step 3b), in place of today's: /dpp/{id} for everyone,
/// /dpp/{id}/detail for signed-in readers.
/// </summary>
[Collection(MongoCollection.Name)]
public sealed class PassportControllerTests(MongoFixture mongo)
{
    private const string OwnClusterUser = "user@example.com";

    private static string View(string name) => File.ReadAllText(DppTestFiles.RepoFile("web", "Views", "Passport", $"{name}.cshtml"));

    [Fact]
    public void ThePages_ShouldBeOpenToEveryoneAndOnlyRead()
    {
        var type = typeof(PassportController);

        Assert.NotNull(type.GetCustomAttribute<AllowAnonymousAttribute>());
        Assert.Equal("dpp", type.GetCustomAttribute<RouteAttribute>()?.Template);
        Assert.Equal("{id}", type.GetMethod(nameof(PassportController.Summary))!.GetCustomAttribute<HttpGetAttribute>()?.Template);
        Assert.Equal("{id}/detail", type.GetMethod(nameof(PassportController.Detail))!.GetCustomAttribute<HttpGetAttribute>()?.Template);
        Assert.NotNull(typeof(QrController).GetCustomAttribute<AllowAnonymousAttribute>());
        Assert.False(File.Exists(Path.Combine(Path.GetDirectoryName(DppTestFiles.RepoFile("web", "Views", "Passport", "Summary.cshtml"))!, "Battery.cshtml")));
    }

    [Fact]
    public async Task TheSummary_ShouldOpenWithoutSigningInByTheBatteryIdAsTheQrCodeCarriesIt()
    {
        var rig = await RigAsync();

        var view = Assert.IsType<ViewResult>(await Pages(rig, null).Summary(Uri.EscapeDataString(PassportPagesTests.Identifier(rig)), CancellationToken.None));

        var page = Assert.IsType<PassportPage>(view.Model);
        Assert.Equal("Summary", view.ViewName); // named, as the version addresses reuse it (step 6d)
        Assert.Empty(page.Audiences);
        Assert.False(page.ReaderSeesMore);
        Assert.All(page.Sections.SelectMany(section => section.Values), value => Assert.Equal(Audience.Public, value.Attribute.Audience));
    }

    [Fact]
    public async Task AnUnknownIdOrADraft_ShouldSayNotAvailable()
    {
        var draft = await PublicationTesting.RigAsync(mongo);

        foreach (var result in new[]
        {
            await Pages(draft, null).Summary(PassportPagesTests.Identifier(draft), CancellationToken.None),
            await Pages(draft, null).Summary("VPC8C05-0000", CancellationToken.None),
            await Pages(draft, "team@example.com", AccessControlService.RoleAdmin).Detail(PassportPagesTests.Identifier(draft), CancellationToken.None),
        })
        {
            var view = Assert.IsType<ViewResult>(result);
            Assert.Equal("~/Views/Dpp/NotAvailable.cshtml", view.ViewName);
            Assert.Equal(StatusCodes.Status404NotFound, view.StatusCode);
            Assert.Equal("passport", view.ViewData["Subject"]);
        }
    }

    [Fact]
    public async Task TheDetailPage_ShouldAskToSignInFirst()
    {
        var rig = await RigAsync();

        Assert.IsType<ChallengeResult>(await Pages(rig, null).Detail(PassportPagesTests.Identifier(rig), CancellationToken.None));
        Assert.IsType<ChallengeResult>(await Pages(rig, null).Detail("no-such-passport", CancellationToken.None));
    }

    [Fact]
    public async Task TheDetailPage_ShouldShowTheBatterysClusterItsSectionsAndStayOutOfCaches()
    {
        var rig = await RigAsync();
        var controller = Pages(rig, OwnClusterUser);

        var view = Assert.IsType<ViewResult>(await controller.Detail(PassportPagesTests.Identifier(rig), CancellationToken.None));

        var page = Assert.IsType<PassportPage>(view.Model);
        Assert.Equal(["purchaser", "shared"], page.Audiences.Select(section => section.Key));
        Assert.Equal("private, no-store", controller.Response.Headers.CacheControl.ToString());
    }

    [Fact]
    public async Task AnotherClustersUser_ShouldGetThePublicDataOnly()
    {
        var rig = await RigAsync();

        var page = Assert.IsType<PassportPage>(Assert.IsType<ViewResult>(await Pages(rig, "other@example.com").Detail(PassportPagesTests.Identifier(rig), CancellationToken.None)).Model);

        Assert.Empty(page.Audiences);
        Assert.False(page.ReaderSeesMore);
    }

    [Fact]
    public async Task TheQrCode_ShouldBeDrawnForAPublishedBatteryOnly()
    {
        var rig = await RigAsync();
        var draft = await PublicationTesting.RigAsync(mongo);

        var svg = Assert.IsType<ContentResult>(await Qr(rig).QrCode("VPC8C05-6788", CancellationToken.None));
        var png = Assert.IsType<FileContentResult>(await Qr(rig).Download(Uri.EscapeDataString(PassportPagesTests.Identifier(rig)), CancellationToken.None));

        Assert.StartsWith("image/svg+xml", svg.ContentType, StringComparison.Ordinal);
        Assert.Contains("<svg", svg.Content, StringComparison.Ordinal);
        Assert.Equal("image/png", png.ContentType);
        Assert.Equal("VPC8C05-6788-qr.png", png.FileDownloadName);
        Assert.IsType<NotFoundResult>(await Qr(draft).QrCode(PassportPagesTests.Identifier(draft), CancellationToken.None));
        Assert.IsType<NotFoundResult>(await Qr(draft, email: "cluster@example.com").QrCode(PassportPagesTests.Identifier(draft), CancellationToken.None));
        // The team gets a draft's code, to print labels before publishing (decision 30).
        Assert.IsType<ContentResult>(await Qr(draft, email: "team@example.com", role: AccessControlService.RoleAdmin).QrCode(PassportPagesTests.Identifier(draft), CancellationToken.None));
        Assert.IsType<FileContentResult>(await Qr(draft, email: "team@example.com", role: AccessControlService.RoleAdmin).Download(Uri.EscapeDataString(PassportPagesTests.Identifier(draft)), CancellationToken.None));
        Assert.IsType<NotFoundResult>(await Qr(rig, DocumentTesting.NoLinks).QrCode("VPC8C05-6788", CancellationToken.None));
    }

    [Theory]
    [InlineData("https://passport.example.com/dpp/urn:x-battery-pass:TEST-0f3a", "urn:x-battery-pass:TEST-0f3a")]
    [InlineData("https://passport.example.com/dpp/urn%3Ax-battery-pass%3ATEST-0f3a/detail", "urn:x-battery-pass:TEST-0f3a")]
    [InlineData("urn:x-battery-pass:TEST-0f3a", "urn:x-battery-pass:TEST-0f3a")]
    [InlineData(" VPC8C05-6788 ", "VPC8C05-6788")]
    [InlineData("https://passport.example.com/?q=VPC8C05-6788", "VPC8C05-6788")]
    [InlineData("https://passport.example.com/BAT-1/latest/summary", "BAT-1")]
    [InlineData("", "")]
    public void TheSearch_ShouldReadTheIdOutOfWhatWasTypedOrScanned(string typed, string expected)
    {
        Assert.Equal(expected, HomeController.IdFromSearch(typed));
    }

    [Fact]
    public async Task TheSearch_ShouldOpenTheSummaryOrWhenSignedInTheDetailPage()
    {
        var rig = await RigAsync();
        var identifier = PassportPagesTests.Identifier(rig);

        var anonymous = Assert.IsType<RedirectResult>(await Home(rig, null).Search("VPC8C05-6788", CancellationToken.None));
        var signedIn = Assert.IsType<RedirectResult>(await Home(rig, OwnClusterUser).Search($"{DocumentTesting.BaseUrl}{PassportPages.SummaryHref(identifier)}", CancellationToken.None));
        var unknown = Assert.IsType<RedirectToActionResult>(await Home(rig, null).Search("VPC8C05-0000", CancellationToken.None));

        Assert.Equal(PassportPages.SummaryHref(identifier), anonymous.Url);
        Assert.Equal(PassportPages.DetailHref(identifier), signedIn.Url);
        Assert.Equal("Index", unknown.ActionName);
        Assert.Equal("1", unknown.RouteValues!["notFound"]);
    }

    [Fact]
    public async Task TheSearch_ShouldOpenAnArchivedVersionAtItsOwnPassportId()
    {
        var rig = await RigAsync();
        var first = (await rig.Store.GetActiveVersionAsync(rig.PackId))!;
        await rig.Pipeline.SaveAsync(
            PublicationTesting.Team,
            new ChangeSet(TargetKind.Model, "model-test", new Dictionary<string, JsonNode?> { ["batteryMass"] = JsonValue.Create(520) }, "form"));

        var result = Assert.IsType<RedirectResult>(await Home(rig, null).Search(first.DigitalProductPassportId, CancellationToken.None));

        Assert.Equal(PassportPages.SummaryHref(first.DigitalProductPassportId), result.Url);
    }

    [Fact]
    public void TheViews_ShouldKeepTodaysLookWithTheNewData()
    {
        var hero = View("_PassportHero");
        var tabs = View("_PassportTabs");
        var summary = View("Summary");
        var detail = View("Detail");

        Assert.DoesNotContain("Cluster", hero, StringComparison.Ordinal);
        Assert.DoesNotContain("Battery status", hero, StringComparison.Ordinal);
        Assert.Contains("@await Html.PartialAsync(\"_TestTag\", \"Test passport\")", hero, StringComparison.Ordinal); // a tag since redesign R3
        Assert.Contains("Model.PictureHref", hero, StringComparison.Ordinal);
        Assert.Contains("Open the current version", hero, StringComparison.Ordinal);
        Assert.Contains("Model.Audiences", tabs, StringComparison.Ordinal);
        Assert.Contains("@(origin.Measured ? \"Measured\" : \"Entered\") @ValueWords.Time(origin.GeneratedAtUtc) · @PassportPages.SourceWord(origin.Source)", tabs, StringComparison.Ordinal);
        Assert.Contains("are on the <a href=\"@PassportPages.DetailHref(Model.Identifier)\">current version</a>", tabs, StringComparison.Ordinal);
        Assert.Contains("ViewData[\"Detail\"] = false;", summary, StringComparison.Ordinal);
        Assert.Contains("ViewData[\"Detail\"] = true;", detail, StringComparison.Ordinal);
        Assert.DoesNotContain("chart.js", summary + detail, StringComparison.OrdinalIgnoreCase);
        var links = Regex.Matches(tabs, "<a href=\"@part.Href\"[^>]*>").Select(match => match.Value).ToArray();
        Assert.All(links, link => Assert.Contains("target=\"_blank\" rel=\"noreferrer\"", link, StringComparison.Ordinal));
        Assert.NotEmpty(links);
    }

    [Fact]
    public void TheHomePage_ShouldSearchTheNewPassportsWithoutTheOldSample()
    {
        var home = File.ReadAllText(DppTestFiles.RepoFile("web", "Views", "Home", "Index.cshtml"));

        Assert.DoesNotContain("SamplePassportId", home, StringComparison.Ordinal); // not the old app's sample; R6 links a published demo battery instead
        Assert.DoesNotContain("DID", home, StringComparison.Ordinal);
        Assert.Contains("placeholder=\"Serial number, battery ID or passport ID\"", home, StringComparison.Ordinal);
    }

    [Fact]
    public void TheTabs_ShouldWrapOntoRowsInsteadOfScrollingSideways()
    {
        // Owner's change after 3b's check: a team member sees 11 tabs, so they wrap rather than scroll (site.css untouched).
        Assert.Contains("<ul class=\"nav nav-tabs bp-tabs\" id=\"passport-tabs\" role=\"tablist\" style=\"flex-wrap:wrap;overflow-x:visible\">", View("_PassportTabs"), StringComparison.Ordinal);
    }

    [Fact]
    public void TheHomePageScript_ShouldHaveNoApostropheInsideASingleQuotedText()
    {
        // 'the battery's …' ends the text at the apostrophe and the browser drops the whole script (the QR icon did nothing).
        var home = File.ReadAllText(DppTestFiles.RepoFile("web", "Views", "Home", "Index.cshtml"));

        Assert.DoesNotMatch(new Regex(@"\('[^'\r\n]*[A-Za-z]'[A-Za-z]"), home);
    }

    /// <summary>The seed pack, published, with a member of its cluster and a member of another cluster.</summary>
    private async Task<PublicationRig> RigAsync()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo);
        var clusters = new ClusterRepository(rig.Context);
        await clusters.UpsertClusterMembershipAsync(OwnClusterUser, BatteryTesting.North, "member");
        await clusters.UpsertClusterMembershipAsync("other@example.com", BatteryTesting.South, "member");
        return rig;
    }

    [Fact]
    public async Task TheDetailPage_ShouldShowTheLiveValueHistoryToThoseWhoMaySeeIt()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo);
        var pack = (await rig.Store.GetAsync<BatteryPassWeb.Dpp.Store.BatteryPack>(rig.PackId))!;
        await BatteryApiTests.Api(rig).WriteLiveValuesAsync(PublicationTesting.Team, pack, new Dictionary<string, JsonNode?> { ["stateOfChargeSoC"] = JsonValue.Create(55) }, null, preview: false);
        var id = PassportPagesTests.Identifier(rig);

        var team = Assert.IsType<ViewResult>(await Pages(rig, "team@example.com", AccessControlService.RoleAdmin).Detail(id, CancellationToken.None, BatteryApiTests.Api(rig)));
        var outsider = Assert.IsType<ViewResult>(await Pages(rig, "someone@example.com").Detail(id, CancellationToken.None, BatteryApiTests.Api(rig)));

        var history = Assert.IsAssignableFrom<IReadOnlyList<BatteryPassWeb.Dpp.Api.LiveHistoryEntry>>(team.ViewData["LiveHistory"]);
        Assert.Contains(history, entry => entry.Attribute.Number == 63 && entry.Value.ToJsonString().Contains("55", StringComparison.Ordinal));
        Assert.Empty(Assert.IsAssignableFrom<IReadOnlyList<BatteryPassWeb.Dpp.Api.LiveHistoryEntry>>(outsider.ViewData["LiveHistory"])); // a signed-in stranger sees no live values
    }

    private static HttpContext Http(PublicationRig rig, string? email, string role) => new DefaultHttpContext
    {
        User = email is null
            ? new ClaimsPrincipal(new ClaimsIdentity())
            : TestAccounts.SignedIn(rig.Context, email, role),
    };

    private static PassportController Pages(PublicationRig rig, string? email, string role = "member") =>
        new(new DppReaders(new ClusterRepository(rig.Context)), PassportPagesTests.Pages(rig))
        {
            ControllerContext = new ControllerContext { HttpContext = Http(rig, email, role) },
        };

    private static QrController Qr(PublicationRig rig, BatteryPassWeb.Dpp.Documents.DppLinks? links = null, string? email = null, string role = "member") =>
        new(PassportPagesTests.Pages(rig), links ?? DocumentTesting.Links, new PassportQrCodeService(), rig.Store)
        {
            ControllerContext = new ControllerContext { HttpContext = Http(rig, email, role) },
        };

    private static HomeController Home(PublicationRig rig, string? email) =>
        new(PassportPagesTests.Pages(rig))
        {
            ControllerContext = new ControllerContext { HttpContext = Http(rig, email, "member") },
        };
}
