using System.Text.Json;
using MongoDB.Bson;

namespace BatteryPassWeb.Services;

public sealed class DemoRequiredDataCompletionService
{
    private static readonly IReadOnlyDictionary<string, string> PayloadPaths =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["generalProductInformation"] = Path.Combine("BatteryPass", "io.BatteryPass.GeneralProductInformation", "1.2.0", "gen", "GeneralProductInformation-payload.json"),
            ["carbonFootprintForBatteries"] = Path.Combine("BatteryPass", "io.BatteryPass.CarbonFootprint", "1.2.0", "gen", "CarbonFootprintForBatteries-payload.json"),
            ["circularity"] = Path.Combine("BatteryPass", "io.BatteryPass.Circularity", "1.2.0", "gen", "Circularity.json"),
            ["materialComposition"] = Path.Combine("BatteryPass", "io.BatteryPass.MaterialComposition", "1.2.0", "gen", "MaterialComposition-payload.json"),
            ["performanceAndDurability"] = Path.Combine("BatteryPass", "io.BatteryPass.Performance", "1.2.0", "gen", "PerformanceAndDurability-payload.json"),
            ["labeling"] = Path.Combine("BatteryPass", "io.BatteryPass.Labels", "1.2.0", "gen", "Labeling-payload.json"),
            ["supplyChainDueDiligence"] = Path.Combine("BatteryPass", "io.BatteryPass.SupplyChainDueDiligence", "1.2.0", "gen", "SupplyChainDueDiligence-payload.json")
        };

    private readonly SchemaRegistryService _schemaRegistryService;
    private readonly string _repoRoot;

    public DemoRequiredDataCompletionService(SchemaRegistryService schemaRegistryService)
    {
        _schemaRegistryService = schemaRegistryService;
        _repoRoot = ResolveRepoRoot(AppContext.BaseDirectory);
    }

    public BsonDocument CompleteRequiredData(BsonDocument passport, string completedAt = "")
    {
        var completed = passport.DeepClone().AsBsonDocument;
        var now = string.IsNullOrWhiteSpace(completedAt)
            ? DateTimeOffset.UtcNow.ToString("O")
            : completedAt;

        EnsureIdentityFields(completed, now);
        var aspects = EnsureDocument(completed, "aspects");
        foreach (var schema in _schemaRegistryService.ListSchemas())
        {
            var payload = LoadPayload(schema.AspectKey);
            ApplyReadableDemoValues(completed, schema.AspectKey, payload, now);

            var aspect = EnsureDocument(aspects, schema.AspectKey);
            aspect["payload"] = payload;
            aspect["visibility"] = aspect.GetValue("visibility", "public");

            var verification = EnsureDocument(aspect, "verification");
            verification["state"] = "draft";
            verification["signedAt"] = BsonNull.Value;
            if (!verification.Contains("issuer"))
            {
                verification["issuer"] = "did:web:acme.battery.pass:issuer";
            }
        }

        var registryInfo = EnsureDocument(completed, "registryInfo");
        registryInfo["status"] = "draft";
        registryInfo["updatedAt"] = now;
        return completed;
    }

    private BsonDocument LoadPayload(string aspectKey)
    {
        if (!PayloadPaths.TryGetValue(aspectKey, out var relativePath))
        {
            return new BsonDocument();
        }

        var path = Path.Combine(_repoRoot, relativePath);
        using var jsonDocument = JsonDocument.Parse(File.ReadAllText(path));
        return ToBsonDocument(jsonDocument.RootElement);
    }

    private static void EnsureIdentityFields(BsonDocument passport, string now)
    {
        var registryInfo = EnsureDocument(passport, "registryInfo");
        if (string.IsNullOrWhiteSpace(BsonHelpers.GetString(passport, "registryInfo", "registryId")))
        {
            registryInfo["registryId"] = Guid.NewGuid().ToString("N");
        }
        if (string.IsNullOrWhiteSpace(BsonHelpers.GetString(passport, "registryInfo", "createdAt")))
        {
            registryInfo["createdAt"] = now;
        }

        var app = EnsureDocument(passport, "app");
        var display = EnsureDocument(app, "display");
        SetIfEmpty(display, "modelNumber", "DemoPack-42");
        SetIfEmpty(display, "serialNumber", "DEMO-SERIAL-42");
        SetIfEmpty(display, "manufacturerName", "Demo Batteries GmbH");
        SetIfEmpty(display, "name", "Battery passport demonstration pack");
        SetIfEmpty(display, "facilityId", "DEMO-FACILITY-01");

        var media = EnsureDocument(app, "media");
        SetIfEmpty(media, "batteryImageUrl", "https://images.unsplash.com/photo-1558618666-fcd25c85cd64?auto=format&fit=crop&w=1200&q=80");
    }

    private static void ApplyReadableDemoValues(BsonDocument passport, string aspectKey, BsonDocument payload, string now)
    {
        switch (aspectKey)
        {
            case "generalProductInformation":
                ApplyGeneralProductInformation(passport, payload, now);
                break;
            case "performanceAndDurability":
                ApplyPerformanceAndDurability(payload, now);
                break;
            case "materialComposition":
                ApplyMaterialComposition(payload);
                break;
            case "circularity":
                ApplyCircularity(payload);
                break;
            case "carbonFootprintForBatteries":
                ApplyCarbonFootprint(payload);
                break;
        }
    }

    private static void ApplyGeneralProductInformation(BsonDocument passport, BsonDocument payload, string now)
    {
        var display = BsonHelpers.GetValue(passport, "app", "display") as BsonDocument ?? new BsonDocument();
        var serialNumber = BsonHelpers.GetString(passport, "app", "display", "serialNumber");
        payload["productIdentifier"] = BsonHelpers.GetString(passport, "app", "display", "modelNumber");
        payload["batteryPassportIdentifier"] = "urn:bmwk:123456687678";
        payload["batteryCategory"] = "lmt";
        payload["batteryStatus"] = "Original";
        payload["batteryMass"] = 699;
        payload["manufacturingDate"] = now;
        payload["puttingIntoService"] = now;

        var manufacturerInformation = EnsureDocument(payload, "manufacturerInformation");
        manufacturerInformation["contactName"] = BsonHelpers.GetString(passport, "app", "display", "manufacturerName");
        manufacturerInformation["identifier"] = string.IsNullOrWhiteSpace(serialNumber) ? "DEMO-SERIAL-42" : serialNumber;

        var operatorInformation = EnsureDocument(payload, "operatorInformation");
        operatorInformation["contactName"] = display.GetValue("manufacturerName", "Demo Batteries GmbH").ToString();
    }

    private static void ApplyPerformanceAndDurability(BsonDocument payload, string now)
    {
        var technical = EnsureDocument(payload, "batteryTechicalProperties");
        technical["ratedEnergy"] = 72.5;
        technical["ratedCapacity"] = 180.0;
        technical["ratedMaximumPower"] = 250.0;
        technical["nominalVoltage"] = 400.0;
        technical["expectedLifetime"] = 10;
        technical["expectedNumberOfCycles"] = 3000;
        technical["maximumVoltage"] = 440.0;
        technical["minimumVoltage"] = 320.0;
        technical["capacityThresholdForExhaustion"] = 70.0;
        technical["roundtripEfficiency"] = 92.5;
        technical["roundTripEfficiencyat50PerCentCycleLife"] = 90.0;
        technical["roundTripEfficiencyFade"] = 2.5;
        technical["powerFade"] = 3.5;
        technical["temperatureRangeIdleState"] = new BsonDocument
        {
            ["minimum"] = -20.0,
            ["maximum"] = 45.0
        };

        var condition = EnsureDocument(payload, "batteryCondition");
        EnsureMetric(condition, "energyThroughput", "energyThroughputValue", 15000.0, now);
        EnsureMetric(condition, "capacityThroughput", "capacityThroughputValue", 820.0, now);
        EnsureMetric(condition, "numberOfFullCycles", "numberOfFullCyclesValue", 120, now);
        EnsureMetric(condition, "stateOfCertifiedEnergy", "stateOfCertifiedEnergyValue", 98.0, now);
        EnsureMetric(condition, "capacityFade", "capacityFadeValue", 4.5, now);
        EnsureMetric(condition, "stateOfCharge", "stateOfChargeValue", 82.0, now);
        EnsureMetric(condition, "remainingEnergy", "remainingEnergyValue", 68.0, now);
        EnsureMetric(condition, "remainingCapacity", "remainingCapacityValue", 94.0, now);
        EnsureMetric(condition, "evolutionOfSelfDischarge", "evolutionOfSelfDischargeValue", 1.2, now);
        EnsureMetric(condition, "currentSelfDischargingRate", "currentSelfDischargingRateValue", 0.4, now);
        EnsureMetric(condition, "remainingRoundTripEnergyEfficiency", "remainingRoundTripEnergyEfficiencyValue", 90.0, now);
        condition["remainingPowerCapability"] = new BsonDocument
        {
            ["remainingPowerCapabilityValue"] = new BsonDocument
            {
                ["atSoC"] = 80.0,
                ["powerCapabilityAt"] = 230.0,
                ["rPCLastUpdated"] = now
            },
            ["lastUpdate"] = now
        };

        if (condition.GetValue("internalResistanceIncrease", new BsonArray()) is BsonArray resistanceValues)
        {
            foreach (var item in resistanceValues.OfType<BsonDocument>())
            {
                item["internalResistanceIncreaseValue"] = 0.04;
                item["lastUpdate"] = now;
                item["batteryComponent"] = "pack";
            }
        }
    }

    private static void ApplyMaterialComposition(BsonDocument payload)
    {
        if (payload.GetValue("batteryMaterials", new BsonArray()) is not BsonArray materials)
        {
            materials = new BsonArray();
            payload["batteryMaterials"] = materials;
        }

        var requiredMaterials = new[]
        {
            "Nickel",
            "Copper",
            "Aluminium",
            "Graphite",
            "Manganese",
            "Cobalt",
            "Lithium",
            "Electrolyte and separators"
        };

        foreach (var materialName in requiredMaterials)
        {
            if (!materials.OfType<BsonDocument>().Any(material => BsonHelpers.GetString(material, "batteryMaterialName").Equals(materialName, StringComparison.OrdinalIgnoreCase)))
            {
                materials.Add(new BsonDocument
                {
                    ["batteryMaterialName"] = materialName,
                    ["batteryMaterialMass"] = materialName.Equals("Electrolyte and separators", StringComparison.OrdinalIgnoreCase) ? 42.0 : 18.0,
                    ["batteryMaterialLocation"] = DemoMaterialLocation()
                });
            }
        }

        var index = 1;
        foreach (var material in materials.OfType<BsonDocument>())
        {
            if (material.GetValue("batteryMaterialLocation", BsonNull.Value) is not BsonDocument)
            {
                material["batteryMaterialLocation"] = DemoMaterialLocation();
            }
            material["batteryMaterialIdentifier"] = "7439-93-2";
            if (!material.Contains("isCriticalRawMaterial"))
            {
                material["isCriticalRawMaterial"] = index <= 3;
            }

            index++;
        }
    }

    private static BsonDocument DemoMaterialLocation()
    {
        return new BsonDocument
        {
            ["componentName"] = "Cell",
            ["componentId"] = "DEMO-CELL-01"
        };
    }

    private static void ApplyCircularity(BsonDocument payload)
    {
        payload["renewableContent"] = 38.0;
        var endOfLifeInformation = EnsureDocument(payload, "endOfLifeInformation");
        SetIfEmpty(endOfLifeInformation, "informationOnCollection", "https://example.test/battery-collection");
        SetIfEmpty(endOfLifeInformation, "separateCollection", "https://example.test/separate-collection");
        SetIfEmpty(endOfLifeInformation, "wastePrevention", "https://example.test/waste-prevention");

        var recycledContent = payload.GetValue("recycledContent", new BsonArray()) as BsonArray ?? new BsonArray();
        payload["recycledContent"] = recycledContent;
        foreach (var material in new[] { "Nickel", "Cobalt", "Lithium", "Lead" })
        {
            var existing = recycledContent
                .OfType<BsonDocument>()
                .FirstOrDefault(row => BsonHelpers.GetString(row, "recycledMaterial").Equals(material, StringComparison.OrdinalIgnoreCase));
            if (existing == null)
            {
                recycledContent.Add(new BsonDocument
                {
                    ["recycledMaterial"] = material,
                    ["preConsumerShare"] = 18.0,
                    ["postConsumerShare"] = 12.0
                });
            }
            else
            {
                existing["preConsumerShare"] = existing.GetValue("preConsumerShare", 18.0);
                existing["postConsumerShare"] = existing.GetValue("postConsumerShare", 12.0);
            }
        }
    }

    private static void ApplyCarbonFootprint(BsonDocument payload)
    {
        payload["batteryCarbonFootprint"] = 68.0;
        payload["absoluteCarbonFootprint"] = 4930.0;
        payload["carbonFootprintPerformanceClass"] = "B";
        payload["carbonFootprintStudy"] = "https://example.test/carbon-footprint-study";

        var rows = payload.GetValue("carbonFootprintPerLifecycleStage", new BsonArray()) as BsonArray ?? new BsonArray();
        payload["carbonFootprintPerLifecycleStage"] = rows;
        var stages = new[]
        {
            ("RawMaterialExtraction", 21.0),
            ("MainProduction", 31.0),
            ("Distribution", 9.0),
            ("Recycling", 7.0)
        };

        foreach (var (stage, value) in stages)
        {
            var existing = rows
                .OfType<BsonDocument>()
                .FirstOrDefault(row => BsonHelpers.GetString(row, "lifecycleStage").Equals(stage, StringComparison.OrdinalIgnoreCase));
            if (existing == null)
            {
                rows.Add(new BsonDocument
                {
                    ["lifecycleStage"] = stage,
                    ["carbonFootprint"] = value
                });
            }
            else
            {
                existing["carbonFootprint"] = value;
            }
        }
    }

    private static void EnsureMetric(BsonDocument parent, string key, string valueKey, double value, string now)
    {
        var metric = EnsureDocument(parent, key);
        metric[valueKey] = value;
        metric["lastUpdate"] = now;
    }

    private static void SetIfEmpty(BsonDocument document, string key, string value)
    {
        if (!document.Contains(key) || string.IsNullOrWhiteSpace(document.GetValue(key, string.Empty).ToString()))
        {
            document[key] = value;
        }
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

    private static BsonDocument ToBsonDocument(JsonElement element)
    {
        var document = new BsonDocument();
        foreach (var property in element.EnumerateObject())
        {
            document[property.Name] = ToBsonValue(property.Value);
        }

        return document;
    }

    private static BsonArray ToBsonArray(JsonElement element)
    {
        var array = new BsonArray();
        foreach (var item in element.EnumerateArray())
        {
            array.Add(ToBsonValue(item));
        }

        return array;
    }

    private static BsonValue ToBsonValue(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.Object => ToBsonDocument(element),
            JsonValueKind.Array => ToBsonArray(element),
            JsonValueKind.String => element.GetString() ?? string.Empty,
            JsonValueKind.Number when element.TryGetInt32(out var intValue) => intValue,
            JsonValueKind.Number when element.TryGetInt64(out var longValue) => longValue,
            JsonValueKind.Number when element.TryGetDouble(out var doubleValue) => doubleValue,
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            _ => BsonNull.Value
        };
    }

    private static string ResolveRepoRoot(string start)
    {
        var directory = new DirectoryInfo(start);
        while (directory != null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "README.md"))
                && Directory.Exists(Path.Combine(directory.FullName, "BatteryPass"))
                && Directory.Exists(Path.Combine(directory.FullName, "web")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        return Directory.GetCurrentDirectory();
    }
}
