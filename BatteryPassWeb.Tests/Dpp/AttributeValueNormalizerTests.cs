using System.Text.Json.Nodes;
using BatteryPassWeb.Dpp.Catalog;
using BatteryPassWeb.Dpp.Values;

namespace BatteryPassWeb.Tests.Dpp;

public sealed class AttributeValueNormalizerTests
{
    private static readonly AttributeValueNormalizer Normalizer = new(DppStoreTesting.Schemas);
    private static readonly AttributeValueValidator Validator = new(DppStoreTesting.Schemas);

    // Design 8.2: one value with a fixed unit — percentages, °C, Ah, kg, kgCO2-eq/kWh, A/Ah, the three voltages, #70,
    // #79 (the pack value) and the two status-like codes.
    private static readonly int[] SingleValueAttributes =
        [20, 21, 22, 30, 31, 32, 33, 34, 49, 50, 51, 52, 53, 57, 59, 63, 64, 65, 66, 69, 70, 71, 72, 74, 79, 85, 87, 88, 89];

    [Fact]
    public void BareValues_ShouldBeWrappedForExactlyTheSingleValueAttributes()
    {
        var wrapped = AttributeCatalog.All
            .Where(a => a.Storage != StorageLocation.System)
            .Where(a => Normalizer.Normalize(a, JsonValue.Create(1)) is JsonObject)
            .Select(a => a.Number);

        Assert.Equal(SingleValueAttributes, wrapped.Order());
    }

    [Theory]
    [InlineData(63, "76")]
    [InlineData(53, "3")]
    [InlineData(59, "4")]
    [InlineData(87, "23")]
    [InlineData(88, "-30")]
    [InlineData(57, "200")]
    [InlineData(21, "512.5")]
    [InlineData(30, "75.4")]
    [InlineData(85, "0.5")]
    [InlineData(64, "520.0")]
    [InlineData(70, "300000")]
    [InlineData(79, "5")]
    [InlineData(22, "\"re-used\"")]
    [InlineData(20, "\"industrial/non-stationary battery\"")]
    public void BareValue_ShouldBecomeAValidSchemaValue(int number, string bare)
    {
        var attribute = AttributeCatalog.ByNumber(number);

        var normalized = Normalizer.Normalize(attribute, JsonNode.Parse(bare));

        Assert.IsType<JsonObject>(normalized);
        Assert.Empty(Validator.Validate(attribute, normalized));
    }

    [Fact]
    public void BareCapacityAndMass_ShouldGetTheCataloguesUnit()
    {
        Assert.Equal("Ah", Normalizer.Normalize(AttributeCatalog.ByNumber(57), JsonValue.Create(200))!["ampereHourMiliamperehourUnit"]!.GetValue<string>());
        Assert.Equal("kg", Normalizer.Normalize(AttributeCatalog.ByNumber(21), JsonValue.Create(512.5))!["gramKilogramUnit"]!.GetValue<string>());
    }

    [Fact]
    public void ValuesInTheSchemaForm_ShouldPassThroughUnchanged()
    {
        var sources = DppTestSeed.Load().Sources;
        var stored = new[] { sources.Pack.Values, sources.Operator.Values, sources.Batch.Values, sources.Site.Values, sources.Model.Values }
            .SelectMany(values => values);

        foreach (var (property, value) in stored)
        {
            var attribute = AttributeCatalog.FindByProperty(property)!;
            Assert.True(JsonNode.DeepEquals(value.Value, Normalizer.Normalize(attribute, value.Value)), property);
        }
    }

    [Theory]
    [InlineData(83, "412")]
    [InlineData(81, "12.5")]
    [InlineData(11, "\"VPC8C05-6788\"")]
    [InlineData(17, "\"2026-09\"")]
    public void PlainValuesOfOtherAttributes_ShouldPassThroughUnchanged(int number, string json)
    {
        var value = JsonNode.Parse(json);

        Assert.True(JsonNode.DeepEquals(value, Normalizer.Normalize(AttributeCatalog.ByNumber(number), value)));
    }

    [Fact]
    public void Null_ShouldStayNull()
    {
        Assert.Null(Normalizer.Normalize(AttributeCatalog.ByNumber(63), null));
    }
}
