using BatteryPassWeb.Models.Trust;
using MongoDB.Bson;
using MongoDB.Driver;

namespace BatteryPassWeb.Services;

public sealed class DataCompletionPolicyService
{
    public const string PolicyKey = "batteryPassportCompletion:v1";

    private static readonly IReadOnlyList<DataRequirementSection> DefaultSections = BuildDefaultSections();

    private readonly MongoContext _mongoContext;

    public DataCompletionPolicyService(MongoContext mongoContext)
    {
        _mongoContext = mongoContext;
    }

    public static DataCompletionPolicySnapshot CreateDefaultPolicy(IReadOnlyDictionary<string, bool>? requiredOverrides = null)
    {
        return new DataCompletionPolicySnapshot
        {
            PolicyKey = PolicyKey,
            Sections = DefaultSections
                .Select(section => new DataRequirementSection
                {
                    SectionKey = section.SectionKey,
                    Label = section.Label,
                    SortOrder = section.SortOrder,
                    Fields = section.Fields.Select(field => new DataRequirementField
                    {
                        FieldKey = field.FieldKey,
                        SectionKey = field.SectionKey,
                        Label = field.Label,
                        DataPath = field.DataPath,
                        Guidance = field.Guidance,
                        DefaultRequired = field.DefaultRequired,
                        IsRequired = requiredOverrides != null && requiredOverrides.TryGetValue(field.FieldKey, out var isRequired)
                            ? isRequired
                            : field.DefaultRequired,
                        SortOrder = field.SortOrder
                    }).ToList()
                })
                .ToList()
        };
    }

    public async Task<DataCompletionPolicySnapshot> GetPolicyAsync(CancellationToken cancellationToken = default)
    {
        var collection = GetCollection();
        if (collection == null)
        {
            return CreateDefaultPolicy();
        }

        var document = await collection
            .Find(Builders<BsonDocument>.Filter.Eq("policyKey", PolicyKey))
            .FirstOrDefaultAsync(cancellationToken);
        if (document == null)
        {
            var defaultPolicy = CreateDefaultPolicy();
            await SavePolicyAsync(defaultPolicy, "system", cancellationToken);
            return defaultPolicy;
        }

        return FromBsonDocument(document);
    }

    public async Task<DataCompletionPolicySnapshot> GetPolicyForPassportAsync(BsonDocument passport, CancellationToken cancellationToken = default)
    {
        var productId = BsonHelpers.GetString(passport, "app", "product", "productId");
        return string.IsNullOrWhiteSpace(productId)
            ? await GetPolicyAsync(cancellationToken)
            : await GetProductPolicyAsync(productId, cancellationToken);
    }

    public async Task<DataCompletionPolicySnapshot> GetProductPolicyAsync(string productId, CancellationToken cancellationToken = default)
    {
        var collection = GetProductPolicyCollection();
        if (collection == null || string.IsNullOrWhiteSpace(productId))
        {
            return await GetPolicyAsync(cancellationToken);
        }

        var document = await collection
            .Find(Builders<BsonDocument>.Filter.Eq("productId", productId.Trim()))
            .FirstOrDefaultAsync(cancellationToken);
        return document == null ? CreateDefaultPolicy() : FromBsonDocument(document);
    }

    public async Task SavePolicyAsync(IReadOnlyCollection<string> requiredFieldKeys, string actor, CancellationToken cancellationToken = default)
    {
        var required = requiredFieldKeys.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var policy = CreateDefaultPolicy(DefaultSections
            .SelectMany(section => section.Fields)
            .ToDictionary(field => field.FieldKey, field => required.Contains(field.FieldKey), StringComparer.OrdinalIgnoreCase));
        await SavePolicyAsync(policy, actor, cancellationToken);
    }

