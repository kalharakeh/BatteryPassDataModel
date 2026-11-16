using System.Text.Json.Nodes;
using BatteryPassWeb.Dpp.Documents;
using BatteryPassWeb.Dpp.Editing;
using BatteryPassWeb.Dpp.Forms;
using BatteryPassWeb.Dpp.Loading;
using BatteryPassWeb.Dpp.Store;
using BatteryPassWeb.Tests.Dpp;
using static BatteryPassWeb.Tests.Loading.LoadTesting;

namespace BatteryPassWeb.Tests.Loading;

/// <summary>
/// Package X1, points 3 and 4: records are found again (the family by name, a model by name within it, a site and an
/// operator by name, a batch by model, site and year), so a second load updates and never duplicates; an empty cell
/// changes nothing and <c>none</c> removes; and a change that reaches a published passport gives it a new version.
/// </summary>
[Collection(MongoCollection.Name)]
public sealed class ProductDataLoadAgainTests(MongoFixture mongo)
{
    private async Task<LoadRig> LoadedAsync()
    {
        var rig = await EmptyAsync(mongo);
        Assert.True((await rig.LoadAsync(Core())).Loaded);
        return rig;
    }

    private static async Task<BatteryModel> ModelAsync(LoadRig rig, string name) =>
        (await rig.Store.ListAsync<BatteryModel>()).Single(model => model.Name == name);

    private static string[] Changed(LoadCheck check) =>
        check.Records.Where(record => record.State != LoadStates.Same).Select(record => $"{record.Name}: {record.State}").ToArray();

    [Theory]
    [InlineData(true)]
    [InlineData(false)] // the files needn't be sent again: they are the records' documents already
    public async Task ASecondLoadOfTheSameWorkbook_ShouldChangeNothingAndMakeNothing(bool withFiles)
    {
        var rig = await LoadedAsync();
        var before = await rig.CountsAsync();
        IReadOnlyList<SentFile> files = withFiles ? CoreFiles() : [];

        var result = await rig.LoadAsync(Core(), files);

        Assert.Empty(result.Check.Texts());
        Assert.Equal((false, ProductDataLoader.NothingToLoad), (result.Loaded, result.Message));
        Assert.Equal(9, result.Check.Records.Count);
        Assert.All(result.Check.Records, record => Assert.Equal((LoadStates.Same, 0), (record.State, record.Changes.Count)));
        Assert.All(result.Check.Records.SelectMany(record => record.Documents), document => Assert.Equal(LoadStates.Same, document.State));
        Assert.False(result.Check.CanLoad);
        Assert.Equal(before, await rig.CountsAsync());
    }

    [Fact]
    public async Task OneChangedCell_ShouldShowAsThatOneChange_AndOnlyItIsSaved()
    {
        var rig = await LoadedAsync();
        var workbook = Core().SetNumber(ModelSheet, "model|batteryMass|gramKilogramValue", "Core800s", 455);

        var check = await rig.CheckAsync(workbook);
        var result = await rig.LoadAsync(workbook);

        SameLines(["Core800s: Changed"], Changed(check));
        Assert.Equal([new LoadChangeLine("Battery mass", "470", "455")], check.Records.Single(record => record.Name == "Core800s").Changes);
        Assert.True(result.Loaded, result.Message);
        Assert.Equal("Product data loaded: 1 record changed, of 1 battery family, 3 battery models, 3 batches, 1 manufacturing site and 1 economic operator.", result.Message);
        Assert.Equal("455", (await ModelAsync(rig, "Core800s")).Values["batteryMass"].Value!["gramKilogramValue"]!.ToJsonString());
        Assert.Equal("470", (await ModelAsync(rig, "Core800")).Values["batteryMass"].Value!["gramKilogramValue"]!.ToJsonString());
        Assert.Equal(2, (await rig.AuditAsync(ProductDataLoader.LoadedEvent)).Count);
        Assert.Equal(3, (await rig.Store.ListAsync<BatteryModel>()).Count);
    }

