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

/// <summary>Hand-overs on the pages (mock-up S1, S2 and S8; step 4d).</summary>
[Collection(MongoCollection.Name)]
public sealed class HandOverPagesTests(MongoFixture mongo)
{
    private const string NorthAdmin = "north-admin@example.com";

    private const string SouthAdmin = "south-admin@example.com";

    [Fact]
    public async Task TheSendersPage_ShouldNameTheClusterFirstThenAskAndShowItWaiting()
    {
        var rig = await RigAsync();
        var sender = ClusterController(rig, NorthAdmin);

        var check = Assert.IsType<RedirectResult>(await sender.HandOver(rig.PackId, " Cluster-South ", null, HandOverTests.Service(rig), CancellationToken.None));
        Assert.Equal($"{ClusterBatteryPages.BatteryHref(rig.PackId)}#handover", check.Url);
        Assert.Equal(BatteryTesting.South, sender.TempData[ClusterBatteriesController.HandOverToKey]);
        Assert.Equal("Customer South", sender.TempData[ClusterBatteriesController.HandOverNameKey]);
        Assert.Null(await rig.Store.FindWaitingHandOverAsync(rig.PackId));

        // "Ask them to accept" sends the ID whose name was shown; a changed ID is named again instead.
        await sender.HandOver(rig.PackId, "cluster-other", BatteryTesting.South, HandOverTests.Service(rig), CancellationToken.None);
        Assert.Null(await rig.Store.FindWaitingHandOverAsync(rig.PackId));
        await sender.HandOver(rig.PackId, BatteryTesting.South, BatteryTesting.South, HandOverTests.Service(rig), CancellationToken.None);

        Assert.Equal("Asked. The battery stays with your cluster until Customer South accepts.", sender.TempData[BatteriesController.CardStatusKey]);
        var card = (await ClusterBatteryPagesTests.Pages(rig).BatteryAsync(ChangeSender.ClusterAdmin(NorthAdmin, BatteryTesting.North), rig.PackId))!.HandOver;
        Assert.Equal((BatteryTesting.South, "Customer South"), (card.Waiting!.ToClusterId, card.ReceiverName));
        Assert.Equal($"{ClusterBatteryPages.BatteryHref(rig.PackId)}/handover", card.Action);
    }

    [Fact]
    public async Task AnUnknownCluster_ShouldBeSaidOnTheCard()
    {
        var rig = await RigAsync();
        var sender = ClusterController(rig, NorthAdmin);

        await sender.HandOver(rig.PackId, "cluster-unknown", null, HandOverTests.Service(rig), CancellationToken.None);

        Assert.Equal($"Not asked: {HandOverService.NoSuchCluster}.", sender.TempData[BatteriesController.CardErrorKey]);
        Assert.Equal("handover", sender.TempData[BatteriesController.CardKey]);
        Assert.Null(sender.TempData[ClusterBatteriesController.HandOverNameKey]);
    }

    [Fact]
    public async Task TheSender_ShouldCancelTheWaitingHandOverFromItsCard()
    {
        var rig = await RigAsync();
        var sender = ClusterController(rig, NorthAdmin);
        await HandOverTests.Service(rig).AskAsync(ChangeSender.ClusterAdmin(NorthAdmin, BatteryTesting.North), rig.PackId, BatteryTesting.South, confirm: true);

        await sender.CancelHandOver(rig.PackId, HandOverTests.Service(rig), CancellationToken.None);

        Assert.Equal("Hand-over cancelled. The battery stays with your cluster.", sender.TempData[BatteriesController.CardStatusKey]);
        Assert.Null((await ClusterBatteryPagesTests.Pages(rig).BatteryAsync(ChangeSender.ClusterAdmin(NorthAdmin, BatteryTesting.North), rig.PackId))!.HandOver.Waiting);
    }

