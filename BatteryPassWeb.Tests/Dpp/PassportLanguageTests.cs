using System.Text.Json.Nodes;
using BatteryPassWeb.Dpp.Catalog;
using BatteryPassWeb.Dpp.Forms;
using BatteryPassWeb.Dpp.Pages;

namespace BatteryPassWeb.Tests.Dpp;

/// <summary>
/// Package R1, G19 (decision 94, the owner's answer O3): the EU's format gives every text a language. The passport's
/// texts are in British English for now, named in one place; the texts added for the EU are kept as language maps
/// ({"en-GB": "…"}) so that more languages can come; the pages show and take one.
/// </summary>
public sealed class PassportLanguageTests
{
    private static readonly AttributeForm Form = new(
        AttributeCatalog.ByNumber(27),
        FormKind.Single,
        [new FormInput(string.Empty, "A text", InputKind.LanguageText, true, [])],
        new Dictionary<string, string>());

    [Fact]
    public void ThePassportsLanguage_ShouldBeBritishEnglish_InOnePlace()
    {
        Assert.Equal("en-GB", PassportLanguage.Tag);
        Assert.Equal("""{"en-GB":"Store dry."}""", PassportLanguage.Map("Store dry.").ToJsonString());
    }

    [Theory]
    [InlineData("""{"en-GB":"Store dry."}""", "Store dry.")]
    [InlineData("""{"sv-SE":"Förvara torrt.","en-GB":"Store dry."}""", "Store dry.")]
    [InlineData("""{"sv-SE":"Förvara torrt."}""", "Förvara torrt.")]
    [InlineData("\"Store dry.\"", "Store dry.")]
    [InlineData("12", null)]
    public void AText_ShouldBeReadInThePassportsLanguage_OrItsFirst(string json, string? text)
    {
        Assert.Equal(text, PassportLanguage.TextOf(JsonNode.Parse(json)));
    }

    [Theory]
    [InlineData("""{"en-GB":"Store dry."}""", true)]
    [InlineData("""{"en-GB":"Store dry.","sv-SE":"Förvara torrt."}""", true)]
    [InlineData("""{}""", false)]
    [InlineData("""{"english":"Store dry."}""", false)]
    [InlineData("""{"en-GB":"  "}""", false)]
    [InlineData("""{"en-GB":12}""", false)]
    [InlineData("\"Store dry.\"", false)]
    public void ALanguageMap_ShouldHoldTextsUnderTagsSuchAsEnGb(string json, bool valid)
    {
        Assert.Equal(valid, PassportLanguage.Problem(JsonNode.Parse(json)) is null);
    }

    [Fact]
    public void ATextTypedInOneLanguage_ShouldBeKeptAsALanguageMap_AndShownAsItsText()
    {
        var value = FormValues.Read(Form, _ => " Store dry.\r\nKeep away from heat. ");

        Assert.Equal("{\"en-GB\":\"Store dry.\\nKeep away from heat.\"}", value!.ToJsonString());
        Assert.Equal("Store dry.\nKeep away from heat.", Assert.Single(FormValues.Show(Form, value)).Value);
        Assert.Equal("Store dry.\nKeep away from heat.", Assert.Single(ValueWords.Lines(Form, value)).Text);
        Assert.Null(FormValues.Read(Form, _ => "   "));
    }
}
