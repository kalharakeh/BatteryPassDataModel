using BatteryPassWeb.Dpp.Documents;
using BatteryPassWeb.Dpp.Editing;
using BatteryPassWeb.Dpp.Forms;
using BatteryPassWeb.Dpp.Pages;
using BatteryPassWeb.Dpp.Store;
using BatteryPassWeb.Dpp.Values;

namespace BatteryPassWeb.Tests.Dpp;

[Collection(MongoCollection.Name)]
public sealed class RecordEditorTests(MongoFixture mongo)
{
    private const string ModelId = "model-test";

    [Theory]
    [InlineData(TargetKind.Model, 37)]
    [InlineData(TargetKind.Batch, 16)]
    [InlineData(TargetKind.Site, 3)]
    [InlineData(TargetKind.Operator, 2)]
    public void EachRecord_ShouldEditItsOwnAttributes(TargetKind kind, int count)
    {
        Assert.Equal(count, RecordEditor.AttributesOf(kind).Count);
    }

    [Theory]
    [InlineData(TargetKind.Model, 28)]
    [InlineData(TargetKind.Batch, 4)]
    [InlineData(TargetKind.Site, 3)]
    [InlineData(TargetKind.Operator, 2)]
    public void AnEmptyRecord_ShouldMissOnlyItsValuesNeededByFeb2027(TargetKind kind, int count)
    {
        // Values needed only later (8 of a batch's, 2 of a model's) and conditional ones aren't counted.
        Assert.Equal(count, RecordEditor.CountMissing(kind, new Dictionary<string, AttributeValue>()));
    }

    [Fact]
    public async Task MissingValues_ShouldBeTheEmptyOnesNeededByFeb2027()
    {
        var rig = await PublicationTesting.RigAsync(mongo);
        var stored = (await rig.Store.GetAsync<BatteryModel>(ModelId))!.Values;
        IReadOnlyDictionary<string, AttributeValue> Without(string property) => stored.Where(pair => pair.Key != property).ToDictionary();

        Assert.Equal(0, RecordEditor.CountMissing(TargetKind.Model, stored));
        Assert.Equal(0, RecordEditor.CountMissing(TargetKind.Model, Without("symbolsForCadmiumAndLead"))); // #24 is conditional
        Assert.Equal(1, RecordEditor.CountMissing(TargetKind.Model, Without("batteryMass"))); // #21 is needed by Feb 2027

        var sections = await PageTesting.Editor(rig).SectionsAsync(TargetKind.Model, ModelId, Without("batteryMass"));
        var missing = Assert.Single(sections.SelectMany(section => section.Fields), field => field.IsMissing);
        Assert.Equal(21, missing.Attribute.Number);
        Assert.Equal([1, 0, 0, 0, 0, 0], sections.Select(section => section.Missing));
    }

    [Fact]
    public async Task UnchangedForm_ShouldChangeNothing()
    {
        var rig = await PublicationTesting.RigAsync(mongo);
        var stored = (await rig.Store.GetAsync<BatteryModel>(ModelId))!.Values;

        var post = PageTesting.Editor(rig).Read(TargetKind.Model, stored, PageTesting.Field(PageTesting.ShownForm(TargetKind.Model, stored)));

        Assert.Empty(post.Changed);
    }

    [Fact]
    public async Task OneEditedField_ShouldBeTheOnlyChange()
    {
        var rig = await PublicationTesting.RigAsync(mongo);
        var stored = (await rig.Store.GetAsync<BatteryModel>(ModelId))!.Values;
        var form = PageTesting.ShownForm(TargetKind.Model, stored);
        form["v.batteryMass"] = "520";

        var post = PageTesting.Editor(rig).Read(TargetKind.Model, stored, PageTesting.Field(form));

        var change = Assert.Single(post.Changed);
        Assert.Equal("batteryMass", change.Key);
        Assert.Equal("520", post.Texts["v.batteryMass"]);
    }

    [Fact]
    public async Task Sections_ShouldFollowTheSchemaSectionsAndShowTheUploadedFile()
    {
        var rig = await PublicationTesting.RigAsync(mongo);
        var editor = PageTesting.Editor(rig);
        var uploaded = await PageTesting.Documents(rig).UploadAsync(PublicationTesting.Team, TargetKind.Model, ModelId, "dismantlingInformation", new DocumentUpload("manual.pdf", DocumentTesting.Pdf()));
        var stored = (await rig.Store.GetAsync<BatteryModel>(ModelId))!.Values;

        var sections = await editor.SectionsAsync(TargetKind.Model, ModelId, stored);

        Assert.Equal(
            ["Identifiers and product data", "Symbols, labels and conformity", "Due diligence", "Materials and composition", "Circularity", "Performance and durability"],
            sections.Select(section => section.Title));
        Assert.Equal(37, sections.Sum(section => section.Fields.Count));
        var dismantling = sections.SelectMany(section => section.Fields).Single(field => field.Attribute.Number == 45);
        Assert.Equal(uploaded.Document!.Id, dismantling.Document?.Id);
        Assert.Equal(("Required by Feb 2027", "Shared"), (dismantling.Marker, dismantling.AudienceLabel));
        var substances = sections.SelectMany(section => section.Fields).Single(field => field.Attribute.Number == 44);
        Assert.Equal(FormValues.RowCount(stored["hazardousSubstancesAndTheirImpact"].Value) + 1, substances.Rows); // one blank row to add another
    }

