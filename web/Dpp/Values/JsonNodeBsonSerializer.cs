using System.Text.Json.Nodes;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;

namespace BatteryPassWeb.Dpp.Values;

/// <summary>
/// Stores a JSON value as native BSON (objects as documents, arrays as arrays), so stored values stay readable
/// and queryable in MongoDB, and reads it back as the same JSON.
/// </summary>
public sealed class JsonNodeBsonSerializer : SerializerBase<JsonNode?>
{
    public override void Serialize(BsonSerializationContext context, BsonSerializationArgs args, JsonNode? value)
    {
        if (value is null)
        {
            context.Writer.WriteNull();
            return;
        }

        var wrapper = BsonDocument.Parse($"{{\"v\":{value.ToJsonString()}}}");
        BsonValueSerializer.Instance.Serialize(context, wrapper["v"]);
    }

    public override JsonNode? Deserialize(BsonDeserializationContext context, BsonDeserializationArgs args) =>
        ToJson(BsonValueSerializer.Instance.Deserialize(context));

    /// <summary>
    /// Converts BSON back to JSON directly. Going through the driver's JSON text would write doubles with 17 digits
    /// (75.4 becomes 75.400000000000006); System.Text.Json writes the shortest form.
    /// </summary>
    private static JsonNode? ToJson(BsonValue value) => value.BsonType switch
    {
        BsonType.Null => null,
        BsonType.Document => new JsonObject(value.AsBsonDocument.Select(element => KeyValuePair.Create(element.Name, ToJson(element.Value)))),
        BsonType.Array => new JsonArray(value.AsBsonArray.Select(ToJson).ToArray()),
        BsonType.String => JsonValue.Create(value.AsString),
        BsonType.Boolean => JsonValue.Create(value.AsBoolean),
        BsonType.Int32 => JsonValue.Create(value.AsInt32),
        BsonType.Int64 => JsonValue.Create(value.AsInt64),
        BsonType.Double => JsonValue.Create(value.AsDouble),
        BsonType.Decimal128 => JsonValue.Create(Decimal128.ToDecimal(value.AsDecimal128)),
        _ => throw new FormatException($"Unexpected BSON type {value.BsonType} in a stored value."),
    };
}
