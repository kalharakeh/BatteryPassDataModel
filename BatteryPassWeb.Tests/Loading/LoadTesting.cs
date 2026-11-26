using BatteryPassWeb.Dpp.Documents;
using BatteryPassWeb.Dpp.Identifiers;
using BatteryPassWeb.Dpp.Loading;
using BatteryPassWeb.Dpp.Publication;
using BatteryPassWeb.Dpp.Signing;
using BatteryPassWeb.Dpp.Store;
using BatteryPassWeb.Services;
using BatteryPassWeb.Tests.Dpp;
using MongoDB.Bson;
using MongoDB.Driver;

namespace BatteryPassWeb.Tests.Loading;

/// <summary>The loader on a test database, wired to the pages, the editor and the document service as in the app.</summary>
internal sealed record LoadRig(PublicationRig Rig, ProductDataLoader Loader)
{
    public DppStore Store => Rig.Store;

    public DocumentStore Documents { get; } = new(Rig.Context);

    /// <summary>The audit entries of one kind, oldest first.</summary>
    public async Task<IReadOnlyList<BsonDocument>> AuditAsync(string? eventType = null)
    {
        var all = await Rig.Context.Database!.GetCollection<BsonDocument>("applicationAuditEvents").Find(FilterDefinition<BsonDocument>.Empty).ToListAsync();
        return all.Where(entry => eventType is null || entry["eventType"].AsString == eventType).ToArray();
    }

    /// <summary>How many records of every kind, uploaded documents, stored files and audit entries the database holds.</summary>
    public async Task<string> CountsAsync()
    {
        var database = Rig.Context.Database!;
        async Task<long> Count(string collection) => await database.GetCollection<BsonDocument>(collection).CountDocumentsAsync(FilterDefinition<BsonDocument>.Empty);
        return $"families {await Count(DppStore.Families)}, models {await Count(DppStore.Models)}, batches {await Count(DppStore.Batches)}, sites {await Count(DppStore.Sites)}, operators {await Count(DppStore.Operators)}, "
            + $"documents {await Count(DocumentStore.Records)}, files {await Count(DocumentStore.Bucket + ".files")}, audit {await Count("applicationAuditEvents")}";
    }
}

internal static class LoadTesting
{
    public const string Who = "team@example.com";

    public const string Nothing = "families 0, models 0, batches 0, sites 0, operators 0, documents 0, files 0, audit 0";

    /// <summary>The loader on an empty database with no demo data at all: the owner's (decision 82).</summary>
    public static async Task<LoadRig> EmptyAsync(MongoFixture mongo)
    {
        var context = mongo.NewContext();
        var store = new DppStore(context);
        await store.EnsureIndexesAsync();
        var clock = new MutableTimeProvider(DppStoreTesting.Now);
        var publication = new PublicationService(
            store,
            TestDemo.For(store, context),
            new PublicationChecks(DppStoreTesting.Schemas),
            new PassportSigner(SigningTesting.Keys),
            new TestIdentifierScheme(),
            new ApplicationAuditService(context),
            clock);
        var pipeline = SavePipelineTesting.Pipeline(store, context, clock, publication);
        return Around(new PublicationRig(store, context, DppTestSeed.Load(), publication, pipeline, clock));
    }

    /// <summary>The loader around a rig that already holds records (the seed battery, published or not).</summary>
    public static LoadRig Around(PublicationRig rig) => new(rig, new ProductDataLoader(
        rig.Store,
        PageTesting.Pages(rig),
        PageTesting.Editor(rig),
        rig.Pipeline,
        PageTesting.Documents(rig),
        new DocumentStore(rig.Context),
        DocumentTesting.Links,
        AttributeFormsTests.Forms,
        new ApplicationAuditService(rig.Context),
        new BatteryPassWeb.Dpp.Identifiers.TestIdentifierScheme(),
        rig.Publication));

    public const string ModelSheet = LoadFields.ModelsSheet;
    public const string BatchSheet = LoadFields.BatchesSheet;
    public const string SiteSheet = LoadFields.SiteSheet;
    public const string OperatorSheet = LoadFields.OperatorSheet;
    public const string FamilySheet = LoadFields.FamilySheet;
    public const string FirstBatch = "Batch of Core800";
    public const string Web = "https://www.sib-scania-eubp.eu";

    /// <summary>
    /// The workbook filled the way the owner is asked to: Core800 in full, Core800s and Core700 left empty (the same as
    /// Core800), one batch each, one site, one operator, one substance for all models, every document, both pictures.
    /// It starts from the example column and replaces every value that is the demo's example.
    /// </summary>
    public static FilledWorkbook Core() => Core(FilledWorkbook.Empty());

