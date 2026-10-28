using System.Text.Json.Nodes;
using MongoDB.Bson;
using MongoDB.Bson.IO;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;

namespace BatteryPassWeb.Dpp.Values;

/// <summary>
/// Stores a JSON value as native BSON (objects as documents, arrays as arrays), so stored values stay readable
/// and queryable in MongoDB, and reads it back as the same JSON.
/// </summary>
public sealed class JsonNodeBsonSerializer : SerializerBase<JsonNode?>
{
    private static readonly JsonWriterSettings RelaxedJson = new() { OutputMode = JsonOutputMode.RelaxedExtendedJson };

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

    public override JsonNode? Deserialize(BsonDeserializationContext context, BsonDeserializationArgs args)
    {
        var bson = BsonValueSerializer.Instance.Deserialize(context);
        if (bson.IsBsonNull)
        {
            return null;
        }

        var json = new BsonDocument("v", bson).ToJson(RelaxedJson);
        return JsonNode.Parse(json)!["v"]!.DeepClone();
    }
}
