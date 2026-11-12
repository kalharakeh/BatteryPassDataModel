using System.Text.Json.Nodes;
using BatteryPassWeb.Dpp.Editing;
using BatteryPassWeb.Dpp.Forms;
using BatteryPassWeb.Dpp.Pages;
using BatteryPassWeb.Dpp.Store;
using BatteryPassWeb.Dpp.Values;

namespace BatteryPassWeb.Tests.Dpp;

/// <summary>
/// The editor page carries a stamp of what each field showed. A save then changes only the fields that were edited on
/// the page, so a page left open never undoes someone else's newer value; a field edited on both sides is refused.
/// </summary>
[Collection(MongoCollection.Name)]
public sealed class EditorStampTests(MongoFixture mongo)
{
    private const string ModelId = "model-test";

    private static async Task<IReadOnlyDictionary<string, AttributeValue>> StoredAsync(PublicationRig rig) =>
        (await rig.Store.GetAsync<BatteryModel>(ModelId))!.Values;

    // Someone else saves a mass of 600 after the page was opened.
    private static Task SomeoneElseSavesAMassAsync(PublicationRig rig) => rig.Pipeline.SaveAsync(
        ChangeSender.Team("colleague@example.com"),
        new ChangeSet(TargetKind.Model, ModelId, new Dictionary<string, JsonNode?> { ["batteryMass"] = JsonNode.Parse("""{"gramKilogramValue":600,"gramKilogramUnit":"kg"}""") }, "form"));

    [Fact]
    public async Task APageLeftOpen_ShouldNotUndoANewerValue()
    {
        var rig = await PublicationTesting.RigAsync(mongo);
        var editor = PageTesting.Editor(rig);
        var opened = await StoredAsync(rig);
        var stamp = editor.Stamp(TargetKind.Model, opened, "Model test");
        var form = PageTesting.ShownForm(TargetKind.Model, opened);
        await SomeoneElseSavesAMassAsync(rig);

        var post = editor.Read(TargetKind.Model, await StoredAsync(rig), PageTesting.Field(form), stamp);

        Assert.Empty(post.Changed);
        Assert.Empty(post.Conflicts);
        Assert.Equal(EditorOutcome.NothingChanged, (await editor.SaveAsync(PublicationTesting.Team, TargetKind.Model, ModelId, post, confirmed: false)).Outcome);
        Assert.Equal("600", (await StoredAsync(rig))["batteryMass"].Value!["gramKilogramValue"]!.ToJsonString());
    }

    [Fact]
    public async Task AFieldEditedHere_ShouldSaveWhileAnotherFieldChangedElsewhere()
    {
        var rig = await PublicationTesting.RigAsync(mongo);
        var editor = PageTesting.Editor(rig);
        var opened = await StoredAsync(rig);
        var stamp = editor.Stamp(TargetKind.Model, opened, "Model test");
        var form = PageTesting.ShownForm(TargetKind.Model, opened);
        form["v.ratedCapacity"] = "210";
        await SomeoneElseSavesAMassAsync(rig);

        var post = editor.Read(TargetKind.Model, await StoredAsync(rig), PageTesting.Field(form), stamp);

        Assert.Equal(["ratedCapacity"], post.Changed.Keys);
    }

