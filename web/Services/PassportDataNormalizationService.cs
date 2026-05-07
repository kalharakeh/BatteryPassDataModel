using BatteryPassWeb.Models.Trust;
using MongoDB.Bson;

namespace BatteryPassWeb.Services;

public sealed class PassportDataNormalizationService
{
    public BsonDocument Normalize(BsonDocument passport, string normalizedAt = "")
    {
        var normalized = passport.DeepClone().AsBsonDocument;
        var before = normalized.ToJson();
        var now = string.IsNullOrWhiteSpace(normalizedAt)
            ? DateTime.UtcNow.ToString("O")
            : normalizedAt;

        var app = EnsureDocument(normalized, "app");
        var aspects = EnsureDocument(normalized, "aspects");
        var passportId = BsonHelpers.GetString(normalized, "passportId");
        var chartMaterialMasses = ReadChartMaterialMasses(GetDocument(app.GetValue("charts", new BsonDocument())));

        NormalizeDisplay(app, passportId);
        NormalizeGeneralProductInformation(aspects, app, passportId);
        NormalizeMaterialComposition(aspects, chartMaterialMasses);
        NormalizeCarbonFootprint(aspects);
        NormalizeSupplyChain(aspects);
        RemoveDuplicatedDisplayCharts(app);

        var afterCanonical = normalized.ToJson();
        if (!string.Equals(before, afterCanonical, StringComparison.Ordinal))
        {
            var registryInfo = EnsureDocument(normalized, "registryInfo");
            registryInfo["updatedAt"] = now;
            MarkDirtyIfSigned(normalized, now);
        }

        return normalized;
    }

    private static void NormalizeDisplay(BsonDocument app, string passportId)
    {
        var display = EnsureDocument(app, "display");
        display["serialNumber"] = BatteryPassCanonicalDataCatalog.NormalizeManufacturerSerialNumber(
            BsonHelpers.GetString(display, "serialNumber"),
            passportId);
    }

    private static void NormalizeGeneralProductInformation(BsonDocument aspects, BsonDocument app, string passportId)
    {
        var aspect = EnsureDocument(aspects, "generalProductInformation");
        var payload = EnsureDocument(aspect, "payload");
        var display = GetDocument(app.GetValue("display", new BsonDocument()));

        payload["batteryPassportIdentifier"] = BatteryPassCanonicalDataCatalog.NormalizeBatteryPassportIdentifier(
            BsonHelpers.GetString(payload, "batteryPassportIdentifier"),
            BsonHelpers.GetString(display, "serialNumber"),
            passportId);
        payload["batteryCategory"] = BatteryPassCanonicalDataCatalog.NormalizeBatteryCategory(
            BsonHelpers.GetString(payload, "batteryCategory"));
    }

    private static void NormalizeMaterialComposition(
        BsonDocument aspects,
        IReadOnlyDictionary<string, double> preferredMassesByLabel)
    {
        var aspect = EnsureDocument(aspects, "materialComposition");
        var payload = EnsureDocument(aspect, "payload");
        var existingRows = payload.GetValue("batteryMaterials", new BsonArray()) as BsonArray ?? new BsonArray();
        var existingByLabel = existingRows
            .OfType<BsonDocument>()
            .Where(row => !string.IsNullOrWhiteSpace(BsonHelpers.GetString(row, "batteryMaterialName")))
            .GroupBy(row => BsonHelpers.GetString(row, "batteryMaterialName"), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);

        var labels = preferredMassesByLabel.Count > 0
            ? preferredMassesByLabel.Keys.ToList()
            : BatteryPassCanonicalDataCatalog.Materials.Select(material => material.Label).ToList();

        var rows = new BsonArray();
        foreach (var label in labels)
        {
            var definition = BatteryPassCanonicalDataCatalog.MaterialByLabel(label);
            var existing = existingByLabel.TryGetValue(label, out var found)
                ? found.DeepClone().AsBsonDocument
                : new BsonDocument();
            var rawMass = preferredMassesByLabel.TryGetValue(label, out var preferredMass)
                ? preferredMass
                : ReadNumber(existing.GetValue("batteryMaterialMass", definition.DemoMassKg), definition.DemoMassKg);

            existing["batteryMaterialName"] = label;
            existing["batteryMaterialMass"] = BatteryPassCanonicalDataCatalog.NormalizeMaterialMass(label, rawMass);
            existing["batteryMaterialIdentifier"] = existing.GetValue("batteryMaterialIdentifier", "7439-93-2");
            existing["batteryMaterialLocation"] = EnsureMaterialLocation(existing);
            existing["isCriticalRawMaterial"] = definition.IsCriticalRawMaterial;
            rows.Add(existing);
        }

        payload["batteryMaterials"] = rows;
    }

