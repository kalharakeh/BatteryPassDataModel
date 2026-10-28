using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using BatteryPassWeb.Dpp.Catalog;
using BatteryPassWeb.Dpp.Schemas;

namespace BatteryPassWeb.Dpp.Values;

/// <summary>
/// Checks one value in the schema's own form: first against its property in the official schema (type, range,
/// allowed values, web-address pattern), then against the app's rules. The schema's "format" keywords are only
/// annotations, so this class checks them itself (design section 11, decision 18).
/// </summary>
public sealed partial class AttributeValueValidator(OfficialSchemas schemas)
{
    public IReadOnlyList<SchemaIssue> Validate(AttributeDefinition attribute, JsonNode? value)
    {
        ArgumentNullException.ThrowIfNull(attribute);
        var issues = schemas.CheckProperty(attribute.Section, attribute.Property, value);
        return issues.Count > 0 ? issues : AppRuleIssues(attribute, value).ToArray();
    }

    private IEnumerable<SchemaIssue> AppRuleIssues(AttributeDefinition attribute, JsonNode? value)
    {
        var format = schemas.FindProperty(attribute.Section, attribute.Property)?["format"]?.GetValue<string>();
        if (format is not null && value?.GetValueKind() == JsonValueKind.String)
        {
            var text = value.GetValue<string>();
            var problem = format switch
            {
                "uri" => Uri.TryCreate(text, UriKind.Absolute, out _) ? null : "must be an absolute URI, e.g. urn:… or https://…",
                "date-time" => IsUtcTimestamp(text) ? null : "must be a UTC timestamp, e.g. 2026-10-28T09:30:00Z",
                "duration" => IsoPeriod().IsMatch(text) ? null : "must be an ISO 8601 period, e.g. P8Y or P3Y6M",
                "date" => YearMonth().IsMatch(text) ? null : "must be a year and month, e.g. 2026-10 (decision 18)",
                _ => null,
            };
            if (problem is not null)
            {
                yield return new SchemaIssue(string.Empty, $"format: {problem}");
            }
        }

        // The schema allows two units for these; the catalogue fixes one for this battery category.
        (string? UnitProperty, string? Unit) fixedUnit = attribute.Number switch
        {
            21 => ("gramKilogramUnit", "kg"),
            57 => ("ampereHourMiliamperehourUnit", "Ah"),
            _ => (null, null),
        };
        if (fixedUnit.UnitProperty is not null && value?[fixedUnit.UnitProperty]?.GetValue<string>() != fixedUnit.Unit)
        {
            yield return new SchemaIssue($"/{fixedUnit.UnitProperty}", $"unit: must be \"{fixedUnit.Unit}\" for #{attribute.Number}");
        }
    }

    private static bool IsUtcTimestamp(string text) => DateTime.TryParseExact(
        text,
        ["yyyy-MM-dd'T'HH:mm:ss'Z'", "yyyy-MM-dd'T'HH:mm:ss.FFFFFFF'Z'"],
        CultureInfo.InvariantCulture,
        DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
        out _);

    [GeneratedRegex(@"^\d{4}-(0[1-9]|1[0-2])$")]
    private static partial Regex YearMonth();

    [GeneratedRegex(@"^P(?!$)(\d+Y)?(\d+M)?(\d+W)?(\d+D)?(T(?=\d)(\d+H)?(\d+M)?(\d+S)?)?$")]
    private static partial Regex IsoPeriod();
}
