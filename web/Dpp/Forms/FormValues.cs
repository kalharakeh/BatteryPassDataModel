using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
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
            texts[form.FieldName(input)] = Text(input.Path.Length == 0 ? value : value[input.Path]);
        }

        return texts;
    }

    /// <summary>How many rows a stored list has; 0 for anything else.</summary>
    public static int RowCount(JsonNode? value) => value is JsonArray array ? array.Count : 0;

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
