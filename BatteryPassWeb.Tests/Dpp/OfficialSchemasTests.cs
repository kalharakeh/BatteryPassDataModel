using System.Security.Cryptography;
using System.Text.Json.Nodes;
using BatteryPassWeb.Dpp.Schemas;

namespace BatteryPassWeb.Tests.Dpp;

public sealed class OfficialSchemasTests
{
    private static readonly OfficialSchemas Schemas = OfficialSchemas.Load();

    /// <summary>
    /// The files as published, with one local deviation (NOTICE.md, finding D06, decision 94): #78's ohm values are
    /// numbers, not whole numbers. Undoing exactly that gives the published file byte for byte.
    /// </summary>
    [Theory]
    [InlineData(OfficialSchemas.FullResourceName, "31c0c4a5bdbbf2dc0d4908cfb94db1a0da1a65d7b41d545a04029a94c97000ab", "3011fdc36ea8a5cf3f8a9e105a5878bf617748e01e22ecab6d20019746e0d33d")]
    [InlineData(OfficialSchemas.Feb2027ResourceName, "aa2ef3f30bd967023946032e338c02ca82f03bc8d26b0a110f0edeafbe4604f5", "81cb73db5e2dd3478f4e8339f1550940a1bfb841795a449c32a2728dc425e5f3")]
    public void EmbeddedSchema_ShouldBeTheOfficialFile_WithOnlyTheDecimalOhmsDeviation(string resourceName, string publishedSha256, string embeddedSha256)
    {
        using var stream = typeof(OfficialSchemas).Assembly.GetManifestResourceStream(resourceName);
        Assert.NotNull(stream);
        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);
        var bytes = buffer.ToArray();
        Assert.Equal(embeddedSha256, Convert.ToHexStringLower(SHA256.HashData(bytes)));

        const string Published = "\"ohmInteger\": {\n      \"type\": \"object\",\n      \"properties\": {\n        \"ohmValueForBatteryModule\": {\n          \"type\": \"integer\"\n        },\n        \"ohmValueForBatteryPack\": {\n          \"type\": \"integer\"\n        },\n        \"ohmValueForBatteryCell\": {\n          \"type\": \"integer\"\n        },\n";
        var text = System.Text.Encoding.UTF8.GetString(bytes);
        var deviation = Published.Replace("\"integer\"", "\"number\"", StringComparison.Ordinal);
        Assert.Equal(1, text.Split(deviation).Length - 1);
        Assert.Equal(publishedSha256, Convert.ToHexStringLower(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(text.Replace(deviation, Published, StringComparison.Ordinal)))));
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
