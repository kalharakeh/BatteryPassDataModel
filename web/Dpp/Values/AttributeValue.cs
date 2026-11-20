using System.Text.Json.Nodes;
using MongoDB.Bson.Serialization.Attributes;

namespace BatteryPassWeb.Dpp.Values;

/// <summary>
/// One stored value in the schema's own JSON form (units included), with when the information was generated,
/// where it came from and who sent it (design 4.1).
/// </summary>
public sealed class AttributeValue
{
    [BsonSerializer(typeof(JsonNodeBsonSerializer))]
    public required JsonNode Value { get; init; }

    /// <summary>Measurement time for live values; entry time otherwise.</summary>
    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public required DateTime GeneratedAtUtc { get; init; }

    /// <summary>"form", "upload", "feed" or "seed".</summary>
    public required string Source { get; init; }

    public string? Who { get; init; }
}