    private static void NormalizeCarbonFootprint(BsonDocument aspects)
    {
        var aspect = EnsureDocument(aspects, "carbonFootprintForBatteries");
        var payload = EnsureDocument(aspect, "payload");

        payload["batteryCarbonFootprint"] = BatteryPassCanonicalDataCatalog.NormalizeCarbonFootprint(
            ReadNumber(payload.GetValue("batteryCarbonFootprint", BatteryPassCanonicalDataCatalog.DemoCarbonFootprint), BatteryPassCanonicalDataCatalog.DemoCarbonFootprint));
        payload["carbonFootprintPerformanceClass"] = BatteryPassCanonicalDataCatalog.NormalizePerformanceClass(
            ValueText(payload.GetValue("carbonFootprintPerformanceClass", BatteryPassCanonicalDataCatalog.DemoPerformanceClass)));

        var existingRows = payload.GetValue("carbonFootprintPerLifecycleStage", new BsonArray()) as BsonArray ?? new BsonArray();
        var existingByStage = existingRows
            .OfType<BsonDocument>()
            .Where(row => !string.IsNullOrWhiteSpace(BsonHelpers.GetString(row, "lifecycleStage")))
            .GroupBy(row => BsonHelpers.GetString(row, "lifecycleStage"), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);

        var rows = new BsonArray();
        foreach (var stage in BatteryPassCanonicalDataCatalog.CarbonStages)
        {
            var row = existingByStage.TryGetValue(stage.Stage, out var found)
                ? found.DeepClone().AsBsonDocument
                : new BsonDocument();
            var rawValue = ReadNumber(row.GetValue("carbonFootprint", stage.DemoValue), stage.DemoValue);
            row["lifecycleStage"] = stage.Stage;
            row["carbonFootprint"] = BatteryPassCanonicalDataCatalog.NormalizeCarbonStageValue(stage.Stage, rawValue);
            rows.Add(row);
        }

        payload["carbonFootprintPerLifecycleStage"] = rows;
    }

    private static void NormalizeSupplyChain(BsonDocument aspects)
    {
        var aspect = EnsureDocument(aspects, "supplyChainDueDiligence");
        var payload = EnsureDocument(aspect, "payload");
        payload["supplyChainIndicies"] = BatteryPassCanonicalDataCatalog.NormalizeSupplyChainIndex(
            ReadNumber(payload.GetValue("supplyChainIndicies", BatteryPassCanonicalDataCatalog.DemoSupplyChainIndex), BatteryPassCanonicalDataCatalog.DemoSupplyChainIndex));
    }

    private static IReadOnlyDictionary<string, double> ReadChartMaterialMasses(BsonDocument appCharts)
    {
        if (appCharts.GetValue("materialComposition", new BsonArray()) is not BsonArray materialChart)
        {
            return new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        }

        var values = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in materialChart.OfType<BsonDocument>())
        {
            var label = BsonHelpers.GetString(item, "label");
            if (string.IsNullOrWhiteSpace(label))
            {
                continue;
            }

            var value = BatteryPassCanonicalDataCatalog.NormalizeMaterialMass(
                label,
                ReadNumber(item.GetValue("value", BatteryPassCanonicalDataCatalog.MaterialByLabel(label).DemoMassKg), 0));
            values[label] = value;
        }

        return values;
    }

    private static BsonDocument EnsureMaterialLocation(BsonDocument material)
    {
        if (material.GetValue("batteryMaterialLocation", BsonNull.Value) is BsonDocument location)
        {
            if (!location.Contains("componentName"))
            {
                location["componentName"] = "Cell";
            }

            if (!location.Contains("componentId"))
            {
                location["componentId"] = "DEMO-CELL-01";
            }

            return location;
        }

        return new BsonDocument
        {
            ["componentName"] = "Cell",
            ["componentId"] = "DEMO-CELL-01"
        };
    }

    private static void RemoveDuplicatedDisplayCharts(BsonDocument app)
    {
        if (app.GetValue("charts", BsonNull.Value) is not BsonDocument charts)
        {
            return;
        }

        charts.Remove("materialComposition");
        charts.Remove("carbonFootprint");
        charts.Remove("recycledContent");
        if (!charts.Any())
        {
            app.Remove("charts");
        }
    }

    private static void MarkDirtyIfSigned(BsonDocument passport, string now)
    {
        var trust = EnsureDocument(passport, "trust");
        var latestProof = GetDocument(trust.GetValue("latestProof", new BsonDocument()));
        var hasSignedProof = !string.IsNullOrWhiteSpace(BsonHelpers.GetString(trust, "latestHash"))
            || !string.IsNullOrWhiteSpace(BsonHelpers.GetString(latestProof, "proofValue"));
        if (!hasSignedProof)
        {
            return;
        }

        trust["state"] = TrustState.Dirty;
        trust["isDirty"] = true;
        trust["dirtyAt"] = now;
        trust["dirtyReason"] = "canonicalDataNormalized";
    }

    private static BsonDocument EnsureDocument(BsonDocument parent, string key)
    {
        if (!parent.TryGetValue(key, out var value) || value is not BsonDocument document)
        {
            document = new BsonDocument();
            parent[key] = document;
        }

        return document;
    }

    private static BsonDocument GetDocument(BsonValue? value)
    {
        return value is BsonDocument document ? document : new BsonDocument();
    }

    private static double ReadNumber(BsonValue value, double fallback)
    {
        if (value.IsNumeric)
        {
            return value.ToDouble();
        }

        return double.TryParse(value.ToString(), out var parsed) ? parsed : fallback;
    }

    private static string ValueText(BsonValue? value)
    {
        return value == null || value.IsBsonNull ? string.Empty : value.ToString() ?? string.Empty;
    }
}
