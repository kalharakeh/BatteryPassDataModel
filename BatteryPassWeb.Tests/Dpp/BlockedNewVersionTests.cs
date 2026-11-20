using System.Text.Json.Nodes;
using BatteryPassWeb.Dpp.Editing;
using BatteryPassWeb.Dpp.Pages;

namespace BatteryPassWeb.Tests.Dpp;

/// <summary>
/// R01, decision 9: a change to a model whose published passports can't get a new version (its questions unanswered, or
/// a value its answers ask for missing) is saved, the published versions stay, and the save says so instead of
/// claiming new versions.
/// </summary>
[Collection(MongoCollection.Name)]
public sealed class BlockedNewVersionTests(MongoFixture mongo)
{
    private const string ModelId = "model-test";

    [Fact]
    public async Task AChangeWhileTheModelIsUnanswered_ShouldSayThePassportKeptItsVersion()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo);
        await rig.Store.SetModelAnswersAsync(ModelId, null, null, null);

        var save = await PageTesting.Editor(rig).SaveAsync(PublicationTesting.Team, TargetKind.Model, ModelId, Mass(513), confirmed: true);

        Assert.Equal(EditorOutcome.Saved, save.Outcome);
        Assert.Empty(save.Affected);
        Assert.Equal(rig.PackId, Assert.Single(save.KeptTheirVersion).PackId);
        Assert.Equal(1, (await rig.Store.GetActiveVersionAsync(rig.PackId))!.Number);
    }

    [Fact]
    public async Task AnsweringTheQuestionsAfterwards_ShouldPublishTheChangeThatWaited()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo);
        await rig.Store.SetModelAnswersAsync(ModelId, null, null, null);
        await PageTesting.Editor(rig).SaveAsync(PublicationTesting.Team, TargetKind.Model, ModelId, Mass(513), confirmed: true);
        var pages = PageTesting.Pages(rig);
        var model = (await rig.Store.GetAsync<BatteryPassWeb.Dpp.Store.BatteryModel>(ModelId))!;

        await pages.SetModelAnswersAsync("team@example.com", model, new BatteryPassWeb.Dpp.Catalog.ModelAnswers(true, true, null), confirmed: true);
        Assert.Equal(1, (await rig.Store.GetActiveVersionAsync(rig.PackId))!.Number); // one question still open

        await pages.SetModelAnswersAsync("team@example.com", (await rig.Store.GetAsync<BatteryPassWeb.Dpp.Store.BatteryModel>(ModelId))!, new BatteryPassWeb.Dpp.Catalog.ModelAnswers(null, null, true), confirmed: true);
        var active = (await rig.Store.GetActiveVersionAsync(rig.PackId))!;

        Assert.Equal(2, active.Number);
        Assert.Equal("513", active.StaticCore["batteryPassportData"]!["identifiersAndProductData"]!["batteryMass"]!["gramKilogramValue"]!.ToJsonString());
    }

    [Fact]
    public async Task AnsweringWithNothingWaiting_ShouldPublishNoNewVersion()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo);
        var model = (await rig.Store.GetAsync<BatteryPassWeb.Dpp.Store.BatteryModel>(ModelId))!;

        await PageTesting.Pages(rig).SetModelAnswersAsync("team@example.com", model, new BatteryPassWeb.Dpp.Catalog.ModelAnswers(null, false, null), confirmed: true);

        Assert.Equal(1, (await rig.Store.GetActiveVersionAsync(rig.PackId))!.Number);
    }

    [Fact]
    public async Task AChangeToAnAnsweredModel_ShouldStillPublishTheNewVersions()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo);

        var save = await PageTesting.Editor(rig).SaveAsync(PublicationTesting.Team, TargetKind.Model, ModelId, Mass(513), confirmed: true);

        Assert.Equal(rig.PackId, Assert.Single(save.Affected).PackId);
        Assert.Empty(save.KeptTheirVersion);
        Assert.Equal(2, (await rig.Store.GetActiveVersionAsync(rig.PackId))!.Number);
    }

    private static EditorPost Mass(decimal kilograms) => new(
        new Dictionary<string, JsonNode>(StringComparer.Ordinal) { ["batteryMass"] = new JsonObject { ["gramKilogramUnit"] = "kg", ["gramKilogramValue"] = kilograms } },
        new Dictionary<string, string>(StringComparer.Ordinal),
        new Dictionary<string, int>(StringComparer.Ordinal));
}
