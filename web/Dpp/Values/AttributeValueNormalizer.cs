using System.Text.Json;
using System.Text.Json.Nodes;
using BatteryPassWeb.Dpp.Catalog;
using BatteryPassWeb.Dpp.Schemas;

namespace BatteryPassWeb.Dpp.Values;

/// <summary>
/// Turns a bare value into the schema's own form, e.g. 76 into {"percentUnit":"%","percentageValue":76}, for
/// attributes that are one value with a fixed unit (design 8.2). Everything else is returned unchanged; the
/// validator then decides whether it is right.
/// </summary>
public sealed class AttributeValueNormalizer(OfficialSchemas schemas)
{
    /// <summary>Schema definition → the property that holds the value, and the unit property with its fixed value (null = the catalogue's unit).</summary>
    private static readonly Dictionary<string, (string ValueProperty, string? UnitProperty, string? Unit)> SingleValueDefinitions =
        new(StringComparer.Ordinal)
        {
            ["percentInteger"] = ("percentageValue", "percentUnit", "%"),
            ["celsiusInteger"] = ("celsiusValue", "degreeCelsiusUnit", "°C"),
            ["ampereHourMiliamperehourInteger"] = ("amperehourMiliamperehourValue", "ampereHourMiliamperehourUnit", null),
            ["gramKilogramDecimal"] = ("gramKilogramValue", "gramKilogramUnit", null),
            ["kgCO2EquivalentPerKilowattHourDecimal"] = ("kgCO2EquivalentPerKilowattHourValue", "kgCO2EquivalentPerKilowattHourUnit", "kgCO2-eq/kWh"),
            ["amperePerAmpereHourDecimal"] = ("amperePerAmpereHourValue", "amperePerAmpereHourUnit", "A/Ah"),
            ["voltCelsiusDecimal"] = ("voltValue", "voltUnit", "V"),
            ["wattCelsiusDecimalOneValue"] = ("wattValue", "wattUnit", "W"),
            ["internalResistanceIncreaseType"] = ("internalResistanceIncreaseValueForBatteryPack", "percentUnit", "%"),
            ["batteryStatusType"] = ("batteryStatusValues", null, null),
            ["batteryCategoryType"] = ("batteryCategoryValue", null, null),
        };

    public JsonNode? Normalize(AttributeDefinition attribute, JsonNode? value)
    {
        ArgumentNullException.ThrowIfNull(attribute);
        if (value is null || value.GetValueKind() is JsonValueKind.Object or JsonValueKind.Array)
        {
            return value;
        }

        var reference = schemas.FullDocument["$defs"]?[attribute.Section]?["properties"]?[attribute.Property]?["$ref"]?.GetValue<string>();
        var definition = reference?[(reference.LastIndexOf('/') + 1)..];
        if (definition is null || !SingleValueDefinitions.TryGetValue(definition, out var shape))
        {
            return value;
        }

        var wrapped = new JsonObject { [shape.ValueProperty] = value.DeepClone() };
        if (shape.UnitProperty is not null)
        {
            wrapped[shape.UnitProperty] = shape.Unit ?? attribute.Unit;
        }

        return wrapped;
    }
}
