using BatteryPassWeb.Dpp.Loading;
using BatteryPassWeb.Dpp.Store;
using BatteryPassWeb.Tests.Dpp;
using static BatteryPassWeb.Tests.Loading.LoadTesting;

namespace BatteryPassWeb.Tests.Loading;

/// <summary>
/// Package X1, round two, points 2 and 4: a batch belongs to the model whose heading its own heading names, not to the
/// model in the same position; "the first model" is the first model column of Models as it stands, and the check says
/// which model empty cells were taken from. A later model's document line is the first model's only when all three of
/// its cells are empty.
/// </summary>
[Collection(MongoCollection.Name)]
public sealed class ProductDataLoadColumnsTests(MongoFixture mongo)
{
    private static string[] Records(LoadCheck check) => check.Records.Select(record => $"{record.Kind} {record.Name}").ToArray();

    [Fact]
    public async Task AModelColumnDeletedOnModelsOnly_ShouldLeaveEachBatchWithItsOwnModel()
    {
        var rig = await EmptyAsync(mongo);
        var workbook = Core()
            .SetNumber(BatchSheet, "batch|renewableContentShare|percentageValue", "Batch of Core700", 22)
            .DeleteColumn(ModelSheet, "Core800s");

        var check = await rig.CheckAsync(workbook);
        var result = await rig.LoadAsync(workbook);

        Assert.Empty(check.Texts());
        SameLines(
            [
                "Battery family Core", "Battery model Core800", "Battery model Core700",
                "Batch Core800, batch 2026 · Plant South", "Batch Core700, batch 2026 · Plant South",
                "Manufacturing site Plant South", "Economic operator Fleet Services AB",
            ],
            Records(check));
        Assert.Contains("No batch is loaded from the column Batch of Core800s: the sheet Models has no column for its model, and it is empty.", check.Notes);
        Assert.True(result.Loaded, result.Message);
        var core700 = (await rig.Store.ListAsync<BatteryModel>()).Single(model => model.Name == "Core700");
        Assert.Equal("22", Assert.Single(await rig.Store.ListBatchesAsync(core700.Id)).Values["renewableContentShare"].Value!["percentageValue"]!.ToJsonString());
    }

    [Fact]
    public async Task ABatchColumnWithValuesAndNoModelColumn_ShouldBeAProblem()
    {
        var rig = await EmptyAsync(mongo);
        var workbook = Core()
            .Set(BatchSheet, "batch|batteryModelIdentifier", "Batch of Core800s", "CORE-S")
            .DeleteColumn(ModelSheet, "Core800s");

        var check = await rig.CheckAsync(workbook);

        SameLines(["Batches, Batch of Core800s: the sheet Models has no column for this batch's model. A batch column is headed \"Batch of\" and its model's heading on the sheet Models"], check.Texts());
    }

    [Fact]
    public async Task AModelWithoutABatchColumn_ShouldBeSaidInTheCheck()
    {
        var rig = await EmptyAsync(mongo);

        var check = await rig.CheckAsync(Core().DeleteColumn(BatchSheet, "Batch of Core700"));

        Assert.Empty(check.Texts());
        Assert.Contains("No batch for Core700: the sheet Batches has no column headed \"Batch of Core700\".", check.Notes);
        Assert.DoesNotContain("Batch Core700, batch 2026 · Plant South", Records(check));
    }

    [Fact]
    public async Task TheFirstModel_ShouldBeTheFirstColumnOfModelsAsItStands_AndTheCheckSaysWhich()
    {
        var rig = await EmptyAsync(mongo);
        var workbook = Core()
            .DeleteColumn(ModelSheet, "Core800")
            .DeleteColumn(BatchSheet, "Batch of Core800")
            .ExampleInto(ModelSheet, "Core800s")
            .ExampleInto(BatchSheet, "Batch of Core800s")
            .Set(BatchSheet, "batch|Site", "Batch of Core800s", "Plant South")
            .Set(BatchSheet, "batch|webLinkToPublicCarbonFootprintStudy", "Batch of Core800s", $"{Web}/core/carbon-footprint-study")
            .Clear(ModelSheet, "model|Picture", "Core800s");

        var check = await rig.CheckAsync(workbook, CoreFiles().Where(file => file.FileName != "core-model.png").ToArray());

        var core700 = check.Records.Single(record => record.Name == "Core700");
        Assert.Contains(core700.Notes, note => note.EndsWith("left empty take their values from Core800s, the first model on the sheet Models.", StringComparison.Ordinal));
        Assert.Contains(check.Records.Single(record => record.Name == "Core700, batch 2026 · Plant South").Notes, note => note.EndsWith("from the batch of Core800s.", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ALaterModelsLineWithItsOwnFile_ShouldTakeItsOwnTitleAndLanguage_EvenWhenEmpty()
    {
        var rig = await EmptyAsync(mongo);
        var workbook = Core().Document("Core800s", "model", "safetyMeasures", "safety-core800s.pdf");
        var files = CoreFiles().Append(new SentFile("safety-core800s.pdf", DocumentTesting.Pdf("safety, short pack"))).ToArray();

        var check = await rig.CheckAsync(workbook, files);
        var result = await rig.LoadAsync(workbook, files);

        Assert.True(result.Loaded, result.Message);
        Assert.Contains(check.Records.Single(record => record.Name == "Core800s").Documents, document => document is { Name: "Safety measures" } && document.Detail.EndsWith("title \"safety-core800s.pdf\" · no language", StringComparison.Ordinal));
        var core800s = (await rig.Store.ListAsync<BatteryModel>()).Single(model => model.Name == "Core800s");
        var link = core800s.Values["safetyMeasures"].Value!;
        Assert.Equal("safety-core800s.pdf", link["resourceTitle"]!.GetValue<string>());
        Assert.Null(link["language"]);
    }
}
