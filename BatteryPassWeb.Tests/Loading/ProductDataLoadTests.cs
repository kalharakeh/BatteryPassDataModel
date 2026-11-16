using System.Text.Json.Nodes;
using BatteryPassWeb.Dpp.Catalog;
using BatteryPassWeb.Dpp.Documents;
using BatteryPassWeb.Dpp.Editing;
using BatteryPassWeb.Dpp.Forms;
using BatteryPassWeb.Dpp.Loading;
using BatteryPassWeb.Dpp.Pages;
using BatteryPassWeb.Dpp.Store;
using BatteryPassWeb.Tests.Dpp;
using static BatteryPassWeb.Tests.Loading.LoadTesting;

namespace BatteryPassWeb.Tests.Loading;

/// <summary>
/// Package X1, points 3, 5, 6 and 9: "Load" saves what the check showed through the save path of the forms, on a
/// database with no demo data, and what it saves is what the team would have saved by hand.
/// </summary>
[Collection(MongoCollection.Name)]
public sealed class ProductDataLoadTests(MongoFixture mongo)
{
    private static void Same(string expectedJson, JsonNode? actual) =>
        Assert.True(FormValues.SameValue(JsonNode.Parse(expectedJson), actual), $"expected {expectedJson}, the record holds {actual?.ToJsonString()}");

    private static async Task<(BatteryFamily Family, IReadOnlyList<BatteryModel> Models)> CoreAsync(LoadRig rig)
    {
        var family = Assert.Single(await rig.Store.ListAsync<BatteryFamily>());
        return (family, await rig.Store.ListModelsAsync(family.Id));
    }

    [Fact]
    public async Task Load_ShouldMakeTheFamilyItsModelsBatchesSiteAndOperator_OnADatabaseWithoutDemoData()
    {
        var rig = await EmptyAsync(mongo);

        var result = await rig.LoadAsync(Core());

        Assert.True(result.Loaded, result.Message);
        Assert.Equal("Product data loaded: 9 records made, of 1 battery family, 3 battery models, 3 batches, 1 manufacturing site and 1 economic operator.", result.Message);
        var (family, models) = await CoreAsync(rig);
        Assert.Equal(family.Id, result.FamilyId);
        Assert.Equal("Core", family.Name);
        Assert.False(family.IsDemo);
        Assert.Equal("image/png", family.PictureContentType);
        Assert.Equal(["Core700", "Core800", "Core800s"], models.Select(model => model.Name).Order());
        var site = Assert.Single(await rig.Store.ListAsync<ManufacturingSite>());
        Assert.Equal("Plant South", site.Name);
        Assert.Equal("Fleet Services AB", Assert.Single(await rig.Store.ListAsync<EconomicOperator>()).Name);
        foreach (var model in models)
        {
            var batch = Assert.Single(await rig.Store.ListBatchesAsync(model.Id));
            Assert.Equal((2026, site.Id, false), (batch.Year, batch.SiteId, batch.IsDemo));
        }
    }

    [Fact]
    public async Task ALoadedModel_ShouldHoldItsValuesInTheFormTheFormsSaveThem()
    {
        var rig = await EmptyAsync(mongo);

        await rig.LoadAsync(Core());

        var model = (await CoreAsync(rig)).Models.Single(item => item.Name == "Core800");
        var values = model.Values.ToDictionary(entry => entry.Key, entry => entry.Value.Value);
        Same("""{"batteryCategoryValue":"industrial/non-stationary battery"}""", values["batteryCategory"]); // the app's own, never typed
        Same("""{"gramKilogramUnit":"kg","gramKilogramValue":470}""", values["batteryMass"]); // a number with the unit the app writes
        Same("""{"amperePerAmpereHourValue":0.5,"amperePerAmpereHourUnit":"A/Ah"}""", values["cRateOfRelevantCycleLifeTest"]);
        Same("""["Cobalt","Lithium","Natural graphite","Nickel"]""", values["criticalRawMaterials"]); // a list, set apart with semicolons
        Same("10", values["expectedLifetimeInCalendarYears"]);
        Same("""{"agentFireClass":"B","extinguishingAgent":"Water"}""", values["extinguishingAgent"]);
        Same("""{"voltValue":512,"voltUnit":"V"}""", values["minimumVoltage"]); // no temperature typed: no temperature unit either
        Same("""{"voltValue":640,"voltUnit":"V","temperatureRangeLowerBoundaryValue":-20,"temperatureRangeLowerBoundaryUnit":"°C","temperatureRangeUpperBoundaryValue":45,"temperatureRangeUpperBoundaryUnit":"°C"}""", values["nominalVoltage"]);
        Same("""{"chemicalCodeValue":"Li-ion NMC"}""", values["batteryChemistry"]);
        Same("""[{"chemicalName":"Lithium hexafluorophosphate","identifierType":"CAS No","identifier":"21324-40-3","concentration":1.2,"hazardPhrases":["H301","H314"],"precautionaryPhrases":["P260","P280"],"substanceLocation":"Electrolyte"}]""", values["hazardousSubstancesAndTheirImpact"]);
        Same($$"""{"contentType":"image/svg+xml","url":"{{Web}}/symbols/separate-collection.svg","language":"en-GB","resourceTitle":"Separate collection symbol"}""", values["separateCollectionSymbol"]);
        Same($$"""{"contentType":"text/html","url":"{{Web}}/battery/waste-prevention","language":"en-GB","resourceTitle":"Your role in waste prevention"}""", values["informationOnTheRoleOfEndUsersInContributingToWastePrevention"]);
        Assert.All(model.Values.Values, value => Assert.Equal(("form", Who), (value.Source, value.Who)));

        // Nothing the passport needs is missing: the example column fills a whole model.
        Assert.Equal(new ModelAnswers(true, true, false), model.Answers());
        Assert.Equal(0, RecordEditor.CountMissing(TargetKind.Model, model.Values, model.Answers()));
        Assert.Equal("Ten liquid-cooled NMC modules around a central cooling plate, with the battery management unit on top.", model.ModuleDescription);
        Assert.Equal("image/png", model.PictureContentType);
        Assert.Empty(ExampleValues.Of(model.Values, model.ExampleDocumentIds));
    }

