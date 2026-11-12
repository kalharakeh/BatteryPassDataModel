using System.Text.Json.Nodes;
using BatteryPassWeb.Dpp.Catalog;
using BatteryPassWeb.Dpp.Forms;

namespace BatteryPassWeb.Tests.Dpp;

public sealed class FormValuesTests
{
    private static readonly AttributeForms Forms = AttributeFormsTests.Forms;

    public static TheoryData<string> SeedValues()
    {
        var sources = DppTestSeed.Load().Sources;
        var data = new TheoryData<string>();
        foreach (var key in new[] { sources.Model.Values, sources.Batch.Values, sources.Site.Values, sources.Operator.Values, sources.Pack.Values }.SelectMany(values => values.Keys))
        {
            data.Add(key);
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(SeedValues))]
    public void EverySeedValue_ShouldComeBackUnchangedThroughItsForm(string property)
    {
        var sources = DppTestSeed.Load().Sources;
        var stored = new[] { sources.Model.Values, sources.Batch.Values, sources.Site.Values, sources.Operator.Values, sources.Pack.Values }
            .Select(values => values.GetValueOrDefault(property)).First(value => value is not null)!.Value;
        var form = Forms.For(AttributeCatalog.FindByProperty(property)!);

        var texts = new Dictionary<string, string>(FormValues.Show(form, stored), StringComparer.Ordinal)
        {
            [form.RowCountField] = FormValues.RowCount(stored).ToString(System.Globalization.CultureInfo.InvariantCulture),
        };
        var read = FormValues.Read(form, name => texts.GetValueOrDefault(name));

        if (form.Kind == FormKind.Upload)
        {
            Assert.Null(read); // uploaded documents are never part of the save form
        }
        else
        {
            Assert.True(FormValues.SameValue(stored, read), $"{property}: {stored?.ToJsonString()} came back as {read?.ToJsonString()}");
        }
    }

    [Theory]
    [InlineData("512.5", "512.5")]
    [InlineData("512,5", "512.5")]
    [InlineData(" 600 ", "600")]
    [InlineData("about 500", "\"about 500\"")] // stays text, so the save pipeline refuses it with the schema's reason
    public void Numbers_ShouldBeReadInvariantly(string typed, string expectedValue)
    {
        var form = Forms.For(AttributeCatalog.ByNumber(21));

        var value = FormValues.Read(form, name => name == "v.batteryMass" ? typed : null);

        Assert.Equal($"{{\"gramKilogramValue\":{expectedValue},\"gramKilogramUnit\":\"kg\"}}", value!.ToJsonString());
    }

    [Fact]
    public void BlankForms_ShouldReadAsNothing()
    {
        Assert.Null(FormValues.Read(Forms.For(AttributeCatalog.ByNumber(21)), _ => "  "));
        Assert.Null(FormValues.Read(Forms.For(AttributeCatalog.ByNumber(14)), _ => null));
        Assert.Null(FormValues.Read(Forms.For(AttributeCatalog.ByNumber(42)), _ => "\n \n"));
    }

    [Fact]
    public void TemperatureUnits_ShouldOnlyBeAddedWithATemperature()
    {
        var form = Forms.For(AttributeCatalog.ByNumber(66));

        var plain = FormValues.Read(form, name => name == "v.nominalVoltage.voltValue" ? "650" : null)!;
        var withRange = FormValues.Read(form, name => name switch
        {
            "v.nominalVoltage.voltValue" => "650",
            "v.nominalVoltage.temperatureRangeLowerBoundaryValue" => "-20",
            _ => null,
        })!;

        Assert.True(JsonNode.DeepEquals(JsonNode.Parse("""{"voltValue":650,"voltUnit":"V"}"""), plain));
        Assert.Equal(-20, withRange["temperatureRangeLowerBoundaryValue"]!.GetValue<long>());
        Assert.Equal("°C", withRange["temperatureRangeLowerBoundaryUnit"]!.GetValue<string>());
        Assert.Equal("°C", withRange["temperatureRangeUpperBoundaryUnit"]!.GetValue<string>());
    }

    [Fact]
    public void Lines_ShouldBeTrimmedAndBlankLinesDropped()
    {
        var value = FormValues.Read(Forms.For(AttributeCatalog.ByNumber(42)), _ => " Cobalt \r\n\r\nLithium\n");

        Assert.Equal("""["Cobalt","Lithium"]""", value!.ToJsonString());
    }

    [Fact]
    public void Rows_ShouldDropBlankRows()
    {
        var form = Forms.For(AttributeCatalog.ByNumber(44));
        var posted = new Dictionary<string, string>
        {
            [form.RowCountField] = "3",
            ["v.hazardousSubstancesAndTheirImpact[0].chemicalName"] = "Lithium hexafluorophosphate",
            ["v.hazardousSubstancesAndTheirImpact[0].identifierType"] = "CAS No",
            ["v.hazardousSubstancesAndTheirImpact[0].identifier"] = "21324-40-3",
            ["v.hazardousSubstancesAndTheirImpact[0].concentration"] = "1.5",
            ["v.hazardousSubstancesAndTheirImpact[0].hazardPhrases"] = "H301\nH314",
            ["v.hazardousSubstancesAndTheirImpact[0].precautionaryPhrases"] = "P280",
            ["v.hazardousSubstancesAndTheirImpact[1].chemicalName"] = " ",
            ["v.hazardousSubstancesAndTheirImpact[2].identifier"] = "7439-93-2",
        };

        var value = FormValues.Read(form, name => posted.GetValueOrDefault(name))!.AsArray();

        Assert.Equal(2, value.Count);
        Assert.Equal("""["H301","H314"]""", value[0]!["hazardPhrases"]!.ToJsonString());
        Assert.Equal("7439-93-2", value[1]!["identifier"]!.GetValue<string>());
    }

    [Fact]
    public void BatteryCategory_ShouldAlwaysReadAsItsOnlyValue()
    {
        var value = FormValues.Read(Forms.For(AttributeCatalog.ByNumber(20)), _ => null);

        Assert.Equal("""{"batteryCategoryValue":"industrial/non-stationary battery"}""", value!.ToJsonString());
    }

    [Theory]
    [InlineData("""{"a":1.50,"b":"x"}""", """{"b":"x","a":1.5}""", true)]
    [InlineData("""{"a":2}""", """{"a":2.0}""", true)]
    [InlineData("""{"a":2}""", """{"a":3}""", false)]
    [InlineData("null", """{"a":3}""", false)]
    public void SameValue_ShouldIgnoreNumberSpellingAndKeyOrder(string first, string second, bool same)
    {
        Assert.Equal(same, FormValues.SameValue(JsonNode.Parse(first), JsonNode.Parse(second)));
    }
}
