using System.Text.Json.Nodes;
using BatteryPassWeb.Dpp.Catalog;
using BatteryPassWeb.Dpp.Editing;
using BatteryPassWeb.Dpp.Pages;
using BatteryPassWeb.Dpp.Publication;
using BatteryPassWeb.Dpp.Store;

namespace BatteryPassWeb.Tests.Dpp;

/// <summary>
/// R01 on the pages: a new model starts unanswered and its batteries can't publish; every completeness line follows the
/// model's answers; a "yes" that published batteries can't meet says how many before it is saved.
/// </summary>
[Collection(MongoCollection.Name)]
public sealed class ModelQuestionsPagesTests(MongoFixture mongo)
{
    private const string Who = "team@example.com";
    private const string ModelId = "model-test";

    [Fact]
    public async Task ANewModel_ShouldStartWithItsThreeQuestionsUnanswered()
    {
        var rig = await PublicationTesting.RigAsync(mongo);
        var pages = PageTesting.Pages(rig);

        var modelId = (await pages.CreateModelAsync(Who, "family-test", "C7M-2"))!;
        var copyId = (await pages.CreateModelAsync(Who, "family-test", "C7M-3", copyFromId: ModelId))!;

        Assert.Equal(ModelAnswers.NotAnswered, (await rig.Store.GetAsync<BatteryModel>(modelId))!.Answers());
        Assert.Equal(ModelAnswers.NotAnswered, (await rig.Store.GetAsync<BatteryModel>(copyId))!.Answers());
        var chips = (await pages.FamilyAsync("family-test", modelId))!.Models.ToDictionary(chip => chip.Model.Id);
        Assert.False(chips[modelId].Answered);
        Assert.True(chips[ModelId].Answered);
        Assert.Equal(2, Assert.Single((await pages.FamiliesAsync()).Families).Unanswered);
    }

    [Fact]
    public async Task ABatteryOfAnUnansweredModel_ShouldNotPublish_AndItsPageShouldAskForTheAnswers()
    {
        var rig = await PublicationTesting.RigAsync(mongo);
        await rig.Store.SetModelAnswersAsync(ModelId, null, null, null);

        var result = await rig.Publication.PublishAsync(PublicationTesting.Team, rig.PackId);
        var page = (await BatteryTesting.Pages(rig).BatteryAsync(rig.PackId))!;

        Assert.Null(result.Version);
        Assert.Contains(PassportNeeds.QuestionsNotAnswered, result.Report!.Blocking);
        Assert.False(page.Publication.Ready);
        Assert.Equal("/admin/families/family-test?model=model-test#questions", page.Publication.QuestionsHref);
        Assert.DoesNotContain(PassportNeeds.QuestionsNotAnswered, page.Publication.Problems);
    }

