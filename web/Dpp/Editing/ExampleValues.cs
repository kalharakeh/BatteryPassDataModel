using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using BatteryPassWeb.Dpp.Assembly;
using BatteryPassWeb.Dpp.Catalog;
using BatteryPassWeb.Dpp.Documents;
using BatteryPassWeb.Dpp.Publication;
using BatteryPassWeb.Dpp.Values;

namespace BatteryPassWeb.Dpp.Editing;

/// <summary>One value or document of a record that is still the demo's example.</summary>
public sealed record ExampleItem(string Label, bool IsFile)
{
    /// <remarks>Not "#7 …" first: the battery page reads such a line as a missing value.</remarks>
    public string Words => $"Still the demo's example {(IsFile ? "file" : "value")}: {Label}";
}

/// <summary>
/// Decision 70: product data copied from the demo keeps the demo's example values and files until the team replaces them,
/// and a battery with a real ID isn't published for the first time while its passport holds one. A text value is an
/// example when it contains "(demo)", "Demo " as a word, "example.com" or "demo data" (capitals don't matter); a document
/// is one when the record still links the copied demo file. A test ID is never held back: that is what test IDs are for.
/// </summary>
public static partial class ExampleValues
{
    public const string WhatToDo = "Replace the demo's example values and files before a real battery is published, or use a test ID to try it.";

    /// <summary>True when a value holds example text anywhere in it.</summary>
    public static bool IsExampleText(JsonNode? value) => value switch
    {
        null => false,
        JsonValue text when text.TryGetValue<string>(out var words) => IsExampleText(words),
        JsonObject item => item.Any(pair => IsExampleText(pair.Value)),
        JsonArray items => items.Any(IsExampleText),
        _ => false,
    };

    /// <remarks>The app's own links (a document's or the accident page's address) are never example text: a document is judged by its file.</remarks>
    public static bool IsExampleText(string? text) =>
        text is { Length: > 0 }
        && !AppLink().IsMatch(text)
        && (text.Contains("(demo)", StringComparison.OrdinalIgnoreCase)
            || text.Contains("example.com", StringComparison.OrdinalIgnoreCase)
            || text.Contains("demo data", StringComparison.OrdinalIgnoreCase)
            || DemoWord().IsMatch(text));

    /// <summary>
    /// A record's example values and files, in attribute order. <paramref name="exampleDocumentIds"/> are the copied demo
    /// documents the record was given; a document attribute that links one of them is an example file.
    /// </summary>
    public static IReadOnlyList<ExampleItem> Of(IReadOnlyDictionary<string, AttributeValue> values, IReadOnlyCollection<string>? exampleDocumentIds = null)
    {
        ArgumentNullException.ThrowIfNull(values);
        var items = new List<(int Number, ExampleItem Item)>();
        foreach (var (key, value) in values)
        {
            var attribute = AttributeCatalog.FindByProperty(key);
            if (attribute is null)
            {
                continue;
            }

            var label = $"{attribute.Label} {attribute.Name}";
            if (DppLinks.RestrictedDocumentProperties.Contains(key) && exampleDocumentIds is { Count: > 0 } && LinkedDocument(value.Value) is { } documentId && exampleDocumentIds.Contains(documentId))
            {
                items.Add((attribute.Number, new ExampleItem(label, IsFile: true)));
            }
            else if (IsExampleText(value.Value))
            {
                items.Add((attribute.Number, new ExampleItem(label, IsFile: false)));
            }
        }

        return items.OrderBy(item => item.Number).Select(item => item.Item).ToArray();
    }

    /// <summary>Every example value and file a battery's passport would hold: its own, its batch's, model's, site's and operator's.</summary>
    public static IReadOnlyList<ExampleItem> InPassport(PassportSources sources)
    {
        ArgumentNullException.ThrowIfNull(sources);
        return Of(sources.Pack.Values)
            .Concat(Of(sources.Batch.Values, sources.Batch.ExampleDocumentIds))
            .Concat(Of(sources.Model.Values, sources.Model.ExampleDocumentIds))
            .Concat(Of(sources.Site.Values))
            .Concat(Of(sources.Operator.Values))
            .DistinctBy(item => item.Label)
            .ToArray();
    }

    /// <summary>
    /// The first publication of a battery with a real ID is blocked while its passport holds an example value or file,
    /// naming each; a test ID and an already published battery are not touched.
    /// </summary>
    public static PublicationReport Check(PassportSources sources, PublicationReport report, int versionNumber)
    {
        ArgumentNullException.ThrowIfNull(sources);
        ArgumentNullException.ThrowIfNull(report);
        if (versionNumber != 1 || !DemoDataRule.HasRealId(sources.Pack) || InPassport(sources) is not { Count: > 0 } items)
        {
            return report;
        }

        return report with { Blocking = [.. report.Blocking, .. items.Select(item => item.Words), WhatToDo] };
    }

    /// <summary>The document ID a document attribute's link names (…/dpp/documents/{id}); null otherwise.</summary>
    private static string? LinkedDocument(JsonNode? value)
    {
        var url = value is JsonObject link && link["url"] is JsonValue address && address.TryGetValue<string>(out var text) ? text : null;
        var at = url?.LastIndexOf("/dpp/documents/", StringComparison.Ordinal) ?? -1;
        return at < 0 ? null : Uri.UnescapeDataString(url![(at + "/dpp/documents/".Length)..]);
    }

    [GeneratedRegex(@"(?<![\p{L}\p{N}])demo\s", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex DemoWord();

    [GeneratedRegex(@"^https?://[^/\s]+/dpp/(documents|packs)/\S+$", RegexOptions.CultureInvariant)]
    private static partial Regex AppLink();
}
