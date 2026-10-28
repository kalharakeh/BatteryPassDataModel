using System.Text.Json.Nodes;
using BatteryPassWeb.Dpp.Catalog;
using BatteryPassWeb.Dpp.Identifiers;
using BatteryPassWeb.Dpp.Values;

namespace BatteryPassWeb.Tests.Dpp;

public sealed class TestIdentifierSchemeTests
{
    private readonly TestIdentifierScheme _scheme = new();

    [Fact]
    public void NewPackIdentifier_ShouldBeAMarkedTestUrnThatPassesTheValueChecks()
    {
        var identifier = _scheme.NewPackIdentifier();

        Assert.StartsWith("urn:x-battery-pass:TEST-", identifier, StringComparison.Ordinal);
        Assert.Empty(new AttributeValueValidator(DppStoreTesting.Schemas).Validate(AttributeCatalog.ByNumber(6), JsonValue.Create(identifier)));
    }

    [Fact]
    public void NewPassportIdentifier_ShouldBeAMarkedTestUrnThatPassesTheValueChecks()
    {
        var identifier = _scheme.NewPassportIdentifier();

        Assert.StartsWith("urn:x-battery-pass:TEST-DPP-", identifier, StringComparison.Ordinal);
        Assert.True(_scheme.IsTestIdentifier(identifier));
        Assert.NotEqual(identifier, _scheme.NewPassportIdentifier());
        Assert.Empty(new AttributeValueValidator(DppStoreTesting.Schemas).Validate(AttributeCatalog.ByNumber(5), JsonValue.Create(identifier)));
    }

    [Fact]
    public void NewPackIdentifier_ShouldNeverRepeat()
    {
        var identifiers = Enumerable.Range(0, 200).Select(_ => _scheme.NewPackIdentifier()).ToList();

        Assert.Equal(identifiers.Count, identifiers.Distinct(StringComparer.Ordinal).Count());
    }

    [Theory]
    [InlineData("urn:x-battery-pass:TEST-0f8c", true)]
    [InlineData("urn:test:battery:VPC8C05-6788", false)]
    [InlineData("https://id.example.com/01/123", false)]
    public void IsTestIdentifier_ShouldRecogniseOnlyMarkedIdentifiers(string identifier, bool expected)
    {
        Assert.Equal(expected, _scheme.IsTestIdentifier(identifier));
    }
}