    [Fact]
    public async Task AFieldEditedOnBothSides_ShouldBeRefusedAndKeepWhatWasTyped()
    {
        var rig = await PublicationTesting.RigAsync(mongo);
        var editor = PageTesting.Editor(rig);
        var opened = await StoredAsync(rig);
        var stamp = editor.Stamp(TargetKind.Model, opened, "Model test");
        var form = PageTesting.ShownForm(TargetKind.Model, opened);
        form["v.batteryMass"] = "700";
        await SomeoneElseSavesAMassAsync(rig);
        var now = await StoredAsync(rig);

        var post = editor.Read(TargetKind.Model, now, PageTesting.Field(form), stamp);
        var save = await editor.SaveAsync(PublicationTesting.Team, TargetKind.Model, ModelId, post, confirmed: false);

        Assert.Equal(["batteryMass"], post.Conflicts);
        Assert.Equal(EditorOutcome.Refused, save.Outcome);
        Assert.Contains("after you opened this page", save.Refusals(AttributeFormsTests.Forms)["batteryMass"].Text, StringComparison.Ordinal);
        Assert.Equal("600", (await StoredAsync(rig))["batteryMass"].Value!["gramKilogramValue"]!.ToJsonString());

        // Shown again: the typed 700 stays in its field, the other fields show the stored values, and the new stamp lets
        // the next save go through.
        var sections = await editor.SectionsAsync(TargetKind.Model, ModelId, now, post, save.Refusals(AttributeFormsTests.Forms));
        var mass = sections.SelectMany(section => section.Fields).Single(field => field.Attribute.Property == "batteryMass");
        Assert.Equal("700", mass.Text("v.batteryMass"));
        var again = editor.Read(TargetKind.Model, now, PageTesting.Field(form), editor.Stamp(TargetKind.Model, now, "Model test"));
        Assert.Equal(["batteryMass"], again.Changed.Keys);
    }

    [Fact]
    public async Task AnEmptiedField_ShouldRemoveItsValue()
    {
        var rig = await PublicationTesting.RigAsync(mongo);
        var editor = PageTesting.Editor(rig);
        var opened = await StoredAsync(rig);
        var form = PageTesting.ShownForm(TargetKind.Model, opened);
        form["v.batteryMass"] = string.Empty;

        var post = editor.Read(TargetKind.Model, opened, PageTesting.Field(form), editor.Stamp(TargetKind.Model, opened, "Model test"));
        var save = await editor.SaveAsync(PublicationTesting.Team, TargetKind.Model, ModelId, post, confirmed: false);

        Assert.Equal(["batteryMass"], post.Removals);
        Assert.Equal((EditorOutcome.Saved, 0, 1), (save.Outcome, save.SavedCount, save.RemovedCount));
        Assert.False((await StoredAsync(rig)).ContainsKey("batteryMass"));
    }

    [Fact]
    public async Task EveryRowRemoved_ShouldRemoveTheList()
    {
        var rig = await PublicationTesting.RigAsync(mongo);
        var editor = PageTesting.Editor(rig);
        var opened = await StoredAsync(rig);
        var form = PageTesting.ShownForm(TargetKind.Model, opened);
        foreach (var name in form.Keys.Where(name => name.StartsWith("v.hazardousSubstancesAndTheirImpact[", StringComparison.Ordinal)).ToArray())
        {
            form[name] = string.Empty;
        }

        var post = editor.Read(TargetKind.Model, opened, PageTesting.Field(form), editor.Stamp(TargetKind.Model, opened, "Model test"));

        Assert.Equal(["hazardousSubstancesAndTheirImpact"], post.Removals);
    }

    [Fact]
    public async Task WithoutAStamp_AnEmptyFieldShouldKeepItsValue()
    {
        var rig = await PublicationTesting.RigAsync(mongo);
        var opened = await StoredAsync(rig);

        var post = PageTesting.Editor(rig).Read(TargetKind.Model, opened, PageTesting.Field(new Dictionary<string, string> { ["v.batteryMass"] = "520" }));

        Assert.Equal(["batteryMass"], post.Changed.Keys);
        Assert.Empty(post.Removals);
    }

    [Fact]
    public void TheName_ShouldCountAsEditedOnlyWhenItDiffersFromWhatThePageShowed()
    {
        var stamp = new RecordEditor(AttributeFormsTests.Forms, null!, null!, null!).Stamp(TargetKind.Site, new Dictionary<string, AttributeValue>(), "Plant A");

        Assert.False(RecordEditor.NameEdited(stamp, "Plant A"));
        Assert.False(RecordEditor.NameEdited(stamp, " Plant A "));
        Assert.True(RecordEditor.NameEdited(stamp, "Plant B"));
        Assert.True(RecordEditor.NameEdited(null, "Plant A"));
    }
}