    [Fact]
    public async Task ALoadedBatchSiteAndOperator_ShouldHoldTheirValues()
    {
        var rig = await EmptyAsync(mongo);

        await rig.LoadAsync(Core());

        var model = (await CoreAsync(rig)).Models.Single(item => item.Name == "Core700");
        var batch = Assert.Single(await rig.Store.ListBatchesAsync(model.Id));
        Same("\"CORE\"", batch.Values["batteryModelIdentifier"].Value);
        Same("""{"kgCO2EquivalentPerKilowattHourValue":59,"kgCO2EquivalentPerKilowattHourUnit":"kgCO2-eq/kWh"}""", batch.Values["batteryCarbonFootprintPerFunctionalUnit"].Value);
        Same("""{"percentUnit":"%","percentageValue":38}""", batch.Values["renewableContentShare"].Value);
        Same($"\"{Web}/core/carbon-footprint-study\"", batch.Values["webLinkToPublicCarbonFootprintStudy"].Value);
        Same($$"""{"contentType":"image/png","url":"{{Web}}/core/carbon-footprint-label.png","language":"en-GB","resourceTitle":"Carbon footprint label"}""", batch.Values["carbonFootprintLabel"].Value);
        Assert.Equal(0, RecordEditor.CountMissing(TargetKind.Batch, batch.Values, ModelAnswers.NotAnswered));
        var site = Assert.Single(await rig.Store.ListAsync<ManufacturingSite>());
        Same($$"""{"name":"Scania Industrial Batteries AB","registeredTradeNameOrRegisteredTrademark":"Scania Industrial Batteries","postalAddress":"Verkstadsgatan 1, 151 38 Sodertalje, Sweden","webAddress":"{{Web}}","eMailAddress":"batteries@sib-scania-eubp.eu"}""", site.Values["manufacturerInformation"].Value);
        Same("\"iso6523-actorid-upis::0060:778474481\"", site.Values["uniqueManufacturerIdentifier"].Value);
        Assert.Equal(0, RecordEditor.CountMissing(TargetKind.Site, site.Values, ModelAnswers.NotAnswered));
        var economicOperator = Assert.Single(await rig.Store.ListAsync<EconomicOperator>());
        Same("\"iso6523-actorid-upis::0060:999999999\"", economicOperator.Values["economicOperatorId"].Value);
        Assert.Equal(0, RecordEditor.CountMissing(TargetKind.Operator, economicOperator.Values, ModelAnswers.NotAnswered));
    }

