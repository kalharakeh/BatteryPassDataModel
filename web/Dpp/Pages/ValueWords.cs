using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using BatteryPassWeb.Dpp.Forms;

namespace BatteryPassWeb.Dpp.Pages;

/// <summary>
/// One line of a value as passport readers see it: an optional label (a part of a grouped value), the text, and a
/// link where the value is a document or a web address. <paramref name="StartsRow"/> marks the first line of each
/// item of a list of objects (#44).
/// </summary>
public sealed record ValueLine(string? Label, string Text, string? Href = null, bool StartsRow = false);

/// <summary>
/// A passport value in words for the public summary and the detail page (design 12). It follows the attribute's form,
/// the same reading of the official schema as the team's editors, so every shape is covered: "512.5 kg", "8 years",
/// "September 2026", a document's title as a link, a list one item per line.
/// </summary>
public static class ValueWords
{
    private static readonly CultureInfo English = CultureInfo.GetCultureInfo("en-GB");

    /// <summary>The battery category (#20) in words: "industrial/non-stationary battery" reads "Industrial, non-stationary battery".</summary>
    public static string CategoryWords(string code)
    {
        ArgumentNullException.ThrowIfNull(code);
        var words = code.Trim().Replace("/", ", ", StringComparison.Ordinal);
        return words.Length == 0 ? words : char.ToUpperInvariant(words[0]) + words[1..];
    }

    /// <summary>A coded value: the text itself, or the one text in the schema's small object around it.</summary>
    private static string? Code(JsonNode? value) => value switch
    {
        JsonValue text when text.TryGetValue<string>(out var code) => code,
        JsonObject holder => holder.Select(pair => pair.Value).OfType<JsonValue>().Select(inner => inner.TryGetValue<string>(out var code) ? code : null).FirstOrDefault(code => code is not null),
        _ => null,
    };

    public static IReadOnlyList<ValueLine> Lines(AttributeForm form, JsonNode? value)
    {
        ArgumentNullException.ThrowIfNull(form);
        if (value is null)
        {
            return [];
        }

        if (form.Attribute.Number == 4 && Instant(value) is { } updated)
        {
            return [new ValueLine(null, Time(updated))];
        }

        // A code from the schema's lists in words: the passport status (#2, "Active"), the battery category (#20) and the
        // battery status (#22, "Re-used"), as the badges say them (redesign R5 and R6). The passport keeps the schema's
        // code; the schema holds each in a small object, e.g. { "dppStatusValue": "active" }.
        if (form.Attribute.Number is 2 or 20 or 22 && Code(value) is { } code)
        {
            return [new ValueLine(null, form.Attribute.Number switch
            {
                2 => BatteryPassWeb.Dpp.Store.DppStatuses.Words(code),
                22 => StatusBadge.Battery(code)?.Words ?? code,
                _ => CategoryWords(code),
            })];
        }

        switch (form.Kind)
        {
            case FormKind.Link:
            case FormKind.Upload:
                return Document(value) is { } document ? [document] : [];
            case FormKind.Fixed:
                return value is JsonObject fixedValue && fixedValue.Select(property => Scalar(property.Value)).FirstOrDefault(text => text is not null) is { } text
                    ? [new ValueLine(null, text)]
                    : [];
            case FormKind.Lines:
                return (value as JsonArray ?? []).Select(Scalar).OfType<string>().Select(item => new ValueLine(null, item)).ToArray();
            case FormKind.Rows:
                var rows = new List<ValueLine>();
                foreach (var item in value as JsonArray ?? [])
                {
                    var first = true;
                    foreach (var input in form.Inputs)
                    {
                        if (Part(input, item?[input.Path]) is { } part)
                        {
                            rows.Add(new ValueLine(input.Label, part, StartsRow: first));
                            first = false;
                        }
                    }
                }

                return rows;
            case FormKind.Single:
                var only = form.Inputs[0];
                var single = Part(only, only.Path.Length == 0 ? value : value[only.Path]);
                return single is null ? [] : [new ValueLine(null, single, only.Kind == InputKind.Url ? single : null)];
            default:
                return form.Inputs
                    .Select(input => (input.Label, Text: Part(input, value[input.Path])))
                    .Where(part => part.Text is not null)
                    .Select(part => new ValueLine(part.Label, part.Text!))
                    .ToArray();
        }
    }

