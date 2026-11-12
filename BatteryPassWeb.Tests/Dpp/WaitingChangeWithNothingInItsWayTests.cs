using System.Text.Json.Nodes;
using BatteryPassWeb.Controllers;
using BatteryPassWeb.Dpp.Editing;
using BatteryPassWeb.Dpp.Publication;
using BatteryPassWeb.Services;
using Microsoft.AspNetCore.Mvc;

namespace BatteryPassWeb.Tests.Dpp;

/// <summary>
/// Round three, 4: a waiting change with nothing in its way is published: when the team undoes a mark, and when the
/// team presses "Publish the waiting changes" on the battery's page. The log says it carries the waiting changes (5).
/// </summary>
[Collection(MongoCollection.Name)]
public sealed class WaitingChangeWithNothingInItsWayTests(MongoFixture mongo)
{
    private const string ModelId = "model-test";

    [Fact]
    public async Task UndoingAMark_ShouldPublishAChangeSavedWhileItWasMarked()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo).WithClustersAsync();
        var marks = new PassportMarks(rig.Store, TestDemo.For(rig), new ApplicationAuditService(rig.Context), rig.Clock, rig.Publication);
        Assert.Null(await marks.MarkInvalidAsync(PublicationTesting.Team, rig.PackId, "Wrong battery"));
        await SaveMassAsync(rig, 513); // saved; the marked passport gets no version

        var (problem, published) = await marks.UndoWithOutcomeAsync(PublicationTesting.Team, rig.PackId);

        Assert.Null(problem);
        Assert.Equal(PublishOutcome.NewVersion, published!.Outcome);
        var active = (await rig.Store.GetActiveVersionAsync(rig.PackId))!;
        Assert.Equal(2, active.Number);
        Assert.Equal("513", active.StaticCore["batteryPassportData"]!["identifiersAndProductData"]!["batteryMass"]!["gramKilogramValue"]!.ToJsonString());
    }

    [Fact]
    public async Task PublishTheWaitingChanges_ShouldPublishThem_WhenNothingBlocks()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo).WithClustersAsync();
        await rig.Store.SetModelAnswersAsync(ModelId, null, null, null);
        await SaveMassAsync(rig, 513); // held back
        await rig.Store.SetModelAnswersAsync(ModelId, true, true, true); // answered behind the page's back: nothing republished

        var page = (await BatteryTesting.Pages(rig).BatteryAsync(rig.PackId))!;
        Assert.Empty(page.Publication.Waiting!.Blocking); // the page offers "Publish the waiting changes"

        var controller = BatteriesControllerTests.Controller(rig, new Dictionary<string, string>());
        Assert.IsType<RedirectResult>(await controller.PublishWaiting(rig.PackId, rig.Publication, CancellationToken.None));

        Assert.Equal(2, (await rig.Store.GetActiveVersionAsync(rig.PackId))!.Number);
        Assert.Equal("Version 2 of the passport is published.", controller.TempData[BatteriesController.CardStatusKey]);
    }

    [Fact]
    public async Task PublishTheWaitingChanges_ShouldSayWhatStillBlocks()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo).WithClustersAsync();
        await rig.Store.SetModelAnswersAsync(ModelId, null, null, null);
        await SaveMassAsync(rig, 513);

        var outcome = await rig.Publication.PublishWaitingAsync(PublicationTesting.Team, rig.PackId);

        Assert.Equal(PublishOutcome.HeldBack, outcome!.Outcome);
        Assert.Equal(1, (await rig.Store.GetActiveVersionAsync(rig.PackId))!.Number);
    }

    private static Task SaveMassAsync(PublicationRig rig, int kilograms) =>
        rig.Pipeline.SaveAsync(PublicationTesting.Team, new ChangeSet(TargetKind.Model, ModelId, new Dictionary<string, JsonNode?> { ["batteryMass"] = new JsonObject { ["gramKilogramUnit"] = "kg", ["gramKilogramValue"] = kilograms } }, "form"));
}
