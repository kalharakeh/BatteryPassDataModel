using System.Text.Json.Nodes;
using BatteryPassWeb.Dpp.Catalog;
using BatteryPassWeb.Dpp.Schemas;
using BatteryPassWeb.Dpp.Values;

namespace BatteryPassWeb.Tests.Dpp;

public sealed class AttributeValueValidatorTests
{
    private static readonly AttributeValueValidator Validator = new(OfficialSchemas.Load());

    [Theory]
    [InlineData(59, """{"percentUnit":"%","percentageValue":12}""")]
    [InlineData(17, "\"2026-10\"")]
    [InlineData(19, "\"P8Y\"")]
    [InlineData(19, "\"P100Y\"")] // round two, D: 1 to 100 years
    [InlineData(19, "\"P6M\"")]
    [InlineData(19, "\"P1200M\"")] // or 1 to 1200 months
    [InlineData(6, "\"urn:test:battery:VPC8C05-6788\"")]
    [InlineData(4, "\"2026-10-28T09:30:00Z\"")]
    [InlineData(36, "\"https://www.example.com/carbon-footprint-study\"")]
    [InlineData(57, """{"amperehourMiliamperehourValue":200,"ampereHourMiliamperehourUnit":"Ah"}""")]
    [InlineData(21, """{"gramKilogramUnit":"kg","gramKilogramValue":512.5}""")]
    [InlineData(22, """{"batteryStatusValues":"re-used"}""")]
    public void Validate_ShouldAcceptValidValues(int number, string json)
    {
        Assert.Empty(Validator.Validate(AttributeCatalog.ByNumber(number), JsonNode.Parse(json)));
    }

    [Theory]
    [InlineData(59, """{"percentUnit":"%","percentageValue":120}""", "maximum")] // schema range
    [InlineData(59, "12", "type")] // bare values are turned into the schema form before this check (step 2)
    [InlineData(22, """{"batteryStatusValues":"repaired"}""", "enum")] // decision 19: no "repaired" status
    [InlineData(28, """{"contentType":"application/pdf","url":"ftp://example.com/doc.pdf"}""", "pattern")]
    [InlineData(17, "\"2026-10-14\"", "decision 18")] // year and month only
    [InlineData(17, "\"2026-13\"", "decision 18")]
    [InlineData(19, "\"8 years\"", "whole number of years")]
    [InlineData(19, "\"P0Y\"", "whole number of years")] // round two, D: 0 is no warranty
    [InlineData(19, "\"P101Y\"", "1 to 100")]
    [InlineData(19, "\"P1201M\"", "1 to 1200")]
    [InlineData(19, "\"P99999999999Y\"", "whole number of years")]
    [InlineData(19, "\"P8X\"", "whole number of years")] // a unit other than years or months
    [InlineData(19, "\"P3Y6M\"", "whole number of years")] // years or months, not both
    [InlineData(6, "\"not an identifier\"", "absolute URI")]
    [InlineData(4, "\"2026-10-28 09:30\"", "UTC timestamp")]
    [InlineData(57, """{"amperehourMiliamperehourValue":200000,"ampereHourMiliamperehourUnit":"mAh"}""", "\"Ah\"")]
    [InlineData(21, """{"gramKilogramUnit":"g","gramKilogramValue":512500}""", "\"kg\"")]
    public void Validate_ShouldRejectInvalidValues(int number, string json, string expectedMessagePart)
    {
        var issues = Validator.Validate(AttributeCatalog.ByNumber(number), JsonNode.Parse(json));

        Assert.Contains(issues, issue => issue.Message.Contains(expectedMessagePart, StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(21, """{"gramKilogramValue":-1,"gramKilogramUnit":"kg"}""", false)]
    [InlineData(21, """{"gramKilogramValue":0,"gramKilogramUnit":"kg"}""", true)]
    [InlineData(88, """{"celsiusValue":-20,"degreeCelsiusUnit":"°C"}""", true)] // a temperature may be below zero
    public void PhysicalQuantities_ShouldNotBeNegative(int number, string json, bool valid)
    {
        var issues = Validator.Validate(AttributeCatalog.ByNumber(number), JsonNode.Parse(json));

        Assert.Equal(valid, issues.Count == 0);
    }
}
