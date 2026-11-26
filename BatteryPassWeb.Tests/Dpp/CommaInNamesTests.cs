using System.Text.Json.Nodes;
using BatteryPassWeb.Dpp.Catalog;
using BatteryPassWeb.Dpp.Forms;
using BatteryPassWeb.Dpp.Pages;
using BatteryPassWeb.Dpp.Values;

namespace BatteryPassWeb.Tests.Dpp;

/// <summary>
/// Package R1, round two (review L3): #42 is never split on commas. The workbook sets names apart with ";" and the page
/// posts each ticked box as a value of its own, so a name such as "Rare earth elements (Nd, Dy)" stays one name.
/// </summary>
[Collection(MongoCollection.Name)]
public sealed class CommaInNamesTests(MongoFixture mongo)
{
    private const string Raw = EuCodeLists.RawMaterialBase;

    private static AttributeDefinition Materials => AttributeCatalog.ByNumber(42);

    private static AttributeForm Form => AttributeFormsTests.Forms.For(Materials);

    [Fact]
    public void NoLabelOfTheEusLists_ShouldHoldACommaOrASemicolon()
    {
        Assert.All(EuCodeLists.RawMaterials.Values.Concat(EuCodeLists.ChemicalSymbols.Values), label => Assert.True(label.IndexOfAny([',', ';']) < 0, label));
    }

    [Fact]
    public void ANameWithACommaTyped_ShouldStayOneName()
    {
        var value = FormValues.Read(Form, _ => "Rare earth elements (Nd, Dy)\nLithium");

        Assert.True(JsonNode.DeepEquals(new JsonArray(Raw + "lithium", "Rare earth elements (Nd, Dy)"), value), value!.ToJsonString());
    }

    [Fact]
    public async Task AStoredNameWithACommaNotOnTheList_ShouldSurviveAnUnchangedSaveOfTheForm()
    {
        var rig = await PublicationTesting.RigAsync(mongo);
        var editor = PageTesting.Editor(rig);
        var stored = new Dictionary<string, AttributeValue>(StringComparer.Ordinal)
        {
            [Materials.Property] = new() { Value = new JsonArray("Rare earth elements (Nd, Dy)", "Lithium"), GeneratedAtUtc = DppTestSeed.GeneratedAtUtc, Source = "form" },
        };

        // The page shows one box per entry, all ticked; the browser posts each ticked box's value, as the controller passes them.
        var shown = FormValues.Show(Form, stored[Materials.Property].Value)[Form.FieldName(Form.Inputs[0])];
        var ticked = shown.Split('\n');
        Assert.Contains("Rare earth elements (Nd, Dy)", ticked);
        var post = editor.Read([Materials], stored, name => name == Form.FieldName(Form.Inputs[0]) ? RecordEditor.Posted(ticked) : null);

        Assert.Empty(post.Changed);
    }
}
