using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Nodes;
using Json.Schema;

namespace BatteryPassWeb.Dpp.Schemas;

/// <summary>One problem found by a schema check: where it is (a JSON pointer into the checked value) and what is wrong.</summary>
public sealed record SchemaIssue(string Location, string Message);

/// <summary>
/// The official BatteryPass-Ready v2.0.1 schemas for Other industrial batteries above 2 kWh, embedded in this
/// assembly (see Dpp/Schemas/v2.0.1/NOTICE.md). Checks whole passports, or single values against one property.
/// </summary>
public sealed class OfficialSchemas
{
    public const string FullResourceName = "Dpp.Schemas.Other_Industrial_Above_2kWh_2.0.1.json";
    public const string Feb2027ResourceName = "Dpp.Schemas.Other_Industrial_Above_2kWh_Feb27_2.0.1.json";

    private const string BaseAddress = "https://battery-pass.local/schemas/v2.0.1/";
    private const string FullAddress = BaseAddress + "other-industrial.json";
    private const string Feb2027Address = BaseAddress + "other-industrial-feb27.json";
    private const string DefsPrefix = "#/$defs/";

    private static readonly EvaluationOptions ListOutput = new() { OutputFormat = OutputFormat.List };

    private readonly BuildOptions _buildOptions = new() { SchemaRegistry = new SchemaRegistry() };
    private readonly ConcurrentDictionary<string, Lazy<JsonSchema>> _propertySchemas = new(StringComparer.Ordinal);

    private OfficialSchemas()
    {
        var fullText = ReadResource(FullResourceName);
        var feb2027Text = ReadResource(Feb2027ResourceName);
        FullDocument = JsonNode.Parse(fullText)!;
        Feb2027Document = JsonNode.Parse(feb2027Text)!;
        Full = JsonSchema.FromText(fullText, _buildOptions, new Uri(FullAddress));
        Feb2027 = JsonSchema.FromText(feb2027Text, _buildOptions, new Uri(Feb2027Address));
    }

    /// <summary>The full schema as JSON, for reading property definitions and required lists.</summary>
    public JsonNode FullDocument { get; }

    /// <summary>The Feb-2027 variant as JSON.</summary>
    public JsonNode Feb2027Document { get; }

    public JsonSchema Full { get; }

    public JsonSchema Feb2027 { get; }

    /// <summary>Loads and builds both schemas. Build once and reuse it: building takes a moment.</summary>
    public static OfficialSchemas Load() => new();

    /// <summary>The definition of a property in a section (or "dppHeader"), with references followed; null if the full schema has no such property.</summary>
    public JsonObject? FindProperty(string section, string property) => FindProperty(FullDocument, section, property);

    public static JsonObject? FindProperty(JsonNode document, string section, string property)
    {
        var node = document["$defs"]?[section]?["properties"]?[property];
        return node is null ? null : Resolve(document, node);
    }

    /// <summary>Follows local "#/$defs/..." references until the node is a definition. All references in these schemas are local.</summary>
    public static JsonObject Resolve(JsonNode document, JsonNode node)
    {
        var current = node.AsObject();
        while (current["$ref"]?.GetValue<string>() is { } reference)
        {
            if (!reference.StartsWith(DefsPrefix, StringComparison.Ordinal))
            {
                throw new InvalidOperationException($"Unsupported schema reference '{reference}'.");
            }

            current = document["$defs"]![reference[DefsPrefix.Length..]]!.AsObject();
        }

        return current;
    }

    /// <summary>The "required" list of a section (or "dppHeader"); empty when the schema has no such section.</summary>
    public static IReadOnlyList<string> RequiredProperties(JsonNode document, string section) =>
        document["$defs"]?[section]?["required"]?.AsArray().Select(name => name!.GetValue<string>()).ToArray() ?? [];

    /// <summary>Checks a whole passport against the full schema, or against the Feb-2027 variant.</summary>
    public IReadOnlyList<SchemaIssue> CheckPassport(JsonNode passport, bool feb2027Variant) =>
        Evaluate(feb2027Variant ? Feb2027 : Full, passport);

    /// <summary>Checks one value against its property's definition in the full schema.</summary>
    public IReadOnlyList<SchemaIssue> CheckProperty(string section, string property, JsonNode? value)
    {
        if (FindProperty(section, property) is null)
        {
            throw new ArgumentException($"The schema has no property {section}.{property}.", nameof(property));
        }

        var pointer = $"/$defs/{section}/properties/{property}";
        var schema = _propertySchemas.GetOrAdd(pointer, key => new Lazy<JsonSchema>(() => JsonSchema.FromText(
            $$"""{"$schema":"https://json-schema.org/draft/2020-12/schema","$ref":"{{FullAddress}}#{{key}}"}""",
            _buildOptions,
            new Uri($"{BaseAddress}property/{section}/{property}.json")))).Value;
        return Evaluate(schema, value);
    }

    private static IReadOnlyList<SchemaIssue> Evaluate(JsonSchema schema, JsonNode? instance)
    {
        var results = schema.Evaluate(JsonSerializer.SerializeToElement(instance), ListOutput);
        if (results.IsValid)
        {
            return [];
        }

        var issues = new List<SchemaIssue>();
        foreach (var result in (results.Details ?? []).Prepend(results))
        {
            foreach (var (keyword, message) in result.Errors ?? [])
            {
                issues.Add(new SchemaIssue(result.InstanceLocation.ToString(), $"{keyword}: {message}"));
            }
        }

        return issues;
    }

    private static string ReadResource(string name)
    {
        using var stream = typeof(OfficialSchemas).Assembly.GetManifestResourceStream(name)
            ?? throw new InvalidOperationException($"Embedded schema '{name}' is missing.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
