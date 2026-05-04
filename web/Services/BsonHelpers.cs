using MongoDB.Bson;

namespace BatteryPassWeb.Services;

public static class BsonHelpers
{
    public static string GetString(BsonDocument document, params string[] path)
    {
        var value = GetValue(document, path);
        return value == null || value.IsBsonNull ? string.Empty : value.ToString() ?? string.Empty;
    }

    public static DateTime? GetDateTime(BsonDocument document, params string[] path)
    {
        var value = GetValue(document, path);
        if (value == null || value.IsBsonNull)
        {
            return null;
        }

        if (value.IsValidDateTime)
        {
            return value.ToUniversalTime();
        }

        if (DateTime.TryParse(value.ToString(), out var parsed))
        {
            return parsed;
        }

        return null;
    }

    public static BsonValue? GetValue(BsonDocument document, params string[] path)
    {
        BsonValue current = document;
        foreach (var segment in path)
        {
            if (current is not BsonDocument child || !child.TryGetValue(segment, out current))
            {
                return null;
            }
        }

        return current;
    }

    public static object? ToDotNet(BsonValue? value)
    {
        if (value == null || value.IsBsonNull)
        {
            return null;
        }

        if (value is BsonDocument document)
        {
            var dictionary = new Dictionary<string, object?>();
            foreach (var element in document.Elements)
            {
                dictionary[element.Name] = ToDotNet(element.Value);
            }
            return dictionary;
        }

        if (value is BsonArray array)
        {
            return array.Select(ToDotNet).ToList();
        }

        return value.BsonType switch
        {
            BsonType.Boolean => value.AsBoolean,
            BsonType.Int32 => value.AsInt32,
            BsonType.Int64 => value.AsInt64,
            BsonType.Double => value.AsDouble,
            BsonType.Decimal128 => value.AsDecimal,
            BsonType.DateTime => value.ToUniversalTime(),
            BsonType.ObjectId => value.AsObjectId.ToString(),
            _ => value.ToString(),
        };
    }
}
