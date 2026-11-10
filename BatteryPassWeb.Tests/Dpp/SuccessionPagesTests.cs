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
using Microsoft.Extensions.Primitives;

namespace BatteryPassWeb.Tests.Dpp;

/// <summary>The new linked passport on the pages (mock-up S4, S5 and S7; step 4c-1).</summary>
[Collection(MongoCollection.Name)]
public sealed class SuccessionPagesTests(MongoFixture mongo)
{
    private const string NorthAdmin = "north-admin@example.com";

    [Fact]
    public async Task TheWindow_ShouldOfferThePlacingOnTheMarketAgainChoices()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo).WithClustersAsync();

        var card = (await BatteryTesting.Pages(rig).BatteryAsync(rig.PackId))!.Status!;

        Assert.Equal(
            [StatusEvent.Repaired, StatusEvent.PlacedOnMarketAgain, StatusEvent.Remanufactured, StatusEvent.Repurposed, StatusEvent.Waste, StatusEvent.Corrected],
            card.Choices.Select(choice => choice.What));
        Assert.Null(card.Succession);
    }

    [Fact]
    public async Task AWaitingBattery_ShouldAskWhoPlacesItOnTheMarketAgain()
    {
        var rig = await new SuccessionTests(mongo).RepurposedAsync();
        var old = (await rig.Store.GetAsync<BatteryPack>(rig.PackId))!;

        var team = (await BatteryTesting.Pages(rig).BatteryAsync(rig.PackId))!.Status!;
        var admin = (await ClusterBatteryPagesTests.Pages(rig).BatteryAsync(ChangeSender.ClusterAdmin(NorthAdmin, BatteryTesting.North), rig.PackId))!.Status!;

        Assert.Equal("repurposed", team.Succession!.Status);
        Assert.Equal($"{BatteryPages.BatteryHref(rig.PackId)}/successor", team.Succession.Action);
        Assert.Contains(team.Succession.Operators, item => item.Id == old.OperatorId);
        Assert.Equal(old.OperatorId, team.Succession.DefaultOperatorId);
        Assert.Equal([StatusEvent.Corrected], team.Choices.Select(choice => choice.What)); // no other status change while waiting
        Assert.Empty(admin.Succession!.Operators); // the team makes Scania's
        Assert.False(admin.CanChange);
        Assert.Equal("The battery is waiting for its new passport.", admin.WhyNot);
    }

    [Fact]
    public async Task TheTeamsPage_ShouldMakeTheNewPassportAndOpenIt()
    {
        var rig = await new SuccessionTests(mongo).RepurposedAsync();
        var old = (await rig.Store.GetAsync<BatteryPack>(rig.PackId))!;
        var controller = BatteriesControllerTests.Controller(rig, new Dictionary<string, string>());

        var redirect = Assert.IsType<RedirectResult>(await controller.MakeSuccessor(rig.PackId, old.OperatorId, CancellationToken.None));

        var successorId = (await rig.Store.GetAsync<BatteryPack>(rig.PackId))!.Succession!.SuccessorPackId!;
        Assert.Equal($"{BatteryPages.BatteryHref(successorId)}#status", redirect.Url);
        Assert.StartsWith("The new passport is made as a draft.", (string)controller.TempData[BatteriesController.CardStatusKey]!, StringComparison.Ordinal);
        var successorCard = (await BatteryTesting.Pages(rig).BatteryAsync(successorId))!.Status!;
        Assert.Equal(new PredecessorView(Text(old, "batterySerialNumber")!, BatteryPages.BatteryHref(rig.PackId), Inactive: false), successorCard.Predecessor);
        var oldCard = (await BatteryTesting.Pages(rig).BatteryAsync(rig.PackId))!.Status!;
        Assert.True(oldCard.Succession!.SuccessorMade);
        Assert.False(oldCard.Succession.SuccessorPublished);
    }

    [Fact]
    public async Task TheClusterAdmin_ShouldLinkAnotherOperatorsPassportAndGoBackToTheRegistry()
    {
        var rig = await new SuccessionTests(mongo).RepurposedAsync();
        await new ClusterRepository(rig.Context).UpsertClusterMembershipAsync(NorthAdmin, BatteryTesting.North, "clusterAdmin");
        var controller = ClusterController(rig);

        var redirect = Assert.IsType<RedirectResult>(await controller.OtherOperator(rig.PackId, "urn:test:op", "Reman AB", "https://example.com/p/1", link: true, CancellationToken.None));

        Assert.Equal("/registry", redirect.Url); // the battery's page closes: its passport is inactive
        Assert.Equal("Linked: the new passport is named, and this passport is inactive.", controller.TempData[BatteriesController.CardStatusKey]);
        Assert.Equal(DppStatuses.Inactive, await rig.Store.PassportStatusAsync(rig.PackId));
    }

    [Fact]
    public async Task TheTeamsPage_ShouldCancelTheNewPassport()
    {
        var rig = await new SuccessionTests(mongo).RepurposedAsync();
        var controller = BatteriesControllerTests.Controller(rig, new Dictionary<string, string>());

        Assert.IsType<RedirectResult>(await controller.CancelSuccessor(rig.PackId, CancellationToken.None));

        Assert.Equal("The new passport is cancelled, and the old status is back.", controller.TempData[BatteriesController.CardStatusKey]);
        Assert.Null((await rig.Store.GetAsync<BatteryPack>(rig.PackId))!.Succession);
    }

    [Fact]
    public async Task ThePassportPages_ShouldLinkTheOldAndTheNewPassport()
    {
        var rig = await new SuccessionTests(mongo).InactiveAsync();
        var old = (await rig.Store.GetAsync<BatteryPack>(rig.PackId))!;
        var successor = (await rig.Store.GetAsync<BatteryPack>(old.Succession!.SuccessorPackId!))!;
        var oldId = Text(old, "uniqueProductIdentifier")!;
        var newId = Text(successor, "uniqueProductIdentifier")!;
        var pages = PassportPagesTests.Pages(rig);

        var oldSummary = (await pages.SummaryAsync(Reader.Anonymous, oldId))!;
        var newDetail = (await pages.DetailAsync(Reader.With("user", ReaderRole.ClusterUser, BatteryTesting.North), newId))!;

        var day = ValueWords.Day(old.Succession.StartedAtUtc.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture));
        Assert.Equal("Inactive", oldSummary.StatusWords);
        Assert.Equal(new PassportNotice("Inactive", $"This battery continues on a new passport since {day}.", PassportPages.SummaryHref(newId), "New passport") { Calm = true }, oldSummary.Notice); // no #22 word for anyone
        Assert.Equal(new PassportNotice("Active", $"Repurposed on {day}. Its earlier passport is inactive and stays readable.", PassportPages.DetailHref(oldId), "Earlier passport") { Calm = true }, newDetail.Notice);
    }

    [Fact]
    public async Task AnotherOperatorsPassport_ShouldBeNamedOnTheOldPassport()
    {
        var rig = await new SuccessionTests(mongo).RepurposedAsync();
        await SuccessionTests.Service(rig).SaveOtherOperatorAsync(PublicationTesting.Team, rig.PackId, new OtherOperator(null, "Example Remanufacturing AB", "https://example.com/p/1"), link: true);
        var old = (await rig.Store.GetAsync<BatteryPack>(rig.PackId))!;
        var oldId = Text(old, "uniqueProductIdentifier")!;
        var day = ValueWords.Day(old.Succession!.StartedAtUtc.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture));

        var summary = (await PassportPagesTests.Pages(rig).SummaryAsync(Reader.Anonymous, oldId))!;

        Assert.Equal(new PassportNotice("Inactive", $"This battery continues on a new passport issued by Example Remanufacturing AB since {day}.", "https://example.com/p/1", "New passport") { Calm = true }, summary.Notice);
    }

    [Fact]
    public async Task TheListsAndTheRegistry_ShouldTellAnInactivePassportFromADraft()
    {
        var rig = await new SuccessionTests(mongo).InactiveAsync();

        var rows = (await BatteryTesting.Pages(rig).ListAsync(null)).Rows;
        var registry = RegistryList.Build(Reader.With("team", ReaderRole.Team), await BatteryTesting.Pages(rig).ListAsync(null));

        var old = rows.Single(row => row.PackId == rig.PackId);
        Assert.False(old.IsDraft);
        Assert.Equal(DppStatuses.Inactive, old.StatusName);
        Assert.Null(old.Active);
        Assert.DoesNotContain(registry.Rows, row => row.PackId == rig.PackId); // it continues on its new passport
        Assert.Single(registry.Rows);
        var page = (await BatteryTesting.Pages(rig).BatteryAsync(rig.PackId))!;
        Assert.True(page.Publication.NoLongerActive);
        Assert.True(page.Cards.Frozen); // no live values or accident report forms
        Assert.False(page.Status!.CanChange);
    }

    private static string? Text(BatteryPack pack, string property) =>
        pack.Values.GetValueOrDefault(property)?.Value is System.Text.Json.Nodes.JsonValue value && value.TryGetValue<string>(out var text) ? text : null;

    private static ClusterBatteriesController ClusterController(PublicationRig rig)
    {
        var http = new DefaultHttpContext
        {
            User = TestAccounts.SignedIn(rig.Context, NorthAdmin, "clusterAdmin"),
        };
        http.Request.Form = new FormCollection(new Dictionary<string, StringValues>());
        return new ClusterBatteriesController(ClusterBatteryPagesTests.Pages(rig), new DppReaders(new ClusterRepository(rig.Context)), UploadCheckTests.Pages(rig))
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
