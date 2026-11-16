using BatteryPassWeb.Dpp.Editing;
using BatteryPassWeb.Dpp.Loading;
using BatteryPassWeb.Dpp.Pages;
using BatteryPassWeb.Dpp.Store;
using BatteryPassWeb.Tests.Dpp;
using static BatteryPassWeb.Tests.Loading.LoadTesting;

namespace BatteryPassWeb.Tests.Loading;

/// <summary>
/// The manager's answer of 16 November 2026 to package X1: a document the workbook names that wasn't sent doesn't stop
/// the load. The check lists it under "Documents still to come" with what it means for a passport; everything else is
/// loaded and that document's value stays as it was; a later load with the file completes it. A file that is already
/// stored with the same content isn't stored again. An empty cell is never a problem.
/// </summary>
[Collection(MongoCollection.Name)]
public sealed class ProductDataLoadPendingTests(MongoFixture mongo)
{
    private static readonly SentFile[] WithoutSafety = CoreFiles().Where(file => file.FileName != "safety-measures.pdf").ToArray();

    [Fact]
    public async Task AFileNamedButNotSent_ShouldBeStillToCome_AndEverythingElseLoaded()
    {
        var rig = await EmptyAsync(mongo);

        var result = await rig.LoadAsync(Core(), WithoutSafety);

        Assert.True(result.Loaded, result.Message);
        Assert.Empty(result.Check.Texts());
        Assert.Equal(
            ["Battery model Core800", "Battery model Core800s", "Battery model Core700"],
            result.Check.Pending.Select(pending => pending.Record));
        Assert.All(result.Check.Pending, pending => Assert.Equal(
            ("Safety measures", "the file safety-measures.pdf wasn't sent", "The passport of a real battery can't be published until it is there."),
            (pending.Document, pending.Missing, pending.Meaning)));
        var models = await rig.Store.ListAsync<BatteryModel>();
        Assert.All(models, model => Assert.False(model.Values.ContainsKey("safetyMeasures")));
        Assert.All(models, model => Assert.True(model.Values.ContainsKey("dismantlingInformation")));
        Assert.All(models, model => Assert.Equal(1, RecordEditor.CountMissing(TargetKind.Model, model.Values, model.Answers())));
    }

    [Fact]
    public async Task ALaterLoadWithTheFile_ShouldCompleteIt_AndStoreNothingTwice()
    {
        var rig = await EmptyAsync(mongo);
        await rig.LoadAsync(Core(), WithoutSafety);
        var documents = (await rig.CountsAsync()).Split("documents ")[1].Split(',')[0];

        var check = await rig.CheckAsync(Core(), CoreFiles());
        var result = await rig.LoadAsync(Core(), CoreFiles());

        Assert.Empty(check.Pending);
        Assert.Equal(["Core800: Changed", "Core800s: Changed", "Core700: Changed"], check.Records.Where(record => record.State != LoadStates.Same).Select(record => $"{record.Name}: {record.State}"));
        Assert.Equal(["Safety measures"], check.Records.SelectMany(record => record.Documents).Where(document => document.State != LoadStates.Same).Select(document => document.Name).Distinct());
        Assert.True(result.Loaded, result.Message);
        Assert.Equal(int.Parse(documents) + 3, int.Parse((await rig.CountsAsync()).Split("documents ")[1].Split(',')[0]));
        Assert.All(await rig.Store.ListAsync<BatteryModel>(), model => Assert.True(model.Values.ContainsKey("safetyMeasures")));
    }

    [Fact]
    public async Task ADocumentThatIsThere_ShouldStay_WhenItsFileIsntSentAgain()
    {
        var rig = await EmptyAsync(mongo);
        await rig.LoadAsync(Core());
        var before = await rig.CountsAsync();

        var check = await rig.CheckAsync(Core().Document("Core800s", "model", "safetyMeasures", null, "Another title"), []);

        Assert.Empty(check.Texts());
        var pending = Assert.Single(check.Pending);
        Assert.Equal(("Battery model Core800s", "the file safety-measures.pdf wasn't sent, so its new title or language isn't saved yet", "The document that is in the app stays until then."), (pending.Record, pending.Missing, pending.Meaning));
        Assert.False(check.CanLoad); // nothing else changes
        Assert.Equal(before, await rig.CountsAsync());
    }

