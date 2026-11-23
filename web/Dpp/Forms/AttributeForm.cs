using BatteryPassWeb.Dpp.Catalog;

namespace BatteryPassWeb.Dpp.Forms;

/// <summary>How an attribute is entered on the team's pages, following its v2.0.1 schema shape.</summary>
public enum FormKind
{
    /// <summary>One input: a text, a number, or a number whose fixed unit the app adds (e.g. #21 in kg).</summary>
    Single,

    /// <summary>Several inputs that make one object, e.g. #14's name and address, or #66's voltage and test temperatures.</summary>
    Group,

    /// <summary>A list of texts, one per line (#42, #43).</summary>
    Lines,

    /// <summary>A list of objects, one row each (#44).</summary>
    Rows,

    /// <summary>A public document: a link to the company website with its content type, title and language (decision 7).</summary>
    Link,

    /// <summary>A restricted document, uploaded to the app (decision 7); not part of the save form.</summary>
    Upload,

    /// <summary>A value the schema allows only one way (#20); the app fills it in.</summary>
    Fixed,
}

public enum InputKind
{
    Text,

    /// <summary>Text that runs to a sentence or more (#27), shown as a text area.</summary>
    LongText,
    Number,
    Integer,
    Month,
    Url,
    Select,
    Lines,

    /// <summary>A period (#19): a whole number with years or months; the app stores the official form, e.g. P8Y (W05).</summary>
    Duration,
}

/// <summary>
/// One input of an attribute's form. <paramref name="Path"/> is the schema property it fills inside the value, or ""
/// when the input is the whole value.
/// </summary>
/// <param name="OptionWords">For a choice whose stored codes aren't words (a country's "SWE"), what the page shows for each.</param>
public sealed record FormInput(
    string Path,
    string Label,
    InputKind Kind,
    bool Required,
    IReadOnlyList<string> Options,
    string? Unit = null,
    string? Hint = null,
    IReadOnlyDictionary<string, string>? OptionWords = null)
{
    /// <summary>An option in words: its <see cref="OptionWords"/> entry, or the option itself.</summary>
    public string Words(string option) => OptionWords?.GetValueOrDefault(option) ?? option;

    /// <summary>The option typed as its code or its words, capitals not minding; null when it is neither.</summary>
    public string? FindOption(string typed)
    {
        ArgumentNullException.ThrowIfNull(typed);
        var text = typed.Trim();
        return Options.FirstOrDefault(option => string.Equals(option, text, StringComparison.OrdinalIgnoreCase))
            ?? Options.FirstOrDefault(option => string.Equals(Words(option), text, StringComparison.OrdinalIgnoreCase));
    }
}

/// <summary>
/// The form of one attribute. <see cref="Constants"/> are the schema properties the app fills in itself, such as
/// units that have only one allowed value. For <see cref="FormKind.Rows"/>, <see cref="Inputs"/> describe one row.
/// </summary>
public sealed record AttributeForm(
    AttributeDefinition Attribute,
    FormKind Kind,
    IReadOnlyList<FormInput> Inputs,
    IReadOnlyDictionary<string, string> Constants)
{
    public const string Prefix = "v.";

    /// <summary>The posted field that holds the number of rows of a <see cref="FormKind.Rows"/> form.</summary>
    public string RowCountField => $"{Prefix}{Attribute.Property}.rows";

    /// <summary>The posted field with a <see cref="InputKind.Duration"/> input's unit ("Y" or "M"), next to its number.</summary>
    public static string UnitField(string fieldName) => $"{fieldName}.unit";

    /// <summary>The HTML name of an input: v.batteryMass, v.extinguishingAgent.agentFireClass or v.hazardousSubstancesAndTheirImpact[0].identifier.</summary>
    public string FieldName(FormInput input, int? row = null)
    {
        ArgumentNullException.ThrowIfNull(input);
        var owner = row is null ? $"{Prefix}{Attribute.Property}" : $"{Prefix}{Attribute.Property}[{row}]";
        return input.Path.Length == 0 || Kind == FormKind.Single ? owner : $"{owner}.{input.Path}";
    }
}
