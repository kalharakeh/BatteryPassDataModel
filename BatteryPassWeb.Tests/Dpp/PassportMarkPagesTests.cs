using System.Security.Claims;
using BatteryPassWeb.Controllers;
using BatteryPassWeb.Dpp.Access;
using BatteryPassWeb.Dpp.Editing;
using BatteryPassWeb.Dpp.Pages;
using BatteryPassWeb.Dpp.Store;
using BatteryPassWeb.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;

namespace BatteryPassWeb.Tests.Dpp;

/// <summary>"Invalid" and "marked for deletion" on the pages (mock-up S5, S6 and S7; step 4c-2).</summary>
[Collection(MongoCollection.Name)]
public sealed class PassportMarkPagesTests(MongoFixture mongo)
{
    private const string Reason = "Demo: wrong carbon footprint figures";

    [Fact]
    public async Task TheTeamsPage_ShouldMarkInvalidAndUndoItFromThePassportStatusCard()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo).WithClustersAsync();
        var controller = BatteriesControllerTests.Controller(rig, new Dictionary<string, string>());

        Assert.True((await BatteryTesting.Pages(rig).BatteryAsync(rig.PackId))!.Marks.Active);
        var marked = Assert.IsType<RedirectResult>(await controller.MarkInvalid(rig.PackId, Reason, CancellationToken.None));
        var card = (await BatteryTesting.Pages(rig).BatteryAsync(rig.PackId))!.Marks;
        await controller.UndoMark(rig.PackId, CancellationToken.None);

        Assert.Equal($"{BatteryPages.BatteryHref(rig.PackId)}#passport-status", marked.Url);
        Assert.True(card.Marked);
        Assert.Equal(DppStatuses.Invalid, card.Status);
        Assert.Equal(Reason, card.Mark!.Reason);
        Assert.Equal("Undone: the passport is active again.", controller.TempData[BatteriesController.CardStatusKey]);
        Assert.Equal(DppStatuses.Active, await rig.Store.PassportStatusAsync(rig.PackId));
    }

    [Fact]
    public async Task MarkingANonWasteBatteryForDeletion_ShouldBeSaidOnTheCard()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo).WithClustersAsync();
        var controller = BatteriesControllerTests.Controller(rig, new Dictionary<string, string>());

        await controller.MarkForDeletion(rig.PackId, CancellationToken.None);

        Assert.Equal($"Nothing changed: {PassportMarks.OnlyWaste}.", controller.TempData[BatteriesController.CardErrorKey]);
        Assert.False((await BatteryTesting.Pages(rig).BatteryAsync(rig.PackId))!.Marks.Waste);
    }

    [Fact]
    public async Task ThePassportPages_ShouldSayInvalidAndShowTheReasonToTheTeamAndSurveillanceOnly()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo).WithClustersAsync();
        await PassportMarkTests.Service(rig).MarkInvalidAsync(ChangeSender.MarketSurveillance("msa@example.com"), rig.PackId, Reason);
        var id = PassportPagesTests.Identifier(rig);
        var pages = PassportPagesTests.Pages(rig);

        var summary = (await pages.SummaryAsync(Reader.Anonymous, id))!;
        var clusterUser = (await pages.DetailAsync(Reader.With("user", ReaderRole.ClusterUser, BatteryTesting.North), id))!;
        var surveillance = (await pages.DetailAsync(Reader.With("msa@example.com", ReaderRole.MarketSurveillance), id))!;

        Assert.Equal("Invalid", summary.StatusWords);
        Assert.Equal("Invalid", summary.Notice!.Pill);
        Assert.StartsWith("This passport was marked invalid on ", summary.Notice.Text, StringComparison.Ordinal);
        Assert.Null(summary.MarkReason);
        Assert.Null(clusterUser.MarkReason);
        Assert.Equal($"{Reason} (marked by market surveillance, msa@example.com)", surveillance.MarkReason);
        Assert.False(surveillance.MayMarkInvalid); // already invalid
    }

    [Fact]
    public async Task AnInvalidBattery_ShouldSayWhyNothingChangesInsteadOfCallingItADraft()
    {
        // Found in the redesign review: its values editor said "Its passport is a draft" and offered Save.
        var rig = await PublicationTesting.PublishedRigAsync(mongo).WithClustersAsync();
        await PassportMarkTests.Service(rig).MarkInvalidAsync(ChangeSender.MarketSurveillance("msa@example.com"), rig.PackId, Reason);
        var page = (await BatteryTesting.Pages(rig).BatteryAsync(rig.PackId))!;
        var editor = File.ReadAllText(DppTestFiles.RepoFile("web", "Views", "ProductData", "_RecordEditor.cshtml"));
        var battery = File.ReadAllText(DppTestFiles.RepoFile("web", "Views", "Batteries", "Battery.cshtml"));

        Assert.Equal("The passport is invalid: nothing on it changes until your team undoes it.", page.Cards.FrozenNote);
        Assert.Equal(page.Cards.FrozenNote, page.Editor.FrozenNote);
        Assert.Null(page.Values.FrozenNote);
        Assert.Contains("<fieldset class=\"bp-frozen-fields\" disabled=\"@(Model.FrozenNote is not null)\">", editor, StringComparison.Ordinal);
        Assert.Contains("@if (Model.FrozenNote is null)", editor, StringComparison.Ordinal);
        Assert.Contains("@await Html.PartialAsync(\"~/Views/ProductData/_RecordEditor.cshtml\", Model.Editor)", battery, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AnInvalidPassport_ShouldStillBeFoundByItsSerialNumber()
    {
        // Point 10b: marking it invalid leaves no active version, and the search said "No published battery passport was found".
        var rig = await PublicationTesting.PublishedRigAsync(mongo).WithClustersAsync();
        await PassportMarkTests.Service(rig).MarkInvalidAsync(ChangeSender.MarketSurveillance("msa@example.com"), rig.PackId, Reason);
        var pages = PassportPagesTests.Pages(rig);

        var found = await pages.FindAsync("VPC8C05-6788");
        var located = (await pages.LocateAsync("VPC8C05-6788"))!;

        Assert.Equal(DppStatuses.Invalid, found!.DppStatus);
        Assert.Equal(PassportPagesTests.Identifier(rig), located.Identifier);
        Assert.Equal("Invalid", (await pages.SummaryAsync(Reader.Anonymous, located.Address))!.Notice!.Pill);
    }

    [Fact]
    public async Task MarketSurveillance_ShouldMarkInvalidFromTheDetailPageAndNobodyElse()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo).WithClustersAsync();
        var id = PassportPagesTests.Identifier(rig);
        Assert.True((await PassportPagesTests.Pages(rig).DetailAsync(Reader.With("msa@example.com", ReaderRole.MarketSurveillance), id))!.MayMarkInvalid);
        Assert.False((await PassportPagesTests.Pages(rig).DetailAsync(Reader.With("team", ReaderRole.Team), id))!.MayMarkInvalid); // the team uses its battery page

        var byUser = await Controller(rig, "user@example.com", "member").MarkInvalid(Uri.EscapeDataString(id), Reason, PassportMarkTests.Service(rig), CancellationToken.None);
        var surveillance = Controller(rig, "msa@example.com", "marketSurveillanceAuthority");
        var bySurveillance = await surveillance.MarkInvalid(Uri.EscapeDataString(id), Reason, PassportMarkTests.Service(rig), CancellationToken.None);

        Assert.IsType<NotFoundResult>(byUser);
        Assert.Equal(PassportPages.DetailHref(id), Assert.IsType<RedirectResult>(bySurveillance).Url);
        Assert.Equal("The passport is marked invalid. The team is the only one who can undo it.", surveillance.TempData[PassportController.MarkMessageKey]);
        Assert.Equal(DppStatuses.Invalid, await rig.Store.PassportStatusAsync(rig.PackId));
    }

    [Fact]
    public async Task TheRegistry_ShouldKeepAnInvalidBatteryWithItsStatusButNoManageLink()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo).WithClustersAsync();
        await PassportMarkTests.Service(rig).MarkInvalidAsync(PublicationTesting.Team, rig.PackId, Reason);
        var batteries = await BatteryTesting.Pages(rig).ListAsync(null);

        var surveillance = RegistryList.Build(Reader.With("msa", ReaderRole.MarketSurveillance), batteries);
        var admin = RegistryList.Build(
            Reader.With("admin", ReaderRole.ClusterAdmin, BatteryTesting.North), batteries, ChangeSender.ClusterAdmin("admin", BatteryTesting.North));

        var row = Assert.Single(surveillance.Rows);
        Assert.Equal(DppStatuses.Invalid, row.StatusName);
        Assert.Null(admin.ManageHref(Assert.Single(admin.Rows))); // nothing on it changes, so there is nothing to manage
    }

    [Fact]
    public void ThePages_ShouldDrawTheCardTheButtonAndTheReason()
    {
        var battery = File.ReadAllText(DppTestFiles.RepoFile("web", "Views", "Batteries", "Battery.cshtml"));
        var hero = File.ReadAllText(DppTestFiles.RepoFile("web", "Views", "Passport", "_PassportHero.cshtml"));
        var registry = File.ReadAllText(DppTestFiles.RepoFile("web", "Views", "Registry", "Index.cshtml"));

        Assert.Contains("~/Views/Batteries/_PassportStatusCard.cshtml\", marks", battery, StringComparison.Ordinal);
        Assert.Contains("Model.MayMarkInvalid", hero, StringComparison.Ordinal);
        Assert.Contains("Model.MarkReason is { } reason", hero, StringComparison.Ordinal);
        Assert.Contains("DppStatuses.Words(shown.DppStatus)", registry, StringComparison.Ordinal);
    }

    private static PassportController Controller(PublicationRig rig, string email, string role)
    {
        var http = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Email, email), new Claim(ClaimTypes.Role, role)], "test")),
        };
        return new PassportController(new DppReaders(new ClusterRepository(rig.Context)), PassportPagesTests.Pages(rig))
        {
            ControllerContext = new ControllerContext { HttpContext = http },
            TempData = new TempDataDictionary(http, new NoTempDataStorage()),
        };
    }

    private sealed class NoTempDataStorage : ITempDataProvider
    {
        public IDictionary<string, object> LoadTempData(HttpContext context) => new Dictionary<string, object>();

        public void SaveTempData(HttpContext context, IDictionary<string, object> values)
        {
        }
    }
}
