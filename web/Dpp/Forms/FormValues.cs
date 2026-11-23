using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using BatteryPassWeb.Dpp.Catalog;
using BatteryPassWeb.Dpp.Signing;

namespace BatteryPassWeb.Dpp.Forms;

/// <summary>
/// Turns what was typed into an attribute's form into its value in the schema's own form, and a stored value back
/// into the texts the form shows. Nothing here judges a value: the save pipeline checks it against the schema.
/// </summary>
public static class FormValues
{
    private const string TemperatureRange = "temperatureRange";

    /// <summary>The value typed for one attribute; null when nothing was entered, so the stored value is kept.</summary>
    /// <param name="field">Gives the posted text of a field name, or null when it wasn't posted.</param>
    public static JsonNode? Read(AttributeForm form, Func<string, string?> field)
    {
        ArgumentNullException.ThrowIfNull(form);
        ArgumentNullException.ThrowIfNull(field);
        switch (form.Kind)
        {
            case FormKind.Upload:
                return null;
            case FormKind.Fixed:
                return WithConstants(new JsonObject(), form.Constants);
            case FormKind.Single when form.Inputs[0].Kind == InputKind.Duration:
                return Duration(field(form.FieldName(form.Inputs[0])), field(AttributeForm.UnitField(form.FieldName(form.Inputs[0]))));
            case FormKind.Single when form.Inputs[0].Path.Length == 0:
            case FormKind.Lines:
                return Parse(form.Inputs[0], field(form.FieldName(form.Inputs[0])));
            case FormKind.Rows:
                var count = int.TryParse(field(form.RowCountField), NumberStyles.None, CultureInfo.InvariantCulture, out var rows) ? rows : 0;
                var items = Enumerable.Range(0, count).Select(row => ReadObject(form, field, row)).OfType<JsonObject>().ToArray();
                return items.Length == 0 ? null : new JsonArray(items);
            default:
                return ReadObject(form, field, row: null);
        }
    }

    /// <summary>The texts the form shows for a stored value, by field name.</summary>
    public static IReadOnlyDictionary<string, string> Show(AttributeForm form, JsonNode? value)
    {
        ArgumentNullException.ThrowIfNull(form);
        var texts = new Dictionary<string, string>(StringComparer.Ordinal);
        if (value is null || form.Kind is FormKind.Upload or FormKind.Fixed)
        {
            return texts;
        }

        if (form.Kind == FormKind.Rows)
        {
            var row = 0;
            foreach (var item in value as JsonArray ?? [])
            {
                foreach (var input in form.Inputs)
                {
                    texts[form.FieldName(input, row)] = Text(item?[input.Path]);
                }

                row++;
            }

            return texts;
        }

        foreach (var input in form.Inputs)
        {
            var text = input.Kind switch
            {
                InputKind.LanguageText => PassportLanguage.TextOf(input.Path.Length == 0 ? value : value[input.Path]) ?? string.Empty,
                InputKind.Choices => string.Join(",", Choices(input, ((input.Path.Length == 0 ? value : value[input.Path]) as JsonArray ?? []).OfType<JsonValue>().Select(item => item.TryGetValue<string>(out var entry) ? entry : item.ToJsonString()))),
                _ => Text(input.Path.Length == 0 ? value : value[input.Path]),
            };
            if (input.Kind == InputKind.Duration)
            {
                var (amount, unit) = DurationParts(text);
                texts[form.FieldName(input)] = amount;
                texts[AttributeForm.UnitField(form.FieldName(input))] = unit;
                continue;
            }

            texts[form.FieldName(input)] = text;
        }

        return texts;
    }

    /// <summary>
    /// A period typed as a whole number and "Y" (years, the default) or "M" (months), in the official form: "8" years is
    /// P8Y (W05). Anything else typed, e.g. an ISO period such as P3Y6M, is kept as written for the save pipeline to judge.
    /// </summary>
    public static JsonNode? Duration(string? amount, string? unit)
    {
        var text = amount?.Trim();
        if (string.IsNullOrEmpty(text))
        {
            return null;
        }

        if (!int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var number))
        {
            return JsonValue.Create(text);
        }

