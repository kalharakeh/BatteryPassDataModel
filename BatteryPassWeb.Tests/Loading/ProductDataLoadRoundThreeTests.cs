using System.Globalization;
using BatteryPassWeb.Dpp.Loading;
using BatteryPassWeb.Tests.Dpp;
using static BatteryPassWeb.Tests.Loading.LoadTesting;

namespace BatteryPassWeb.Tests.Loading;

/// <summary>
/// Package X1, round three: columns an owner may copy or add (a heading used twice, a column without one) are problem
/// lines, never passed over; the check says before Load what the load does to published passports; a formula whose
/// saved result is empty text is an empty cell; values taken over from the first model's column are shown as such.
/// </summary>
[Collection(MongoCollection.Name)]
public sealed class ProductDataLoadRoundThreeTests(MongoFixture mongo)
{
    [Fact]
    public async Task ACopiedModelAndBatchColumnWithTheirHeadingsUnchanged_ShouldBeProblemLines_NotAnotherModelsFigures()
    {
        // Scenario A: the Core700 column copied as a fourth model, only its name changed to Core600; its batch column too.
        var rig = await EmptyAsync(mongo);
        var workbook = Core()
            .CopyColumn(ModelSheet, "Core700")
            .SetAt(ModelSheet, "model|Name", 4, "Core600")
            .CopyColumn(BatchSheet, "Batch of Core700");

        var check = await rig.CheckAsync(workbook);

        Assert.False(check.CanLoad);
        Assert.Contains("Models, Core700: columns K and L are both headed \"Core700\", so nothing is loaded from either. Give each column a heading of its own", check.Texts());
        Assert.Contains("Batches, Batch of Core700: columns K and L are both headed \"Batch of Core700\", so nothing is loaded from either. Give each column a heading of its own. One batch per model in a workbook: add another batch on the model's page, or in a second load", check.Texts());
        Assert.DoesNotContain(check.Records, record => record.Name.StartsWith("Core600", StringComparison.Ordinal));
        Assert.Equal(Nothing, await rig.CountsAsync());
    }

    [Fact]
    public async Task ASecondBatchColumnForOneModel_ShouldBeAProblemLine()
    {
        var rig = await EmptyAsync(mongo);

        var check = await rig.CheckAsync(Core().CopyColumn(BatchSheet, "Batch of Core700").SetNumberAt(BatchSheet, "batch|Year", 4, 2027));

        Assert.Contains(check.Texts(), text => text.StartsWith("Batches, Batch of Core700: columns K and L are both headed", StringComparison.Ordinal));
    }

    [Fact]
    public async Task AColumnWithValuesAndNoHeading_ShouldBeAProblemLine()
    {
        var rig = await EmptyAsync(mongo);

        var check = await rig.CheckAsync(Core().SetAt(ModelSheet, "model|batteryMass|gramKilogramValue", 5, "500").SetAt(SiteSheet, "site|manufacturingPlace", 4, "Lulea"));

        SameLines(
            [
                "Models: column M has values but no heading in row 1. Give it a heading, or empty it",
                "Site: column L has values but no heading in row 1. Give it a heading, or empty it",
            ],
            check.Texts().Order(StringComparer.Ordinal));
    }

