using BatteryPassWeb.Dpp.Catalog;
using BatteryPassWeb.Dpp.Forms;

namespace BatteryPassWeb.Tests.Dpp;

/// <summary>The save pipeline's reasons, as it really words them, turned into plain sentences for the field.</summary>
public sealed class RefusalWordingTests
{
    [Theory]
    [InlineData(71, "properties: Some properties did not match the required schema: [\"percentageValue\"]; /percentageValue maximum: 120 should be at most 100", "Must be at most 100.")]
    [InlineData(53, "properties: Some properties did not match the required schema: [\"percentageValue\"]; /percentageValue minimum: -1 should be at least 0", "Must be at least 0.")]
    [InlineData(53, "properties: Some properties did not match the required schema: [\"percentageValue\"]; /percentageValue type: Value is \"string\" but should be \"integer\"", "Must be a whole number.")]
    [InlineData(30, "properties: Some properties did not match the required schema: [\"kgCO2EquivalentPerKilowattHourValue\"]; /kgCO2EquivalentPerKilowattHourValue type: Value is \"string\" but should be \"number\"", "Must be a number.")]
    [InlineData(66, "properties: Some properties did not match the required schema: [\"temperatureRangeLowerBoundaryValue\"]; /temperatureRangeLowerBoundaryValue type: Value is \"number\" but should be \"integer\"", "Test temperature from must be a whole number.")]
    [InlineData(41, "properties: Some properties did not match the required schema: [\"chemicalCodeValue\"]; /chemicalCodeValue enum: Value should match one of the values specified by the enum", "Chemistry must be one of the listed choices.")]
    [InlineData(28, "properties: Some properties did not match the required schema: [\"url\"]; /url pattern: The string value is not a match for the indicated regular expression", "Link must be a web address starting with https:// or http://.")]
    [InlineData(25, "required: Required properties [\"contentType\"] are not present", "File type is missing.")]
    [InlineData(15, "required: Required properties [\"registeredTradeNameOrRegisteredTrademark\",\"postalAddress\"] are not present", "Registered trade name or trademark and Postal address are missing.")]
    [InlineData(44, "items: Some items do not match the required schema; failing indexes: [0]; /0 required: Required properties [\"identifierType\",\"identifier\",\"concentration\"] are not present", "Row 1: Identifier type, Identifier and Concentration are missing.")]
    [InlineData(29, "#29 must link to a file uploaded to the app", "#29 must link to a file uploaded to the app.")]
    [InlineData(21, "#21 can't be changed by this sender", "#21 can't be changed by this sender.")]
    public void Reasons_ShouldReadAsPlainSentences(int number, string message, string expected)
    {
        var form = AttributeFormsTests.Forms.For(AttributeCatalog.ByNumber(number));

        Assert.Equal(expected, RefusalWording.Describe(form, message));
    }

    [Theory]
    [InlineData(15, "required: Required properties [\"registeredTradeNameOrRegisteredTrademark\",\"postalAddress\"] are not present", "postalAddress,registeredTradeNameOrRegisteredTrademark")]
    [InlineData(66, "properties: Some properties did not match the required schema: [\"temperatureRangeLowerBoundaryValue\"]; /temperatureRangeLowerBoundaryValue type: Value is \"number\" but should be \"integer\"", "temperatureRangeLowerBoundaryValue")]
    [InlineData(28, "properties: Some properties did not match the required schema: [\"url\"]; /url pattern: The string value is not a match for the indicated regular expression", "url")]
    [InlineData(29, "#29 must link to a file uploaded to the app", "")]
    public void TheRefusedParts_ShouldBeNamed(int number, string message, string expected)
    {
        var form = AttributeFormsTests.Forms.For(AttributeCatalog.ByNumber(number));

        Assert.Equal(expected, string.Join(",", RefusalWording.Parts(form, message).Order(StringComparer.Ordinal)));
    }

    [Fact]
    public void AnUnknownRule_ShouldStillSayWhatWasRefused()
    {
        var form = AttributeFormsTests.Forms.For(AttributeCatalog.ByNumber(11));

        Assert.Equal("Was refused (maxLength: Value is too long).", RefusalWording.Describe(form, "maxLength: Value is too long"));
        Assert.Equal(string.Empty, RefusalWording.Describe(form, " "));
    }
}