        // Only years or months; any other unit is passed on as it is, so the save pipeline refuses it (round two, D).
        var chosen = string.IsNullOrWhiteSpace(unit) ? "Y" : unit.Trim();
        return JsonValue.Create($"P{number.ToString(CultureInfo.InvariantCulture)}{chosen}");
    }

    /// <summary>
    /// A stored period as the field shows it: P8Y as 8 and years, P6M as 6 and months, P1Y6M as 18 months. Anything else
    /// (days, or not a period) shows as written, with years, so saving it unchanged keeps it.
    /// </summary>
    public static (string Amount, string Unit) DurationParts(string text)
    {
        var match = System.Text.RegularExpressions.Regex.Match(text, @"^P(?:(\d+)Y)?(?:(\d+)M)?$");
        if (!match.Success || text == "P")
        {
            return (text, "Y");
        }

        // A stored value too large to read as a number shows as written (round two, D): the page never breaks on it.
        var years = 0L;
        var months = 0L;
        if ((match.Groups[1].Success && !long.TryParse(match.Groups[1].Value, NumberStyles.None, CultureInfo.InvariantCulture, out years))
            || (match.Groups[2].Success && !long.TryParse(match.Groups[2].Value, NumberStyles.None, CultureInfo.InvariantCulture, out months))
            || years > 100_000 || months > 1_200_000)
        {
            return (text, "Y");
        }

        return match.Groups[2].Success
            ? ((years * 12 + months).ToString(CultureInfo.InvariantCulture), "M")
            : (years.ToString(CultureInfo.InvariantCulture), "Y");
    }

    /// <summary>
    /// The entries of a choice as the form keeps them: each found on the input's list (by its address, its label or a
    /// plain other name) as its address, in the list's order, then the names not on it, as they were, in their order.
    /// </summary>
    public static IReadOnlyList<string> Choices(FormInput input, IEnumerable<string> entries)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(entries);
        var found = new HashSet<string>(StringComparer.Ordinal);
        var others = new List<string>();
        foreach (var entry in entries.Select(item => item.Trim()).Where(item => item.Length > 0))
        {
            if (Choice(input, entry) is { } address)
            {
                found.Add(address);
            }
            else if (!others.Contains(entry, StringComparer.Ordinal))
            {
                others.Add(entry);
            }
        }

        return [.. input.Options.Where(found.Contains), .. others];
    }

    /// <summary>The address on the input's list an entry names; null when it names none.</summary>
    public static string? Choice(FormInput input, string entry)
    {
        ArgumentNullException.ThrowIfNull(input);
        return input.FindOption(entry)
            ?? ((EuCodeLists.FindRawMaterial(entry) ?? EuCodeLists.FindChemicalSymbol(entry)) is { } address && input.Options.Contains(address) ? address : null);
    }

    /// <summary>How many rows a stored list has; 0 for anything else.</summary>
    public static int RowCount(JsonNode? value) => value is JsonArray array ? array.Count : 0;

    /// <summary>
    /// True when two values of this form are the same: written canonically, or for a choice (#42, E9) naming the same
    /// entries, so that a list kept from before by names ("Natural graphite") isn't rewritten as addresses by a save or a
    /// load that didn't change it (G16). It becomes addresses when the team changes it.
    /// </summary>
    public static bool Same(AttributeForm form, JsonNode? first, JsonNode? second)
    {
        ArgumentNullException.ThrowIfNull(form);
        return SameValue(first, second)
            || (form.Inputs is [{ Kind: InputKind.Choices }] && first is JsonArray && second is JsonArray
                && Show(form, first).SequenceEqual(Show(form, second)));
    }

    /// <summary>True when two values are the same once written canonically (numbers in their shortest form, keys sorted).</summary>
    public static bool SameValue(JsonNode? first, JsonNode? second) =>
        CanonicalJson.Serialize(first) == CanonicalJson.Serialize(second);

    private static JsonObject? ReadObject(AttributeForm form, Func<string, string?> field, int? row)
    {
        var value = new JsonObject();
        foreach (var input in form.Inputs)
        {
            if (Parse(input, field(form.FieldName(input, row))) is { } part)
            {
                value[input.Path] = part;
            }
        }

        return value.Count == 0 ? null : WithConstants(value, form.Constants);
    }

    /// <summary>Adds the constants; the temperature-range units only when a temperature was entered.</summary>
    private static JsonObject WithConstants(JsonObject value, IReadOnlyDictionary<string, string> constants)
    {
        var hasRange = value.Any(property => property.Key.StartsWith(TemperatureRange, StringComparison.Ordinal));
        foreach (var (name, constant) in constants)
        {
            if (hasRange || !name.StartsWith(TemperatureRange, StringComparison.Ordinal))
            {
                value[name] = constant;
            }
        }

        return value;
    }

    /// <summary>
    /// The typed text as a JSON value; null when blank. A number that doesn't parse stays text, so the save pipeline
    /// refuses it with the schema's reason instead of the form guessing.
    /// </summary>
    private static JsonNode? Parse(FormInput input, string? typed)
    {
        var text = typed?.Trim();
        if (string.IsNullOrEmpty(text))
        {
            return null;
        }

        switch (input.Kind)
        {
            case InputKind.Integer when long.TryParse(text, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var whole):
                return JsonValue.Create(whole);
            case InputKind.Number when decimal.TryParse(text.Contains('.', StringComparison.Ordinal) ? text : text.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out var number):
                return JsonValue.Create(number);
            // Entries of the EU's lists (#42, E9): ticked boxes post their addresses; typed names (the workbook) are found
            // by their label or a plain other name. A name not on the list is kept as it was (G16).
            case InputKind.Choices:
                var chosen = Choices(input, text.Split([',', ';', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
                return chosen.Count == 0 ? null : new JsonArray(chosen.Select(item => (JsonNode)JsonValue.Create(item)).ToArray());
            // A text in the passport's language is kept as a language map (decision 94); the page shows one language.
            case InputKind.LanguageText:
                return PassportLanguage.Map(text.Replace("\r\n", "\n", StringComparison.Ordinal));
            // A choice kept as a code (a country's "SWE") may be typed as its words ("Sweden"), in the workbook too.
            case InputKind.Select when input.OptionWords is not null && input.FindOption(text) is { } option:
                return JsonValue.Create(option);
            case InputKind.Lines:
                var lines = text.Split('\n').Select(line => line.Trim()).Where(line => line.Length > 0).ToArray();
                return lines.Length == 0 ? null : new JsonArray(lines.Select(line => (JsonNode)JsonValue.Create(line)).ToArray());
            default:
                return JsonValue.Create(text);
        }
    }

    private static string Text(JsonNode? value) => value switch
    {
        null => string.Empty,
        JsonArray lines => string.Join("\n", lines.Select(Text)),
        JsonObject => value.ToJsonString(),
        _ when value.GetValueKind() == JsonValueKind.String => value.GetValue<string>(),
        _ => CanonicalJson.Serialize(value),
    };
}
