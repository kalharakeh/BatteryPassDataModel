namespace BatteryPassWeb.Services;

using System.Text;
using System.Text.RegularExpressions;

public sealed record MaterialDefinition(
    string Field,
    string Label,
    string Color,
    double DemoMassKg,
    bool IsCriticalRawMaterial);

public sealed record CarbonStageDefinition(
    string Field,
    string Label,
    string Stage,
    string Color,
    double DemoValue);

public static class BatteryPassCanonicalDataCatalog
{
    public const double DemoSupplyChainIndex = 82.0;
    public const double DemoCarbonFootprint = 68.0;
    public const string DemoPerformanceClass = "B";
    public const string DemoBatteryPassportIdentifier = "urn:bmwk:123456687678";
    public const string DemoBatteryCategory = "industrial";

    public static readonly IReadOnlyList<MaterialDefinition> Materials =
    [
        new("materialNickel", "Nickel", "#4f6f7d", 28.4, true),
        new("materialCopper", "Copper", "#d76f3d", 18.2, true),
        new("materialAluminium", "Aluminium", "#aeb4ba", 36.8, false),
        new("materialGraphite", "Graphite", "#27313f", 24.5, false),
        new("materialManganese", "Manganese", "#d9b64e", 10.6, true),
        new("materialCobalt", "Cobalt", "#0aa34f", 12.1, true),
        new("materialLithium", "Lithium", "#85c7d6", 20.6, true),
        new("materialElectrolyte", "Electrolyte and separators", "#e7d99d", 42.0, false)
    ];

    public static readonly IReadOnlyList<CarbonStageDefinition> CarbonStages =
    [
        new("carbonRawMaterial", "raw material extraction", "RawMaterialExtraction", "#08a348", 21.0),
        new("carbonMainProduction", "main production", "MainProduction", "#df6b3b", 31.0),
        new("carbonDistribution", "distribution", "Distribution", "#ead9a4", 9.0),
        new("carbonRecycling", "recycling", "Recycling", "#4f6f7d", 7.0)
    ];

    public static MaterialDefinition MaterialByLabel(string label)
    {
        return Materials.FirstOrDefault(material => material.Label.Equals(label, StringComparison.OrdinalIgnoreCase))
            ?? new MaterialDefinition(string.Empty, label, "#4f6f7d", 18.0, false);
    }

    public static CarbonStageDefinition CarbonStageByStage(string stage)
    {
        return CarbonStages.FirstOrDefault(item => item.Stage.Equals(stage, StringComparison.OrdinalIgnoreCase))
            ?? CarbonStages[0];
    }

    public static CarbonStageDefinition CarbonStageByLabel(string label)
    {
        return CarbonStages.FirstOrDefault(item => item.Label.Equals(label, StringComparison.OrdinalIgnoreCase))
            ?? CarbonStages[0];
    }

    public static double NormalizeMaterialMass(string label, double value)
    {
        return IsReasonableNumber(value, 0.1, 500)
            ? Math.Round(value, 2)
            : MaterialByLabel(label).DemoMassKg;
    }

    public static double NormalizeSupplyChainIndex(double value)
    {
        return IsReasonableNumber(value, 0, 100) ? Math.Round(value, 2) : DemoSupplyChainIndex;
    }

    public static double NormalizeCarbonFootprint(double value)
    {
        return IsReasonableNumber(value, 0.1, 1000) ? Math.Round(value, 2) : DemoCarbonFootprint;
    }

    public static double NormalizeCarbonStageValue(string stage, double value)
    {
        return IsReasonableNumber(value, 0, 1000)
            ? Math.Round(value, 2)
            : CarbonStageByStage(stage).DemoValue;
    }

    public static string NormalizePerformanceClass(string value)
    {
        var normalized = value.Trim().ToUpperInvariant();
        return normalized is "A" or "B" or "C" or "D" or "E" ? normalized : DemoPerformanceClass;
    }

    public static string NormalizeBatteryCategory(string value)
    {
        var normalized = value.Trim().ToLowerInvariant();
        return normalized is "lmt" or "ev" or "industrial" or "stationary"
            ? normalized
            : DemoBatteryCategory;
    }

    public static string NormalizeBatteryPassportIdentifier(string currentValue, string serialNumber, string passportId)
    {
        var current = currentValue.Trim().ToLowerInvariant();
        if (Regex.IsMatch(current, "^urn:[a-z0-9]+:[a-z0-9]+$"))
        {
            return current;
        }

        var token = NormalizeIdentifierToken(serialNumber);
        if (string.IsNullOrWhiteSpace(token))
        {
            token = NormalizeIdentifierToken(passportId);
        }

        return $"urn:acme:{(string.IsNullOrWhiteSpace(token) ? "battery" : token)}";
    }

    public static string NormalizeManufacturerSerialNumber(string currentValue, string passportId)
    {
        var current = currentValue.Trim();
        if (string.IsNullOrWhiteSpace(current))
        {
            return string.Empty;
        }

        if (!Regex.IsMatch(current, "^urn:[a-z0-9]+:[a-z0-9]+$", RegexOptions.IgnoreCase))
        {
            return current;
        }

        var token = NormalizeIdentifierToken(current.Split(':').LastOrDefault() ?? string.Empty);
        if (string.IsNullOrWhiteSpace(token))
        {
            token = NormalizeIdentifierToken(passportId.Split(':').LastOrDefault() ?? passportId);
        }

        if (string.IsNullOrWhiteSpace(token))
        {
            return string.Empty;
        }

        return $"SN-{token[..Math.Min(token.Length, 16)].ToUpperInvariant()}";
    }

    public static bool IsReasonableNumber(double value, double min, double max)
    {
        return !double.IsNaN(value)
            && !double.IsInfinity(value)
            && value >= min
            && value <= max;
    }

    private static string NormalizeIdentifierToken(string value)
    {
        var builder = new StringBuilder();
        foreach (var character in value.Trim().ToLowerInvariant())
        {
            if (character is >= 'a' and <= 'z' or >= '0' and <= '9')
            {
                builder.Append(character);
            }
        }

        return builder.ToString();
    }
}
