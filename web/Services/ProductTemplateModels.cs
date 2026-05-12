using BatteryPassWeb.Models.Trust;
using System.Security.Cryptography;
using System.Text;
using MongoDB.Bson;

namespace BatteryPassWeb.Services;

public sealed record BatteryProductTemplate(
    string ProductId,
    string ProductName,
    string Description,
    string ImageUrl,
    int ModuleCount,
    double BatteryMassKg,
    double RatedEnergyKwh,
    double RatedCapacityAh,
    double RatedMaximumPowerKw,
    double NominalVoltageV,
    double ExpectedLifetimeYears,
    double ExpectedCycles,
    double SupplyChainIndex,
    double CarbonFootprint,
    string PerformanceClass,
    IReadOnlyDictionary<string, double> MaterialMassesKg,
    IReadOnlyDictionary<string, double> CarbonStages,
    IReadOnlyDictionary<string, ProductTemplateRecycledContent> RecycledContent,
    IReadOnlyList<ProductTemplateDocumentSeed> TemplateDocuments,
    IReadOnlyList<string> RequiredFieldKeys)
{
    public IReadOnlyList<BatteryProductVersion> ProductVersions { get; init; } = [];

    public BatteryProductVersion LatestProductVersion =>
        ProductVersions.FirstOrDefault()
        ?? new BatteryProductVersion(
            "1.0",
            BatteryMassKg,
            RatedEnergyKwh,
            RatedCapacityAh,
            RatedMaximumPowerKw,
            NominalVoltageV,
            ExpectedLifetimeYears,
            ExpectedCycles,
            SupplyChainIndex,
            CarbonFootprint,
            PerformanceClass,
            MaterialMassesKg,
            CarbonStages,
            RecycledContent,
            BatteryProductTemplateCatalog.DefaultSoftwareVersion,
            string.Empty,
            string.Empty,
            TemplateDocuments,
            RequiredFieldKeys);
}

public sealed record BatteryProductVersion(
    string Version,
    double BatteryMassKg,
    double RatedEnergyKwh,
    double RatedCapacityAh,
    double RatedMaximumPowerKw,
    double NominalVoltageV,
    double ExpectedLifetimeYears,
    double ExpectedCycles,
    double SupplyChainIndex,
    double CarbonFootprint,
    string PerformanceClass,
    IReadOnlyDictionary<string, double> MaterialMassesKg,
    IReadOnlyDictionary<string, double> CarbonStages,
    IReadOnlyDictionary<string, ProductTemplateRecycledContent> RecycledContent,
    string SoftwareVersion,
    string SoftwareReleaseDate,
    string SoftwareLatestUpdate,
    IReadOnlyList<ProductTemplateDocumentSeed> TemplateDocuments,
    IReadOnlyList<string> RequiredFieldKeys);

public sealed record ProductTemplateDocumentSeed(
    string DocumentKey,
    string Label,
    string FileName,
    string Visibility);

public sealed record ProductTemplateRecycledContent(
    double PreConsumerShare,
    double PostConsumerShare);

public sealed class ProductTemplateBatteryIdentity
{
    public string ModelNumber { get; init; } = string.Empty;
    public string SerialNumber { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public string FacilityId { get; init; } = string.Empty;
    public string ClusterId { get; init; } = string.Empty;
    public string ManufacturingDate { get; init; } = string.Empty;
}

public sealed class ProductTemplateSafeUpdateResult
{
    public BsonDocument UpdatedPassport { get; init; } = new();
    public IReadOnlyList<string> UpdatedPaths { get; init; } = [];
    public IReadOnlyList<string> SkippedOverridePaths { get; init; } = [];
}

public sealed class ProductTemplatePushResult
{
    public int MatchedBatteries { get; init; }
    public int UpdatedBatteries { get; init; }
    public int SkippedBatteries { get; init; }
    public IReadOnlyList<string> UpdatedPassportIds { get; init; } = [];
    public IReadOnlyList<string> SkippedOverridePaths { get; init; } = [];
}

public sealed record ProductTemplateResetResult(
    int PassportCount,
    IReadOnlyList<string> PassportIds,
    string ResetAt)
{
    public int BatteryCount { get; init; }
    public IReadOnlyList<string> BatteryIds { get; init; } = [];
}

public static class BatteryProductTemplateCatalog
{
    public const string DefaultProductId = "compact-7m";
    public const string DefaultSoftwareVersion = "1.0";

    private static readonly IReadOnlyList<ProductTemplateDocumentSeed> DefaultDocuments =
    [
        new("conformityAssessment", "Conformity assessment", "01-conformity-assessment-report.pdf", "public"),
        new("euDeclarationOfConformity", "EU declaration of conformity ID", "02-eu-declaration-of-conformity.pdf", "public"),
        new("sustainabilityReport", "Sustainability report", "03-sustainability-report.pdf", "private"),
        new("dueDiligenceReport", "Due diligence report", "04-due-diligence-report.pdf", "private"),
        new("thirdPartyAudit", "Third party audit", "05-third-party-audit.pdf", "private"),
        new("taxonomyReport", "Taxonomy report", "06-taxonomy-report.pdf", "private"),
        new("co2StudyReference", "CO2 study reference", "07-co2-study-reference.pdf", "public")
    ];

