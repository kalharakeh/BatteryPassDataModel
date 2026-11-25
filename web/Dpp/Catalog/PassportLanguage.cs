using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace BatteryPassWeb.Dpp.Catalog;

/// <summary>
/// The language of the passport's texts (the owner's answer O3: British English for now). The texts added for the EU's
/// battery format are kept as language maps, <c>{"en-GB": "…"}</c>, as the EU writes them, so that more languages can
/// come later; the pages show and take one language, this one. The texts kept before them carry no language: the EU
/// view gives them this one. One place, so that a setting can take its place later.
/// </summary>
public static partial class PassportLanguage
{
    /// <summary>The language every text of the passport is written in.</summary>
    public const string Tag = "en-GB";

    /// <summary>A text as a language map in the passport's language.</summary>
    public static JsonObject Map(string text) => new() { [Tag] = text };

    /// <summary>
    /// The text of a language map in the passport's language, or its first text when it has none in it; a plain text as
    /// it is; null for anything else.
    /// </summary>
    public static string? TextOf(JsonNode? value) => value switch
    {
        JsonValue plain when plain.TryGetValue<string>(out var text) => text,
        JsonObject map => (map[Tag] ?? map.Select(pair => pair.Value).FirstOrDefault(node => node is JsonValue)) is JsonValue chosen && chosen.TryGetValue<string>(out var text) ? text : null,
        _ => null,
    };

    /// <summary>
    /// The text in the passport's language only, as the editor shows and takes it: a map's text under <see cref="Tag"/>,
    /// a plain text as it is; null when the map has none in this language (round two, L5: never another language's text).
    /// </summary>
    public static string? OwnText(JsonNode? value) => value switch
    {
        JsonValue plain when plain.TryGetValue<string>(out var text) => text,
        JsonObject map => map[Tag] is JsonValue own && own.TryGetValue<string>(out var text) ? text : null,
        _ => null,
    };

    /// <summary>
    /// A text saved in the passport's language, put into the stored map so that its other languages stay (round two, L5).
    /// A stored value that isn't a map is replaced.
    /// </summary>
    public static JsonNode Merge(JsonNode? stored, JsonNode saved)
    {
        ArgumentNullException.ThrowIfNull(saved);
        if (stored is not JsonObject map || saved is not JsonObject typed || typed[Tag] is not { } text)
        {
            return saved;
        }

        var merged = map.DeepClone().AsObject();
        merged[Tag] = text.DeepClone();
        return merged;
    }

    /// <summary>
    /// Why a value isn't a language map as the EU writes it: an object of at least one text, each under a tag such as
    /// en-GB; null when it is one.
    /// </summary>
    public static string? Problem(JsonNode? value)
    {
        if (value is not JsonObject map || map.Count == 0)
        {
            return "must be a text";
        }

        foreach (var (tag, text) in map)
        {
            if (!TagPattern().IsMatch(tag))
            {
                return $"\"{tag}\" isn't a language such as {Tag}";
            }

            if (text is not JsonValue leaf || leaf.GetValueKind() != JsonValueKind.String || string.IsNullOrWhiteSpace(leaf.GetValue<string>()))
            {
                return "must be a text";
            }
        }

        return null;
    }

    [GeneratedRegex("^[A-Za-z]{2}-[A-Za-z]{2}$", RegexOptions.CultureInvariant)]
    private static partial Regex TagPattern();
}
