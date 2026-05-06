using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using MongoDB.Bson;

namespace BatteryPassWeb.Services;

public sealed class CanonicalPassportSnapshotService
{
    private readonly SchemaRegistryService _schemaRegistryService;

    public CanonicalPassportSnapshotService(SchemaRegistryService schemaRegistryService)
    {
        _schemaRegistryService = schemaRegistryService;
    }

    public BsonDocument BuildSnapshot(BsonDocument passport)
    {
        var snapshot = new BsonDocument
        {
            ["passportId"] = BsonHelpers.GetString(passport, "passportId"),
            ["schemaVersions"] = BuildSchemaVersions(),
            ["registryInfo"] = new BsonDocument
            {
                ["registryId"] = BsonHelpers.GetString(passport, "registryInfo", "registryId")
            }
        };

        var signedAspects = BuildSignedAspects(passport);
        if (signedAspects.ElementCount > 0)
        {
            snapshot["aspects"] = signedAspects;
        }

        var signedApp = BuildSignedApp(passport);
        if (signedApp.ElementCount > 0)
        {
            snapshot["app"] = signedApp;
        }

        return snapshot;
    }

    public string Canonicalize(BsonValue value)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = false }))
        {
            WriteCanonicalValue(writer, value);
        }

        return Encoding.UTF8.GetString(stream.ToArray());
    }

    public string Sha256Hex(string canonicalJson)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(canonicalJson));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private BsonDocument BuildSchemaVersions()
    {
        var versions = new BsonDocument();
        foreach (var schema in _schemaRegistryService.ListSchemas().OrderBy(schema => schema.AspectKey, StringComparer.Ordinal))
        {
            versions[schema.AspectKey] = schema.Version;
        }

        return versions;
    }

    private static BsonDocument BuildSignedAspects(BsonDocument passport)
    {
        var signedAspects = new BsonDocument();
        var aspects = BsonHelpers.GetValue(passport, "aspects") as BsonDocument;
        if (aspects == null)
        {
            return signedAspects;
        }

        foreach (var aspect in aspects.Elements.OrderBy(element => element.Name, StringComparer.Ordinal))
        {
            if (aspect.Value is not BsonDocument aspectDocument)
            {
                continue;
            }

            var signedAspect = new BsonDocument();
            if (aspectDocument.TryGetValue("visibility", out var visibility) && !visibility.IsBsonNull)
            {
                signedAspect["visibility"] = visibility.DeepClone();
            }

            if (aspectDocument.GetValue("payload", BsonNull.Value) is BsonDocument payload)
            {
                signedAspect["payload"] = payload.DeepClone();
            }

            if (signedAspect.ElementCount > 0)
            {
                signedAspects[aspect.Name] = signedAspect;
            }
        }

        return signedAspects;
    }

    private static BsonDocument BuildSignedApp(BsonDocument passport)
    {
        var signedApp = new BsonDocument();
        var documents = BsonHelpers.GetValue(passport, "app", "documents") as BsonDocument;
        if (documents == null)
        {
            return signedApp;
        }

        var signedDocuments = new BsonDocument();
        foreach (var document in documents.Elements.OrderBy(element => element.Name, StringComparer.Ordinal))
        {
            if (document.Value is not BsonDocument documentInfo)
            {
                continue;
            }

            var signedDocument = new BsonDocument();
            foreach (var field in new[] { "url", "fileId", "sha256", "contentType", "visibility" })
            {
                if (documentInfo.TryGetValue(field, out var value) && !value.IsBsonNull)
                {
                    signedDocument[field] = value.DeepClone();
                }
            }

            if (signedDocument.ElementCount > 0)
            {
                signedDocuments[document.Name] = signedDocument;
            }
        }

        if (signedDocuments.ElementCount > 0)
        {
            signedApp["documents"] = signedDocuments;
        }

        return signedApp;
    }

    private static void WriteCanonicalValue(Utf8JsonWriter writer, BsonValue value)
    {
        if (value == null || value.IsBsonNull)
        {
            writer.WriteNullValue();
            return;
        }

        if (value is BsonDocument document)
        {
            writer.WriteStartObject();
            foreach (var element in document.Elements.OrderBy(element => element.Name, StringComparer.Ordinal))
            {
                writer.WritePropertyName(element.Name);
                WriteCanonicalValue(writer, element.Value);
            }
            writer.WriteEndObject();
            return;
        }

        if (value is BsonArray array)
        {
            writer.WriteStartArray();
            foreach (var item in array)
            {
                WriteCanonicalValue(writer, item);
            }
            writer.WriteEndArray();
            return;
        }

        switch (value.BsonType)
        {
            case BsonType.Boolean:
                writer.WriteBooleanValue(value.AsBoolean);
                break;
            case BsonType.DateTime:
                writer.WriteStringValue(value.ToUniversalTime().ToString("O"));
                break;
            case BsonType.Decimal128:
                writer.WriteRawValue(value.AsDecimal.ToString(System.Globalization.CultureInfo.InvariantCulture));
                break;
            case BsonType.Double:
                writer.WriteNumberValue(value.AsDouble);
                break;
            case BsonType.Int32:
                writer.WriteNumberValue(value.AsInt32);
                break;
            case BsonType.Int64:
                writer.WriteNumberValue(value.AsInt64);
                break;
            case BsonType.ObjectId:
                writer.WriteStringValue(value.AsObjectId.ToString());
                break;
            case BsonType.String:
                writer.WriteStringValue(value.AsString);
                break;
            default:
                writer.WriteStringValue(value.ToString());
                break;
        }
    }
}
