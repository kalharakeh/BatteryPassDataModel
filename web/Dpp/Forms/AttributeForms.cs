using System.Text.Json.Nodes;
using BatteryPassWeb.Dpp.Catalog;
using BatteryPassWeb.Dpp.Documents;
using BatteryPassWeb.Dpp.Schemas;

namespace BatteryPassWeb.Dpp.Forms;

/// <summary>
/// The form of every catalogue attribute, read from the official v2.0.1 schema (design 6.2): which inputs it has,
/// their kind and label, and which parts of the value the app fills in itself.
/// </summary>
public sealed class AttributeForms
{
    /// <summary>Content types offered for public document links; a stored value outside the list is still shown.</summary>
    public static readonly IReadOnlyList<string> LinkContentTypes = ["application/pdf", "text/html", "image/png", "image/jpeg"];

    /// <summary>
    /// The inputs inside grouped values, by schema property name, in the order the form shows them (the schema's own
    /// order puts e.g. the upper test temperature before the lower one).
    /// </summary>
    private static readonly (string Path, string Label)[] Parts =
    [
        ("name", "Name"),
        ("registeredTradeNameOrRegisteredTrademark", "Registered trade name or trademark"),
        ("postalAddress", "Postal address"),
        ("webAddress", "Website"),
        ("eMailAddress", "Email"),
        ("chemicalCodeValue", "Chemistry"),
        ("additionallyPossibleValue", "Other chemistry"),
        ("agentFireClass", "Fire class"),
        ("extinguishingAgent", "Extinguishing agent"),
        ("voltValue", "Voltage"),
        ("wattValue", "Power"),
        ("wattValueAt80PercentStateOfCharge", "Power at 80 % state of charge"),
        ("wattValueAt20PercentStateOfCharge", "Power at 20 % state of charge"),
        ("ohmValueForBatteryPack", "Pack"),
        ("ohmValueForBatteryModule", "Module"),
        ("ohmValueForBatteryCell", "Cell"),
        ("internalResistanceIncreaseValueForBatteryPack", "Pack"),
        ("internalResistanceIncreaseValueForBatteryModule", "Module"),
        ("internalResistanceIncreaseValueForBatteryCell", "Cell"),
        ("temperatureRangeLowerBoundaryValue", "Test temperature from"),
        ("temperatureRangeUpperBoundaryValue", "Test temperature to"),
        ("url", "Link"),
        ("resourceTitle", "Title"),
        ("contentType", "File type"),
        ("language", "Language"),
        ("chemicalName", "Chemical name"),
        ("identifierType", "Identifier type"),
        ("identifier", "Identifier"),
        ("concentration", "Concentration"),
        ("hazardPhrases", "Hazard phrases"),
        ("precautionaryPhrases", "Precautionary phrases"),
        ("substanceLocation", "Where in the battery"),
    ];

    private static readonly Dictionary<string, string> Labels = Parts.ToDictionary(part => part.Path, part => part.Label, StringComparer.Ordinal);

    private static readonly Dictionary<string, int> Order = Parts.Select((part, index) => (part.Path, index)).ToDictionary(entry => entry.Path, entry => entry.index, StringComparer.Ordinal);

    private readonly Dictionary<string, AttributeForm> _forms;

    public AttributeForms(OfficialSchemas schemas)
    {
        ArgumentNullException.ThrowIfNull(schemas);
        _forms = AttributeCatalog.All.ToDictionary(attribute => attribute.Property, attribute => Build(schemas.FullDocument, attribute), StringComparer.Ordinal);
        Languages = EnumValues(OfficialSchemas.Resolve(schemas.FullDocument, schemas.FullDocument["$defs"]!["RelatedResource"]!["properties"]!["language"]!));
    }

    /// <summary>The languages a document may be given (the schema's list), for links and uploads.</summary>
    public IReadOnlyList<string> Languages { get; }

    public AttributeForm For(AttributeDefinition attribute)
    {
        ArgumentNullException.ThrowIfNull(attribute);
        return _forms[attribute.Property];
    }