    [Fact]
    public async Task TheWorkbookAsSent_ShouldHaveNoColumnWithoutAHeading()
    {
        var rig = await EmptyAsync(mongo);

        var check = await rig.CheckAsync(FilledWorkbook.Empty(), []);

        Assert.DoesNotContain(check.Texts(), text => text.Contains("no heading", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ALoadReachingAPublishedBattery_ShouldSayBeforeLoadHowManyVersionsItGets()
    {
        var rig = Around(await PublicationTesting.PublishedRigAsync(mongo));
        var workbook = FilledWorkbook.Empty()
            .Set(FamilySheet, "family|Name", "Core", "Test battery family")
            .Set(ModelSheet, "model|Name", "Core800", "EXB-NMC-100")
            .Clear(ModelSheet, "model|Name", "Core800s")
            .Clear(ModelSheet, "model|Name", "Core700")
            .Clear(ModelSheet, "model|SerialFormat", "Core800")
            .SetNumber(ModelSheet, "model|batteryMass|gramKilogramValue", "Core800", 520)
            .Document("Core800", "model", "safetyMeasures", "safety.pdf", "Safety measures", "en-GB")
            .Document("Core800", "model", "partNumbersForComponents", "parts.pdf", "Part numbers", "en-GB");
        SentFile[] files = [new("safety.pdf", DocumentTesting.Pdf("safety 2")), new("parts.pdf", DocumentTesting.Pdf("parts 2"))];

        var check = await rig.CheckAsync(workbook, files);
        var result = await rig.LoadAsync(workbook, files);

        Assert.Empty(check.Texts());
        Assert.StartsWith("1 published battery is built from records this load changes. It gets one new version, made after the whole load is saved, with all of the changes: Battery model EXB-NMC-100: its values and 2 documents. If the load stops part way", check.Publication?.Text, StringComparison.Ordinal);
        Assert.True(result.Loaded, result.Message);
        Assert.Equal(2, (await rig.Store.GetActiveVersionAsync(rig.Rig.PackId))?.Number); // as said: one new version for the whole load
    }

    [Fact]
    public async Task ALoadOnADatabaseWithoutPublishedBatteries_ShouldSayNothingAboutPassports()
    {
        var rig = await EmptyAsync(mongo);

        Assert.Null((await rig.CheckAsync(Core())).Publication);
    }

    [Fact]
    public async Task AFormulaWhoseSavedResultIsEmptyText_ShouldBeAnEmptyCell_AndTakeTheFirstModelsValue()
    {
        var rig = await EmptyAsync(mongo);

        var check = await rig.CheckAsync(Core().SetFormulaWithEmptyResult(ModelSheet, "model|batteryMass|gramKilogramValue", "Core800s"));

        Assert.Empty(check.Texts());
        Assert.Contains(new LoadChangeLine("Battery mass", string.Empty, "470 kg") { TakenFrom = "Core800" }, check.Records.Single(record => record.Name == "Core800s").Changes);
    }

    [Theory]
    [InlineData("batch|nickelRecycledContentShare|percentageValue", "Nickel recycled content share", 40)]
    [InlineData("batch|cobaltRecycledContentShare|percentageValue", "Cobalt recycled content share", 34)]
    [InlineData("batch|lithiumRecycledContentShare|percentageValue", "Lithium recycled content share", 30)]
    [InlineData("batch|leadRecycledContentShare|percentageValue", "Lead recycled content share", 25)]
    public async Task ARecycledContentShareLeftEmptyForALaterBatch_ShouldShowAsTakenFromTheFirst(string key, string row, int share)
    {
        var rig = await EmptyAsync(mongo);
        var workbook = Core()
            .SetNumber(BatchSheet, key, FirstBatch, share)
            .Set(BatchSheet, "batch|batteryModelIdentifier", "Batch of Core700", "CORE-700");

        var check = await rig.CheckAsync(workbook);

        var batch = check.Records.Single(record => record.Name.StartsWith("Core700, batch", StringComparison.Ordinal));
        Assert.Contains(new LoadChangeLine(row, string.Empty, $"{share.ToString(CultureInfo.InvariantCulture)} %") { TakenFrom = FirstBatch }, batch.Changes);
        Assert.Contains(new LoadChangeLine("Battery model identifier", string.Empty, "CORE-700"), batch.Changes);
    }

    [Fact]
    public async Task TheSerialFormatCheck_ShouldReadOnlyTheModelsBatteries_SerialAlone()
    {
        var rig = Around(await PublicationTesting.RigAsync(mongo));

        var own = await rig.Store.ListSerialsOfBatchesAsync(["batch-test-2026"]);
        var other = await rig.Store.ListSerialsOfBatchesAsync(["batch-of-another-model"]);

        var pack = Assert.Single(own);
        Assert.Equal("VPC8C05-6788", pack.SerialNumber);
        Assert.False(pack.IsDemo);
        Assert.Null(pack.SuccessorOf);
        Assert.Empty(other);
    }

    [Fact]
    public async Task AnUnchangedSerialFormat_ShouldSayNothing_AndPlanNothing()
    {
        var rig = Around(await PublicationTesting.RigAsync(mongo));
        await rig.Store.SetModelSerialFormatAsync("model-test", "VPC8C05-#####");
        var workbook = FilledWorkbook.Empty()
            .Set(FamilySheet, "family|Name", "Core", "Test battery family")
            .Set(ModelSheet, "model|Name", "Core800", "EXB-NMC-100")
            .Clear(ModelSheet, "model|Name", "Core800s")
            .Clear(ModelSheet, "model|Name", "Core700");

        var model = (await rig.CheckAsync(workbook, [])).Records.Single(record => record.Name == "EXB-NMC-100");

        Assert.DoesNotContain(model.Changes, line => line.Name == "Serial number format");
        Assert.DoesNotContain(model.Notes, note => note.Contains("doesn't fit", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ALaterBatchWithAnEmptyColumn_ShouldSayEveryValueIsTheFirstBatchs()
    {
        var rig = await EmptyAsync(mongo);

        var check = await rig.CheckAsync(Core());

        Assert.Contains("Its column on the sheet Batches is empty, so every value, the year and the site included, is taken from the batch of Core800.", check.Records.Single(record => record.Name.StartsWith("Core700, batch", StringComparison.Ordinal)).Notes);
    }
}
