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
    [InlineData(19, "\"P3Y6M\"")]
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
    [InlineData(19, "\"8 years\"", "ISO 8601 period")]
    [InlineData(6, "\"not an identifier\"", "absolute URI")]
    [InlineData(4, "\"2026-10-28 09:30\"", "UTC timestamp")]
    [InlineData(57, """{"amperehourMiliamperehourValue":200000,"ampereHourMiliamperehourUnit":"mAh"}""", "\"Ah\"")]
    [InlineData(21, """{"gramKilogramUnit":"g","gramKilogramValue":512500}""", "\"kg\"")]
    public void Validate_ShouldRejectInvalidValues(int number, string json, string expectedMessagePart)
    {
        var issues = Validator.Validate(AttributeCatalog.ByNumber(number), JsonNode.Parse(json));

        Assert.Contains(issues, issue => issue.Message.Contains(expectedMessagePart, StringComparison.Ordinal));
    }
}
