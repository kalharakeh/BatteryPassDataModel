using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace BatteryPassWeb.Dpp.Signing;

/// <summary>
/// The canonical text of a JSON value, for hashing and signing: object keys sorted by ordinal, no whitespace, numbers
/// written in their shortest form (520.0 and 520 give the same text), and text escaped only where JSON requires it.
/// The same value always gives the same text, whether it came from a form, a file or MongoDB.
/// </summary>
public static class CanonicalJson
{
    private static readonly JsonWriterOptions WriterOptions = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        Indented = false,
    };

    public static string Serialize(JsonNode? node)
    {
        using var buffer = new MemoryStream();
        using (var writer = new Utf8JsonWriter(buffer, WriterOptions))
        {
            Write(writer, node);
        }

        return Encoding.UTF8.GetString(buffer.ToArray());
    }

    /// <summary>"sha256:" and the lowercase hex SHA-256 of the canonical text.</summary>
    public static string Hash(JsonNode? node) =>
        "sha256:" + Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(Serialize(node))));

    private static void Write(Utf8JsonWriter writer, JsonNode? node)
    {
        switch (node)
        {
            case null:
                writer.WriteNullValue();
                break;
            case JsonObject obj:
                writer.WriteStartObject();
                foreach (var (key, value) in obj.OrderBy(property => property.Key, StringComparer.Ordinal))
                {
                    writer.WritePropertyName(key);
                    Write(writer, value);
                }

                writer.WriteEndObject();
                break;
            case JsonArray array:
                writer.WriteStartArray();
                foreach (var item in array)
                {
                    Write(writer, item);
                }

                writer.WriteEndArray();
                break;
            default:
                WriteValue(writer, node.AsValue());
                break;
        }
    }

    private static void WriteValue(Utf8JsonWriter writer, JsonValue value)
    {
        var element = JsonSerializer.SerializeToElement(value);
        if (element.ValueKind != JsonValueKind.Number)
        {
            element.WriteTo(writer);
        }
        else if (element.TryGetInt64(out var whole))
        {
            writer.WriteNumberValue(whole);
        }
        else
        {
            var number = element.GetDouble();
            if (number == Math.Floor(number) && Math.Abs(number) < 1e15)
            {
                writer.WriteNumberValue((long)number);
            }
            else
            {
                writer.WriteNumberValue(number);
            }
        }
    }
}
