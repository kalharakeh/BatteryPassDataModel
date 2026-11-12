using BatteryPassWeb.Dpp.Catalog;
using BatteryPassWeb.Dpp.Forms;

namespace BatteryPassWeb.Tests.Dpp;

public sealed class AttributeFormsTests
{
    internal static readonly AttributeForms Forms = new(DppStoreTesting.Schemas);

    [Fact]
    public void EveryAttribute_ShouldHaveAForm()
    {
        Assert.All(AttributeCatalog.All, attribute => Assert.Same(attribute, Forms.For(attribute).Attribute));
    }

    [Theory]
    [InlineData(11, FormKind.Single, InputKind.Text, null)]
    [InlineData(17, FormKind.Single, InputKind.Month, null)]
    [InlineData(19, FormKind.Single, InputKind.Text, null)]
    [InlineData(21, FormKind.Single, InputKind.Number, "kg")]
    [InlineData(57, FormKind.Single, InputKind.Integer, "Ah")]
    [InlineData(59, FormKind.Single, InputKind.Integer, "%")]
    [InlineData(81, FormKind.Single, InputKind.Number, "years")]
    [InlineData(82, FormKind.Single, InputKind.Integer, null)]
    [InlineData(87, FormKind.Single, InputKind.Integer, "°C")]
    [InlineData(30, FormKind.Single, InputKind.Number, "kgCO2eq/kWh")]
    [InlineData(42, FormKind.Lines, InputKind.Lines, null)]
    [InlineData(22, FormKind.Single, InputKind.Select, null)]
    public void OneInputAttributes_ShouldBeLabelledByTheirName(int number, FormKind kind, InputKind input, string? unit)
    {
        var form = Forms.For(AttributeCatalog.ByNumber(number));

        Assert.Equal(kind, form.Kind);
        var only = Assert.Single(form.Inputs);
        Assert.Equal(input, only.Kind);
        Assert.Equal(unit, only.Unit);
        Assert.Equal(form.Attribute.Name, only.Label);
        Assert.True(only.Required);
    }

    [Fact]
    public void UnitsWithOneAllowedValue_ShouldBeFilledInByTheApp()
    {
        Assert.Equal("kg", Forms.For(AttributeCatalog.ByNumber(21)).Constants["gramKilogramUnit"]);
        Assert.Equal("Ah", Forms.For(AttributeCatalog.ByNumber(57)).Constants["ampereHourMiliamperehourUnit"]);
        Assert.Equal("%", Forms.For(AttributeCatalog.ByNumber(59)).Constants["percentUnit"]);
    }

    [Fact]
    public void OperatorInformation_ShouldBeAGroupWithTheSchemasRequiredParts()
    {
        var form = Forms.For(AttributeCatalog.ByNumber(14));

        Assert.Equal(FormKind.Group, form.Kind);
        Assert.Equal(
            ["name:True", "registeredTradeNameOrRegisteredTrademark:True", "postalAddress:True", "webAddress:False", "eMailAddress:False"],
            form.Inputs.Select(input => $"{input.Path}:{input.Required}"));
        Assert.Equal("Registered trade name or trademark", form.Inputs[1].Label);
    }

    [Fact]
    public void Voltages_ShouldKeepTheirOptionalTestTemperatures()
    {
        var form = Forms.For(AttributeCatalog.ByNumber(66));

        Assert.Equal(FormKind.Group, form.Kind);
        Assert.Equal(
            ["temperatureRangeLowerBoundaryValue", "temperatureRangeUpperBoundaryValue", "voltValue"],
            form.Inputs.Select(input => input.Path).Order(StringComparer.Ordinal));
        Assert.Equal("V", form.Inputs.Single(input => input.Path == "voltValue").Unit);
        Assert.All(form.Inputs.Where(input => input.Path.StartsWith("temperature", StringComparison.Ordinal)), input => Assert.Equal(("°C", false), (input.Unit, input.Required)));
    }

    [Fact]
    public void BatteryCategory_ShouldBeFilledInByTheApp()
    {
        var form = Forms.For(AttributeCatalog.ByNumber(20));

        Assert.Equal(FormKind.Fixed, form.Kind);
        Assert.Empty(form.Inputs);
        Assert.Equal("industrial/non-stationary battery", form.Constants["batteryCategoryValue"]);
    }

