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

/// <summary>The battery status card and its window on both battery pages, and the repair history on the detail page (step 4b).</summary>
[Collection(MongoCollection.Name)]
public sealed class StatusPagesTests(MongoFixture mongo)
{
    private const string NorthAdmin = "north-admin@example.com";

    [Fact]
    public async Task TheCard_ShouldShowTheStatusAndFillInTheBatterysCurrentValues()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo).WithClustersAsync();

        var card = (await BatteryTesting.Pages(rig).BatteryAsync(rig.PackId))!.Status!;

        Assert.Equal("original", card.Status);
        Assert.Equal("Original", card.StatusWords);
        Assert.True(card.CanChange);
        Assert.Empty(card.Repairs);
        Assert.Equal(200m, card.RatedCapacityAh);
        Assert.Equal($"{BatteryPages.BatteryHref(rig.PackId)}/status", card.Restated.SaveAction);
        var fields = card.Restated.Sections.SelectMany(section => section.Fields).ToArray();
        Assert.Equal([59, 69, 74, 79, 81], fields.Select(field => field.Attribute.Number));
        Assert.Equal("4", fields[0].Text(BatteryTesting.Field("capacityFade"))); // the battery's own value
        Assert.Equal("12", fields[4].Text(BatteryTesting.Field("expectedLifetimeInCalendarYears"))); // the model's, until re-stated
        Assert.Equal([StatusEvent.Repaired, StatusEvent.PlacedOnMarketAgain, StatusEvent.Remanufactured, StatusEvent.Repurposed, StatusEvent.Waste, StatusEvent.Corrected], card.Choices.Select(choice => choice.What));
        Assert.True(card.MayAddNote);
        Assert.Equal($"{BatteryPages.BatteryHref(rig.PackId)}/status/note", card.NoteAction);
    }

    [Fact]
    public async Task TheClusterAdminsCard_ShouldOfferNoCorrection()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo).WithClustersAsync();

        var card = (await ClusterBatteryPagesTests.Pages(rig).BatteryAsync(ChangeSender.ClusterAdmin(NorthAdmin, BatteryTesting.North), rig.PackId))!.Status!;

        Assert.Equal([StatusEvent.Repaired, StatusEvent.PlacedOnMarketAgain, StatusEvent.Remanufactured, StatusEvent.Repurposed, StatusEvent.Waste], card.Choices.Select(choice => choice.What));
        Assert.True(card.MayAddNote);
    }

    [Fact]
    public async Task ADraftOrAWasteBattery_ShouldOfferNoStatusChange()
    {
        var draft = await PublicationTesting.RigAsync(mongo).WithClustersAsync();
        var waste = await PublicationTesting.PublishedRigAsync(mongo).WithClustersAsync();
        await StatusChangeTests.Service(waste).ChangeAsync(PublicationTesting.Team, waste.PackId, new StatusChange(StatusEvent.Waste, await StatusChangeTests.CurrentAsync(waste)));

        var draftCard = (await BatteryTesting.Pages(draft).BatteryAsync(draft.PackId))!.Status!;
        var wasteCard = (await BatteryTesting.Pages(waste).BatteryAsync(waste.PackId))!.Status!;

        Assert.False(draftCard.CanChange);
        Assert.Equal("The status changes once the passport is published.", draftCard.WhyNot);
        Assert.True(wasteCard.CanChange); // the team may still correct the values
        Assert.Equal([StatusEvent.Corrected, StatusEvent.WasteUndone], wasteCard.Choices.Select(choice => choice.What)); // R10: the team may undo a waste
        Assert.Equal("Waste", wasteCard.StatusWords);
        Assert.False(draftCard.MayAddNote);
    }

    [Fact]
    public async Task TheWindow_ShouldBeReadLikeTheEditorsAndSaved()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo).WithClustersAsync();
        var form = await WindowAsync(rig, ("capacityFade", "7")); // whole percent, as the schema says

        var result = (await StatusChangeTests.Pages(rig).ChangeAsync(PublicationTesting.Team, rig.PackId, "Repaired", PageTesting.Field(form), "2026-10-20", "Module 4 replaced."))!;

        Assert.Null(result.Problem);
        Assert.Equal("re-used", result.NewStatus);
        Assert.Equal(("Saved: the battery is re-used. Version 2 of the passport is published.", (string?)null), StatusChangeTests.Pages(rig).Message(result, versionBefore: 1));
        var pack = (await rig.Store.GetAsync<BatteryPack>(rig.PackId))!;
        Assert.Equal(7, pack.Values["capacityFade"].Value!["percentageValue"]!.GetValue<int>());
        var card = (await BatteryTesting.Pages(rig).BatteryAsync(rig.PackId))!.Status!;
        Assert.True(card.AfterRepair);
        Assert.Equal("Module 4 replaced.", Assert.Single(card.Repairs).Description);
    }

    [Fact]
    public async Task ARefusal_ShouldSayWhatAndWhichValueInWords()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo).WithClustersAsync();

        var none = (await StatusChangeTests.Pages(rig).ChangeAsync(PublicationTesting.Team, rig.PackId, "Lost", PageTesting.Field(await WindowAsync(rig)), null, null))!;
        var empty = (await StatusChangeTests.Pages(rig).ChangeAsync(PublicationTesting.Team, rig.PackId, "Waste", PageTesting.Field(await WindowAsync(rig, ("capacityFade", ""))), null, null))!;

        Assert.Equal((null, "Nothing was changed: choose what happened."), StatusChangeTests.Pages(rig).Message(none, 1));
        var (_, problem) = StatusChangeTests.Pages(rig).Message(empty, 1);
        Assert.Equal($"Nothing was changed: {StatusChangeService.ValuesRefused}. Capacity fade: it needs a value.", problem);
    }

    [Fact]
    public async Task TheTeamsPage_ShouldChangeTheStatusAndOpenAtTheCard()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo).WithClustersAsync();
        var controller = BatteriesControllerTests.Controller(rig, await WindowAsync(rig));

        var redirect = Assert.IsType<RedirectResult>(await controller.ChangeStatus(rig.PackId, "Waste", null, null, CancellationToken.None));

        Assert.Equal($"{BatteryPages.BatteryHref(rig.PackId)}#status", redirect.Url);
        Assert.Equal("status", controller.TempData[BatteriesController.CardKey]);
        Assert.Equal("Saved: the battery is waste. Version 2 of the passport is published.", controller.TempData[BatteriesController.CardStatusKey]);
    }

    [Fact]
    public async Task TheClusterAdminsPage_ShouldChangeTheStatusOfTheirOwnBatteryOnly()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo).WithClustersAsync();
        var clusters = new ClusterRepository(rig.Context);
        await clusters.UpsertClusterMembershipAsync(NorthAdmin, BatteryTesting.North, "clusterAdmin");
        await clusters.UpsertClusterMembershipAsync("south-admin@example.com", BatteryTesting.South, "clusterAdmin");
        var form = await WindowAsync(rig);

        var other = await ClusterController(rig, "south-admin@example.com", form).ChangeStatus(rig.PackId, "Repaired", "2026-10-20", "Fan replaced.", CancellationToken.None);
        var own = ClusterController(rig, NorthAdmin, form);
        var redirect = Assert.IsType<RedirectResult>(await own.ChangeStatus(rig.PackId, "Repaired", "2026-10-20", "Fan replaced.", CancellationToken.None));

        Assert.IsType<NotFoundResult>(other);
        Assert.Equal($"{ClusterBatteryPages.BatteryHref(rig.PackId)}#status", redirect.Url);
        Assert.Equal("Saved: the battery is re-used. Version 2 of the passport is published.", own.TempData[BatteriesController.CardStatusKey]);
        var repair = Assert.Single((await rig.Store.GetAsync<BatteryPack>(rig.PackId))!.Repairs);
        Assert.Equal(NorthAdmin, repair.RecordedBy);
        Assert.Equal(nameof(SenderKind.ClusterAdmin), repair.RecordedAs);
    }

    [Fact]
    public async Task BothPages_ShouldAddANoteAndOpenAtTheCard()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo).WithClustersAsync();
        await new ClusterRepository(rig.Context).UpsertClusterMembershipAsync(NorthAdmin, BatteryTesting.North, "clusterAdmin");
        var team = BatteriesControllerTests.Controller(rig, new Dictionary<string, string>());
        var admin = ClusterController(rig, NorthAdmin, new Dictionary<string, string>());

        var byTeam = Assert.IsType<RedirectResult>(await team.AddHistoryNote(rig.PackId, "Checked again: the seal holds.", CancellationToken.None));
        var empty = Assert.IsType<RedirectResult>(await admin.AddHistoryNote(rig.PackId, " ", CancellationToken.None));

        Assert.Equal($"{BatteryPages.BatteryHref(rig.PackId)}#status", byTeam.Url);
        Assert.Equal("Note added to the history.", team.TempData[BatteriesController.CardStatusKey]);
        Assert.Equal($"{ClusterBatteryPages.BatteryHref(rig.PackId)}#status", empty.Url);
        Assert.Equal($"The note wasn't added: {StatusChangeService.NoteNeedsText}.", admin.TempData[BatteriesController.CardErrorKey]);
        Assert.True(Assert.Single((await rig.Store.GetAsync<BatteryPack>(rig.PackId))!.Repairs).IsCorrection);
    }

    [Theory]
    [InlineData(ReaderRole.ClusterUser, true)]
    [InlineData(ReaderRole.MarketSurveillance, true)]
    [InlineData(ReaderRole.Team, true)]
    [InlineData(ReaderRole.Commission, false)]
    [InlineData(ReaderRole.LiRepair, false)]
    public async Task TheDetailPage_ShouldShowTheRepairHistoryToThoseWhoSeeTheStatus(ReaderRole role, bool sees)
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo).WithClustersAsync();
        await StatusChangeTests.Pages(rig).ChangeAsync(PublicationTesting.Team, rig.PackId, "Repaired", PageTesting.Field(await WindowAsync(rig)), "2026-10-20", "Fan replaced.");
        var reader = role == ReaderRole.ClusterUser ? Reader.With("user", role, BatteryTesting.North) : Reader.With("reader", role);

        var page = (await PassportPagesTests.Pages(rig).DetailAsync(reader, PassportPagesTests.Identifier(rig)))!;

        Assert.Equal(sees, page.Repairs is { Count: 1 });
    }

    [Fact]
    public void ThePages_ShouldDrawTheCardTheWindowsScriptAndTheHistory()
    {
        var team = File.ReadAllText(DppTestFiles.RepoFile("web", "Views", "Batteries", "Battery.cshtml"));
        var clusterAdmin = File.ReadAllText(DppTestFiles.RepoFile("web", "Views", "ClusterAdmin", "Battery.cshtml"));
        var tabs = File.ReadAllText(DppTestFiles.RepoFile("web", "Views", "Passport", "_PassportTabs.cshtml"));
        var css = File.ReadAllText(DppTestFiles.RepoFile("web", "wwwroot", "css", "bp-product-data.css"));

        Assert.All(new[] { team, clusterAdmin }, page =>
        {
            Assert.Contains("~/Views/Batteries/_StatusCard.cshtml\", status", page, StringComparison.Ordinal);
            Assert.Contains("~/js/bp-status-change.js", page, StringComparison.Ordinal);
        });
        Assert.Contains("Model.Repairs is { } repairs", tabs, StringComparison.Ordinal);
        Assert.Contains("repair.IsCorrection", tabs, StringComparison.Ordinal);
        Assert.Contains("repair.IsCorrection", File.ReadAllText(DppTestFiles.RepoFile("web", "Views", "Batteries", "_StatusCard.cshtml")), StringComparison.Ordinal);
        Assert.Contains("Purchaser editing and lifecycle (step 4)", css, StringComparison.Ordinal);
        Assert.Contains(".bp-history", css, StringComparison.Ordinal);
    }

    /// <summary>The status window as it opens, with the given fields typed over.</summary>
    private static async Task<Dictionary<string, string>> WindowAsync(PublicationRig rig, params (string Property, string Text)[] typed)
    {
        var card = (await BatteryTesting.Pages(rig).BatteryAsync(rig.PackId))!.Status!;
        var form = card.Restated.Sections.SelectMany(section => section.Fields)
            .SelectMany(field => field.Form.Inputs.Select(input => field.Form.FieldName(input)).Select(name => (name, text: field.Text(name))))
            .ToDictionary(entry => entry.name, entry => entry.text, StringComparer.Ordinal);
        foreach (var (property, text) in typed)
        {
            form[BatteryTesting.Field(property)] = text;
        }

        return form;
    }

    private static ClusterBatteriesController ClusterController(PublicationRig rig, string email, IReadOnlyDictionary<string, string> form)
    {
        var http = new DefaultHttpContext
        {
            User = TestAccounts.SignedIn(rig.Context, email, "clusterAdmin"),
        };
        http.Request.Form = new FormCollection(form.ToDictionary(entry => entry.Key, entry => new StringValues(entry.Value)));
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