    private static AttributeForm Build(JsonNode document, AttributeDefinition attribute)
    {
        var noConstants = new Dictionary<string, string>(StringComparer.Ordinal);
        if (attribute.IsExtension)
        {
            // Beyond the schema (decision 28): a whole number of seconds, shown as "seconds" (the catalogue's unit is "s").
            return new AttributeForm(attribute, FormKind.Single, [new FormInput(string.Empty, attribute.Name, InputKind.Integer, false, [], "seconds")], noConstants);
        }

        var node = OfficialSchemas.FindProperty(document, attribute.Section, attribute.Property)
            ?? throw new InvalidOperationException($"{attribute.Label} {attribute.Property} is not in the schema.");

        if (DppLinks.RestrictedDocumentProperties.Contains(attribute.Property))
        {
            return new AttributeForm(attribute, FormKind.Upload, [], noConstants);
        }

        var type = node["type"]?.GetValue<string>();
        if (type == "array")
        {
            var items = OfficialSchemas.Resolve(document, node["items"]!);
            if (items["type"]?.GetValue<string>() == "object")
            {
                var (rowInputs, _) = ObjectInputs(document, items, attribute);
                return new AttributeForm(attribute, FormKind.Rows, Arrange(rowInputs), noConstants);
            }

            return new AttributeForm(attribute, FormKind.Lines, [new FormInput(string.Empty, attribute.Name, InputKind.Lines, true, [], Hint: "One per line.")], noConstants);
        }

        if (type != "object")
        {
            return new AttributeForm(attribute, FormKind.Single, [ScalarInput(string.Empty, attribute.Name, node, required: true, attribute)], noConstants);
        }

        var (inputs, constants) = ObjectInputs(document, node, attribute);
        if (node["properties"]?["url"] is not null && node["properties"]?["contentType"] is not null)
        {
            return new AttributeForm(attribute, FormKind.Link, Arrange(inputs), constants);
        }

        var kind = inputs.Count switch
        {
            0 => FormKind.Fixed,
            1 => FormKind.Single,
            _ => FormKind.Group,
        };

        // A value with one input is labelled by the attribute itself; grouped inputs need their own label.
        return kind == FormKind.Single
            ? new AttributeForm(attribute, kind, [inputs[0] with { Label = attribute.Name, Required = true }], constants)
            : new AttributeForm(attribute, kind, Arrange(inputs), constants);
    }

    /// <summary>The inputs of an object value, and its constants: single-valued enums, and units (the catalogue's unit).</summary>
    private static (IReadOnlyList<FormInput> Inputs, Dictionary<string, string> Constants) ObjectInputs(JsonNode document, JsonObject node, AttributeDefinition attribute)
    {
        var required = node["required"]?.AsArray().Select(name => name!.GetValue<string>()).ToHashSet(StringComparer.Ordinal) ?? [];
        var inputs = new List<FormInput>();
        var constants = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (name, child) in node["properties"]?.AsObject() ?? [])
        {
            var property = OfficialSchemas.Resolve(document, child!);
            var options = EnumValues(property);
            if (options.Count == 1)
            {
                constants[name] = options[0];
                continue;
            }

            if (options.Count > 1 && name.EndsWith("Unit", StringComparison.Ordinal))
            {
                constants[name] = attribute.Unit is { } unit && options.Contains(unit) ? unit : options[0];
                continue;
            }

            if (name == "contentType" && node["properties"]?["url"] is not null)
            {
                inputs.Add(new FormInput(name, name, InputKind.Select, required.Contains(name), LinkContentTypes));
                continue;
            }

            if (property["type"]?.GetValue<string>() == "array")
            {
                inputs.Add(new FormInput(name, name, InputKind.Lines, required.Contains(name), [], Hint: "One per line."));
                continue;
            }

            inputs.Add(ScalarInput(name, name, property, required.Contains(name), attribute));
        }

        return (inputs, constants);
    }

    private static FormInput ScalarInput(string path, string label, JsonObject property, bool required, AttributeDefinition attribute)
    {
        var options = EnumValues(property);
        if (options.Count > 0)
        {
            return new FormInput(path, label, InputKind.Select, required, options);
        }

        var unit = path.StartsWith("temperatureRange", StringComparison.Ordinal) ? "°C" : attribute.Unit;
        return property["type"]?.GetValue<string>() switch
        {
            "integer" => new FormInput(path, label, InputKind.Integer, required, [], unit),
            "number" => new FormInput(path, label, InputKind.Number, required, [], path == "concentration" ? null : unit),
            _ => property["format"]?.GetValue<string>() switch
            {
                // The file and the regulation give the manufacturing date as year and month (decision 18).
                "date" => new FormInput(path, label, InputKind.Month, required, [], Hint: "Year and month."),
                "duration" => new FormInput(path, label, InputKind.Text, required, [], Hint: "A duration such as P8Y (8 years) or P6M (6 months)."),
                "uri" when path == "url" || attribute.Property.StartsWith("webLink", StringComparison.Ordinal) => new FormInput(path, label, InputKind.Url, required, [], Hint: "A link to your website, starting with https://."),
                "uri" => new FormInput(path, label, InputKind.Text, required, [], Hint: "A URI, for example urn:… or https://…."),
                _ when path == "url" => new FormInput(path, label, InputKind.Url, required, [], Hint: "A link to your website, starting with https://."),
                _ when attribute.Property == "meaningOfLabelsAndSymbols" => new FormInput(path, label, InputKind.LongText, required, []),
                _ => new FormInput(path, label, InputKind.Text, required, []),
            },
        };
    }

    private static IReadOnlyList<string> EnumValues(JsonObject property) =>
        property["enum"]?.AsArray().Select(value => value!.GetValue<string>()).ToArray() ?? [];

    /// <summary>Labels the inputs of a grouped value and puts them in the form's order.</summary>
    private static FormInput[] Arrange(IEnumerable<FormInput> inputs) =>
        inputs.Select(input => input with { Label = Label(input.Path) }).OrderBy(input => Order[input.Path]).ToArray();

    private static string Label(string name) =>
        Labels.TryGetValue(name, out var label) ? label : throw new InvalidOperationException($"No label for the schema property '{name}'.");
}