    [Fact]
    public async Task AFile_ShouldGoThroughTheDocumentService_OncePerRecordThatNeedsIt()
    {
        var rig = await EmptyAsync(mongo);

        await rig.LoadAsync(Core());

        var models = (await CoreAsync(rig)).Models;
        var documentIds = new HashSet<string>();
        foreach (var model in models)
        {
            // The same file named for three models: each model has its own document of it, as three uploads would make.
            var document = Assert.Single(await rig.Documents.ListAsync(TargetKind.Model, model.Id, "safetyMeasures"));
            Assert.True(documentIds.Add(document.Id));
            Assert.Equal(("safety-measures.pdf", "application/pdf", "Safety measures", "sv-SE", Who), (document.FileName, document.ContentType, document.Title, document.Language, document.UploadedBy));
            Assert.Equal(Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(DocumentTesting.Pdf("safety"))), document.Sha256);
            Assert.False(document.IsExample);
            Same($$"""{"contentType":"application/pdf","url":"{{DocumentTesting.BaseUrl}}/dpp/documents/{{document.Id}}","resourceTitle":"Safety measures","language":"sv-SE"}""", model.Values["safetyMeasures"].Value);
            Assert.Equal(DocumentTesting.Pdf("safety"), await rig.Documents.DownloadAsync(document));
            var batch = Assert.Single(await rig.Store.ListBatchesAsync(model.Id));
            Assert.Equal("Core test report 2026", Assert.Single(await rig.Documents.ListAsync(TargetKind.Batch, batch.Id, "resultsOfTestReportsProvingCompliance")).Title);
        }

