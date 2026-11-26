using System.Text.Json.Nodes;
using BatteryPassWeb.Dpp.Signing;

namespace BatteryPassWeb.Tests.Dpp;

public sealed class CanonicalJsonTests
{
    [Fact]
    public void Serialize_ShouldSortKeysAndDropWhitespace()
    {
        var value = JsonNode.Parse("""{ "b": 1, "a": { "d": 2, "c": [3, 1] } }""");

        Assert.Equal("""{"a":{"c":[3,1],"d":2},"b":1}""", CanonicalJson.Serialize(value));
    }

    [Theory]
    [InlineData("520.0", "520")]
    [InlineData("520", "520")]
    [InlineData("75.4", "75.4")]
    [InlineData("-20", "-20")]
    [InlineData("0.5", "0.5")]
    [InlineData("1.20", "1.2")]
    public void Numbers_ShouldBeWrittenInTheirShortestForm(string json, string expected)
    {
        Assert.Equal(expected, CanonicalJson.Serialize(JsonNode.Parse(json)));
    }

    [Fact]
    public void SameValueFromDifferentSources_ShouldGiveTheSameText()
    {
        var parsed = JsonNode.Parse("""{"voltValue":520.0,"x":75.4}""");
        var built = new JsonObject { ["x"] = 75.4, ["voltValue"] = 520d };

        Assert.Equal(CanonicalJson.Serialize(parsed), CanonicalJson.Serialize(built));
        Assert.Equal(CanonicalJson.Hash(parsed), CanonicalJson.Hash(built));
    }

    [Fact]
    public void Text_ShouldKeepLettersAndEscapeOnlyWhatJsonRequires()
    {
        var value = new JsonObject { ["unit"] = "°C", ["address"] = "Exempelvägen 1", ["quote"] = "say \"hi\"" };

        Assert.Equal("""{"address":"Exempelvägen 1","quote":"say \"hi\"","unit":"°C"}""", CanonicalJson.Serialize(value));
    }

    [Fact]
    public void Hash_ShouldChangeWithTheValue()
    {
        var hash = CanonicalJson.Hash(JsonNode.Parse("""{"a":1}"""));

        Assert.StartsWith("sha256:", hash, StringComparison.Ordinal);
        Assert.Equal(71, hash.Length);
        Assert.NotEqual(hash, CanonicalJson.Hash(JsonNode.Parse("""{"a":2}""")));
    }
}