    [Fact]
    public async Task SavingADraftsModel_ShouldNotAsk()
    {
        var rig = await PublicationTesting.RigAsync(mongo);
        var editor = PageTesting.Editor(rig);
        var stored = (await rig.Store.GetAsync<BatteryModel>(ModelId))!.Values;
        var form = PageTesting.ShownForm(TargetKind.Model, stored);
        form["v.batteryMass"] = "520";

        var result = await editor.SaveAsync(PublicationTesting.Team, TargetKind.Model, ModelId, editor.Read(TargetKind.Model, stored, PageTesting.Field(form)), confirmed: false);

        Assert.Equal((EditorOutcome.Saved, 1), (result.Outcome, result.SavedCount));
        Assert.Equal("520", (await rig.Store.GetAsync<BatteryModel>(ModelId))!.Values["batteryMass"].Value!["gramKilogramValue"]!.ToJsonString());
    }

    [Fact]
    public async Task ChangingAPublishedPassportsModel_ShouldAskBeforePublishing()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo);
        var editor = PageTesting.Editor(rig);
        var stored = (await rig.Store.GetAsync<BatteryModel>(ModelId))!.Values;
        var form = PageTesting.ShownForm(TargetKind.Model, stored);
        form["v.ratedCapacity"] = "210";
        var post = editor.Read(TargetKind.Model, stored, PageTesting.Field(form));

        var asked = await editor.SaveAsync(PublicationTesting.Team, TargetKind.Model, ModelId, post, confirmed: false);

        Assert.Equal(EditorOutcome.NeedsConfirmation, asked.Outcome);
        Assert.Equal(new AffectedPassport(rig.PackId, "VPC8C05-6788", "cluster-test", 1), Assert.Single(asked.Affected));
        Assert.True(FormValues.SameValue(stored["ratedCapacity"].Value, (await rig.Store.GetAsync<BatteryModel>(ModelId))!.Values["ratedCapacity"].Value));

        var confirmed = await editor.SaveAsync(PublicationTesting.Team, TargetKind.Model, ModelId, post, confirmed: true);

        Assert.Equal(EditorOutcome.Saved, confirmed.Outcome);
        Assert.Equal(2, (await rig.Store.GetActiveVersionAsync(rig.PackId))?.Number);
    }

    [Fact]
    public async Task ARefusedValue_ShouldNotStopTheOthers()
    {
        var rig = await PublicationTesting.RigAsync(mongo);
        var editor = PageTesting.Editor(rig);
        var stored = (await rig.Store.GetAsync<BatteryModel>(ModelId))!.Values;
        var form = PageTesting.ShownForm(TargetKind.Model, stored);
        form["v.batteryMass"] = "520";
        form["v.initialRoundTripEnergyEfficiency"] = "120";

        var result = await editor.SaveAsync(PublicationTesting.Team, TargetKind.Model, ModelId, editor.Read(TargetKind.Model, stored, PageTesting.Field(form)), confirmed: false);

        Assert.Equal((EditorOutcome.Refused, 1), (result.Outcome, result.SavedCount));
        Assert.Equal(["initialRoundTripEnergyEfficiency"], result.Refusals(AttributeFormsTests.Forms).Keys);
        Assert.Equal("520", (await rig.Store.GetAsync<BatteryModel>(ModelId))!.Values["batteryMass"].Value!["gramKilogramValue"]!.ToJsonString());
    }

    [Fact]
    public async Task NothingToSave_ShouldSaySo()
    {
        var rig = await PublicationTesting.RigAsync(mongo);
        var editor = PageTesting.Editor(rig);
        var empty = new EditorPost(new Dictionary<string, System.Text.Json.Nodes.JsonNode>(), new Dictionary<string, string>(), new Dictionary<string, int>());
        var change = new EditorPost(new Dictionary<string, System.Text.Json.Nodes.JsonNode> { ["manufacturingPlace"] = "Example town" }, new Dictionary<string, string>(), new Dictionary<string, int>());

        Assert.Equal(EditorOutcome.NothingChanged, (await editor.SaveAsync(PublicationTesting.Team, TargetKind.Site, "site-test", empty, confirmed: false)).Outcome);
        Assert.Equal(EditorOutcome.NotFound, (await editor.SaveAsync(PublicationTesting.Team, TargetKind.Site, "no-such-site", change, confirmed: false)).Outcome);
    }
}