    public async Task SaveProductPolicyAsync(string productId, IReadOnlyCollection<string> requiredFieldKeys, string actor, CancellationToken cancellationToken = default)
    {
        var required = requiredFieldKeys.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var policy = CreateDefaultPolicy(DefaultSections
            .SelectMany(section => section.Fields)
            .ToDictionary(field => field.FieldKey, field => required.Contains(field.FieldKey), StringComparer.OrdinalIgnoreCase));
        await SaveProductPolicyAsync(productId, policy, actor, cancellationToken);
    }

    public async Task SaveProductPolicyAsync(string productId, DataCompletionPolicySnapshot policy, string actor, CancellationToken cancellationToken = default)
    {
        var collection = GetProductPolicyCollection();
        if (collection == null || string.IsNullOrWhiteSpace(productId))
        {
            return;
        }

        var now = DateTime.UtcNow.ToString("O");
        var document = ToBsonDocument(policy, actor, now);
        document["productId"] = productId.Trim();
        document["policyKey"] = $"{PolicyKey}:product:{productId.Trim()}";
        await collection.ReplaceOneAsync(
            Builders<BsonDocument>.Filter.Eq("productId", productId.Trim()),
            document,
            new ReplaceOptions { IsUpsert = true },
            cancellationToken);
    }

    public async Task SavePolicyAsync(DataCompletionPolicySnapshot policy, string actor, CancellationToken cancellationToken = default)
    {
        var collection = GetCollection();
        if (collection == null)
        {
            return;
        }

        var now = DateTime.UtcNow.ToString("O");
        var document = ToBsonDocument(policy, actor, now);
        await collection.ReplaceOneAsync(
            Builders<BsonDocument>.Filter.Eq("policyKey", PolicyKey),
            document,
            new ReplaceOptions { IsUpsert = true },
            cancellationToken);
    }

    public static BsonDocument ToBsonDocument(DataCompletionPolicySnapshot policy, string updatedBy, string updatedAt)
    {
        return new BsonDocument
        {
            ["policyKey"] = PolicyKey,
            ["updatedAt"] = updatedAt,
            ["updatedBy"] = updatedBy,
            ["sections"] = new BsonArray(policy.Sections
                .OrderBy(section => section.SortOrder)
                .Select(section => new BsonDocument
                {
                    ["sectionKey"] = section.SectionKey,
                    ["label"] = section.Label,
                    ["sortOrder"] = section.SortOrder,
                    ["fields"] = new BsonArray(section.Fields
                        .OrderBy(field => field.SortOrder)
                        .Select(field => new BsonDocument
                        {
                            ["fieldKey"] = field.FieldKey,
                            ["sectionKey"] = field.SectionKey,
                            ["label"] = field.Label,
                            ["dataPath"] = field.DataPath,
                            ["guidance"] = field.Guidance,
                            ["defaultRequired"] = field.DefaultRequired,
                            ["isRequired"] = field.IsRequired,
                            ["sortOrder"] = field.SortOrder
                        }))
                }))
        };
    }

    public static DataCompletionPolicySnapshot FromBsonDocument(BsonDocument document)
    {
        var defaultsByKey = DefaultSections
            .SelectMany(section => section.Fields)
            .ToDictionary(field => field.FieldKey, StringComparer.OrdinalIgnoreCase);
        var requiredOverrides = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);

        if (document.GetValue("sections", new BsonArray()) is BsonArray sections)
        {
            foreach (var field in sections
                .OfType<BsonDocument>()
                .SelectMany(section => (section.GetValue("fields", new BsonArray()) as BsonArray ?? new BsonArray()).OfType<BsonDocument>()))
            {
                var fieldKey = BsonHelpers.GetString(field, "fieldKey");
                if (defaultsByKey.ContainsKey(fieldKey))
                {
                    requiredOverrides[fieldKey] = field.GetValue("isRequired", defaultsByKey[fieldKey].DefaultRequired).ToBoolean();
                }
            }
        }