    private static readonly IReadOnlyList<string> DefaultRequiredFields =
    [
        "general.passportId",
        "general.product",
        "general.productVersion",
        "general.softwareVersion",
        "general.serialNumber",
        "general.category",
        "general.batteryStatus",
        "general.batteryMass",
        "general.manufacturingDate",
        "general.facilityId",
        "general.manufacturerName",
        "general.registryStatus",
        "material.nickelMass",
        "material.copperMass",
        "material.aluminiumMass",
        "material.graphiteMass",
        "material.manganeseMass",
        "material.cobaltMass",
        "material.lithiumMass",
        "material.electrolyteMass",
        "performance.ratedEnergy",
        "performance.ratedCapacity",
        "performance.ratedMaximumPower",
        "performance.nominalVoltage",
        "performance.expectedLifetime",
        "performance.expectedNumberOfCycles",
        "performance.stateOfCharge",
        "performance.remainingCapacity",
        "performance.remainingEnergy",
        "performance.fullCycles",
        "compliance.conformityAssessment",
        "compliance.euDeclarationOfConformity",
        "supplyChain.supplyChainIndex",
        "supplyChain.sustainabilityReport",
        "supplyChain.dueDiligenceReport",
        "supplyChain.thirdPartyAudit",
        "supplyChain.taxonomyReport",
        "circularity.separateCollection",
        "circularity.wastePrevention",
        "circularity.recycledNickelPre",
        "circularity.recycledNickelPost",
        "circularity.recycledCobaltPre",
        "circularity.recycledCobaltPost",
        "circularity.recycledLithiumPre",
        "circularity.recycledLithiumPost",
        "circularity.recycledLeadPre",
        "circularity.recycledLeadPost",
        "carbon.amount",
        "carbon.performanceClass",
        "carbon.rawMaterial",
        "carbon.mainProduction",
        "carbon.distribution",
        "carbon.recycling",
        "carbon.co2StudyReference"
    ];

    public static IReadOnlyList<BatteryProductTemplate> DefaultProducts { get; } =
    [
        Product(
            "compact-7m",
            "Compact 7M",
            "Seven-module compact industrial battery pack.",
            BatteryImageCatalog.Compact7ImageUrl,
            moduleCount: 7,
            batteryMassKg: 320,
            ratedEnergyKwh: 56,
            ratedCapacityAh: 140,
            ratedMaximumPowerKw: 180,
            nominalVoltageV: 400,
            expectedCycles: 3000,
            supplyChainIndex: 82,
            carbonFootprint: 64,
            performanceClass: "B",
            materialScale: 1.0,
            carbonScale: 1.0,
            recycledProfile: "standard"),
        Product(
            "compact-13m",
            "Compact 13M",
            "Thirteen-module compact industrial battery pack with the same architecture as Compact 7M.",
            BatteryImageCatalog.Compact13ImageUrl,
            moduleCount: 13,
            batteryMassKg: 565,
            ratedEnergyKwh: 104,
            ratedCapacityAh: 260,
            ratedMaximumPowerKw: 320,
            nominalVoltageV: 720,
            expectedCycles: 3000,
            supplyChainIndex: 84,
            carbonFootprint: 67,
            performanceClass: "B",
            materialScale: 13.0 / 7.0,
            carbonScale: 1.06,
            recycledProfile: "compact13"),
        Product(
            "core",
            "Core",
            "Core industrial battery product for higher-duty customer applications.",
            BatteryImageCatalog.CoreImageUrl,
            moduleCount: 10,
            batteryMassKg: 470,
            ratedEnergyKwh: 88,
            ratedCapacityAh: 220,
            ratedMaximumPowerKw: 280,
            nominalVoltageV: 640,
            expectedCycles: 3500,
            supplyChainIndex: 88,
            carbonFootprint: 59,
            performanceClass: "A",
            materialScale: 1.45,
            carbonScale: 0.92,
            recycledProfile: "core")
    ];

    public static BatteryProductTemplate DefaultProduct =>
        DefaultProducts.First(product => product.ProductId == DefaultProductId);

    public static BatteryProductTemplate? FindProduct(string productId)
    {
        return DefaultProducts.FirstOrDefault(product => product.ProductId.Equals(productId, StringComparison.OrdinalIgnoreCase));
    }

    private static BatteryProductTemplate Product(
        string productId,
        string productName,
        string description,
        string imageUrl,
        int moduleCount,
        double batteryMassKg,
        double ratedEnergyKwh,
        double ratedCapacityAh,
        double ratedMaximumPowerKw,
        double nominalVoltageV,
        double expectedCycles,
        double supplyChainIndex,
        double carbonFootprint,
        string performanceClass,
        double materialScale,
        double carbonScale,
        string recycledProfile)
    {
        var materialMasses = BatteryPassCanonicalDataCatalog.Materials
            .ToDictionary(
                material => material.Label,
                material => Math.Round(material.DemoMassKg * materialScale, 2),
                StringComparer.OrdinalIgnoreCase);
        var carbonStages = BatteryPassCanonicalDataCatalog.CarbonStages
            .ToDictionary(
                stage => stage.Stage,
                stage => Math.Round(stage.DemoValue * carbonScale, 2),
                StringComparer.OrdinalIgnoreCase);

        var latestSoftware = BuildSoftwareParameters(productId, "2.0");
        var firstSoftware = BuildSoftwareParameters(productId, "1.0");
        var latestVersion = new BatteryProductVersion(
            "2.0",
            batteryMassKg,
            ratedEnergyKwh,
            ratedCapacityAh,
            ratedMaximumPowerKw,
            nominalVoltageV,
            ExpectedLifetimeYears: 10,
            expectedCycles,
            supplyChainIndex,
            carbonFootprint,
            performanceClass,
            materialMasses,
            carbonStages,
            BuildRecycledContent(recycledProfile),
            latestSoftware.Version,
            latestSoftware.ReleaseDate,
            latestSoftware.LatestUpdate,
            DefaultDocuments,
            DefaultRequiredFields);
        var firstVersion = new BatteryProductVersion(
            "1.0",
            Math.Round(batteryMassKg * 0.96, 2),
            Math.Round(ratedEnergyKwh * 0.95, 2),
            Math.Round(ratedCapacityAh * 0.95, 2),
            Math.Round(ratedMaximumPowerKw * 0.94, 2),
            nominalVoltageV,
            ExpectedLifetimeYears: 9,
            Math.Round(expectedCycles * 0.9, 0),
            Math.Max(0, supplyChainIndex - 3),
            Math.Round(carbonFootprint * 1.04, 2),
            performanceClass,
            materialMasses.ToDictionary(pair => pair.Key, pair => Math.Round(pair.Value * 0.96, 2), StringComparer.OrdinalIgnoreCase),
            carbonStages.ToDictionary(pair => pair.Key, pair => Math.Round(pair.Value * 1.04, 2), StringComparer.OrdinalIgnoreCase),
            BuildRecycledContent(recycledProfile),
            firstSoftware.Version,
            firstSoftware.ReleaseDate,
            firstSoftware.LatestUpdate,
            DefaultDocuments,
            DefaultRequiredFields);

        return new BatteryProductTemplate(
            productId,
            productName,
            description,
            imageUrl,
            moduleCount,
            batteryMassKg,
            ratedEnergyKwh,
            ratedCapacityAh,
            ratedMaximumPowerKw,
            nominalVoltageV,
            ExpectedLifetimeYears: 10,
            expectedCycles,
            supplyChainIndex,
            carbonFootprint,
            performanceClass,
            materialMasses,
            carbonStages,
            BuildRecycledContent(recycledProfile),
            DefaultDocuments,
            DefaultRequiredFields)
        {
            ProductVersions = [latestVersion, firstVersion]
        };
    }

