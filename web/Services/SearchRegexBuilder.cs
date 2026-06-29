using MongoDB.Bson;
using System.Text.RegularExpressions;

namespace BatteryPassWeb.Services;

public static class SearchRegexBuilder
{
    public const int MaxSearchTextLength = 128;

    public static string NormalizeLiteralSearchPattern(string query)
    {
        var normalized = string.IsNullOrWhiteSpace(query)
            ? string.Empty
            : query.Trim();

        if (normalized.Length > MaxSearchTextLength)
        {
            normalized = normalized[..MaxSearchTextLength];
        }

        return Regex.Escape(normalized);
    }

    public static BsonRegularExpression CreateLiteralContainsRegex(string query)
    {
        return new BsonRegularExpression(NormalizeLiteralSearchPattern(query), "i");
    }
}
