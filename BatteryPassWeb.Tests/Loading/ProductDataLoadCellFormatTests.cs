using System.Text.Json.Nodes;
using BatteryPassWeb.Dpp.Loading;
using BatteryPassWeb.Dpp.Store;
using BatteryPassWeb.Tests.Dpp;
using static BatteryPassWeb.Tests.Loading.LoadTesting;

namespace BatteryPassWeb.Tests.Loading;

/// <summary>
/// Package X1, round two, points 1, 3, 6, 7 and 8: what the owner sees in Excel is what is loaded. A per-cent cell is
/// loaded as the number shown; a date, a formula without its value and a number far too large are problem lines; a cell
/// beyond Excel's last row or column refuses the workbook without holding the load for ever; a hazardous-substances line
/// with only its model is incomplete. Every workbook here is written the way Excel saves one (shared texts, style indexes).
/// </summary>
[Collection(MongoCollection.Name)]
public sealed class ProductDataLoadCellFormatTests(MongoFixture mongo)
{
    private const string Concentration = "Concentration";

    private static async Task<LoadCheck> CheckSavedByExcelAsync(LoadRig rig, FilledWorkbook workbook, IReadOnlyList<SentFile>? files = null) =>
        await rig.Loader.CheckAsync(WorkbookName, workbook.AsExcelSaves(), files ?? CoreFiles());

    [Fact]
    public async Task APerCentCell_ShouldBeLoadedAsTheNumberTheOwnerSees()
    {
        var rig = await EmptyAsync(mongo);
        var workbook = Core()
            .SetFormatted(BatchSheet, "batch|renewableContentShare|percentageValue", FirstBatch, 0.16, 9) // 16 %
            .SetFormatted(BatchSheet, "batch|nickelRecycledContentShare|percentageValue", FirstBatch, 1, 9) // 100 %
            .SetFormatted(ModelSheet, "model|capacityFade|percentageValue", "Core800", 0.07, 164, "0.0%"); // 7.0 %, the workbook's own format
        var bytes = workbook.AsExcelSaves();

        var check = await rig.Loader.CheckAsync(WorkbookName, bytes, CoreFiles());
        var result = await rig.Loader.LoadAsync(Who, WorkbookName, bytes, CoreFiles(), check.Stamp);

        Assert.Empty(check.Texts());
        Assert.Contains(new LoadChangeLine("Renewable content share", string.Empty, "16 %"), check.Records.First(record => record.Record == LoadRecord.Batch).Changes);
        Assert.True(result.Loaded, result.Message);
        var batch = (await rig.Store.ListAsync<ProductionBatch>()).First();
        Assert.Equal("16", batch.Values["renewableContentShare"].Value!["percentageValue"]!.ToJsonString());
        Assert.Equal("100", batch.Values["nickelRecycledContentShare"].Value!["percentageValue"]!.ToJsonString());
        Assert.All(await rig.Store.ListAsync<BatteryModel>(), model => Assert.Equal("7", model.Values["capacityFade"].Value!["percentageValue"]!.ToJsonString()));
    }

    [Fact]
    public async Task APerCentConcentration_ShouldBeLoadedAsShown_AndAPlainNumberAsTyped()
    {
        var rig = await EmptyAsync(mongo);
        var workbook = Core()
            .Hazard(1, "Core800", "Lithium hexafluorophosphate", concentration: 1.2)
            .Hazard(2, "all", "Cobalt sulphate", concentration: 0.005);
        workbook.FormatHazard(2, Concentration, 10); // 0.50 %: Excel stores 0.005

        var bytes = workbook.AsExcelSaves();
        var check = await rig.Loader.CheckAsync(WorkbookName, bytes, CoreFiles());
        var result = await rig.Loader.LoadAsync(Who, WorkbookName, bytes, CoreFiles(), check.Stamp);

        Assert.True(result.Loaded, result.Message);
        var model = (await rig.Store.ListAsync<BatteryModel>()).Single(item => item.Name == "Core800");
        var rows = (JsonArray)model.Values["hazardousSubstancesAndTheirImpact"].Value!;
        Assert.Equal(["1.2", "0.5"], rows.Select(row => row!["concentration"]!.ToJsonString()));
        Assert.Contains("Cobalt sulphate (CAS No 21324-40-3, 0.5)", check.Records.Single(record => record.Name == "Core800").Changes.Single(line => line.Name == "Hazardous substances and their impact").New);
    }

    [Fact]
    public async Task ACommaDecimalTypedAsText_ShouldStillBeAProblem()
    {
        var rig = await EmptyAsync(mongo);

        var check = await CheckSavedByExcelAsync(rig, Core().Hazard(1, "all", "Lithium hexafluorophosphate", concentration: "1,2"));

        Assert.Contains("Hazardous substances, row 4 (Lithium hexafluorophosphate), Concentration: 1,2 is not a number; type the number only, with a point for decimals", check.Texts());
    }