    [Fact]
    public async Task AModelInUseThatLosesACycleValue_ShouldNoLongerSayItIsComplete()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo);
        await rig.Pipeline.SaveAsync(PublicationTesting.Team, new ChangeSet(TargetKind.Model, ModelId, new Dictionary<string, JsonNode?>(), "form", Removals: ["expectedLifetimeNumberOfChargeDischargeCycles"]));
        var answers = (await rig.Store.GetAsync<BatteryModel>(ModelId))!.Answers();

        // Cycling is yes: the published passport needs #82, so the team can't remove it.
        Assert.True((await rig.Store.GetAsync<BatteryModel>(ModelId))!.Values.ContainsKey("expectedLifetimeNumberOfChargeDischargeCycles"));

        // A model that lacks it (stored before the rule) counts it as missing, and the battery page says why.
        var values = (await rig.Store.GetAsync<BatteryModel>(ModelId))!.Values;
        values.Remove("expectedLifetimeNumberOfChargeDischargeCycles");
        await rig.Store.SetValuesAsync<BatteryModel>(ModelId, new Dictionary<string, BatteryPassWeb.Dpp.Values.AttributeValue>(), ["expectedLifetimeNumberOfChargeDischargeCycles"]);
        var chip = (await PageTesting.Pages(rig).FamilyAsync("family-test", ModelId))!.Models.Single(item => item.Model.Id == ModelId);
        var page = (await BatteryTesting.Pages(rig).BatteryAsync(rig.PackId))!;

        Assert.Equal(1, RecordEditor.CountMissing(TargetKind.Model, values, answers));
        Assert.Equal(1, chip.Missing);
        var group = Assert.Single(page.Publication.Missing);
        Assert.Equal("#82 Expected lifetime - Number of charge-discharge cycles (needed because this model is made for cycling use)", group.Line(Assert.Single(group.Attributes)));
    }

    [Fact]
    public async Task AYesThatPublishedBatteriesCantMeet_ShouldSayHowManyBeforeSaving()
    {
        var rig = await PublicationTesting.RigAsync(mongo);
        await rig.Store.SetModelAnswersAsync(ModelId, false, true, true);
        await rig.Pipeline.SaveAsync(PublicationTesting.Team, new ChangeSet(TargetKind.Pack, rig.PackId, new Dictionary<string, JsonNode?>(), "form", Removals: ["warrantyPeriod"]));
        Assert.Null((await rig.Publication.PublishAsync(PublicationTesting.Team, rig.PackId)).Problem);
        var pages = PageTesting.Pages(rig);
        var model = (await rig.Store.GetAsync<BatteryModel>(ModelId))!;

        var asked = await pages.SetModelAnswersAsync(Who, model, new ModelAnswers(true, null, null), confirmed: false);

        Assert.Equal(new AnswersSave(false, 1, NeedsConfirmation: true), asked);
        Assert.False((await rig.Store.GetAsync<BatteryModel>(ModelId))!.HasCommercialWarranty);

        var saved = await pages.SetModelAnswersAsync(Who, model, new ModelAnswers(true, null, null), confirmed: true);

        Assert.True(saved.Saved);
        Assert.Equal(new ModelAnswers(true, true, true), (await rig.Store.GetAsync<BatteryModel>(ModelId))!.Answers());
        Assert.Equal(1, (await rig.Store.GetActiveVersionAsync(rig.PackId))!.Number); // the answers aren't passport data
    }

    [Fact]
    public async Task AnAnswerThatAsksForNothingMissing_ShouldSaveAtOnce_AndAnAnswerIsNeverTakenBack()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo);
        var pages = PageTesting.Pages(rig);
        var model = (await rig.Store.GetAsync<BatteryModel>(ModelId))!;

        var nothing = await pages.SetModelAnswersAsync(Who, model, ModelAnswers.NotAnswered, confirmed: false);
        var no = await pages.SetModelAnswersAsync(Who, model, new ModelAnswers(null, false, null), confirmed: false);

        Assert.False(nothing.Saved);
        Assert.True(no.Saved);
        Assert.Equal(new ModelAnswers(true, false, true), (await rig.Store.GetAsync<BatteryModel>(ModelId))!.Answers());
    }

    [Fact]
    public async Task TheModelEditor_ShouldSayForEachConditionalFieldWhetherThisModelNeedsIt()
    {
        var rig = await PublicationTesting.RigAsync(mongo);
        await rig.Store.SetModelAnswersAsync(ModelId, true, false, null);

        var page = (await PageTesting.Pages(rig).FamilyAsync("family-test", ModelId))!;
        var fields = page.ModelEditor!.Sections.SelectMany(section => section.Fields).ToDictionary(field => field.Attribute.Number);

        Assert.Equal("Not needed: this model isn't made for cycling use.", fields[82].NeedLine);
        Assert.Equal("Needed only if this model's cadmium or lead is above the limit for the symbols.", fields[24].NeedLine);
        Assert.Null(fields[21].NeedLine);
    }
}