        Assert.Equal(15, (await rig.CountsAsync()).Split("documents ")[1].Split(',')[0] is var count ? int.Parse(count) : 0); // 4 per model, 1 per batch
    }

    [Fact]
    public async Task Load_ShouldWriteOneAuditEntryForTheLoad_BesidesTheRecordsOwn()
    {
        var rig = await EmptyAsync(mongo);

        await rig.LoadAsync(Core());

        var load = Assert.Single(await rig.AuditAsync(ProductDataLoader.LoadedEvent));
        Assert.Equal("Product data loaded from Core family - data to fill.xlsx: 9 records made, of 1 battery family, 3 battery models, 3 batches, 1 manufacturing site and 1 economic operator", load["message"].AsString);
        Assert.Equal((Who, "dpp-family", (await CoreAsync(rig)).Family.Id), (load["actor"].AsString, load["entityType"].AsString, load["entityId"].AsString));
        Assert.Equal(15, load["metadata"]["documents"].AsInt32);

        // As by hand: an entry for each record made, for each save of values, for each upload, for the answers.
        Assert.Equal(9, (await rig.AuditAsync(ProductDataPages.CreatedEvent)).Count);
        Assert.Equal(15, (await rig.AuditAsync(DocumentService.UploadedEvent)).Count);
        Assert.Equal(3, (await rig.AuditAsync(ProductDataPages.AnswersEvent)).Count);
        Assert.Equal(3, (await rig.AuditAsync(ProductDataPages.ModuleDescriptionEvent)).Count);
        Assert.Equal(4, (await rig.AuditAsync(ProductDataPages.PictureEvent)).Count);
        var saves = await rig.AuditAsync(SavePipeline.AuditEventType);
        Assert.Equal(8 + 15, saves.Count); // the values of 3 models, 3 batches, the site and the operator; and each upload's link
        Assert.All(saves, save => Assert.Equal(("Team", "form"), (save["actorRole"].AsString, save["source"].AsString)));
    }

    [Fact]
    public async Task ALaterModelsEmptyCell_ShouldMeanTheFirstModels_AndNone_NoValue()
    {
        // The manager's check 2: Core800 filled, Core800s empty except two cells and one none, Core700 empty.
        var rig = await EmptyAsync(mongo);
        var workbook = Core()
            .SetNumber(ModelSheet, "model|batteryMass|gramKilogramValue", "Core800s", 455.5)
            .Set(ModelSheet, "model|cycleLifeReferenceTest", "Core800s", "IEC 62660-1 (short pack)")
            .Set(ModelSheet, "model|extinguishingAgent|agentFireClass", "Core800s", "none");

        var result = await rig.LoadAsync(workbook);

        Assert.True(result.Loaded, result.Message);
        var models = (await CoreAsync(rig)).Models.ToDictionary(model => model.Name);
        var (first, second, third) = (models["Core800"], models["Core800s"], models["Core700"]);
        Same("""{"gramKilogramUnit":"kg","gramKilogramValue":455.5}""", second.Values["batteryMass"].Value);
        Same("\"IEC 62660-1 (short pack)\"", second.Values["cycleLifeReferenceTest"].Value);
        Same("""{"extinguishingAgent":"Water"}""", second.Values["extinguishingAgent"].Value);
        string[] own = ["batteryMass", "cycleLifeReferenceTest", "extinguishingAgent"];
        var uploaded = DppLinks.RestrictedDocumentProperties;
        Assert.Equal(first.Values.Keys.Order(), second.Values.Keys.Order());
        Assert.Equal(first.Values.Keys.Order(), third.Values.Keys.Order());
        foreach (var (property, value) in first.Values.Where(entry => !uploaded.Contains(entry.Key)))
        {
            Assert.True(FormValues.SameValue(value.Value, third.Values[property].Value), $"Core700 differs from Core800 in {property}");
            if (!own.Contains(property))
            {
                Assert.True(FormValues.SameValue(value.Value, second.Values[property].Value), $"Core800s differs from Core800 in {property}");
            }
        }

        Assert.Equal((first.Answers(), first.ModuleDescription, "image/png"), (second.Answers(), second.ModuleDescription, second.PictureContentType));
        Assert.Equal(first.Answers(), third.Answers());
    }

    [Fact]
    public async Task ALoadThatStopsPartWay_ShouldLeaveNothingBehind()
    {
        var rig = await EmptyAsync(mongo);
        var models = 0;
        rig.Loader.StepHook = step => step == "model" && ++models == 2 ? throw new InvalidOperationException("the database went away") : Task.CompletedTask;

        var result = await rig.LoadAsync(Core());

        Assert.False(result.Loaded);
        Assert.Equal("The load stopped part way: an error stopped it (InvalidOperationException). The records it had made were removed again. Nothing was saved.", result.Message);
        var counts = await rig.CountsAsync();
        Assert.StartsWith("families 0, models 0, batches 0, sites 0, operators 0, documents 0, files 0,", counts, StringComparison.Ordinal);
        Assert.Single(await rig.AuditAsync(ProductDataLoader.StoppedEvent));
        Assert.Empty(await rig.AuditAsync(ProductDataLoader.LoadedEvent));

        // And the same workbook loads once the fault is gone.
        rig.Loader.StepHook = null;
        Assert.True((await rig.LoadAsync(Core())).Loaded);
    }

    [Fact]
    public async Task Load_ShouldRefuse_WithoutTheChecksStamp_OrWithAnotherChecks()
    {
        var rig = await EmptyAsync(mongo);
        var bytes = Core().Bytes();
        var other = await rig.Loader.CheckAsync(WorkbookName, Core().SetNumber(ModelSheet, "model|batteryMass|gramKilogramValue", "Core800", 471).Bytes(), CoreFiles());

        var withoutCheck = await rig.Loader.LoadAsync(Who, WorkbookName, bytes, CoreFiles(), stamp: null);
        var afterAnotherCheck = await rig.Loader.LoadAsync(Who, WorkbookName, bytes, CoreFiles(), other.Stamp);

        Assert.Equal((false, ProductDataLoader.CheckFirst), (withoutCheck.Loaded, withoutCheck.Message));
        Assert.Equal((false, ProductDataLoader.ChangedSinceCheck), (afterAnotherCheck.Loaded, afterAnotherCheck.Message));
        Assert.Equal(Nothing, await rig.CountsAsync());
    }

    [Fact]
    public async Task Load_ShouldSaveNothing_WhenTheCheckHasAProblem()
    {
        var rig = await EmptyAsync(mongo);
        var workbook = Core().Set(ModelSheet, "model|batteryMass|gramKilogramValue", "Core700", "4,7 kg");

        var result = await rig.LoadAsync(workbook);

        Assert.Equal((false, ProductDataLoader.HasProblems), (result.Loaded, result.Message));
        Assert.Equal(["Models, Battery mass, Core700: 4,7 kg is not a number; type the number only, with a point for decimals"], result.Check.Texts());
        Assert.Equal(Nothing, await rig.CountsAsync());
    }

    [Fact]
    public async Task AWorkbookSavedByExcel_ShouldLoadTheSameAsTheScriptsOwn()
    {
        var asWritten = await EmptyAsync(mongo);
        var asSaved = await EmptyAsync(mongo);
        var workbook = Core();

        var written = await asWritten.Loader.CheckAsync(WorkbookName, workbook.Bytes(), CoreFiles());
        var saved = await asSaved.Loader.CheckAsync(WorkbookName, workbook.AsExcelSaves(), CoreFiles());

        Assert.Empty(saved.Texts());
        Assert.Equal(written.Records.Select(record => (record.Name, record.State, record.Changes.Count, record.Documents.Count)), saved.Records.Select(record => (record.Name, record.State, record.Changes.Count, record.Documents.Count)));
        Assert.Equal(written.Records.SelectMany(record => record.Changes), saved.Records.SelectMany(record => record.Changes));
        Assert.True((await asSaved.Loader.LoadAsync(Who, WorkbookName, workbook.AsExcelSaves(), CoreFiles(), saved.Stamp)).Loaded);
    }
}