    private static IReadOnlyDictionary<string, ProductTemplateRecycledContent> BuildRecycledContent(string profile)
    {
        var values = profile switch
        {
            "compact13" => new[]
            {
                ("Nickel", 19.0, 13.0),
                ("Cobalt", 17.0, 11.0),
                ("Lithium", 15.0, 10.0),
                ("Lead", 21.0, 16.0)
            },
            "core" => new[]
            {
                ("Nickel", 24.0, 16.0),
                ("Cobalt", 20.0, 14.0),
                ("Lithium", 18.0, 12.0),
                ("Lead", 26.0, 18.0)
            },
            _ => new[]
            {
                ("Nickel", 18.0, 12.0),
                ("Cobalt", 16.0, 10.0),
                ("Lithium", 14.0, 9.0),
                ("Lead", 20.0, 15.0)
            }
        };

        return values.ToDictionary(
            row => row.Item1,
            row => new ProductTemplateRecycledContent(row.Item2, row.Item3),
            StringComparer.OrdinalIgnoreCase);
    }

    private static (string Version, string ReleaseDate, string LatestUpdate) BuildSoftwareParameters(string productId, string productVersion)
    {
        var prefix = productId switch
        {
            "compact-13m" => ("2026-01-10", "2026-04-20", "2026-05-03", "2026-05-09"),
            "core" => ("2026-01-20", "2026-04-22", "2026-05-04", "2026-05-09"),
            _ => ("2026-01-05", "2026-04-18", "2026-05-02", "2026-05-09")
        };

        return productVersion.Equals("2.0", StringComparison.OrdinalIgnoreCase)
            ? ("4.0", prefix.Item3, prefix.Item4)
            : ("2.0", prefix.Item1, prefix.Item2);
    }
}

public static class ProductTemplatePassportBuilder
{
    private static readonly string[] BatterySpecificPaths =
    [
        "passportId",
        "clusterId",
        "registryInfo",
        "validation",
        "trust",
        "app.display.name",
        "app.display.modelNumber",
        "app.display.serialNumber",
        "app.display.facilityId",
        "app.operations",
        "aspects.generalProductInformation.payload.productIdentifier",
        "aspects.generalProductInformation.payload.batteryPassportIdentifier",
        "aspects.generalProductInformation.payload.manufacturingDate",
        "aspects.generalProductInformation.payload.puttingIntoService",
        "aspects.generalProductInformation.payload.manufacturerInformation.identifier"
    ];

    public static BsonDocument BuildPassportFromTemplate(
        string passportId,
        BatteryProductTemplate product,
        ProductTemplateBatteryIdentity identity,
        string now)
    {
        return BuildPassportFromTemplate(passportId, product, product.LatestProductVersion, identity, now);
    }

    public static BsonDocument BuildPassportFromTemplate(
        string passportId,
        BatteryProductTemplate product,
        BatteryProductVersion productVersion,
        ProductTemplateBatteryIdentity identity,
        string now)
    {
        var normalizedNow = string.IsNullOrWhiteSpace(now) ? DateTimeOffset.UtcNow.ToString("O") : now;
        var manufacturingDate = string.IsNullOrWhiteSpace(identity.ManufacturingDate)
            ? string.Empty
            : identity.ManufacturingDate.Length >= 10
                ? identity.ManufacturingDate[..10]
                : identity.ManufacturingDate;

        var basePassport = new BsonDocument
        {
            ["passportId"] = passportId,
            ["clusterId"] = identity.ClusterId,
            ["registryInfo"] = new BsonDocument
            {
                ["registryId"] = Guid.NewGuid().ToString("N"),
                ["status"] = "draft",
                ["createdAt"] = normalizedNow,
                ["updatedAt"] = normalizedNow
            },
            ["app"] = new BsonDocument
            {
                ["display"] = new BsonDocument
                {
                    ["name"] = identity.DisplayName,
                    ["modelNumber"] = identity.ModelNumber,
                    ["serialNumber"] = identity.SerialNumber,
                    ["manufacturerName"] = "Scania Industrial Batteries",
                    ["facilityId"] = identity.FacilityId
                },
                ["media"] = new BsonDocument
                {
                    ["batteryImageUrl"] = product.ImageUrl
                }
            },
            ["aspects"] = new BsonDocument()
        };

        var completed = new DemoRequiredDataCompletionService(new SchemaRegistryService())
            .CompleteRequiredData(basePassport, normalizedNow);
        completed["passportId"] = passportId;
        completed["clusterId"] = identity.ClusterId;
        var registryInfo = EnsureDocument(completed, "registryInfo");
        registryInfo["status"] = "draft";
        registryInfo["createdAt"] = normalizedNow;
        registryInfo["updatedAt"] = normalizedNow;

        ApplyBatteryIdentity(completed, product, identity, manufacturingDate);
        ApplyProductTemplateValues(completed, product, productVersion, normalizedNow);

        var baseline = BuildTemplateBaseline(completed);
        EnsureDocument(EnsureDocument(completed, "app"), "templateBaseline").Clear();
        completed["app"]["templateBaseline"] = baseline;

        completed["validation"] = new BsonDocument
        {
            ["isValid"] = false,
            ["status"] = "unvalidated",
            ["signedAt"] = BsonNull.Value
        };
        completed["trust"] = new BsonDocument
        {
            ["state"] = TrustState.Unvalidated,
            ["isDirty"] = false,
            ["latestHash"] = string.Empty,
            ["latestProof"] = new BsonDocument()
        };
        completed.Remove("_id");
        return completed;
    }