    [Fact]
    public async Task TheReceiversRegistry_ShouldShowItWaitingAndAcceptIt()
    {
        var rig = await RigAsync();
        var asked = await HandOverTests.Service(rig).AskAsync(ChangeSender.ClusterAdmin(NorthAdmin, BatteryTesting.North), rig.PackId, BatteryTesting.South, confirm: true);
        var receiver = RegistryController(rig, SouthAdmin);

        var page = (RegistryPage)Assert.IsType<ViewResult>(await receiver.Index(null, HandOverTests.Service(rig), CancellationToken.None)).Model!;
        var waiting = Assert.Single(page.Waiting);
        Assert.Equal(("Customer North", NorthAdmin), (waiting.FromName, waiting.HandOver.AskedBy));
        Assert.Empty(page.Rows); // Q11: nothing of the battery until it accepts
        Assert.Empty(((RegistryPage)((ViewResult)await RegistryController(rig, NorthAdmin).Index(null, HandOverTests.Service(rig), CancellationToken.None)).Model!).Waiting);

        var accepted = Assert.IsType<RedirectResult>(await receiver.Answer(asked.HandOver!.Id, accept: true, HandOverTests.Service(rig), CancellationToken.None));

        Assert.Equal("/registry", accepted.Url);
        Assert.Equal("Accepted. The battery is now your cluster's; open its battery page from its row below.", receiver.TempData[ProductDataController.StatusKey]);
        var after = (RegistryPage)((ViewResult)await receiver.Index(null, HandOverTests.Service(rig), CancellationToken.None)).Model!;
        Assert.Empty(after.Waiting);
        Assert.NotNull(after.ManageHref(Assert.Single(after.Rows)));
    }

    [Fact]
    public async Task TheReceiver_ShouldDeclineAndAnOutsiderShouldBeRefused()
    {
        var rig = await RigAsync();
        var asked = await HandOverTests.Service(rig).AskAsync(ChangeSender.ClusterAdmin(NorthAdmin, BatteryTesting.North), rig.PackId, BatteryTesting.South, confirm: true);
        var outsider = RegistryController(rig, NorthAdmin);
        var receiver = RegistryController(rig, SouthAdmin);

        await outsider.Answer(asked.HandOver!.Id, accept: true, HandOverTests.Service(rig), CancellationToken.None);
        await receiver.Answer(asked.HandOver.Id, accept: false, HandOverTests.Service(rig), CancellationToken.None);

        Assert.Equal($"Not accepted: {HandOverService.NotTheReceiver}.", outsider.TempData[ProductDataController.ErrorKey]);
        Assert.Equal("Declined. The battery stays with the sending cluster.", receiver.TempData[ProductDataController.StatusKey]);
        Assert.Equal(BatteryTesting.North, (await rig.Store.GetAsync<BatteryPack>(rig.PackId))!.ClusterId);
    }

    [Fact]
    public async Task ARefusedAccept_ShouldTellTheReceiverWhyInPlainWords()
    {
        var rig = await RigAsync();
        var asked = await HandOverTests.Service(rig).AskAsync(ChangeSender.ClusterAdmin(NorthAdmin, BatteryTesting.North), rig.PackId, BatteryTesting.South, confirm: true);
        await new PassportMarks(rig.Store, new ApplicationAuditService(rig.Context), rig.Clock).MarkInvalidAsync(PublicationTesting.Team, rig.PackId, "Wrong figures");
        var receiver = RegistryController(rig, SouthAdmin);

        await receiver.Answer(asked.HandOver!.Id, accept: true, HandOverTests.Service(rig), CancellationToken.None);

        Assert.Equal(
            "Not accepted: the battery's passport is no longer active (it was replaced by a new passport or marked invalid), so it can't move to your cluster. Decline it, or ask the sending cluster to cancel.",
            receiver.TempData[ProductDataController.ErrorKey]);
        Assert.Single(((RegistryPage)((ViewResult)await receiver.Index(null, HandOverTests.Service(rig), CancellationToken.None)).Model!).Waiting); // still there to decline
    }

    [Fact]
    public async Task TheTeamsPage_ShouldShowAWaitingHandOverAndCancelIt()
    {
        var rig = await RigAsync();
        await HandOverTests.Service(rig).AskAsync(ChangeSender.ClusterAdmin(NorthAdmin, BatteryTesting.North), rig.PackId, BatteryTesting.South, confirm: true);
        var team = BatteriesControllerTests.Controller(rig, new Dictionary<string, string>());

        Assert.Equal("Customer South", (await BatteryTesting.Pages(rig).BatteryAsync(rig.PackId))!.HandOverTo);
        var cancelled = Assert.IsType<RedirectResult>(await team.CancelHandOver(rig.PackId, HandOverTests.Service(rig), CancellationToken.None));

        Assert.Equal(BatteryPages.BatteryHref(rig.PackId), cancelled.Url);
        Assert.Equal("Hand-over cancelled. The battery stays in its cluster.", team.TempData[BatteriesController.CardStatusKey]);
        Assert.Null((await BatteryTesting.Pages(rig).BatteryAsync(rig.PackId))!.HandOverTo);
    }

