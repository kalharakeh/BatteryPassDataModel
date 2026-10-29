using System.Reflection;
using System.Security.Claims;
using System.Text.RegularExpressions;
using BatteryPassWeb.Controllers;
using BatteryPassWeb.Dpp.Access;
using BatteryPassWeb.Dpp.Documents;
using BatteryPassWeb.Dpp.Editing;
using BatteryPassWeb.Dpp.Pages;
using BatteryPassWeb.Dpp.Store;
using BatteryPassWeb.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace BatteryPassWeb.Tests.Dpp;

/// <summary>The addresses passports link to (design 9): uploaded documents and the accident page, for each kind of reader.</summary>
[Collection(MongoCollection.Name)]
public sealed class DppControllerTests(MongoFixture mongo)
{
    private const string OwnClusterUser = "user@example.com";
    private const string OtherClusterUser = "other@example.com";

    private static string View(string name) => File.ReadAllText(DppTestFiles.RepoFile("web", "Views", "Dpp", $"{name}.cshtml"));

    [Fact]
    public void TheAddresses_ShouldBeOpenToEveryoneAndOnlyRead()
    {
        var type = typeof(DppController);

        Assert.NotNull(type.GetCustomAttribute<AllowAnonymousAttribute>());
        Assert.Null(type.GetCustomAttribute<AuthorizeAttribute>());
        Assert.Equal("dpp", type.GetCustomAttribute<RouteAttribute>()?.Template);
        Assert.All(
            type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly),
            method => Assert.NotNull(method.GetCustomAttribute<HttpGetAttribute>()));
    }

    [Fact]
    public void TheLinksThePassportsCarry_ShouldBeTheseAddresses()
    {
        Assert.Equal(DocumentTesting.BaseUrl + ReaderPages.DocumentHref("doc-1 a"), DocumentTesting.Links.DocumentUrl("doc-1 a"));
        Assert.Equal(DocumentTesting.BaseUrl + ReaderPages.AccidentsHref("pack-1 a"), DocumentTesting.Links.AccidentPageUrl("pack-1 a"));
    }

    [Fact]
    public async Task ADocument_ShouldBeServedWithItsStoredTypeAsAFileThatIsNeverSniffedOrCached()
    {
        var rig = await RigAsync();
        var uploaded = await UploadDismantlingAsync(rig);
        var controller = Controller(rig, OwnClusterUser);

        var result = Assert.IsType<FileContentResult>(await controller.Document(uploaded.Id, CancellationToken.None));

        Assert.Equal("application/pdf", result.ContentType);
        Assert.Equal(DocumentTesting.Pdf("manual"), result.FileContents);
        var headers = controller.Response.Headers;
        Assert.Equal("inline; filename=dismantling.pdf; filename*=UTF-8''dismantling.pdf", headers.ContentDisposition.ToString());
        Assert.Equal("nosniff", headers.XContentTypeOptions.ToString());
        Assert.Equal("private, no-store", headers.CacheControl.ToString());
    }

    [Fact]
    public async Task NotSignedIn_ShouldBeAskedToSignInFirst()
    {
        var rig = await RigAsync();
        var uploaded = await UploadDismantlingAsync(rig);

        Assert.IsType<ChallengeResult>(await Controller(rig, null).Document(uploaded.Id, CancellationToken.None));
        Assert.IsType<ChallengeResult>(await Controller(rig, null).Accidents(rig.PackId, CancellationToken.None));
        Assert.IsType<ChallengeResult>(await Controller(rig, null).Document("no-such-document", CancellationToken.None));
    }

    [Theory]
    [InlineData("other-cluster", "document")]
    [InlineData("other-cluster", "missing-document")]
    [InlineData("other-cluster", "accidents")]
    [InlineData("other-cluster", "missing-pack")]
    [InlineData("team", "missing-document")]
    [InlineData("team", "missing-pack")]
    public async Task SignedInWithoutAccess_ShouldGetNotAvailableWithoutSayingWhetherItExists(string who, string target)
    {
        var rig = await RigAsync();
        var uploaded = await UploadDismantlingAsync(rig);
        var controller = Controller(rig, who == "team" ? "team@example.com" : OtherClusterUser, who == "team" ? AccessControlService.RoleAdmin : "member");

        var result = target switch
        {
            "document" => await controller.Document(uploaded.Id, CancellationToken.None),
            "missing-document" => await controller.Document("doc-missing", CancellationToken.None),
            "accidents" => await controller.Accidents(rig.PackId, CancellationToken.None),
            _ => await controller.Accidents("pack-missing", CancellationToken.None),
        };

        var view = Assert.IsType<ViewResult>(result);
        Assert.Equal("NotAvailable", view.ViewName);
        Assert.Equal(StatusCodes.Status404NotFound, view.StatusCode);
        Assert.Equal(target.Contains("document", StringComparison.Ordinal) ? "document" : "accidents", view.ViewData["Subject"]);
    }

    [Fact]
    public async Task TheAccidentPage_ShouldListTheReportsNewestFirstForTheBatterysCluster()
    {
        var rig = await RigAsync();
        var documents = PageTesting.Documents(rig);
        await documents.AddAccidentReportAsync(PublicationTesting.Team, rig.PackId, new DocumentUpload("first.pdf", DocumentTesting.Pdf("1"), "Collision"));
        rig.Clock.Now = rig.Clock.Now.AddDays(2);
        await documents.AddAccidentReportAsync(PublicationTesting.Team, rig.PackId, new DocumentUpload("second.png", DocumentTesting.Png()));

        var view = Assert.IsType<ViewResult>(await Controller(rig, OwnClusterUser).Accidents(rig.PackId, CancellationToken.None));

        var page = Assert.IsType<AccidentsPage>(view.Model);
        Assert.Equal(rig.PackId, page.PackId);
        Assert.Equal(rig.Seed.Sources.Pack.Values["batterySerialNumber"].Value.GetValue<string>(), page.SerialNumber);
        Assert.Equal(rig.Seed.Sources.Pack.Values["uniqueProductIdentifier"].Value.GetValue<string>(), page.Identifier);
        Assert.Equal(["second.png", "Collision"], page.Reports.Select(report => report.Title));
    }

    [Fact]
    public async Task ADraftBatterysAccidentPage_ShouldBeTheTeamsOnly()
    {
        var rig = await RigAsync(published: false);

        var team = await Controller(rig, "team@example.com", AccessControlService.RoleAdmin).Accidents(rig.PackId, CancellationToken.None);
        var cluster = await Controller(rig, OwnClusterUser).Accidents(rig.PackId, CancellationToken.None);
        var surveillance = await Controller(rig, "msa@example.com", AccessControlService.RoleMarketSurveillanceAuthority).Accidents(rig.PackId, CancellationToken.None);

        Assert.Empty(Assert.IsType<AccidentsPage>(Assert.IsType<ViewResult>(team).Model).Reports);
        Assert.Equal("NotAvailable", Assert.IsType<ViewResult>(cluster).ViewName);
        Assert.Equal("NotAvailable", Assert.IsType<ViewResult>(surveillance).ViewName);
    }

    [Fact]
    public void TheViews_ShouldFollowTheMockUp()
    {
        var accidents = View("Accidents");
        var notAvailable = View("NotAvailable");

        Assert.Contains("No accidents reported", accidents, StringComparison.Ordinal);
        Assert.Contains("ReaderPages.DocumentHref(report.Id)", accidents, StringComparison.Ordinal);
        Assert.Contains("Identifiers.IsTestIdentifier(Model.Identifier)", accidents, StringComparison.Ordinal);
        Assert.DoesNotContain("UploadedBy", accidents, StringComparison.Ordinal);
        Assert.Contains("Not available", notAvailable, StringComparison.Ordinal);
    }

    [Fact]
    public void TheTeamsPages_ShouldLinkTheFilesAndTheAccidentPage()
    {
        var battery = File.ReadAllText(DppTestFiles.RepoFile("web", "Views", "Batteries", "Battery.cshtml"));
        var field = File.ReadAllText(DppTestFiles.RepoFile("web", "Views", "ProductData", "_EditorField.cshtml"));

        Assert.Contains("ReaderPages.AccidentsHref(Model.Pack.Id)", battery, StringComparison.Ordinal);
        Assert.Contains("ReaderPages.DocumentHref(report.Id)", battery, StringComparison.Ordinal);
        Assert.Contains("<a href=\"@ReaderPages.DocumentHref(document.Id)\" target=\"_blank\" rel=\"noreferrer\">Open the file</a>", field, StringComparison.Ordinal);
    }

    /// <summary>A file opens in a new tab, so the page it was opened from stays where it was (product owner, 29 Oct 2026).</summary>
    [Theory]
    [InlineData("Batteries", "Battery.cshtml")]
    [InlineData("Dpp", "Accidents.cshtml")]
    [InlineData("ProductData", "_EditorField.cshtml")]
    public void EveryFileLink_ShouldOpenInANewTab(string folder, string file)
    {
        var view = File.ReadAllText(DppTestFiles.RepoFile("web", "Views", folder, file));
        var links = Regex.Matches(view, @"<a [^>]*ReaderPages\.DocumentHref\([^>]*>").Select(match => match.Value).ToArray();

        Assert.NotEmpty(links);
        Assert.All(links, link => Assert.Contains("target=\"_blank\" rel=\"noreferrer\"", link, StringComparison.Ordinal));
    }

    /// <summary>The seed pack, published or not, with a member of its cluster and a member of another cluster.</summary>
    private async Task<PublicationRig> RigAsync(bool published = true)
    {
        var rig = published ? await PublicationTesting.PublishedRigAsync(mongo) : await PublicationTesting.RigAsync(mongo);
        var clusters = new ClusterRepository(rig.Context);
        await clusters.UpsertClusterMembershipAsync(OwnClusterUser, BatteryTesting.North, "member");
        await clusters.UpsertClusterMembershipAsync(OtherClusterUser, BatteryTesting.South, "member");
        return rig;
    }

    private static async Task<DppDocument> UploadDismantlingAsync(PublicationRig rig) =>
        (await PageTesting.Documents(rig).UploadAsync(PublicationTesting.Team, TargetKind.Model, "model-test", "dismantlingInformation", new DocumentUpload("dismantling.pdf", DocumentTesting.Pdf("manual")))).Document!;

    private static DppController Controller(PublicationRig rig, string? email, string role = "member")
    {
        var http = new DefaultHttpContext
        {
            User = email is null
                ? new ClaimsPrincipal(new ClaimsIdentity())
                : new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Email, email), new Claim(ClaimTypes.Role, role)], "test")),
        };
        var documents = PageTesting.Documents(rig);
        return new DppController(new DppReaders(new ClusterRepository(rig.Context)), documents, new ReaderPages(rig.Store, documents))
        {
            ControllerContext = new ControllerContext { HttpContext = http },
        };
    }
}