    /// <summary>A time on the passport pages, in the app's time zone: "28 Oct 2026, 10:30".</summary>
    public static string Time(DateTime utc) =>
        TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), TimeZoneInfo.Local).ToString("d MMM yyyy, HH:mm", English);

    /// <summary>A day kept as yyyy-MM-dd (a repair's), in words: "20 Oct 2026"; the text itself when it isn't one.</summary>
    public static string Day(string day) =>
        DateOnly.TryParseExact(day, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed)
            ? parsed.ToString("d MMM yyyy", English)
            : day;

    /// <summary>A JSON number as a decimal, however it was read (the database gives doubles); null for anything else.</summary>
    public static decimal? Decimal(JsonNode? node)
    {
        if (node is not JsonValue value || value.GetValueKind() != JsonValueKind.Number)
        {
            return null;
        }

        return value.TryGetValue<decimal>(out var exact) ? exact
            : value.TryGetValue<double>(out var inexact) ? (decimal)inexact
            : value.TryGetValue<long>(out var whole) ? whole
            : decimal.Parse(value.ToJsonString(), NumberStyles.Float, CultureInfo.InvariantCulture);
    }

    /// <summary>A number as the pages show it: grouped thousands, no trailing zeros ("250,000", "0.5").</summary>
    public static string Number(decimal number) => number.ToString("#,0.######", CultureInfo.InvariantCulture);

    private static string? Part(FormInput input, JsonNode? node)
    {
        if (node is null)
        {
            return null;
        }

        if (node is JsonArray items)
        {
            var texts = items.Select(Scalar).OfType<string>().ToArray();
            return texts.Length == 0 ? null : string.Join(", ", texts);
        }

        if (Decimal(node) is { } number)
        {
            return input.Unit is { } unit ? $"{Number(number)} {unit}" : Number(number);
        }

        var text = Scalar(node);
        if (text is null)
        {
            return null;
        }

        if (input.Kind == InputKind.Month && DateTime.TryParseExact(text, "yyyy-MM", CultureInfo.InvariantCulture, DateTimeStyles.None, out var month))
        {
            return month.ToString("MMMM yyyy", English);
        }

        return input.Hint?.StartsWith("A duration", StringComparison.Ordinal) == true ? Duration(text) : text;
    }

    /// <summary>A public link or an uploaded file: its title, opening its address.</summary>
    private static ValueLine? Document(JsonNode value)
    {
        var url = Scalar(value["url"]);
        if (url is null)
        {
            return null;
        }

        return new ValueLine(null, Scalar(value["resourceTitle"]) ?? url, url);
    }

    /// <summary>"P8Y" as "8 years", "P1Y6M" as "1 year 6 months"; anything else as written.</summary>
    private static string Duration(string text)
    {
        var match = System.Text.RegularExpressions.Regex.Match(text, @"^P(?:(\d+)Y)?(?:(\d+)M)?(?:(\d+)D)?$");
        if (!match.Success || text == "P")
        {
            return text;
        }

        var parts = new List<string>();
        foreach (var (group, word) in new[] { (1, "year"), (2, "month"), (3, "day") })
        {
            if (match.Groups[group].Success)
            {
                parts.Add(Words.Count(int.Parse(match.Groups[group].Value, CultureInfo.InvariantCulture), word));
            }
        }

        return string.Join(" ", parts);
    }

    private static DateTime? Instant(JsonNode value) =>
        Scalar(value) is { } text && DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var utc)
            ? utc
            : null;

    private static string? Scalar(JsonNode? node) => node switch
    {
        null => null,
        JsonValue value when value.GetValueKind() == JsonValueKind.String => value.GetValue<string>() is { Length: > 0 } text ? text : null,
        JsonValue value when Decimal(value) is { } number => Number(number),
        JsonValue value => value.ToJsonString(),
        _ => null,
    };
}