    [Fact]
    public void ThePages_ShouldDrawTheCardTheWindowAndTheWaitingRows()
    {
        var cluster = File.ReadAllText(DppTestFiles.RepoFile("web", "Views", "ClusterAdmin", "Battery.cshtml"));
        var card = File.ReadAllText(DppTestFiles.RepoFile("web", "Views", "ClusterAdmin", "_HandOverCard.cshtml"));
        var window = File.ReadAllText(DppTestFiles.RepoFile("web", "Views", "ClusterAdmin", "_HandOverDialog.cshtml")); // at page level since R5
        var registry = File.ReadAllText(DppTestFiles.RepoFile("web", "Views", "Registry", "Index.cshtml"));
        var battery = File.ReadAllText(DppTestFiles.RepoFile("web", "Views", "Batteries", "Battery.cshtml"));
        var script = File.ReadAllText(DppTestFiles.RepoFile("web", "wwwroot", "js", "bp-record-editor.js"));
        var styles = File.ReadAllText(DppTestFiles.RepoFile("web", "wwwroot", "css", "bp-product-data.css"));

        Assert.Contains("~/Views/ClusterAdmin/_HandOverCard.cshtml\", Model.HandOver", cluster, StringComparison.Ordinal);
        Assert.Contains("data-open-on-load=\"@(found ? \"true\" : \"false\")\"", window, StringComparison.Ordinal);
        Assert.Contains("name=\"checkedId\"", window, StringComparison.Ordinal);
        Assert.Contains("TempData[ClusterBatteriesController.HandOverToKey] as string", window, StringComparison.Ordinal); // read once, so the window opens only right after "Find the cluster"
        Assert.DoesNotContain("TempData.Peek", window, StringComparison.Ordinal);
        Assert.Contains("Cancel the hand-over</button>", cluster, StringComparison.Ordinal); // one Cancel, on the header's line (redesign R4)
        Assert.DoesNotContain("/cancel", card, StringComparison.Ordinal);
        Assert.Contains("foreach (var waiting in Model.Waiting)", registry, StringComparison.Ordinal);
        Assert.Contains("bp-handover-row", registry, StringComparison.Ordinal);
        Assert.Contains("Model.HandOverTo is { } receiver", battery, StringComparison.Ordinal);
        Assert.Contains("dialog[data-open-on-load=\"true\"]", script, StringComparison.Ordinal);
        Assert.Contains(".bp-handover-row", styles, StringComparison.Ordinal);
    }

    private async Task<PublicationRig> RigAsync()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo).WithClustersAsync();
        var clusters = new ClusterRepository(rig.Context);
        await clusters.UpsertClusterMembershipAsync(NorthAdmin, BatteryTesting.North, "clusterAdmin");
        await clusters.UpsertClusterMembershipAsync(SouthAdmin, BatteryTesting.South, "clusterAdmin");
        return rig;
    }

    private static ClusterBatteriesController ClusterController(PublicationRig rig, string email) =>
        With(new ClusterBatteriesController(ClusterBatteryPagesTests.Pages(rig), new DppReaders(new ClusterRepository(rig.Context))), email);

    private static RegistryController RegistryController(PublicationRig rig, string email) =>
        With(new RegistryController(new DppReaders(new ClusterRepository(rig.Context)), BatteryTesting.Pages(rig)), email);

    private static T With<T>(T controller, string email)
        where T : Controller
    {
        var http = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Email, email), new Claim(ClaimTypes.Role, "member")], "test")),
        };
        http.Request.Form = new FormCollection(new Dictionary<string, StringValues>());
        controller.ControllerContext = new ControllerContext { HttpContext = http };
        controller.TempData = new TempDataDictionary(http, new NoTempDataStorage());
        return controller;
    }

    private sealed class NoTempDataStorage : ITempDataProvider
    {
        public IDictionary<string, object> LoadTempData(HttpContext context) => new Dictionary<string, object>();

        public void SaveTempData(HttpContext context, IDictionary<string, object> values)
        {
        }
    }
}
