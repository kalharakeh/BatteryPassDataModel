using System.Text.Json.Nodes;
using BatteryPassWeb.Dpp.Assembly;
using BatteryPassWeb.Dpp.Catalog;
using BatteryPassWeb.Dpp.Forms;
using BatteryPassWeb.Dpp.Pages;
using BatteryPassWeb.Dpp.Values;

namespace BatteryPassWeb.Tests.Dpp;

/// <summary>
/// Package R1, G22 (decision 94, finding D06): #78's cell and pack resistance are a few milliohms, and the EU's format
/// takes decimals in ohms. The app's copy of the schema allows a decimal there (a local deviation, NOTICE.md); a whole
/// number stays valid, and the module value stays optional.
/// </summary>
public sealed class DecimalOhmsTests
{
    private static AttributeDefinition Resistance => AttributeCatalog.ByNumber(78);

    private static readonly AttributeValueValidator Validator = new(DppStoreTesting.Schemas);

    [Fact]
    public void TheResistances_ShouldBeEnteredAsDecimals_TheModuleOptional()
    {
        var form = AttributeFormsTests.Forms.For(Resistance);

        Assert.All(form.Inputs, input => Assert.Equal(InputKind.Number, input.Kind));
        Assert.Equal(
            [("ohmValueForBatteryPack", true), ("ohmValueForBatteryModule", false), ("ohmValueForBatteryCell", true)],
            form.Inputs.Select(input => (input.Path, input.Required)));
    }

    [Theory]
    [InlineData("""{"ohmValueForBatteryPack":0.045,"ohmValueForBatteryCell":0.0008,"ohmUnit":"Ohm"}""", true)]
    [InlineData("""{"ohmValueForBatteryPack":1,"ohmValueForBatteryCell":1,"ohmUnit":"Ohm"}""", true)]
    [InlineData("""{"ohmValueForBatteryPack":0.045,"ohmValueForBatteryCell":0.0008,"ohmValueForBatteryModule":0.004,"ohmUnit":"Ohm"}""", true)]
    [InlineData("""{"ohmValueForBatteryPack":-0.045,"ohmValueForBatteryCell":0.0008,"ohmUnit":"Ohm"}""", false)]
    [InlineData("""{"ohmValueForBatteryPack":"0.045","ohmValueForBatteryCell":0.0008,"ohmUnit":"Ohm"}""", false)]
    [InlineData("""{"ohmValueForBatteryPack":0.045,"ohmUnit":"Ohm"}""", false)]
    public void AResistance_ShouldBeADecimalInOhms_NotBelowZero(string json, bool valid)
    {
        Assert.Equal(valid, Validator.Validate(Resistance, JsonNode.Parse(json)).Count == 0);
    }

    [Fact]
    public void ADecimalTyped_ShouldBeKept_AndShownAsTyped()
    {
        var form = AttributeFormsTests.Forms.For(Resistance);
        var typed = new Dictionary<string, string>
        {
            [form.FieldName(form.Inputs.Single(input => input.Path == "ohmValueForBatteryPack"))] = "0,045",
            [form.FieldName(form.Inputs.Single(input => input.Path == "ohmValueForBatteryCell"))] = "0.0008",
        };

        var value = FormValues.Read(form, name => typed.GetValueOrDefault(name))!;

        Assert.Empty(Validator.Validate(Resistance, value));
        Assert.Equal(0.045m, value["ohmValueForBatteryPack"]!.GetValue<decimal>());
        var lines = ValueWords.Lines(form, value);
        Assert.Contains(lines, line => line.Label == "Pack" && line.Text == "0.045 Ohm");
        Assert.Contains(lines, line => line.Label == "Cell" && line.Text == "0.0008 Ohm");
    }

    [Theory]
    [InlineData("0.00012345", "0.00012345 Ohm")]
    [InlineData("0.0000005", "0.0000005 Ohm")]
    [InlineData("0.0450", "0.045 Ohm")]
    [InlineData("1", "1 Ohm")]
    public void AResistance_ShouldShowAsStored_WithoutRounding(string pack, string shown)
    {
        var value = JsonNode.Parse($$"""{"ohmValueForBatteryPack":{{pack}},"ohmValueForBatteryCell":0.0008,"ohmUnit":"Ohm"}""");

        var lines = ValueWords.Lines(AttributeFormsTests.Forms.For(Resistance), value);

        Assert.Contains(lines, line => line.Label == "Pack" && line.Text == shown); // round two, L8
    }

    [Fact]
    public void APassportWithDecimalOhms_ShouldMeetTheSchema()
    {
        var seed = DppTestSeed.Load();
        var passport = PassportAssembler.Assemble(seed.Sources, seed.Header);
        passport["batteryPassportData"]!["performanceAndDurability"]!["initialInternalResistanceOfBatteryCellAndPackModuleRecommended"] =
            JsonNode.Parse("""{"ohmValueForBatteryPack":0.045,"ohmValueForBatteryCell":0.0008,"ohmUnit":"Ohm"}""");

        Assert.Empty(DppStoreTesting.Schemas.CheckPassport(passport, feb2027Variant: false));
        Assert.Empty(DppStoreTesting.Schemas.CheckPassport(passport, feb2027Variant: true));
    }
}
