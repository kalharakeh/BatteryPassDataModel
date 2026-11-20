using BatteryPassWeb.Dpp.Editing;
using BatteryPassWeb.Dpp.Pages;
using BatteryPassWeb.Dpp.Store;

namespace BatteryPassWeb.Tests.Dpp;

/// <summary>
/// Round two, B: a model whose three questions aren't answered is never shown as complete: not in its editor's summary,
/// not on its batches' page, not on its batteries' values tab, even with every value filled in.
/// </summary>
[Collection(MongoCollection.Name)]
public sealed class UnansweredNeverCompleteTests(MongoFixture mongo)
{
    private const string ModelId = "model-test";

    [Fact]
    public async Task TheModelEditor_ShouldNotSayCompleteWhileTheQuestionsAreOpen()
    {
        var rig = await PublicationTesting.RigAsync(mongo);
        await rig.Store.SetModelAnswersAsync(ModelId, true, null, true);

        var editor = (await PageTesting.Pages(rig).FamilyAsync("family-test", ModelId))!.ModelEditor!;

        Assert.Equal(0, editor.Missing); // every value is there
        Assert.True(editor.QuestionsUnanswered);
        Assert.False(editor.IsComplete);
    }

    [Fact]
    public async Task TheBatchPage_ShouldSayTheModelHasQuestionsToAnswer()
    {
        var rig = await PublicationTesting.RigAsync(mongo);
        await rig.Store.SetModelAnswersAsync(ModelId, null, null, null);

        var model = (await rig.Store.GetAsync<BatteryModel>(ModelId))!;

        Assert.Equal((false, "Questions to answer"), Completeness.OfModel(model));
        await rig.Store.SetModelAnswersAsync(ModelId, true, true, true);
        Assert.Equal((true, "Complete"), Completeness.OfModel((await rig.Store.GetAsync<BatteryModel>(ModelId))!));
    }

    [Fact]
    public async Task TheBatterysValuesTab_ShouldNotSayAllFilledIn()
    {
        var rig = await PublicationTesting.RigAsync(mongo).WithClustersAsync();
        await rig.Store.SetModelAnswersAsync(ModelId, null, null, null);

        var page = (await BatteryTesting.Pages(rig).BatteryAsync(rig.PackId))!;

        Assert.Equal("Model questions to answer", page.Sections.Single(section => section.Id == "values").Hint);
        Assert.False(page.Editor.IsComplete);
    }

    [Fact]
    public async Task AnAnsweredCompleteModel_ShouldStillSayComplete()
    {
        var rig = await PublicationTesting.RigAsync(mongo).WithClustersAsync();

        var editor = (await PageTesting.Pages(rig).FamilyAsync("family-test", ModelId))!.ModelEditor!;
        var page = (await BatteryTesting.Pages(rig).BatteryAsync(rig.PackId))!;

        Assert.True(editor.IsComplete);
        Assert.Equal("All filled in", page.Sections.Single(section => section.Id == "values").Hint);
    }
}
