namespace BatteryPassWeb.Dpp.Forms;

/// <summary>
/// The schema's document languages by name, e.g. "English (United Kingdom)" for en-GB (W05). Pages show the name; the
/// stored value stays the code the official format wants. Written out, so the names don't depend on the server's
/// language data.
/// </summary>
public static class LanguageNames
{
    private static readonly Dictionary<string, string> Names = new(StringComparer.Ordinal)
    {
        ["bg-BG"] = "Bulgarian (Bulgaria)",
        ["cs-CZ"] = "Czech (Czechia)",
        ["da-DK"] = "Danish (Denmark)",
        ["de-AT"] = "German (Austria)",
        ["de-BE"] = "German (Belgium)",
        ["de-CH"] = "German (Switzerland)",
        ["de-DE"] = "German (Germany)",
        ["de-LI"] = "German (Liechtenstein)",
        ["de-LU"] = "German (Luxembourg)",
        ["el-CY"] = "Greek (Cyprus)",
        ["el-GR"] = "Greek (Greece)",
        ["en-GB"] = "English (United Kingdom)",
        ["en-IE"] = "English (Ireland)",
        ["en-MT"] = "English (Malta)",
        ["es-ES"] = "Spanish (Spain)",
        ["et-EE"] = "Estonian (Estonia)",
        ["fi-FI"] = "Finnish (Finland)",
        ["fr-BE"] = "French (Belgium)",
        ["fr-CH"] = "French (Switzerland)",
        ["fr-FR"] = "French (France)",
        ["fr-LU"] = "French (Luxembourg)",
        ["ga-IE"] = "Irish (Ireland)",
        ["hr-HR"] = "Croatian (Croatia)",
        ["hu-HU"] = "Hungarian (Hungary)",
        ["is-IS"] = "Icelandic (Iceland)",
        ["it-CH"] = "Italian (Switzerland)",
        ["it-IT"] = "Italian (Italy)",
        ["lb-LU"] = "Luxembourgish (Luxembourg)",
        ["lt-LT"] = "Lithuanian (Lithuania)",
        ["lv-LV"] = "Latvian (Latvia)",
        ["mt-MT"] = "Maltese (Malta)",
        ["nl-BE"] = "Dutch (Belgium)",
        ["nl-NL"] = "Dutch (Netherlands)",
        ["no-NO"] = "Norwegian (Norway)",
        ["pl-PL"] = "Polish (Poland)",
        ["pt-PT"] = "Portuguese (Portugal)",
        ["rm-CH"] = "Romansh (Switzerland)",
        ["ro-RO"] = "Romanian (Romania)",
        ["sk-SK"] = "Slovak (Slovakia)",
        ["sl-SI"] = "Slovenian (Slovenia)",
        ["sv-FI"] = "Swedish (Finland)",
        ["sv-SE"] = "Swedish (Sweden)",
        ["tr-CY"] = "Turkish (Cyprus)",
    };

    /// <summary>The language's name; the code itself for a code without one.</summary>
    public static string Of(string code) => Names.GetValueOrDefault(code, code);

    /// <summary>The codes ordered by their names, as a list to choose from shows them.</summary>
    public static IReadOnlyList<string> ByName(IEnumerable<string> codes) =>
        codes.OrderBy(Of, StringComparer.Ordinal).ToArray();
}
