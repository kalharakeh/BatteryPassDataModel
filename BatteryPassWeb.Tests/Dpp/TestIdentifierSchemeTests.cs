using System.Text.Json.Nodes;
using BatteryPassWeb.Dpp.Catalog;
using BatteryPassWeb.Dpp.Identifiers;
using BatteryPassWeb.Dpp.Store;
using BatteryPassWeb.Dpp.Values;

namespace BatteryPassWeb.Tests.Dpp;

public sealed class TestIdentifierSchemeTests
{
    private readonly TestIdentifierScheme _scheme = new();

    private static BatteryPack Pack() => new() { Id = "pack-1", BatchId = "batch-1", OperatorId = "operator-1" };

    [Fact]
    public async Task NewPackIdentifier_ShouldBeAMarkedTestUrnThatPassesTheValueChecks()
    {
        var identifier = (await _scheme.NewPackIdentifierAsync(Pack())).Identifier!;

        Assert.StartsWith("urn:x-battery-pass:TEST-", identifier, StringComparison.Ordinal);
        Assert.Empty(new AttributeValueValidator(DppStoreTesting.Schemas).Validate(AttributeCatalog.ByNumber(6), JsonValue.Create(identifier)));
    }

    [Fact]
    public void NewPassportIdentifier_ShouldBeAMarkedTestUrnThatPassesTheValueChecks()
    {
        var identifier = _scheme.NewPassportIdentifier("urn:x-battery-pass:TEST-0f8c", 1);

        Assert.StartsWith("urn:x-battery-pass:TEST-DPP-", identifier, StringComparison.Ordinal);
        Assert.True(_scheme.IsTestIdentifier(identifier));
        Assert.NotEqual(identifier, _scheme.NewPassportIdentifier("urn:x-battery-pass:TEST-0f8c", 1));
        Assert.Empty(new AttributeValueValidator(DppStoreTesting.Schemas).Validate(AttributeCatalog.ByNumber(5), JsonValue.Create(identifier)));
    }

    [Fact]
    public async Task NewPackIdentifier_ShouldNeverRepeat()
    {
        var identifiers = (await Task.WhenAll(Enumerable.Range(0, 200).Select(_ => _scheme.NewPackIdentifierAsync(Pack())))).Select(made => made.Identifier).ToList();

        Assert.Equal(identifiers.Count, identifiers.Distinct(StringComparer.Ordinal).Count());
    }

    [Theory]
    [InlineData("urn:x-battery-pass:TEST-0f8c", true)]
    [InlineData("urn:test:battery:VPC8C05-6788", false)]
    [InlineData("https://id.example.com/01/123", false)]
    [InlineData("https://www.sib-scania-eubp.eu/25S/UN778474481TESTDEMOC7M0001", true)]
    [InlineData("https://www.sib-scania-eubp.eu/dpp/UN778474481TESTDEMOC7M0001R1/v2", true)]
    [InlineData("https://www.sib-scania-eubp.eu/25S/UN778474481VPC12221", false)]
    [InlineData("https://www.sib-scania-eubp.eu/25S/UN778474481-TEST-DEMO-C7M-0001", true)] // 6d-5
    [InlineData("https://www.sib-scania-eubp.eu/dpp/UN778474481-TEST-DEMO-C7M-0001-R1/v2", true)]
    [InlineData("https://www.sib-scania-eubp.eu/25S/UN778474481-VBC-92921", false)]
    [InlineData("https://www.sib-scania-eubp.eu/25S/UN778474481-AB-TEST-1", false)]
    [InlineData("https://www.sib-scania-eubp.eu/25S/UN778474481-ABUN123456789TEST1", false)] // a TEST mark only right after the company
    public void IsTestIdentifier_ShouldRecogniseOnlyMarkedIdentifiers(string identifier, bool expected)
    {
        Assert.Equal(expected, _scheme.IsTestIdentifier(identifier));
    }
}