    [Fact]
    public void Chemistry_ShouldOfferTheSchemasCodes()
    {
        var chemistry = Forms.For(AttributeCatalog.ByNumber(41)).Inputs.Single(input => input.Path == "chemicalCodeValue");

        Assert.Equal(InputKind.Select, chemistry.Kind);
        Assert.Contains("Li-ion NMC", chemistry.Options);
        Assert.Equal(10, chemistry.Options.Count);
    }

    [Fact]
    public void HazardousSubstances_ShouldBeRows()
    {
        var form = Forms.For(AttributeCatalog.ByNumber(44));

        Assert.Equal(FormKind.Rows, form.Kind);
        Assert.Equal(InputKind.Lines, form.Inputs.Single(input => input.Path == "hazardPhrases").Kind);
        Assert.Equal(["CAS No", "EC No"], form.Inputs.Single(input => input.Path == "identifierType").Options);
        Assert.False(form.Inputs.Single(input => input.Path == "substanceLocation").Required);
    }

    [Theory]
    [InlineData(23)]
    [InlineData(28)]
    [InlineData(38)]
    [InlineData(54)]
    public void PublicDocuments_ShouldBeLinks(int number)
    {
        var form = Forms.For(AttributeCatalog.ByNumber(number));

        Assert.Equal(FormKind.Link, form.Kind);
        Assert.Equal(["contentType", "language", "resourceTitle", "url"], form.Inputs.Select(input => input.Path).Order(StringComparer.Ordinal));
        Assert.Equal(AttributeForms.LinkContentTypes, form.Inputs.Single(input => input.Path == "contentType").Options);
        Assert.Equal(InputKind.Url, form.Inputs.Single(input => input.Path == "url").Kind);
    }

    [Theory]
    [InlineData(29)]
    [InlineData(45)]
    [InlineData(48)]
    [InlineData(92)]
    public void RestrictedDocuments_ShouldBeUploads(int number)
    {
        Assert.Equal(FormKind.Upload, Forms.For(AttributeCatalog.ByNumber(number)).Kind);
    }

    [Fact]
    public void GroupedInputs_ShouldReadInAHelpfulOrderWithLabels()
    {
        Assert.Equal(
            ["Voltage", "Test temperature from", "Test temperature to"],
            Forms.For(AttributeCatalog.ByNumber(66)).Inputs.Select(input => input.Label));
        Assert.Equal(["Chemistry", "Other chemistry"], Forms.For(AttributeCatalog.ByNumber(41)).Inputs.Select(input => input.Label));
        Assert.Equal(["Pack", "Module", "Cell"], Forms.For(AttributeCatalog.ByNumber(78)).Inputs.Select(input => input.Label));
        Assert.Equal(["Link", "Title", "File type", "Language"], Forms.For(AttributeCatalog.ByNumber(28)).Inputs.Select(input => input.Label));
    }

    [Fact]
    public void MeaningOfLabels_ShouldBeATextArea()
    {
        Assert.Equal(InputKind.LongText, Assert.Single(Forms.For(AttributeCatalog.ByNumber(27)).Inputs).Kind);
    }

    [Fact]
    public void FieldNames_ShouldSayWhichAttributeAndPart()
    {
        var mass = Forms.For(AttributeCatalog.ByNumber(21));
        var agent = Forms.For(AttributeCatalog.ByNumber(26));
        var substances = Forms.For(AttributeCatalog.ByNumber(44));

        Assert.Equal("v.batteryMass", mass.FieldName(mass.Inputs[0]));
        Assert.Equal("v.extinguishingAgent.agentFireClass", agent.FieldName(agent.Inputs.Single(input => input.Path == "agentFireClass")));
        Assert.Equal("v.hazardousSubstancesAndTheirImpact[2].identifier", substances.FieldName(substances.Inputs.Single(input => input.Path == "identifier"), 2));
        Assert.Equal("v.hazardousSubstancesAndTheirImpact.rows", substances.RowCountField);
    }
}