    public static BsonDocument BuildBatteryFromTemplate(
        string batteryId,
        BatteryProductTemplate product,
        BatteryProductVersion productVersion,
        ProductTemplateBatteryIdentity identity,
        string now)
    {
        var normalizedNow = string.IsNullOrWhiteSpace(now) ? DateTimeOffset.UtcNow.ToString("O") : now;
        var battery = BuildPassportFromTemplate(batteryId, product, productVersion, identity, normalizedNow);
        var display = EnsureDocument(EnsureDocument(battery, "app"), "display");
        var productNode = EnsureDocument(EnsureDocument(battery, "app"), "product");

        battery["batteryId"] = batteryId;
        battery["clusterId"] = identity.ClusterId;
        battery["identity"] = new BsonDocument
        {
            ["batteryFamily"] = product.ProductName,
            ["batteryModel"] = productVersion.Version,
            ["modelNumber"] = identity.ModelNumber,
            ["serialNumber"] = BsonHelpers.GetString(display, "serialNumber"),
            ["displayName"] = identity.DisplayName,
            ["facilityId"] = identity.FacilityId,
            ["productId"] = product.ProductId,
            ["productVersion"] = productVersion.Version,
            ["softwareVersion"] = productVersion.SoftwareVersion
        };
        battery["createdAt"] = normalizedNow;
        battery["updatedAt"] = normalizedNow;
        battery["updatedBy"] = "product-template-reset";
        productNode["batteryModel"] = productVersion.Version;

        battery.Remove("passportId");
        battery.Remove("registryInfo");
        battery.Remove("validation");
        battery.Remove("trust");
        battery.Remove("_id");
        return battery;
    }

    public static BsonDocument BuildTemplateBaseline(BsonDocument passport)
    {
        var baseline = new BsonDocument();
        var flattened = new Dictionary<string, BsonValue>(StringComparer.OrdinalIgnoreCase);
        Flatten(passport, string.Empty, flattened);
        foreach (var (path, value) in flattened)
        {
            if (IsBatterySpecificPath(path))
            {
                continue;
            }

            SetValue(baseline, path, value.DeepClone());
        }

        return baseline;
    }

    public static ProductTemplateSafeUpdateResult ComputeSafeTemplateUpdates(
        BsonDocument passport,
        BsonDocument oldTemplate,
        BsonDocument newTemplate)
    {
        var updatedPassport = passport.DeepClone().AsBsonDocument;
        var updatedPaths = new List<string>();
        var skippedPaths = new List<string>();
        var newValues = new Dictionary<string, BsonValue>(StringComparer.OrdinalIgnoreCase);
        Flatten(newTemplate, string.Empty, newValues);

        foreach (var (path, newValue) in newValues.OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase))
        {
            if (IsBatterySpecificPath(path))
            {
                continue;
            }

            var oldValue = GetValue(oldTemplate, path);
            var currentValue = GetValue(updatedPassport, path);
            if (BsonEquals(currentValue, oldValue))
            {
                SetValue(updatedPassport, path, newValue.DeepClone());
                updatedPaths.Add(path);
            }
            else
            {
                skippedPaths.Add(path);
            }
        }

        SetValue(updatedPassport, "app.templateBaseline", newTemplate.DeepClone());
        return new ProductTemplateSafeUpdateResult
        {
            UpdatedPassport = updatedPassport,
            UpdatedPaths = updatedPaths,
            SkippedOverridePaths = skippedPaths
        };
    }