    [Fact]
    public async Task ADateAFormulaWithoutValueAndAHugeNumber_ShouldEachBeAProblemAtItsCell()
    {
        var rig = await EmptyAsync(mongo);
        var workbook = Core()
            .SetFormatted(ModelSheet, "model|batteryMass|gramKilogramValue", "Core800", 46000, 14) // shows 9 Dec 2025
            .SetFormatted(ModelSheet, "model|ratedCapacity|amperehourMiliamperehourValue", "Core800s", 220, 165, "yyyy-mm-dd")
            .SetFormulaWithoutValue(ModelSheet, "model|nominalVoltage|voltValue", "Core700")
            .SetNumber(BatchSheet, "batch|renewableContentShare|percentageValue", FirstBatch, 1e30);

        var check = await CheckSavedByExcelAsync(rig, workbook);

        Assert.Null(check.WorkbookProblem);
        SameLines(
            [
                "Batches, Renewable content share, Batch of Core800: 1E+30 is far too large to be a value of the passport; type the value itself",
                "Models, Battery mass, Core800: this cell is formatted as a date or a time in Excel, so it doesn't hold what it shows. Format it as text or as a number and type the value again",
                "Models, Nominal voltage, Core700: this cell holds a formula without a value; type the value",
                "Models, Rated capacity, Core800s: this cell is formatted as a date or a time in Excel, so it doesn't hold what it shows. Format it as text or as a number and type the value again",
            ],
            check.Texts().Order(StringComparer.Ordinal));
        Assert.Equal(Nothing, await rig.CountsAsync());
    }

    [Fact]
    public async Task AFormatWithLettersInQuotes_ShouldNotBeTakenForADate()
    {
        var rig = await EmptyAsync(mongo);

        var check = await CheckSavedByExcelAsync(rig, Core().SetFormatted(ModelSheet, "model|batteryMass|gramKilogramValue", "Core800", 470, 166, "0 \"kg mass\""));

        Assert.Empty(check.Texts());
        Assert.Contains(new LoadChangeLine("Battery mass", string.Empty, "470 kg"), check.Records.Single(record => record.Name == "Core800").Changes);
    }

    [Theory]
    [InlineData("A2147483647")]
    [InlineData("XFE1")]
    [InlineData("A1048577")]
    public async Task ACellBeyondExcelsLastRowOrColumn_ShouldRefuseTheWorkbook_AndTheNextLoadStillRuns(string reference)
    {
        var rig = await EmptyAsync(mongo);
        var crafted = Core().CellAt(ModelSheet, reference, "x").Bytes();

        var refused = await rig.Loader.LoadAsync(Who, WorkbookName, crafted, CoreFiles(), "any");
        var next = await rig.LoadAsync(Core());

        Assert.Equal(XlsxReader.OutsideTheSheet, refused.Check.WorkbookProblem);
        Assert.True(next.Loaded, next.Message);
    }

    [Fact]
    public async Task AHazardLineWithOnlyItsModel_ShouldBeIncomplete_AndTheRowsAfterItNamedByTheirOwnRow()
    {
        var rig = await EmptyAsync(mongo);
        var workbook = Core()
            .Hazard(2, "Core800", null, identifierType: null, identifier: null, concentration: null, hazards: null, precautions: null)
            .Hazard(3, "Core800", "Cobalt sulphate", identifierType: "REACH");
        workbook.ClearHazard(2, Concentration);

        var check = await CheckSavedByExcelAsync(rig, workbook);

        SameLines(
            [
                "Hazardous substances, row 5, Model: this line names a model and nothing else, so it is incomplete. Fill in the substance, or empty its Model cell",
                "Hazardous substances, row 6 (Cobalt sulphate), Identifier type: Identifier type must be one of the listed choices",
            ],
            check.Texts().Order(StringComparer.Ordinal));
    }

    [Fact]
    public void TheReader_ShouldTellPerCentsAndDatesByTheirFormat()
    {
        Assert.Equal(CellNote.Percent, XlsxReader.NoteOf(9, null));
        Assert.Equal(CellNote.Percent, XlsxReader.NoteOf(10, null));
        Assert.Equal(CellNote.Date, XlsxReader.NoteOf(14, null));
        Assert.Equal(CellNote.Date, XlsxReader.NoteOf(22, null));
        Assert.Equal(CellNote.None, XlsxReader.NoteOf(0, null));
        Assert.Equal(CellNote.None, XlsxReader.NoteOf(49, null)); // text
        Assert.Equal(CellNote.Percent, XlsxReader.NoteOf(170, "0.00%;[Red]-0.00%"));
        Assert.Equal(CellNote.Date, XlsxReader.NoteOf(171, "dd/mm/yyyy"));
        Assert.Equal(CellNote.Date, XlsxReader.NoteOf(172, "[h]:mm"));
        Assert.Equal(CellNote.None, XlsxReader.NoteOf(173, "#,##0.00 \"mm\""));
        Assert.Equal(CellNote.None, XlsxReader.NoteOf(174, "General"));
        Assert.Equal(CellNote.None, XlsxReader.NoteOf(175, "[Blue]0.0"));
        Assert.Equal(CellNote.None, XlsxReader.NoteOf(176, "0\\%")); // an escaped % is only shown: Excel doesn't multiply the number
    }
}
