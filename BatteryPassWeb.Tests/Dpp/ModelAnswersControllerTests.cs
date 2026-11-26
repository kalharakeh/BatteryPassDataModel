using System.Text.Json.Nodes;
using BatteryPassWeb.Controllers;
using BatteryPassWeb.Dpp.Catalog;
using BatteryPassWeb.Dpp.Editing;
using BatteryPassWeb.Dpp.Store;
using Microsoft.AspNetCore.Mvc;

namespace BatteryPassWeb.Tests.Dpp;

/// <summary>
/// Round two, G: saving a model's answers through the page's action (R01): the round trip, and "Save the answers
/// anyway" when a yes asks for values that published batteries lack.
/// </summary>
[Collection(MongoCollection.Name)]
public sealed class ModelAnswersControllerTests(MongoFixture mongo)
{
    private const string ModelId = "model-test";

    [Fact]
    public async Task SavingTheAnswers_ShouldStoreThemAndReturnToTheQuestions()
    {
        var rig = await PublicationTesting.RigAsync(mongo);
        await rig.Store.SetModelAnswersAsync(ModelId, null, null, null);
        var controller = PageTesting.Controller(rig);

        var result = Assert.IsType<RedirectResult>(await controller.SetAnswers(ModelId, "yes", "no", "yes", confirmed: false, CancellationToken.None));

        Assert.Equal("/admin/families/family-test?model=model-test#questions", result.Url);
        Assert.Equal("Answers saved. The passport doesn't show them; they decide which values it needs.", controller.TempData[ProductDataController.StatusKey]);
        Assert.Equal(new ModelAnswers(true, false, true), (await rig.Store.GetAsync<BatteryModel>(ModelId))!.Answers());

        var again = PageTesting.Controller(rig);
        await again.SetAnswers(ModelId, "yes", "no", "yes", confirmed: false, CancellationToken.None);
        Assert.Equal("Nothing changed.", again.TempData[ProductDataController.InfoKey]);
    }

    [Fact]
    public async Task AYesPublishedBatteriesLack_ShouldWaitForSaveTheAnswersAnyway()
    {
        var rig = await PublicationTesting.RigAsync(mongo);
        await rig.Store.SetModelAnswersAsync(ModelId, false, true, true);
        await rig.Pipeline.SaveAsync(PublicationTesting.Team, new ChangeSet(TargetKind.Pack, rig.PackId, new Dictionary<string, JsonNode?>(), "form", Removals: ["warrantyPeriod"]));
        Assert.Null((await rig.Publication.PublishAsync(PublicationTesting.Team, rig.PackId)).Problem);

        var asking = PageTesting.Controller(rig);
        await asking.SetAnswers(ModelId, "yes", null, null, confirmed: false, CancellationToken.None);

        Assert.Equal("model-test|warranty=yes|cycling=|cadmiumOrLead=", asking.TempData[ProductDataController.PendingAnswersKey]);
        Assert.Equal(1, asking.TempData[ProductDataController.PendingAffectedKey]);
        Assert.False((await rig.Store.GetAsync<BatteryModel>(ModelId))!.HasCommercialWarranty);

        var anyway = PageTesting.Controller(rig);
        await anyway.SetAnswers(ModelId, "yes", string.Empty, string.Empty, confirmed: true, CancellationToken.None);

        Assert.Null(anyway.TempData[ProductDataController.PendingAnswersKey]);
        Assert.StartsWith("Answers saved.", (string)anyway.TempData[ProductDataController.StatusKey]!, StringComparison.Ordinal);
        Assert.Equal(new ModelAnswers(true, true, true), (await rig.Store.GetAsync<BatteryModel>(ModelId))!.Answers());
        Assert.Equal(1, (await rig.Store.GetActiveVersionAsync(rig.PackId))!.Number); // the answers aren't passport data
    }
}
