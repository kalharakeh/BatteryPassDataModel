using System.Text.Json.Nodes;
using BatteryPassWeb.Controllers;
using BatteryPassWeb.Dpp.Catalog;
using BatteryPassWeb.Dpp.Documents;
using BatteryPassWeb.Dpp.Editing;
using BatteryPassWeb.Dpp.Pages;
using BatteryPassWeb.Dpp.Publication;
using BatteryPassWeb.Dpp.Store;
using Microsoft.AspNetCore.Http;

namespace BatteryPassWeb.Tests.Dpp;

/// <summary>
/// Round two, A3 and A4: every action that can publish says what happened per passport (a new version, nothing changed,
/// or held back and by what), and a refusal names who can add each missing value.
/// </summary>
[Collection(MongoCollection.Name)]
public sealed class PublishOutcomesTests(MongoFixture mongo)
{
    private const string ModelId = "model-test";

    [Fact]
    public async Task ADocumentUploadThatPublishes_ShouldSayWhatEachPassportGot()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo);
        var controller = PageTesting.Controller(rig);
        using var content = new MemoryStream(DocumentTesting.Pdf("reports"));

        await controller.UploadBatchDocument("batch-test-2026", "resultsOfTestReportsProvingCompliance", new FormFile(content, 0, content.Length, "file", "test-reports.pdf"), null, null, confirmed: true, CancellationToken.None);

        Assert.Equal("test-reports.pdf uploaded. 1 passport got a new version.", controller.TempData[ProductDataController.EditorStatusKey]);
    }

    [Fact]
    public async Task AModelChangeThePassportDoesntShow_ShouldSayNothingChangedForIt()
    {
        // The seed battery re-states its own capacity fade (#59), so the model's #59 isn't in its passport.
        var rig = await PublicationTesting.PublishedRigAsync(mongo);
        var fade = new JsonObject { ["percentUnit"] = "%", ["percentageValue"] = 1 };
        var post = new EditorPost(new Dictionary<string, JsonNode>(StringComparer.Ordinal) { ["capacityFade"] = fade }, new Dictionary<string, string>(), new Dictionary<string, int>());

        var save = await PageTesting.Editor(rig).SaveAsync(PublicationTesting.Team, TargetKind.Model, ModelId, post, confirmed: true);

        var (outcome, serial) = Assert.Single(save.Outcomes);
        Assert.Equal(PublishOutcome.NothingChanged, outcome.Outcome);
        Assert.Empty(save.KeptTheirVersion);
        Assert.Equal($"Nothing changed for {serial}: it keeps its version.", PublishWords.ForMany(save.Outcomes));
    }

    [Fact]
    public async Task ABatteryFileThatChangesTheStaticValues_ShouldSayTheNewVersion_OnThePageAndInTheReport()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo).WithClustersAsync();
        var file = UploadServiceTests.File(UploadServiceTests.Entry(UploadServiceTests.Serial(rig), """{ "thirdPartyServiceProvider": "urn:example:provider-9" }"""));

        var report = await UploadServiceTests.Run(rig, PublicationTesting.Team, file);

        var battery = Assert.Single(report.Batteries);
        Assert.Equal(new PassportOutcome(rig.PackId, PublishOutcome.NewVersion, 2, []), battery.Passport);
        Assert.Equal("Saved: 1 value. Version 2 of the passport is published.", UploadPages.SavedWords(report));
    }

    [Fact]
    public async Task ABatteryFileWhileThePassportIsHeldBack_ShouldSayWhatHoldsIt()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo).WithClustersAsync();
        await rig.Store.SetModelAnswersAsync(ModelId, null, null, null);
        var file = UploadServiceTests.File(UploadServiceTests.Entry(UploadServiceTests.Serial(rig), """{ "thirdPartyServiceProvider": "urn:example:provider-9" }"""));

        var report = await UploadServiceTests.Run(rig, PublicationTesting.Team, file);

        Assert.Equal(PublishOutcome.HeldBack, Assert.Single(report.Batteries).Passport!.Outcome);
        Assert.Equal("Saved: 1 value. The passport keeps version 1 until this is filled in: the answers to its model's three questions.", UploadPages.SavedWords(report));
    }

    [Fact]
    public async Task ALiveValueInAFile_ShouldSayThePassportKeepsItsVersion()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo).WithClustersAsync();

        var report = await UploadServiceTests.Run(rig, PublicationTesting.Team, UploadServiceTests.File(UploadServiceTests.Entry(UploadServiceTests.Serial(rig), """{ "stateOfChargeSoC": 51 }""")));

        Assert.Equal("Saved: 1 value. The passport shows them now and keeps version 1.", UploadPages.SavedWords(report));
    }

    [Fact]
    public async Task SavingTheAnswers_ShouldSayWhatTheWaitingPassportsGot()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo);
        await rig.Store.SetModelAnswersAsync(ModelId, true, true, null);
        var mass = new JsonObject { ["gramKilogramUnit"] = "kg", ["gramKilogramValue"] = 513 };
        await rig.Pipeline.SaveAsync(PublicationTesting.Team, new ChangeSet(TargetKind.Model, ModelId, new Dictionary<string, JsonNode?> { ["batteryMass"] = mass }, "form"));

        var save = await PageTesting.Pages(rig).SetModelAnswersAsync("team@example.com", (await rig.Store.GetAsync<BatteryModel>(ModelId))!, new ModelAnswers(null, null, true), confirmed: true);

        Assert.Equal(PublishOutcome.NewVersion, Assert.Single(save.Outcomes).Outcome.Outcome);
    }

    [Fact]
    public void ARefusalForAClusterAdmin_ShouldSayWhoAddsEachValue()
    {
        const string cycles = "#83 Number of full charging and discharging cycles is missing (needed because this model is made for cycling use)";
        const string warranty = "#19 Warranty period of the battery is missing (needed because this model comes with a commercial warranty)";

        Assert.Equal(
            "the passport can't get a new version until you add #83 Number of full charging and discharging cycles (needed because this model is made for cycling use) under Live values",
            PublishWords.WhyNoNewVersion([cycles], forTeam: false));
        Assert.Equal(
            "the passport can't get a new version until you add #83 Number of full charging and discharging cycles (needed because this model is made for cycling use) under Live values, and the Scania team adds #19 Warranty period of the battery (needed because this model comes with a commercial warranty). Ask the Scania team for theirs",
            PublishWords.WhyNoNewVersion([cycles, warranty], forTeam: false));
        Assert.StartsWith("the passport can't get a new version until this is filled in: #83 ", PublishWords.WhyNoNewVersion([cycles], forTeam: true), StringComparison.Ordinal);
    }
}