    public static string ComputeTemplateHash(BsonDocument template)
    {
        var json = template.ToJson(new MongoDB.Bson.IO.JsonWriterSettings { OutputMode = MongoDB.Bson.IO.JsonOutputMode.CanonicalExtendedJson });
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(json))).ToLowerInvariant();
    }

    public static BsonDocument ToProductDocument(BatteryProductTemplate product, string updatedAt, string updatedBy)
    {
        return new BsonDocument
        {
            ["productId"] = product.ProductId,
            ["productName"] = product.ProductName,
            ["description"] = product.Description,
            ["imageUrl"] = product.ImageUrl,
            ["moduleCount"] = product.ModuleCount,
            ["batteryMassKg"] = product.BatteryMassKg,
            ["ratedEnergyKwh"] = product.RatedEnergyKwh,
            ["ratedCapacityAh"] = product.RatedCapacityAh,
            ["ratedMaximumPowerKw"] = product.RatedMaximumPowerKw,
            ["nominalVoltageV"] = product.NominalVoltageV,
            ["expectedLifetimeYears"] = product.ExpectedLifetimeYears,
            ["expectedCycles"] = product.ExpectedCycles,
            ["supplyChainIndex"] = product.SupplyChainIndex,
            ["carbonFootprint"] = product.CarbonFootprint,
            ["performanceClass"] = product.PerformanceClass,
            ["materialMassesKg"] = new BsonDocument(product.MaterialMassesKg.Select(pair => new BsonElement(pair.Key, pair.Value))),
            ["carbonStages"] = new BsonDocument(product.CarbonStages.Select(pair => new BsonElement(pair.Key, pair.Value))),
            ["recycledContent"] = new BsonDocument(product.RecycledContent.Select(pair => new BsonElement(pair.Key, new BsonDocument
            {
                ["preConsumerShare"] = pair.Value.PreConsumerShare,
                ["postConsumerShare"] = pair.Value.PostConsumerShare
            }))),
            ["requiredFieldKeys"] = new BsonArray(product.RequiredFieldKeys),
            ["templateDocuments"] = new BsonArray(product.TemplateDocuments.Select(document => new BsonDocument
            {
                ["documentKey"] = document.DocumentKey,
                ["label"] = document.Label,
                ["fileName"] = document.FileName,
                ["visibility"] = document.Visibility
            })),
            ["updatedAt"] = updatedAt,
            ["updatedBy"] = updatedBy
        };
    }

    public static BsonDocument ToProductVersionDocument(
        string productId,
        BatteryProductVersion productVersion,
        string updatedAt,
        string updatedBy)
    {
        return new BsonDocument
        {
            ["productId"] = productId,
            ["version"] = productVersion.Version,
            ["batteryMassKg"] = productVersion.BatteryMassKg,
            ["ratedEnergyKwh"] = productVersion.RatedEnergyKwh,
            ["ratedCapacityAh"] = productVersion.RatedCapacityAh,
            ["ratedMaximumPowerKw"] = productVersion.RatedMaximumPowerKw,
            ["nominalVoltageV"] = productVersion.NominalVoltageV,
            ["expectedLifetimeYears"] = productVersion.ExpectedLifetimeYears,
            ["expectedCycles"] = productVersion.ExpectedCycles,
            ["supplyChainIndex"] = productVersion.SupplyChainIndex,
            ["carbonFootprint"] = productVersion.CarbonFootprint,
            ["performanceClass"] = productVersion.PerformanceClass,
            ["materialMassesKg"] = new BsonDocument(productVersion.MaterialMassesKg.Select(pair => new BsonElement(pair.Key, pair.Value))),
            ["carbonStages"] = new BsonDocument(productVersion.CarbonStages.Select(pair => new BsonElement(pair.Key, pair.Value))),
            ["recycledContent"] = new BsonDocument(productVersion.RecycledContent.Select(pair => new BsonElement(pair.Key, new BsonDocument
            {
                ["preConsumerShare"] = pair.Value.PreConsumerShare,
                ["postConsumerShare"] = pair.Value.PostConsumerShare
            }))),
            ["softwareVersion"] = productVersion.SoftwareVersion,
            ["softwareReleaseDate"] = productVersion.SoftwareReleaseDate,
            ["softwareLatestUpdate"] = productVersion.SoftwareLatestUpdate,
            ["requiredFieldKeys"] = new BsonArray(productVersion.RequiredFieldKeys),
            ["templateDocuments"] = new BsonArray(productVersion.TemplateDocuments.Select(document => new BsonDocument
            {
                ["documentKey"] = document.DocumentKey,
                ["label"] = document.Label,
                ["fileName"] = document.FileName,
                ["visibility"] = document.Visibility
            })),
            ["updatedAt"] = updatedAt,
            ["updatedBy"] = updatedBy
        };
    }

    public static BatteryProductTemplate FromProductDocument(BsonDocument product)
    {
        var productId = BsonHelpers.GetString(product, "productId");
        var defaults = BatteryProductTemplateCatalog.FindProduct(productId) ?? BatteryProductTemplateCatalog.DefaultProduct;
        return defaults with
        {
            ProductId = productId,
            ProductName = FirstNonEmpty(BsonHelpers.GetString(product, "productName"), defaults.ProductName),
            Description = FirstNonEmpty(BsonHelpers.GetString(product, "description"), defaults.Description),
            ImageUrl = FirstNonEmpty(BsonHelpers.GetString(product, "imageUrl"), defaults.ImageUrl),
            ModuleCount = ReadInt(product, "moduleCount", defaults.ModuleCount),
            BatteryMassKg = ReadDouble(product, "batteryMassKg", defaults.BatteryMassKg),
            RatedEnergyKwh = ReadDouble(product, "ratedEnergyKwh", defaults.RatedEnergyKwh),
            RatedCapacityAh = ReadDouble(product, "ratedCapacityAh", defaults.RatedCapacityAh),
            RatedMaximumPowerKw = ReadDouble(product, "ratedMaximumPowerKw", defaults.RatedMaximumPowerKw),
            NominalVoltageV = ReadDouble(product, "nominalVoltageV", defaults.NominalVoltageV),
            ExpectedLifetimeYears = ReadDouble(product, "expectedLifetimeYears", defaults.ExpectedLifetimeYears),
            ExpectedCycles = ReadDouble(product, "expectedCycles", defaults.ExpectedCycles),
            SupplyChainIndex = ReadDouble(product, "supplyChainIndex", defaults.SupplyChainIndex),
            CarbonFootprint = ReadDouble(product, "carbonFootprint", defaults.CarbonFootprint),
            PerformanceClass = FirstNonEmpty(BsonHelpers.GetString(product, "performanceClass"), defaults.PerformanceClass),
            MaterialMassesKg = ReadNumberMap(product, "materialMassesKg", defaults.MaterialMassesKg),
            CarbonStages = ReadNumberMap(product, "carbonStages", defaults.CarbonStages),
            RecycledContent = ReadRecycledContentMap(product, "recycledContent", defaults.RecycledContent),
            RequiredFieldKeys = ReadStringArray(product, "requiredFieldKeys", defaults.RequiredFieldKeys)
        };
    }

    public static BatteryProductVersion FromProductVersionDocument(
        BsonDocument productVersion,
        BatteryProductTemplate fallback)
    {
        var defaults = fallback.ProductVersions.FirstOrDefault(version =>
            version.Version.Equals(BsonHelpers.GetString(productVersion, "version"), StringComparison.OrdinalIgnoreCase))
            ?? fallback.LatestProductVersion;
        return defaults with
        {
            Version = FirstNonEmpty(BsonHelpers.GetString(productVersion, "version"), defaults.Version),
            BatteryMassKg = ReadDouble(productVersion, "batteryMassKg", defaults.BatteryMassKg),
            RatedEnergyKwh = ReadDouble(productVersion, "ratedEnergyKwh", defaults.RatedEnergyKwh),
            RatedCapacityAh = ReadDouble(productVersion, "ratedCapacityAh", defaults.RatedCapacityAh),
            RatedMaximumPowerKw = ReadDouble(productVersion, "ratedMaximumPowerKw", defaults.RatedMaximumPowerKw),
            NominalVoltageV = ReadDouble(productVersion, "nominalVoltageV", defaults.NominalVoltageV),
            ExpectedLifetimeYears = ReadDouble(productVersion, "expectedLifetimeYears", defaults.ExpectedLifetimeYears),
            ExpectedCycles = ReadDouble(productVersion, "expectedCycles", defaults.ExpectedCycles),
            SupplyChainIndex = ReadDouble(productVersion, "supplyChainIndex", defaults.SupplyChainIndex),
            CarbonFootprint = ReadDouble(productVersion, "carbonFootprint", defaults.CarbonFootprint),
            PerformanceClass = FirstNonEmpty(BsonHelpers.GetString(productVersion, "performanceClass"), defaults.PerformanceClass),
            MaterialMassesKg = ReadNumberMap(productVersion, "materialMassesKg", defaults.MaterialMassesKg),
            CarbonStages = ReadNumberMap(productVersion, "carbonStages", defaults.CarbonStages),
            RecycledContent = ReadRecycledContentMap(productVersion, "recycledContent", defaults.RecycledContent),
            SoftwareVersion = FirstNonEmpty(BsonHelpers.GetString(productVersion, "softwareVersion"), defaults.SoftwareVersion),
            SoftwareReleaseDate = FirstNonEmpty(BsonHelpers.GetString(productVersion, "softwareReleaseDate"), defaults.SoftwareReleaseDate),
            SoftwareLatestUpdate = FirstNonEmpty(BsonHelpers.GetString(productVersion, "softwareLatestUpdate"), defaults.SoftwareLatestUpdate),
            RequiredFieldKeys = ReadStringArray(productVersion, "requiredFieldKeys", defaults.RequiredFieldKeys)
        };
    }

    private static void ApplyBatteryIdentity(
        BsonDocument passport,
        BatteryProductTemplate product,
        ProductTemplateBatteryIdentity identity,
        string manufacturingDate)
    {
        var app = EnsureDocument(passport, "app");
        var display = EnsureDocument(app, "display");
        display["name"] = identity.DisplayName;
        display["modelNumber"] = identity.ModelNumber;
        display["serialNumber"] = BatteryPassCanonicalDataCatalog.NormalizeManufacturerSerialNumber(identity.SerialNumber, BsonHelpers.GetString(passport, "passportId"));
        display["manufacturerName"] = "Scania Industrial Batteries";
        display["facilityId"] = identity.FacilityId;

        var generalPayload = EnsureDocument(EnsureDocument(EnsureDocument(passport, "aspects"), "generalProductInformation"), "payload");
        generalPayload["productIdentifier"] = identity.ModelNumber;
        generalPayload["batteryPassportIdentifier"] = BatteryPassCanonicalDataCatalog.NormalizeBatteryPassportIdentifier(
            BsonHelpers.GetString(generalPayload, "batteryPassportIdentifier"),
            identity.SerialNumber,
            BsonHelpers.GetString(passport, "passportId"));
        generalPayload["manufacturingDate"] = string.IsNullOrWhiteSpace(manufacturingDate)
            ? string.Empty
            : $"{manufacturingDate}T00:00:00.000Z";
        generalPayload["puttingIntoService"] = string.IsNullOrWhiteSpace(manufacturingDate)
            ? string.Empty
            : $"{manufacturingDate}T00:00:00.000Z";
        EnsureDocument(generalPayload, "manufacturerInformation")["identifier"] = display["serialNumber"].DeepClone();
    }

    private static void ApplyProductTemplateValues(
        BsonDocument passport,
        BatteryProductTemplate product,
        BatteryProductVersion productVersion,
        string now)
    {
        var app = EnsureDocument(passport, "app");
        var productNode = EnsureDocument(app, "product");
        productNode["productId"] = product.ProductId;
        productNode["productName"] = product.ProductName;
        productNode["description"] = product.Description;
        productNode["moduleCount"] = product.ModuleCount;
        productNode["productVersion"] = productVersion.Version;
        productNode["softwareVersion"] = productVersion.SoftwareVersion;
        productNode["softwareReleaseDate"] = productVersion.SoftwareReleaseDate;
        productNode["softwareLatestUpdate"] = productVersion.SoftwareLatestUpdate;
        productNode["templateAppliedAt"] = now;

        var media = EnsureDocument(app, "media");
        media["batteryImageUrl"] = product.ImageUrl;
        media["batteryImageAlt"] = $"{product.ProductName} battery product";

        ApplyDocuments(app, passport, product, productVersion);
        ApplyGeneral(passport, productVersion);
        ApplyMaterials(passport, product, productVersion);
        ApplyPerformance(passport, productVersion, now);
        ApplyCarbon(passport, productVersion);
        ApplySupplyChain(passport, productVersion);
        ApplyCircularity(passport, productVersion);
        productNode["templateHash"] = ComputeTemplateHash(BuildTemplateBaseline(passport));
    }

    private static void ApplyDocuments(BsonDocument app, BsonDocument passport, BatteryProductTemplate product, BatteryProductVersion productVersion)
    {
        var documents = EnsureDocument(app, "documents");
        foreach (var document in productVersion.TemplateDocuments)
        {
            var key = document.DocumentKey;
            documents[key] = new BsonDocument
            {
                ["label"] = document.Label,
                ["url"] = $"template://{product.ProductId}/{key}/{document.FileName}",
                ["contentType"] = "application/pdf",
                ["sha256"] = $"template-{product.ProductId}-{key}-sha256",
                ["visibility"] = document.Visibility,
                ["source"] = "productTemplate"
            };
        }

        var aspects = EnsureDocument(passport, "aspects");
        var labelingPayload = EnsureDocument(EnsureDocument(aspects, "labeling"), "payload");
        labelingPayload["resultOfTestReport"] = BsonHelpers.GetString(documents, "conformityAssessment", "url");
        labelingPayload["declarationOfConformity"] = BsonHelpers.GetString(documents, "euDeclarationOfConformity", "url");

        var supplyPayload = EnsureDocument(EnsureDocument(aspects, "supplyChainDueDiligence"), "payload");
        supplyPayload["sustainabilityReport"] = BsonHelpers.GetString(documents, "sustainabilityReport", "url");
        supplyPayload["supplyChainDueDiligenceReport"] = BsonHelpers.GetString(documents, "dueDiligenceReport", "url");
        supplyPayload["thirdPartyAussurances"] = BsonHelpers.GetString(documents, "thirdPartyAudit", "url");
        supplyPayload["taxonomyReport"] = BsonHelpers.GetString(documents, "taxonomyReport", "url");

        var carbonPayload = EnsureDocument(EnsureDocument(aspects, "carbonFootprintForBatteries"), "payload");
        carbonPayload["carbonFootprintStudy"] = BsonHelpers.GetString(documents, "co2StudyReference", "url");
    }

    private static void ApplyGeneral(BsonDocument passport, BatteryProductVersion productVersion)
    {
        var generalPayload = EnsureDocument(EnsureDocument(EnsureDocument(passport, "aspects"), "generalProductInformation"), "payload");
        generalPayload["batteryCategory"] = "industrial";
        generalPayload["batteryStatus"] = "Original";
        generalPayload["batteryMass"] = productVersion.BatteryMassKg;
        EnsureDocument(generalPayload, "manufacturerInformation")["contactName"] = "Scania Industrial Batteries";
        EnsureDocument(generalPayload, "operatorInformation")["contactName"] = "Scania Industrial Batteries";
    }

    private static void ApplyMaterials(BsonDocument passport, BatteryProductTemplate product, BatteryProductVersion productVersion)
    {
        var payload = EnsureDocument(EnsureDocument(EnsureDocument(passport, "aspects"), "materialComposition"), "payload");
        payload["batteryMaterials"] = new BsonArray(productVersion.MaterialMassesKg.Select(pair =>
        {
            var definition = BatteryPassCanonicalDataCatalog.MaterialByLabel(pair.Key);
            return new BsonDocument
            {
                ["batteryMaterialName"] = pair.Key,
                ["batteryMaterialMass"] = pair.Value,
                ["batteryMaterialIdentifier"] = "7439-93-2",
                ["batteryMaterialLocation"] = new BsonDocument
                {
                    ["componentName"] = "Cell",
                    ["componentId"] = $"{product.ProductId.ToUpperInvariant()}-CELL"
                },
                ["isCriticalRawMaterial"] = definition.IsCriticalRawMaterial
            };
        }));
    }

    private static void ApplyPerformance(BsonDocument passport, BatteryProductVersion productVersion, string now)
    {
        var payload = EnsureDocument(EnsureDocument(EnsureDocument(passport, "aspects"), "performanceAndDurability"), "payload");
        var technical = EnsureDocument(payload, "batteryTechicalProperties");
        technical["ratedEnergy"] = productVersion.RatedEnergyKwh;
        technical["ratedCapacity"] = productVersion.RatedCapacityAh;
        technical["ratedMaximumPower"] = productVersion.RatedMaximumPowerKw;
        technical["nominalVoltage"] = productVersion.NominalVoltageV;
        technical["expectedLifetime"] = productVersion.ExpectedLifetimeYears;
        technical["expectedNumberOfCycles"] = productVersion.ExpectedCycles;

        var condition = EnsureDocument(payload, "batteryCondition");
        EnsureMetric(condition, "stateOfCharge", "stateOfChargeValue", 82.0, now);
        EnsureMetric(condition, "remainingCapacity", "remainingCapacityValue", 96.0, now);
        EnsureMetric(condition, "remainingEnergy", "remainingEnergyValue", Math.Round(productVersion.RatedEnergyKwh * 0.95, 2), now);
        EnsureMetric(condition, "numberOfFullCycles", "numberOfFullCyclesValue", 45, now);
    }

    private static void ApplyCarbon(BsonDocument passport, BatteryProductVersion productVersion)
    {
        var payload = EnsureDocument(EnsureDocument(EnsureDocument(passport, "aspects"), "carbonFootprintForBatteries"), "payload");
        payload["batteryCarbonFootprint"] = productVersion.CarbonFootprint;
        payload["absoluteCarbonFootprint"] = Math.Round(productVersion.CarbonFootprint * productVersion.RatedEnergyKwh, 2);
        payload["carbonFootprintPerformanceClass"] = productVersion.PerformanceClass;
        payload["carbonFootprintPerLifecycleStage"] = new BsonArray(productVersion.CarbonStages.Select(pair => new BsonDocument
        {
            ["lifecycleStage"] = pair.Key,
            ["carbonFootprint"] = pair.Value
        }));
    }

    private static void ApplySupplyChain(BsonDocument passport, BatteryProductVersion productVersion)
    {
        var payload = EnsureDocument(EnsureDocument(EnsureDocument(passport, "aspects"), "supplyChainDueDiligence"), "payload");
        payload["supplyChainIndicies"] = productVersion.SupplyChainIndex;
    }

    private static void ApplyCircularity(BsonDocument passport, BatteryProductVersion productVersion)
    {
        var payload = EnsureDocument(EnsureDocument(EnsureDocument(passport, "aspects"), "circularity"), "payload");
        var endOfLife = EnsureDocument(payload, "endOfLifeInformation");
        endOfLife["separateCollection"] = "Return battery to an authorised Scania battery collection partner.";
        endOfLife["wastePrevention"] = "Reuse, remanufacture, or recycle according to Scania circularity handling instructions.";
        payload["recycledContent"] = new BsonArray(productVersion.RecycledContent.Select(row => new BsonDocument
        {
            ["recycledMaterial"] = row.Key,
            ["preConsumerShare"] = row.Value.PreConsumerShare,
            ["postConsumerShare"] = row.Value.PostConsumerShare
        }));
    }

    private static void EnsureMetric(BsonDocument parent, string key, string valueKey, double value, string now)
    {
        parent[key] = new BsonDocument
        {
            [valueKey] = value,
            ["lastUpdate"] = now
        };
    }

    private static void Flatten(BsonValue value, string path, IDictionary<string, BsonValue> values)
    {
        if (value is BsonDocument document)
        {
            foreach (var element in document.Elements)
            {
                var childPath = string.IsNullOrWhiteSpace(path) ? element.Name : $"{path}.{element.Name}";
                Flatten(element.Value, childPath, values);
            }
            return;
        }

        if (!string.IsNullOrWhiteSpace(path))
        {
            values[path] = value.DeepClone();
        }
    }

    private static bool IsBatterySpecificPath(string path)
    {
        return BatterySpecificPaths.Any(specific =>
            path.Equals(specific, StringComparison.OrdinalIgnoreCase)
            || path.StartsWith($"{specific}.", StringComparison.OrdinalIgnoreCase));
    }

    private static BsonValue? GetValue(BsonDocument document, string path)
    {
        BsonValue current = document;
        foreach (var segment in path.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (current is not BsonDocument child || !child.TryGetValue(segment, out current))
            {
                return null;
            }
        }

        return current;
    }

    private static void SetValue(BsonDocument document, string path, BsonValue value)
    {
        var segments = path.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var current = document;
        for (var index = 0; index < segments.Length - 1; index++)
        {
            current = EnsureDocument(current, segments[index]);
        }

        current[segments[^1]] = value;
    }

    private static bool BsonEquals(BsonValue? left, BsonValue? right)
    {
        if (left == null && right == null)
        {
            return true;
        }

        if (left == null || right == null)
        {
            return false;
        }

        return left.ToJson() == right.ToJson();
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

    private static IReadOnlyDictionary<string, double> ReadNumberMap(
        BsonDocument document,
        string key,
        IReadOnlyDictionary<string, double> fallback)
    {
        if (document.GetValue(key, BsonNull.Value) is not BsonDocument map)
        {
            return fallback;
        }

        return map.Elements.ToDictionary(
            element => element.Name,
            element => element.Value.IsNumeric ? element.Value.ToDouble() : 0,
            StringComparer.OrdinalIgnoreCase);
    }

    private static IReadOnlyList<string> ReadStringArray(BsonDocument document, string key, IReadOnlyList<string> fallback)
    {
        return document.GetValue(key, BsonNull.Value) is BsonArray values
            ? values.Select(value => value.ToString() ?? string.Empty).Where(value => !string.IsNullOrWhiteSpace(value)).ToList()
            : fallback;
    }

    private static IReadOnlyDictionary<string, ProductTemplateRecycledContent> ReadRecycledContentMap(
        BsonDocument document,
        string key,
        IReadOnlyDictionary<string, ProductTemplateRecycledContent> fallback)
    {
        if (document.GetValue(key, BsonNull.Value) is not BsonDocument map)
        {
            return fallback;
        }

        var result = new Dictionary<string, ProductTemplateRecycledContent>(StringComparer.OrdinalIgnoreCase);
        foreach (var element in map.Elements)
        {
            if (element.Value is not BsonDocument value)
            {
                continue;
            }

            result[element.Name] = new ProductTemplateRecycledContent(
                ReadDouble(value, "preConsumerShare", fallback.TryGetValue(element.Name, out var existing) ? existing.PreConsumerShare : 0),
                ReadDouble(value, "postConsumerShare", fallback.TryGetValue(element.Name, out existing) ? existing.PostConsumerShare : 0));
        }

        return result.Count == 0 ? fallback : result;
    }

    private static int ReadInt(BsonDocument document, string key, int fallback)
    {
        var value = document.GetValue(key, fallback);
        return value.IsNumeric ? value.ToInt32() : fallback;
    }

    private static double ReadDouble(BsonDocument document, string key, double fallback)
    {
        var value = document.GetValue(key, fallback);
        return value.IsNumeric ? value.ToDouble() : fallback;
    }

    private static string FirstNonEmpty(params string[] values)
    {
        return values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;
    }
}
