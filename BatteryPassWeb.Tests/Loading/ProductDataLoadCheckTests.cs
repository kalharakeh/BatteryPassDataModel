using BatteryPassWeb.Dpp.Loading;
using BatteryPassWeb.Tests.Dpp;
using static BatteryPassWeb.Tests.Loading.LoadTesting;

namespace BatteryPassWeb.Tests.Loading;

/// <summary>
/// Package X1, point 2: the check says what a load would do, in the owner's words, and saves nothing. It names the
/// family, each model, batch, site and operator as new or changed, with every value and every document.
/// </summary>
[Collection(MongoCollection.Name)]
public sealed class ProductDataLoadCheckTests(MongoFixture mongo)
{
    [Fact]
    public async Task ACheckOfTheFilledWorkbook_ShouldListEveryRecordAsNew_WithoutAProblem_AndSaveNothing()
    {
        var rig = await EmptyAsync(mongo);

        var check = await rig.CheckAsync(Core());

        Assert.Null(check.WorkbookProblem);
        Assert.Empty(check.Texts());
        Assert.Equal(
            [
                "Battery family Core: New",
                "Battery model Core800: New",
                "Battery model Core800s: New",
                "Battery model Core700: New",
                "Batch Core800, batch 2026 · Plant South: New",
                "Batch Core800s, batch 2026 · Plant South: New",
                "Batch Core700, batch 2026 · Plant South: New",
                "Manufacturing site Plant South: New",
                "Economic operator Fleet Services AB: New",
            ],
            check.Records.Select(record => $"{record.Kind} {record.Name}: {record.State}"));
        Assert.Equal("1 battery family, 3 battery models, 3 batches, 1 manufacturing site and 1 economic operator", check.Summary);
        Assert.True(check.CanLoad);
        Assert.Equal(0, check.PublishedPassports);
        Assert.Equal(Nothing, await rig.CountsAsync());
    }

    [Fact]
    public async Task TheCheck_ShouldShowEveryValueOfANewRecord_ByItsRowsName()
    {
        var rig = await EmptyAsync(mongo);

        var check = await rig.CheckAsync(Core());

        var model = check.Records.Single(record => record.Name == "Core800");
        Assert.Contains(new LoadChangeLine("Battery mass", string.Empty, "470"), model.Changes);
        Assert.Contains(new LoadChangeLine("Critical raw materials", string.Empty, "Cobalt; Lithium; Natural graphite; Nickel"), model.Changes);
        Assert.Contains(new LoadChangeLine("Nominal voltage: valid from this temperature", string.Empty, "-20"), model.Changes);
        Assert.Contains(new LoadChangeLine("Does this battery come with a commercial warranty?", string.Empty, "Yes"), model.Changes);
        Assert.Contains(new LoadChangeLine("Hazardous substances and their impact", string.Empty, "Lithium hexafluorophosphate"), model.Changes);
        Assert.Contains("Serial number format: not loaded yet (it arrives with the serial number update)", model.Notes);
        var site = check.Records.Single(record => record.Record == LoadRecord.Site);
        Assert.Contains(new LoadChangeLine("Manufacturer: name", string.Empty, "Scania Industrial Batteries AB"), site.Changes);
    }

    [Fact]
    public async Task TheCheck_ShouldShowEveryDocument_WithTheTypeItWillBeShownAs()
    {
        var rig = await EmptyAsync(mongo);

        var check = await rig.CheckAsync(Core());

        var model = check.Records.Single(record => record.Name == "Core800");
        Assert.Contains(new LoadDocumentView("Dismantling information: manuals for removal and disassembly", "File dismantling-manual.pdf (a PDF, 1 KB) · title \"Dismantling manual\" · English (United Kingdom)", LoadStates.New), model.Documents);
        Assert.Contains(new LoadDocumentView("Separate collection symbol", $"Web link {Web}/symbols/separate-collection.svg · shown as an SVG picture · title \"Separate collection symbol\" · English (United Kingdom)", LoadStates.New), model.Documents);
        Assert.Contains(model.Documents, document => document.Name.StartsWith("Role of end-users in waste prevention", StringComparison.Ordinal) && document.Detail.Contains("shown as a web page", StringComparison.Ordinal));
        Assert.Contains(new LoadDocumentView("Picture", "File core-model.png (a PNG picture, 1 KB)", LoadStates.New), model.Documents);
        var batch = check.Records.First(record => record.Record == LoadRecord.Batch);
        Assert.Contains(batch.Documents, document => document.Detail.StartsWith("File test-report-2026.pdf", StringComparison.Ordinal));
        Assert.Contains(batch.Documents, document => document.Detail.Contains("shown as a PNG picture", StringComparison.Ordinal));
        // The same documents go to each model that leaves its lines empty.
        Assert.Equal(model.Documents.Count, check.Records.Single(record => record.Name == "Core700").Documents.Count);
    }
}