    [Fact]
    public async Task AChangedCellOfTheFirstModel_ShouldReachTheModelsThatLeaveThatCellEmpty()
    {
        var rig = await LoadedAsync();

        var check = await rig.CheckAsync(Core().SetNumber(ModelSheet, "model|maximumVoltage|voltValue", "Core800", 740));

        SameLines(["Core800: Changed", "Core800s: Changed", "Core700: Changed"], Changed(check));
        Assert.All(check.Records.Where(record => record.State == LoadStates.Changed), record => Assert.Equal([new LoadChangeLine("Maximum voltage", "736", "740")], record.Changes));
    }

    [Fact]
    public async Task AnEmptyCellInALaterLoad_ShouldChangeNothing_AlsoInsideAValueOfSeveralParts()
    {
        var rig = await LoadedAsync();
        var workbook = Core()
            .Clear(ModelSheet, "model|cycleLifeReferenceTest", "Core800")
            .Clear(ModelSheet, "model|nominalVoltage|temperatureRangeLowerBoundaryValue", "Core800")
            .Clear(ModelSheet, "model|MadeForCyclingUse", "Core800")
            .Clear(ModelSheet, "model|ModuleDescription", "Core800")
            .Clear(SiteSheet, "site|manufacturerInformation|postalAddress", "Site 1")
            .Document("Core800", "model", "separateCollectionSymbol", null)
            .Document("Core800", "model", "safetyMeasures", null);

        var result = await rig.LoadAsync(workbook, CoreFiles().Where(file => file.FileName != "safety-measures.pdf").ToArray());

        Assert.Empty(result.Check.Texts());
        Assert.Empty(Changed(result.Check));
        var model = await ModelAsync(rig, "Core800");
        Assert.Equal("IEC 62660-1", model.Values["cycleLifeReferenceTest"].Value!.GetValue<string>());
        Assert.Equal(-20, model.Values["nominalVoltage"].Value!["temperatureRangeLowerBoundaryValue"]!.GetValue<int>());
        Assert.True(model.MadeForCyclingUse);
        Assert.NotNull(model.ModuleDescription);
        Assert.True(model.Values.ContainsKey("separateCollectionSymbol") && model.Values.ContainsKey("safetyMeasures"));
    }

    [Fact]
    public async Task None_ShouldRemoveTheValue_OrThePartOfIt()
    {
        var rig = await LoadedAsync();
        var workbook = Core()
            .Set(ModelSheet, "model|extinguishingAgent|agentFireClass", "Core800", "none") // one part of a value
            .Set(ModelSheet, "model|cycleLifeReferenceTest", "Core800s", "None") // a whole value, for one model
            .Set(ModelSheet, "model|ModuleDescription", "Core700", "none")
            .Set(ModelSheet, "model|Picture", "Core700", "none")
            .Set(BatchSheet, "batch|carbonFootprintPerformanceClass", FirstBatch, "none")
            .Document("Core700", "model", "informationOfDueDiligenceReport", "none");

        var check = await rig.CheckAsync(workbook);
        var result = await rig.LoadAsync(workbook);

        Assert.True(result.Loaded, result.Message);
        Assert.Contains(new LoadChangeLine("Extinguishing agent: fire class", "B", "(removed)"), check.Records.Single(record => record.Name == "Core800").Changes);
        Assert.Contains(new LoadChangeLine("Cycle-life reference test", "IEC 62660-1", "(removed)"), check.Records.Single(record => record.Name == "Core800s").Changes);
        Assert.Contains(check.Records.Single(record => record.Name == "Core700").Documents, document => document is { Name: "Due diligence report", State: LoadStates.Removed });
        var (first, second, third) = (await ModelAsync(rig, "Core800"), await ModelAsync(rig, "Core800s"), await ModelAsync(rig, "Core700"));
        Assert.Equal("""{"extinguishingAgent":"Water"}""", first.Values["extinguishingAgent"].Value!.ToJsonString());
        Assert.True(first.Values.ContainsKey("cycleLifeReferenceTest"));
        Assert.False(second.Values.ContainsKey("cycleLifeReferenceTest"));
        Assert.False(third.Values.ContainsKey("informationOfDueDiligenceReport"));
        Assert.True(first.Values.ContainsKey("informationOfDueDiligenceReport"));
        Assert.Null(third.ModuleDescription);
        Assert.Null(third.PictureFileId);
        Assert.NotNull(first.PictureFileId);
        Assert.All(await rig.Store.ListAsync<ProductionBatch>(), batch => Assert.False(batch.Values.ContainsKey("carbonFootprintPerformanceClass")));
    }