    [Fact]
    public async Task ADocumentWithATitleButNoLinkYet_ShouldBeStillToCome_WithWhatItMeans()
    {
        var rig = await EmptyAsync(mongo);

        var check = await rig.CheckAsync(Core()
            .Document("Core800", "batch", "euDeclarationOfConformity", null, "EU declaration of conformity", "en-GB")
            .Document("Core800", "model", "informationOfDueDiligenceReport", null, "Due diligence report", "en-GB"));

        Assert.Empty(check.Texts());
        Assert.True(check.CanLoad);
        Assert.Contains(new LoadPending("Batch Core800, batch 2026 · Plant South", "EU declaration of conformity", "no web link is written yet", "The passport of a real battery can't be published until it is there."), check.Pending);
        Assert.Contains(new LoadPending("Battery model Core800", "Due diligence report", "no web link is written yet", "No passport waits for it."), check.Pending);
    }

    [Fact]
    public async Task APictureNamedButNotSent_ShouldBeStillToCome()
    {
        var rig = await EmptyAsync(mongo);

        var result = await rig.LoadAsync(Core(), CoreFiles().Where(file => file.FileName != "core-family.png").ToArray());

        Assert.True(result.Loaded, result.Message);
        Assert.Equal(new LoadPending("Battery family Core", "Picture", "the picture core-family.png wasn't sent", "No passport waits for a picture: the public page shows none of its own until then."), Assert.Single(result.Check.Pending));
        Assert.Null(Assert.Single(await rig.Store.ListAsync<BatteryFamily>()).PictureFileId);
    }

    [Fact]
    public async Task ASentFileNamedNowhere_AndARefusedFile_ShouldStillStopTheLoad()
    {
        var rig = await EmptyAsync(mongo);
        var files = CoreFiles().Select(file => file.FileName == "spare-parts.pdf" ? file with { Content = "not a pdf"u8.ToArray() } : file).Append(new SentFile("holiday.png", DocumentTesting.Png())).ToArray();

        var result = await rig.LoadAsync(Core(), files);

        Assert.False(result.Loaded);
        SameLines(
            [
                "Documents and links, Information on sources of spare parts (Core800), File name or web link: spare-parts.pdf: only PDF, PNG and JPEG files are accepted, recognised by their content",
                "the file holiday.png was sent, but the workbook doesn't name it anywhere. Write its name in the cell it belongs to, or leave it out",
            ],
            result.Check.Texts().Order(StringComparer.Ordinal));
        Assert.Equal(Nothing, await rig.CountsAsync());
    }

    [Fact]
    public async Task TooManyFiles_ShouldBeSaidInTheAppsWords()
    {
        var rig = await EmptyAsync(mongo);
        var files = Enumerable.Range(1, LoadLimits.MaxFiles + 1).Select(index => new SentFile($"extra-{index}.pdf", DocumentTesting.Pdf($"{index}"))).ToArray();

        var check = await rig.CheckAsync(Core(), files);

        Assert.Contains($"{LoadLimits.MaxFiles + 1} files were sent; {LoadLimits.TooMany}", check.Texts());
    }

    [Fact]
    public async Task EmptyCells_ShouldNeverBeAProblem_TheEmptyWorkbookIsChecked()
    {
        var rig = await EmptyAsync(mongo);

        var check = await rig.CheckAsync(FilledWorkbook.Empty(), []);

        Assert.DoesNotContain(check.Texts(), text => text.Contains("give", StringComparison.Ordinal) && !text.Contains("year", StringComparison.Ordinal) && !text.Contains("site", StringComparison.Ordinal));
        Assert.DoesNotContain(check.Texts(), text => text.Contains("is missing", StringComparison.Ordinal));
    }
}
