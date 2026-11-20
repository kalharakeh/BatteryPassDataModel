using System.Security.Cryptography;
using System.Text.Json.Nodes;
using BatteryPassWeb.Dpp.Schemas;

namespace BatteryPassWeb.Tests.Dpp;

public sealed class OfficialSchemasTests
{
    private static readonly OfficialSchemas Schemas = OfficialSchemas.Load();

    [Theory]
    [InlineData(OfficialSchemas.FullResourceName, "31c0c4a5bdbbf2dc0d4908cfb94db1a0da1a65d7b41d545a04029a94c97000ab")]
    [InlineData(OfficialSchemas.Feb2027ResourceName, "aa2ef3f30bd967023946032e338c02ca82f03bc8d26b0a110f0edeafbe4604f5")]
    public void EmbeddedSchema_ShouldBeTheOfficialFileUnchanged(string resourceName, string expectedSha256)
    {
        using var stream = typeof(OfficialSchemas).Assembly.GetManifestResourceStream(resourceName);
        Assert.NotNull(stream);
        Assert.Equal(expectedSha256, Convert.ToHexStringLower(SHA256.HashData(stream)));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CheckPassport_ShouldReportMissingTopLevelParts(bool feb2027Variant)
    {
        var issues = Schemas.CheckPassport(new JsonObject(), feb2027Variant);

        Assert.Contains(issues, issue => issue.Message.Contains("dppHeader", StringComparison.Ordinal));
    }

    [Fact]
    public void FindProperty_ShouldFollowReferencesToTheDefinition()
    {
        var capacityFade = Schemas.FindProperty("performanceAndDurability", "capacityFade");

        Assert.NotNull(capacityFade);
        Assert.NotNull(capacityFade["properties"]?["percentageValue"]);
    }

    [Fact]
    public void CheckProperty_ShouldAcceptAValidValue()
    {
        var value = JsonNode.Parse("""{"percentUnit":"%","percentageValue":12}""");

        Assert.Empty(Schemas.CheckProperty("performanceAndDurability", "capacityFade", value));
    }

    [Fact]
    public void CheckProperty_ShouldReportWhereAValueIsOutOfRange()
    {
        var value = JsonNode.Parse("""{"percentUnit":"%","percentageValue":120}""");

        var issues = Schemas.CheckProperty("performanceAndDurability", "capacityFade", value);

        Assert.Contains(issues, issue => issue.Location == "/percentageValue" && issue.Message.StartsWith("maximum", StringComparison.Ordinal));
    }

    [Fact]
    public void CheckProperty_ShouldRejectAPropertyTheSchemaDoesNotHave()
    {
        Assert.Throws<ArgumentException>(() => Schemas.CheckProperty("performanceAndDurability", "noSuchProperty", JsonValue.Create(1)));
    }

    [Fact]
    public void RequiredProperties_ShouldReadASectionsRequiredList()
    {
        var required = OfficialSchemas.RequiredProperties(Schemas.FullDocument, "dppHeader");

        Assert.Equal(7, required.Count);
        Assert.Contains("dppStatus", required);
        Assert.Empty(OfficialSchemas.RequiredProperties(Schemas.Feb2027Document, "batteryCarbonFootprint"));
    }
}