    [Fact]
    public async Task ADocument_ShouldBeUploadedAgain_OnlyWhenItsFileTitleOrLanguageChanged()
    {
        var rig = await LoadedAsync();
        var model = await ModelAsync(rig, "Core800s");
        var files = CoreFiles().Select(file => file.FileName == "part-numbers.pdf" ? file with { Content = DocumentTesting.Pdf("parts, second edition") } : file).ToArray();
        var workbook = Core().Document("Core800s", "model", "safetyMeasures", null, "Safety measures for the short pack");

        var check = await rig.CheckAsync(workbook, files);
        var result = await rig.LoadAsync(workbook, files);

        Assert.True(result.Loaded, result.Message);
        SameLines(["Core800: Changed", "Core800s: Changed", "Core700: Changed"], Changed(check));
        var documents = check.Records.Single(record => record.Name == "Core800s").Documents.Where(document => document.State != LoadStates.Same).ToArray();
        Assert.Equal(["Part numbers for components", "Safety measures"], documents.Select(document => document.Name).Order());
        Assert.All(documents, document => Assert.Equal(LoadStates.Changed, document.State));

        // As on the forms: the new file is a new document that the value links; the earlier one stays stored.
        var parts = await rig.Documents.ListAsync(TargetKind.Model, model.Id, "partNumbersForComponents");
        Assert.Equal(2, parts.Count);
        var linked = (await ModelAsync(rig, "Core800s")).Values["partNumbersForComponents"].Value!["url"]!.GetValue<string>();
        var secondEdition = parts.Single(document => linked.EndsWith(document.Id, StringComparison.Ordinal));
        Assert.Equal(DocumentTesting.Pdf("parts, second edition"), await rig.Documents.DownloadAsync(secondEdition));
        Assert.Contains("Safety measures for the short pack", (await rig.Documents.ListAsync(TargetKind.Model, model.Id, "safetyMeasures")).Select(document => document.Title));
        Assert.Equal("Safety measures for the short pack", (await ModelAsync(rig, "Core800s")).Values["safetyMeasures"].Value!["resourceTitle"]!.GetValue<string>());
        Assert.Single(await rig.Documents.ListAsync(TargetKind.Model, (await ModelAsync(rig, "Core800")).Id, "safetyMeasures"));
    }

    [Fact]
    public async Task ANewYear_ShouldMakeANewBatch_BesideTheOneThatIsThere()
    {
        var rig = await LoadedAsync();
        var workbook = Core().SetNumber(BatchSheet, "batch|Year", "Batch of Core700", 2027);

        var check = await rig.CheckAsync(workbook);
        var result = await rig.LoadAsync(workbook);

        SameLines(["Core700, batch 2027 · Plant South: New"], Changed(check));
        Assert.True(result.Loaded, result.Message);
        var batches = await rig.Store.ListBatchesAsync((await ModelAsync(rig, "Core700")).Id);
        Assert.Equal([2026, 2027], batches.Select(batch => batch.Year).Order());
        Assert.True(FormValues.SameValue(batches[0].Values["renewableContentShare"].Value, batches[1].Values["renewableContentShare"].Value));
        Assert.Single(await rig.Store.ListBatchesAsync((await ModelAsync(rig, "Core800")).Id));
    }

    [Fact]
    public async Task ANameInOtherCapitals_ShouldBeTheSameRecord_AndTakeTheNameAsTyped()
    {
        var rig = await LoadedAsync();
        var workbook = Core().Set(FamilySheet, "family|Name", "Core", "CORE").Set(ModelSheet, "model|Name", "Core800s", "core800S");

        var result = await rig.LoadAsync(workbook);

        Assert.True(result.Loaded, result.Message);
        SameLines(["CORE: Changed", "core800S: Changed", "core800S, batch 2026 · Plant South: No change"], result.Check.Records.Where(record => record.Name.Contains("CORE", StringComparison.Ordinal) || record.Name.Contains("core800S", StringComparison.Ordinal)).Select(record => $"{record.Name}: {record.State}"));
        Assert.Equal("CORE", Assert.Single(await rig.Store.ListAsync<BatteryFamily>()).Name);
        Assert.Equal(["Core700", "Core800", "core800S"], (await rig.Store.ListAsync<BatteryModel>()).Select(model => model.Name).Order(StringComparer.Ordinal));
    }