        var policy = CreateDefaultPolicy(requiredOverrides);
        return new DataCompletionPolicySnapshot
        {
            PolicyKey = BsonHelpers.GetString(document, "policyKey"),
            UpdatedAt = BsonHelpers.GetString(document, "updatedAt"),
            UpdatedBy = BsonHelpers.GetString(document, "updatedBy"),
            Sections = policy.Sections
        };
    }

    public static TrustValidationSectionResult ValidateRequiredFields(BsonDocument passport, DataCompletionPolicySnapshot policy)
    {
        var issues = new List<TrustValidationIssue>();
        foreach (var field in policy.Sections.SelectMany(section => section.Fields).Where(field => field.IsRequired))
        {
            if (!HasCompletionValue(passport, field.DataPath))
            {
                issues.Add(new TrustValidationIssue(
                    TrustValidationSeverity.BlockingError,
                    field.DataPath,
                    $"{field.Label} is required by the data requirements policy."));
            }
            else
            {
                issues.Add(new TrustValidationIssue(
                    TrustValidationSeverity.Passed,
                    field.DataPath,
                    $"{field.Label} is present."));
            }
        }

        if (issues.Count == 0)
        {
            issues.Add(new TrustValidationIssue(TrustValidationSeverity.Passed, "dataCompletionPolicy", "No required data fields are configured."));
        }

        return new TrustValidationSectionResult
        {
            SectionKey = "dataCompletionPolicy",
            SectionLabel = "Data completion policy",
            Issues = issues
        };
    }

    private IMongoCollection<BsonDocument>? GetCollection()
    {
        return _mongoContext.Database?.GetCollection<BsonDocument>("dataCompletionPolicies");
    }

    private IMongoCollection<BsonDocument>? GetProductPolicyCollection()
    {
        return _mongoContext.Database?.GetCollection<BsonDocument>("batteryProductCompletionPolicies");
    }

    private static bool HasCompletionValue(BsonDocument passport, string dataPath)
    {
        var value = ResolveValue(passport, dataPath);
        if (value == null || value.IsBsonNull)
        {
            return false;
        }

        if (value.IsString)
        {
            return !string.IsNullOrWhiteSpace(value.AsString);
        }

        if (value.IsBsonDocument)
        {
            return value.AsBsonDocument.ElementCount > 0;
        }

        if (value.IsBsonArray)
        {
            return value.AsBsonArray.Count > 0;
        }

        return true;
    }

    private static BsonValue? ResolveValue(BsonDocument passport, string dataPath)
    {
        BsonValue current = passport;
        foreach (var segment in dataPath.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (!TryResolveSegment(current, segment, out current))
            {
                return null;
            }
        }

        return current;
    }

    private static bool TryResolveSegment(BsonValue current, string segment, out BsonValue value)
    {
        value = BsonNull.Value;
        var bracketIndex = segment.IndexOf('[', StringComparison.Ordinal);
        if (bracketIndex > 0 && segment.EndsWith(']'))
        {
            if (current is not BsonDocument document)
            {
                return false;
            }

            var arrayName = segment[..bracketIndex];
            var selector = segment[(bracketIndex + 1)..^1];
            if (!document.TryGetValue(arrayName, out var arrayValue) || arrayValue is not BsonArray array)
            {
                return false;
            }

            var match = ResolveArrayItem(arrayName, selector, array);
            if (match == null)
            {
                return false;
            }

            value = match;
            return true;
        }

        if (current is not BsonDocument child || !child.TryGetValue(segment, out value))
        {
            return false;
        }

        return true;
    }

    private static BsonDocument? ResolveArrayItem(string arrayName, string selector, BsonArray array)
    {
        var selectorField = arrayName switch
        {
            "batteryMaterials" => "batteryMaterialName",
            "recycledContent" => "recycledMaterial",
            "carbonFootprintPerLifecycleStage" => "lifecycleStage",
            _ => string.Empty
        };

        if (string.IsNullOrWhiteSpace(selectorField))
        {
            return null;
        }

        return array
            .OfType<BsonDocument>()
            .FirstOrDefault(item => BsonHelpers.GetString(item, selectorField).Equals(selector, StringComparison.OrdinalIgnoreCase));
    }

    private static IReadOnlyList<DataRequirementSection> BuildDefaultSections()
    {
        var sections = new List<DataRequirementSection>();
        sections.Add(Section("general", "General", 10,
            Field("general.passportId", "Passport ID", "passportId", "Unique DID-style battery identifier.", true, 10),
            Field("general.name", "Name", "app.display.name", "Human-readable battery name.", true, 20),
            Field("general.product", "Product", "app.product.productId", "Battery product template selected for this passport.", true, 25),
            Field("general.softwareVersion", "Software version", "app.product.softwareVersion", "Software version selected for this product.", true, 26),
            Field("general.modelNumber", "Model Number", "app.display.modelNumber", "Model identifier shown on public reports.", true, 30),
            Field("general.serialNumber", "Serial Number", "app.display.serialNumber", "Manufacturer serial number.", true, 40),
            Field("general.category", "Product category", "aspects.generalProductInformation.payload.batteryCategory", "Official Battery Pass battery category used by the schema.", true, 50),
            Field("general.batteryStatus", "Status", "aspects.generalProductInformation.payload.batteryStatus", "Lifecycle/original status for the battery.", true, 60),
            Field("general.batteryMass", "Battery mass", "aspects.generalProductInformation.payload.batteryMass", "Battery mass in kg.", true, 70),
            Field("general.manufacturingDate", "Manufactured date", "aspects.generalProductInformation.payload.manufacturingDate", "Manufacturing date.", true, 80),
            Field("general.facilityId", "Facility ID", "app.display.facilityId", "Production or facility identifier.", true, 90),
            Field("general.manufacturerName", "Manufactured by", "app.display.manufacturerName", "Manufacturer legal/display name.", true, 100),
            Field("general.registryStatus", "Registry status", "registryInfo.status", "Draft, published, or archived status.", true, 110),
            Field("general.batteryImageUrl", "Battery image", "app.media.batteryImageUrl", "Display image for the report.", false, 120)));

        sections.Add(Section("materialComposition", "Material composition", 20,
            MaterialField("material.nickelMass", "Nickel kg", "Nickel", 10),
            MaterialField("material.copperMass", "Copper kg", "Copper", 20),
            MaterialField("material.aluminiumMass", "Aluminium kg", "Aluminium", 30),
            MaterialField("material.graphiteMass", "Graphite kg", "Graphite", 40),
            MaterialField("material.manganeseMass", "Manganese kg", "Manganese", 50),
            MaterialField("material.cobaltMass", "Cobalt kg", "Cobalt", 60),
            MaterialField("material.lithiumMass", "Lithium kg", "Lithium", 70),
            MaterialField("material.electrolyteMass", "Electrolyte and separators kg", "Electrolyte and separators", 80)));

        sections.Add(Section("performance", "Performance", 30,
            Field("performance.ratedEnergy", "Rated energy kWh", "aspects.performanceAndDurability.payload.batteryTechicalProperties.ratedEnergy", "Rated energy in kWh.", true, 10),
            Field("performance.ratedCapacity", "Rated capacity Ah", "aspects.performanceAndDurability.payload.batteryTechicalProperties.ratedCapacity", "Rated capacity in Ah.", true, 20),
            Field("performance.ratedMaximumPower", "Maximum power kW", "aspects.performanceAndDurability.payload.batteryTechicalProperties.ratedMaximumPower", "Maximum rated power.", true, 30),
            Field("performance.nominalVoltage", "Nominal voltage V", "aspects.performanceAndDurability.payload.batteryTechicalProperties.nominalVoltage", "Nominal voltage.", true, 40),
            Field("performance.expectedLifetime", "Expected lifetime years", "aspects.performanceAndDurability.payload.batteryTechicalProperties.expectedLifetime", "Expected lifetime in years.", true, 50),
            Field("performance.expectedNumberOfCycles", "Expected cycles", "aspects.performanceAndDurability.payload.batteryTechicalProperties.expectedNumberOfCycles", "Expected cycle count.", true, 60),
            Field("performance.stateOfCharge", "State of charge %", "aspects.performanceAndDurability.payload.batteryCondition.stateOfCharge.stateOfChargeValue", "Current state of charge.", true, 70),
            Field("performance.remainingCapacity", "Remaining capacity %", "aspects.performanceAndDurability.payload.batteryCondition.remainingCapacity.remainingCapacityValue", "Remaining certified capacity.", true, 80),
            Field("performance.remainingEnergy", "Remaining energy kWh", "aspects.performanceAndDurability.payload.batteryCondition.remainingEnergy.remainingEnergyValue", "Remaining energy.", true, 90),
            Field("performance.fullCycles", "Full cycles", "aspects.performanceAndDurability.payload.batteryCondition.numberOfFullCycles.numberOfFullCyclesValue", "Number of full cycles.", true, 100)));

        sections.Add(Section("compliance", "Compliance", 40,
            Field("compliance.conformityAssessment", "Conformity assessment report", "aspects.labeling.payload.resultOfTestReport", "URL or reference for the conformity assessment report.", true, 10),
            Field("compliance.euDeclarationOfConformity", "EU declaration of conformity", "aspects.labeling.payload.declarationOfConformity", "URL or reference for the EU declaration.", true, 20)));

        sections.Add(Section("supplyChain", "Supply chain", 50,
            Field("supplyChain.supplyChainIndex", "Supply chain index", "aspects.supplyChainDueDiligence.payload.supplyChainIndicies", "Supply chain due-diligence index.", true, 10),
            Field("supplyChain.sustainabilityReport", "Sustainability report", "aspects.supplyChainDueDiligence.payload.sustainabilityReport", "Sustainability report reference.", true, 20),
            Field("supplyChain.dueDiligenceReport", "Due diligence report", "aspects.supplyChainDueDiligence.payload.supplyChainDueDiligenceReport", "Due-diligence report reference.", true, 30),
            Field("supplyChain.thirdPartyAudit", "Third-party audit", "aspects.supplyChainDueDiligence.payload.thirdPartyAussurances", "Third-party assurance/audit reference.", true, 40),
            Field("supplyChain.taxonomyReport", "Taxonomy report", "aspects.supplyChainDueDiligence.payload.taxonomyReport", "Taxonomy report reference.", true, 50)));

        sections.Add(Section("circularity", "Circularity", 60,
            Field("circularity.separateCollection", "Separate collection", "aspects.circularity.payload.endOfLifeInformation.separateCollection", "Collection instructions or URL.", true, 10),
            Field("circularity.wastePrevention", "Waste prevention", "aspects.circularity.payload.endOfLifeInformation.wastePrevention", "Waste prevention instructions or URL.", true, 20),
            Field("circularity.recycledContentShareVerification", "Recycled content share verification", "app.notes.circularity.recycledContentShareVerification", "Verification state shown by the admin form.", false, 30),
            RecycledField("circularity.recycledNickelPre", "Nickel pre-consumer %", "Nickel", "preConsumerShare", true, 40),
            RecycledField("circularity.recycledNickelPost", "Nickel post-consumer %", "Nickel", "postConsumerShare", true, 50),
            RecycledField("circularity.recycledNickelPrimary", "Nickel primary material %", "Nickel", "primaryMaterialShare", false, 60),
            RecycledField("circularity.recycledCobaltPre", "Cobalt pre-consumer %", "Cobalt", "preConsumerShare", true, 70),
            RecycledField("circularity.recycledCobaltPost", "Cobalt post-consumer %", "Cobalt", "postConsumerShare", true, 80),
            RecycledField("circularity.recycledCobaltPrimary", "Cobalt primary material %", "Cobalt", "primaryMaterialShare", false, 90),
            RecycledField("circularity.recycledLithiumPre", "Lithium pre-consumer %", "Lithium", "preConsumerShare", true, 100),
            RecycledField("circularity.recycledLithiumPost", "Lithium post-consumer %", "Lithium", "postConsumerShare", true, 110),
            RecycledField("circularity.recycledLithiumPrimary", "Lithium primary material %", "Lithium", "primaryMaterialShare", false, 120),
            RecycledField("circularity.recycledLeadPre", "Lead pre-consumer %", "Lead", "preConsumerShare", true, 130),
            RecycledField("circularity.recycledLeadPost", "Lead post-consumer %", "Lead", "postConsumerShare", true, 140),
            RecycledField("circularity.recycledLeadPrimary", "Lead primary material %", "Lead", "primaryMaterialShare", false, 150)));

        sections.Add(Section("carbonFootprint", "Carbon Footprint", 70,
            Field("carbon.amount", "Amount gCO2e/kWh", "aspects.carbonFootprintForBatteries.payload.batteryCarbonFootprint", "Battery carbon footprint amount.", true, 10),
            Field("carbon.performanceClass", "Performance class", "aspects.carbonFootprintForBatteries.payload.carbonFootprintPerformanceClass", "Carbon footprint performance class.", true, 20),
            CarbonField("carbon.rawMaterial", "Raw material extraction gCO2e/kWh", "RawMaterialExtraction", 30),
            CarbonField("carbon.mainProduction", "Main production gCO2e/kWh", "MainProduction", 40),
            CarbonField("carbon.distribution", "Distribution gCO2e/kWh", "Distribution", 50),
            CarbonField("carbon.recycling", "Recycling gCO2e/kWh", "Recycling", 60),
            Field("carbon.co2StudyReference", "CO2 study reference", "aspects.carbonFootprintForBatteries.payload.carbonFootprintStudy", "Reference URL for the carbon footprint study.", true, 70)));

        return sections;
    }

    private static DataRequirementSection Section(string sectionKey, string label, int sortOrder, params DataRequirementField[] fields)
    {
        return new DataRequirementSection
        {
            SectionKey = sectionKey,
            Label = label,
            SortOrder = sortOrder,
            Fields = fields.Select(field => new DataRequirementField
            {
                FieldKey = field.FieldKey,
                SectionKey = sectionKey,
                Label = field.Label,
                DataPath = field.DataPath,
                Guidance = field.Guidance,
                DefaultRequired = field.DefaultRequired,
                IsRequired = field.IsRequired,
                SortOrder = field.SortOrder
            }).ToList()
        };
    }

    private static DataRequirementField Field(string fieldKey, string label, string dataPath, string guidance, bool required, int sortOrder)
    {
        return new DataRequirementField
        {
            FieldKey = fieldKey,
            Label = label,
            DataPath = dataPath,
            Guidance = guidance,
            DefaultRequired = required,
            IsRequired = required,
            SortOrder = sortOrder
        };
    }

    private static DataRequirementField MaterialField(string fieldKey, string label, string material, int sortOrder)
    {
        return Field(
            fieldKey,
            label,
            $"aspects.materialComposition.payload.batteryMaterials[{material}].batteryMaterialMass",
            $"Mass for {material}.",
            required: true,
            sortOrder);
    }

    private static DataRequirementField RecycledField(string fieldKey, string label, string material, string shareField, bool required, int sortOrder)
    {
        return Field(
            fieldKey,
            label,
            $"aspects.circularity.payload.recycledContent[{material}].{shareField}",
            $"Recycled content value for {material}.",
            required,
            sortOrder);
    }

    private static DataRequirementField CarbonField(string fieldKey, string label, string lifecycleStage, int sortOrder)
    {
        return Field(
            fieldKey,
            label,
            $"aspects.carbonFootprintForBatteries.payload.carbonFootprintPerLifecycleStage[{lifecycleStage}].carbonFootprint",
            $"Carbon footprint value for {label}.",
            required: true,
            sortOrder);
    }
}
