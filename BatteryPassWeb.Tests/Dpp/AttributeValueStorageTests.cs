using System.Text.Json.Nodes;
using BatteryPassWeb.Dpp.Store;
using BatteryPassWeb.Dpp.Values;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;

namespace BatteryPassWeb.Tests.Dpp;

/// <summary>Checks that stored values survive the MongoDB mapping unchanged. No database is needed.</summary>
public sealed class AttributeValueStorageTests
{
    private static readonly DateTime Generated = new(2026, 10, 28, 9, 30, 0, DateTimeKind.Utc);

    [Theory]
    [InlineData("""{"percentUnit":"%","percentageValue":76}""")]
    [InlineData("""{"degreeCelsiusUnit":"°C","celsiusValue":-30}""")]
    [InlineData("""{"gramKilogramUnit":"kg","gramKilogramValue":512.5}""")]
    [InlineData("""["Cobalt","Lithium"]""")]
    [InlineData("\"2026-09\"")]
    [InlineData("412")]
    [InlineData("12.5")]
    [InlineData("75.4")] // not exact in binary: must not come back as 75.400000000000006
    [InlineData("""{"kgCO2EquivalentPerKilowattHourValue":4.8,"kgCO2EquivalentPerKilowattHourUnit":"kgCO2-eq/kWh"}""")]
    [InlineData("""[{"concentration":1.2,"hazardPhrases":["H301"]}]""")]
    [InlineData("true")]
    public void Value_ShouldRoundTripThroughBson(string json)
    {
        var original = new AttributeValue { Value = JsonNode.Parse(json)!, GeneratedAtUtc = Generated, Source = "form", Who = "team@example.com" };

        var restored = BsonSerializer.Deserialize<AttributeValue>(original.ToBsonDocument());

        Assert.True(JsonNode.DeepEquals(original.Value, restored.Value), $"{original.Value.ToJsonString()} became {restored.Value.ToJsonString()}");
        Assert.Equal(Generated, restored.GeneratedAtUtc);
        Assert.Equal(DateTimeKind.Utc, restored.GeneratedAtUtc.Kind);
        Assert.Equal("form", restored.Source);
        Assert.Equal("team@example.com", restored.Who);
    }

    [Fact]
    public void Value_ShouldBeStoredAsNativeBson()
    {
        var value = new AttributeValue { Value = JsonNode.Parse("""{"percentUnit":"%","percentageValue":76}""")!, GeneratedAtUtc = Generated, Source = "feed" };

        var stored = value.ToBsonDocument()["Value"].AsBsonDocument;

        Assert.Equal(76, stored["percentageValue"].AsInt32);
    }

    [Fact]
    public void Pack_ShouldRoundTripWithItsValues()
    {
        var pack = new BatteryPack
        {
            Id = "pack-1",
            BatchId = "batch-1",
            OperatorId = "operator-1",
            ClusterId = "cluster-1",
            Values =
            {
                ["batterySerialNumber"] = new AttributeValue { Value = JsonValue.Create("VPC8C05-6788"), GeneratedAtUtc = Generated, Source = "form" },
                ["stateOfChargeSoC"] = new AttributeValue { Value = JsonNode.Parse("""{"percentUnit":"%","percentageValue":76}""")!, GeneratedAtUtc = Generated, Source = "feed" },
            },
        };

        var document = pack.ToBsonDocument();
        var restored = BsonSerializer.Deserialize<BatteryPack>(document);

        Assert.Equal("pack-1", document["_id"].AsString);
        Assert.Equal("cluster-1", restored.ClusterId);
        Assert.Equal("VPC8C05-6788", restored.Values["batterySerialNumber"].Value.GetValue<string>());
        Assert.Equal(76, restored.Values["stateOfChargeSoC"].Value["percentageValue"]!.GetValue<int>());
    }
}