    /// <summary>The workbook for the seed battery's real records: its family and model by name, its batch by model, site and year.</summary>
    private static FilledWorkbook ForTheSeedBattery() => FilledWorkbook.Empty()
        .Set(FamilySheet, "family|Name", "Core", "Test battery family")
        .Set(ModelSheet, "model|Name", "Core800", "EXB-NMC-100")
        .Clear(ModelSheet, "model|Name", "Core800s")
        .Clear(ModelSheet, "model|Name", "Core700");

    [Fact]
    public async Task AChangeThatReachesAPublishedPassport_ShouldSayHowMany_AndPublishTheNewVersion()
    {
        var rig = Around(await PublicationTesting.PublishedRigAsync(mongo));
        var workbook = ForTheSeedBattery()
            .SetNumber(ModelSheet, "model|batteryMass|gramKilogramValue", "Core800", 520)
            .SetNumber(BatchSheet, "batch|Year", FirstBatch, 2026)
            .Set(BatchSheet, "batch|Site", FirstBatch, "Example plant") // a site that is in the app, not in the workbook
            .Set(BatchSheet, "batch|carbonFootprintPerformanceClass", FirstBatch, "Class D");

        var check = await rig.CheckAsync(workbook, []);
        var result = await rig.Loader.LoadAsync(Who, WorkbookName, workbook.Bytes(), [], check.Stamp);

        Assert.Empty(check.Texts());
        SameLines(["EXB-NMC-100: Changed", "EXB-NMC-100, batch 2026 · Example plant: Changed"], Changed(check));
        Assert.Equal(1, check.PublishedPassports);
        Assert.Equal(["Loading publishes new versions of 1 passport: the published passport built from the records that change."], check.Notes);
        Assert.True(result.Loaded, result.Message);
        Assert.Equal("Product data loaded: 2 records changed, of 1 battery family, 1 battery model and 1 batch. 1 passport got a new version.", result.Message);
        Assert.Equal(3, (await rig.Store.GetActiveVersionAsync(rig.Rig.PackId))?.Number); // one new version for each record's save, as by hand
        Assert.Equal("520", (await rig.Store.GetAsync<BatteryModel>("model-test"))!.Values["batteryMass"].Value!["gramKilogramValue"]!.ToJsonString());
        Assert.Single(await rig.Store.ListBatchesAsync("model-test"));
        Assert.Null((await rig.Store.GetAsync<BatteryPack>(rig.Rig.PackId))!.ChangesWaitingSinceUtc);
    }

    [Fact]
    public async Task AValueAPublishedPassportNeeds_ShouldNotBeRemovedWithNone_AsOnTheForms()
    {
        var rig = Around(await PublicationTesting.PublishedRigAsync(mongo));
        var workbook = ForTheSeedBattery().Set(ModelSheet, "model|batteryMass|gramKilogramValue", "Core800", "none");

        var result = await rig.LoadAsync(workbook, []);

        Assert.False(result.Loaded);
        SameLines(["Models, Battery mass, Core800: The published passports need this value, so it can't be removed. Change it instead"], result.Check.Texts());
        Assert.Equal(1, (await rig.Store.GetActiveVersionAsync(rig.Rig.PackId))?.Number);
    }

    [Fact]
    public async Task OnlyRealRecordsAreMatched_ADemoModelOfARealFamilyIsNeverTakenOver()
    {
        var rig = Around(await PublicationTesting.RigAsync(mongo));
        await TestDemo.MarkAsync(rig.Rig, DppStore.Models, "model-test");

        var check = await rig.CheckAsync(ForTheSeedBattery().SetNumber(ModelSheet, "model|batteryMass|gramKilogramValue", "Core800", 520), []);

        SameLines(
            ["Models, Model name, Core800: a demo battery model is already called EXB-NMC-100, and real product data can't share a name with demo data. Clear the demo data on the Clusters tab, or choose another name"],
            check.Texts());
        Assert.True(FormValues.SameValue(JsonNode.Parse("""{"gramKilogramUnit":"kg","gramKilogramValue":512.5}"""), (await rig.Store.GetAsync<BatteryModel>("model-test"))!.Values["batteryMass"].Value));
    }
}
