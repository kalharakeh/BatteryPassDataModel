using System.Text.Json.Nodes;
using BatteryPassWeb.Dpp.Catalog;
using BatteryPassWeb.Dpp.Forms;
using BatteryPassWeb.Dpp.Values;

namespace BatteryPassWeb.Tests.Dpp;

/// <summary>
/// Package R1, round two (review L5): the editor shows and takes the passport's language only. A save puts its text into
/// the stored language map and keeps the other languages; a map without a text in the passport's language shows an
/// empty box, never another language's text to be saved back as en-GB.
/// </summary>
[Collection(MongoCollection.Name)]
public sealed class LanguageMapSaveTests(MongoFixture mongo)
{
    private static AttributeDefinition Test => AttributeCatalog.FindByProperty(AttributeCatalog.NotInUseTemperatureRangeTest)!;

    private static AttributeForm Form => AttributeFormsTests.Forms.For(Test);

    private static string Field => Form.FieldName(Form.Inputs[0]);

    [Fact]
    public void AMapWithoutThePassportsLanguage_ShouldShowAnEmptyBox()
    {
        Assert.Equal(string.Empty, FormValues.Show(Form, JsonNode.Parse("""{"sv-SE":"Förvara torrt."}"""))[Field]);
        Assert.Equal("Store dry.", FormValues.Show(Form, JsonNode.Parse("""{"sv-SE":"Förvara torrt.","en-GB":"Store dry."}"""))[Field]);
    }

    [Fact]
    public async Task ASave_ShouldPutItsTextIntoTheStoredMap_KeepingTheOtherLanguages()
    {
        var rig = await PublicationTesting.RigAsync(mongo);
        var editor = PageTesting.Editor(rig);
        var stored = new Dictionary<string, AttributeValue>(StringComparer.Ordinal)
        {
            [Test.Property] = new() { Value = JsonNode.Parse("""{"sv-SE":"Förvara torrt.","en-GB":"Store dry."}""")!, GeneratedAtUtc = DppTestSeed.GeneratedAtUtc, Source = "form" },
        };

        var post = editor.Read([Test], stored, name => name == Field ? "Store dry and cool." : null);

        Assert.True(JsonNode.DeepEquals(JsonNode.Parse("""{"sv-SE":"Förvara torrt.","en-GB":"Store dry and cool."}"""), post.Changed[Test.Property]), post.Changed[Test.Property].ToJsonString());

        // Saved unchanged, nothing changes, the Swedish text included.
        var same = editor.Read([Test], stored, name => name == Field ? "Store dry." : null);
        Assert.Empty(same.Changed);
    }
}
