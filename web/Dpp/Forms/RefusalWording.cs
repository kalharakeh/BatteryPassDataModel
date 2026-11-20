using System.Text.RegularExpressions;

namespace BatteryPassWeb.Dpp.Forms;

/// <summary>
/// Turns the save pipeline's reason for refusing a value into plain words for the field it belongs to. The pipeline
/// reports schema issues as "/location keyword: detail", joined with "; ", e.g.
/// "/percentageValue maximum: 120 should be at most 100"; its own rules come as sentences ("#29 must link to …").
/// </summary>
public static partial class RefusalWording
{
    public static string Describe(AttributeForm form, string message)
    {
        ArgumentNullException.ThrowIfNull(form);
        if (string.IsNullOrWhiteSpace(message))
        {
            return string.Empty;
        }

        var sentences = new List<string>();
        foreach (var issue in message.Split("; ", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (issue.StartsWith("failing indexes", StringComparison.Ordinal))
            {
                continue; // part of a list summary; each failing row is reported on its own
            }

            var match = IssuePattern().Match(issue);
            if (!match.Success)
            {
                sentences.Add(issue); // one of the pipeline's own sentences
                continue;
            }

            var keyword = match.Groups["keyword"].Value;
            if (keyword is "properties" or "items")
            {
                continue; // a summary: the issue inside is reported on its own
            }

            sentences.Add(Sentence(form, Part(form, match.Groups["location"].Value), keyword, match.Groups["detail"].Value));
        }

        return string.Join(" ", sentences.Select(Finish));
    }

    /// <summary>
    /// The inputs of a grouped value that a refusal is about (by schema property), so the page marks only those; empty
    /// when the message doesn't name one.
    /// </summary>
    public static IReadOnlySet<string> Parts(AttributeForm form, string message)
    {
        ArgumentNullException.ThrowIfNull(form);
        var paths = form.Inputs.Select(input => input.Path).ToHashSet(StringComparer.Ordinal);
        var parts = new HashSet<string>(StringComparer.Ordinal);
        foreach (var issue in (message ?? string.Empty).Split("; ", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var match = IssuePattern().Match(issue);
            if (!match.Success || match.Groups["keyword"].Value is "properties" or "items")
            {
                continue;
            }

            var located = match.Groups["location"].Value.Split('/', StringSplitOptions.RemoveEmptyEntries).LastOrDefault(segment => paths.Contains(segment));
            if (located is not null)
            {
                parts.Add(located);
            }

            if (match.Groups["keyword"].Value == "required")
            {
                parts.UnionWith(Quoted().Matches(match.Groups["detail"].Value).Select(quoted => quoted.Groups[1].Value).Where(paths.Contains));
            }
        }

        return parts;
    }

    /// <summary>
    /// The part of the value an issue is about: null for a one-input value (the field itself), the input's label inside
    /// a grouped value, or "Row 2" (with the label) inside a list of rows.
    /// </summary>
    private static string? Part(AttributeForm form, string location)
    {
        var segments = location.Split('/', StringSplitOptions.RemoveEmptyEntries);
        var row = segments.Length > 0 && int.TryParse(segments[0], out var index) ? $"Row {index + 1}" : null;
        var path = segments.LastOrDefault(segment => !int.TryParse(segment, out _));
        var label = form.Kind == FormKind.Single ? null : form.Inputs.FirstOrDefault(input => input.Path == path)?.Label;
        return (row, label) switch
        {
            (null, null) => null,
            (null, _) => label,
            (_, null) => row,
            _ => $"{row}, {label}",
        };
    }

    /// <summary>
    /// One issue in plain words. <paramref name="part"/> is null when the issue is about the whole field. Schema
    /// property names in <paramref name="detail"/> (e.g. ["postalAddress"]) have labels in <paramref name="form"/>.
    /// </summary>
    private static string Sentence(AttributeForm form, string? part, string keyword, string detail)
    {
        if (keyword == "required")
        {
            // A row says which row it is; a grouped value names its missing parts.
            var missing = Quoted().Matches(detail).Select(match => form.Inputs.FirstOrDefault(input => input.Path == match.Groups[1].Value)?.Label ?? match.Groups[1].Value).ToArray();
            var list = missing.Length <= 1 ? string.Concat(missing) : $"{string.Join(", ", missing[..^1])} and {missing[^1]}";
            var sentence = $"{list} {(missing.Length == 1 ? "is" : "are")} missing";
            return part is null ? sentence : $"{part}: {sentence}";
        }

        var limit = LastNumber().Match(detail).Value;
        var rule = keyword switch
        {
            "maximum" => $"must be at most {limit}",
            "minimum" => $"must be at least {limit}",
            "type" when detail.EndsWith("\"integer\"", StringComparison.Ordinal) => "must be a whole number",
            "type" when detail.EndsWith("\"number\"", StringComparison.Ordinal) => "must be a number",
            "type" when detail.EndsWith("\"string\"", StringComparison.Ordinal) => "must be text",
            "enum" => "must be one of the listed choices",
            "pattern" => "must be a web address starting with https:// or http://",
            "minItems" => "needs at least one line",
            _ => $"was refused ({keyword}: {detail})",
        };

        // Under a one-input field the field itself is the subject; inside a group, the part is.
        return part is null ? rule : $"{part} {rule}";
    }

    private static string Finish(string sentence) =>
        sentence.Length == 0 ? sentence : char.ToUpperInvariant(sentence[0]) + sentence[1..].TrimEnd('.') + ".";

    [GeneratedRegex(@"^(?<location>/\S*)?\s*(?<keyword>[A-Za-z]+): (?<detail>.*)$")]
    private static partial Regex IssuePattern();

    [GeneratedRegex("\"(\\w+)\"")]
    private static partial Regex Quoted();

    [GeneratedRegex(@"-?\d+(?:\.\d+)?(?=\D*$)")]
    private static partial Regex LastNumber();
}