    /// <summary><see cref="Core"/>, filled into <paramref name="start"/> (the workbook as sent on 13 November, for one).</summary>
    public static FilledWorkbook Core(FilledWorkbook start) => start
        .ExampleInto(FamilySheet, "Core")
        .ExampleInto(ModelSheet, "Core800")
        .ExampleInto(BatchSheet, FirstBatch)
        .ExampleInto(SiteSheet, "Site 1")
        .ExampleInto(OperatorSheet, "Operator 1")
        .Set(SiteSheet, "site|Name", "Site 1", "Plant South")
        .Set(SiteSheet, "site|manufacturerInformation|name", "Site 1", "Scania Industrial Batteries AB")
        .Set(SiteSheet, "site|manufacturerInformation|postalAddress", "Site 1", "Verkstadsgatan 1, 151 38 Sodertalje, Sweden")
        .Set(SiteSheet, "site|manufacturerInformation|webAddress", "Site 1", Web)
        .Set(SiteSheet, "site|manufacturerInformation|eMailAddress", "Site 1", "batteries@sib-scania-eubp.eu")
        .Set(SiteSheet, "site|manufacturingPlace", "Site 1", "Plant South, Sodertalje, Sweden")
        .Set(OperatorSheet, "operator|Name", "Operator 1", "Fleet Services AB")
        .Set(OperatorSheet, "operator|economicOperatorInformation|name", "Operator 1", "Fleet Services AB")
        .Set(OperatorSheet, "operator|economicOperatorInformation|registeredTradeNameOrRegisteredTrademark", "Operator 1", "Fleet Services")
        .Set(OperatorSheet, "operator|economicOperatorInformation|webAddress", "Operator 1", "https://www.fleet-services.se")
        .Set(OperatorSheet, "operator|economicOperatorInformation|eMailAddress", "Operator 1", "info@fleet-services.se")
        .Set(BatchSheet, "batch|webLinkToPublicCarbonFootprintStudy", FirstBatch, $"{Web}/core/carbon-footprint-study")
        .Hazard(1, "all", "Lithium hexafluorophosphate", where: "Electrolyte")
        .Document("Core800", "model", "separateCollectionSymbol", $"{Web}/symbols/separate-collection.svg", "Separate collection symbol", "en-GB")
        .Document("Core800", "model", "informationOfDueDiligenceReport", $"{Web}/core/due-diligence-report.pdf", "Due diligence report", "en-GB")
        .Document("Core800", "model", "dismantlingInformation", "dismantling-manual.pdf", "Dismantling manual", "en-GB")
        .Document("Core800", "model", "partNumbersForComponents", "part-numbers.pdf", "Part numbers for components", "en-GB")
        .Document("Core800", "model", "informationOnSourcesOfSpareParts", "spare-parts.pdf", "Sources of spare parts", "en-GB")
        .Document("Core800", "model", "safetyMeasures", "safety-measures.pdf", "Safety measures", "sv-SE")
        .Document("Core800", "model", "informationOnTheRoleOfEndUsersInContributingToWastePrevention", $"{Web}/battery/waste-prevention", "Your role in waste prevention", "en-GB")
        .Document("Core800", "model", "informationOnTheRoleOfEndUsersInContributingToTheSeparateCollectionOfWasteBatteries", $"{Web}/battery/separate-collection", "Your role in separate collection", "en-GB")
        .Document("Core800", "model", "informationOnBatteryCollectionPreparationForSecondLifeAndOnTreatmentAtEndOfLife", $"{Web}/battery/collection-and-treatment.pdf", "Collection, second life and treatment", "en-GB")
        .Document("Core800", "batch", "carbonFootprintLabel", $"{Web}/core/carbon-footprint-label.png", "Carbon footprint label", "en-GB")
        .Document("Core800", "batch", "euDeclarationOfConformity", $"{Web}/core/eu-declaration-of-conformity.pdf", "EU declaration of conformity", "en-GB")
        .Document("Core800", "batch", "resultsOfTestReportsProvingCompliance", "test-report-2026.pdf", "Core test report 2026", "en-GB");

    /// <summary>The five documents and the two pictures the workbook of <see cref="Core"/> names.</summary>
    public static IReadOnlyList<SentFile> CoreFiles() =>
    [
        new("dismantling-manual.pdf", DocumentTesting.Pdf("dismantling")),
        new("part-numbers.pdf", DocumentTesting.Pdf("parts")),
        new("spare-parts.pdf", DocumentTesting.Pdf("spares")),
        new("safety-measures.pdf", DocumentTesting.Pdf("safety")),
        new("test-report-2026.pdf", DocumentTesting.Pdf("report")),
        new("core-model.png", DocumentTesting.Png()),
        new("core-family.png", [.. DocumentTesting.Png(), 0x01]),
    ];

    public const string WorkbookName = "Core family - data to fill.xlsx";

    public static Task<LoadCheck> CheckAsync(this LoadRig rig, FilledWorkbook workbook, IReadOnlyList<SentFile>? files = null) =>
        rig.Loader.CheckAsync(WorkbookName, workbook.Bytes(), files ?? CoreFiles());

    /// <summary>Checks, then loads what that check showed, as the page does.</summary>
    public static async Task<LoadResult> LoadAsync(this LoadRig rig, FilledWorkbook workbook, IReadOnlyList<SentFile>? files = null)
    {
        var bytes = workbook.Bytes();
        var check = await rig.Loader.CheckAsync(WorkbookName, bytes, files ?? CoreFiles());
        return await rig.Loader.LoadAsync(Who, WorkbookName, bytes, files ?? CoreFiles(), check.Stamp);
    }

    /// <summary>Compares two lists of lines and, when they differ, shows both in full (a long line is cut short otherwise).</summary>
    public static void SameLines(IEnumerable<string> expected, IEnumerable<string> actual)
    {
        var (wanted, got) = (expected.ToArray(), actual.ToArray());
        Assert.True(wanted.SequenceEqual(got), $"Expected:{Environment.NewLine}{string.Join(Environment.NewLine, wanted)}{Environment.NewLine}Actual:{Environment.NewLine}{string.Join(Environment.NewLine, got)}");
    }

    /// <summary>The check's problems as the page lists them.</summary>
    public static string[] Texts(this LoadCheck check) => check.Problems.Select(problem => problem.Text).ToArray();
}
