using System.Globalization;
using System.Text.Json.Nodes;
using BatteryPassWeb.Dpp.Catalog;
using BatteryPassWeb.Dpp.Store;
using BatteryPassWeb.Dpp.Values;

namespace BatteryPassWeb.Dpp.Assembly;

/// <summary>The values that belong to one passport version (#2, #4, #5); versioning supplies them from step 2.</summary>
public sealed record PassportHeader(string DigitalProductPassportId, string DppStatus, DateTime LastUpdatedUtc);

/// <summary>The records one pack's passport is built from.</summary>
public sealed record PassportSources(
    BatteryPack Pack,
    ProductionBatch Batch,
    BatteryModel Model,
    ManufacturingSite Site,
    EconomicOperator Operator);

/// <summary>
/// Builds the v2.0.1 passport JSON for one pack (design section 3). Each attribute is read from the record the
/// catalogue names; stored values are copied as they are, and missing ones are left out.
/// </summary>
public static class PassportAssembler
{
    /// <summary>#1. Names the data model until the JTC-24 standards are published in the Official Journal (plan judgement call).</summary>
    public const string DppSchemaVersion = "BatteryPass-Ready 2.0.1";

    public static JsonObject Assemble(PassportSources sources, PassportHeader header)
    {
        ArgumentNullException.ThrowIfNull(sources);
        ArgumentNullException.ThrowIfNull(header);

        var passport = new JsonObject();
        foreach (var attribute in AttributeCatalog.All)
        {
            if (ValueFor(attribute, sources, header) is not { } value)
            {
                continue;
            }

            var parent = attribute.Section == AttributeDefinition.HeaderSection
                ? Child(passport, AttributeDefinition.HeaderSection)
                : Child(Child(passport, AttributeDefinition.DataRoot), attribute.Section);
            parent[attribute.Property] = value;
        }

        return passport;
    }

    private static JsonNode? ValueFor(AttributeDefinition attribute, PassportSources sources, PassportHeader header) =>
        attribute.Storage switch
        {
            StorageLocation.System => SystemValue(attribute.Number, header),
            StorageLocation.Pack => Stored(sources.Pack.Values, attribute),
            StorageLocation.Operator => Stored(sources.Operator.Values, attribute),
            StorageLocation.Batch => Stored(sources.Batch.Values, attribute),
            StorageLocation.Site => Stored(sources.Site.Values, attribute),
            StorageLocation.Model => Stored(sources.Model.Values, attribute),
            StorageLocation.ModelWithPackOverride => Stored(sources.Pack.Values, attribute) ?? Stored(sources.Model.Values, attribute),
            _ => throw new InvalidOperationException($"Unknown storage for #{attribute.Number}."),
        };

    private static JsonNode? Stored(Dictionary<string, AttributeValue> values, AttributeDefinition attribute) =>
        values.TryGetValue(attribute.Property, out var stored) ? stored.Value.DeepClone() : null;

    private static JsonNode SystemValue(int number, PassportHeader header) => number switch
    {
        1 => JsonValue.Create(DppSchemaVersion),
        2 => new JsonObject { ["dppStatusValue"] = header.DppStatus },
        3 => new JsonObject { ["dppGranularityValue"] = "item" },
        4 => JsonValue.Create(FormatTimestamp(header.LastUpdatedUtc)),
        5 => JsonValue.Create(header.DigitalProductPassportId),
        _ => throw new InvalidOperationException($"#{number} is not a system attribute."),
    };

    /// <summary>#4's format: UTC, ISO 8601, whole seconds (e.g. 2026-10-28T09:30:00Z).</summary>
    public static string FormatTimestamp(DateTime utc) =>
        (utc.Kind == DateTimeKind.Local ? utc.ToUniversalTime() : utc).ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture);

    private static JsonObject Child(JsonObject parent, string name)
    {
        if (parent[name] is JsonObject existing)
        {
            return existing;
        }

        var created = new JsonObject();
        parent[name] = created;
        return created;
    }
}
