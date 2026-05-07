using BatteryPassWeb.Models.Trust;
using BatteryPassWeb.Models.ViewModels;
using MongoDB.Bson;

namespace BatteryPassWeb.Services;

public sealed class PassportViewModelFactory
{
    private const string DefaultImage = "/images/compact7.png";

    public PassportViewModel Create(
        BsonDocument document,
        IReadOnlyDictionary<string, string>? clusterNamesById = null,
        PassportVerificationResult? verificationResult = null)
    {
        var passportId = BsonHelpers.GetString(document, "passportId");
        var app = GetDocument(BsonHelpers.GetValue(document, "app"));
        var appDisplay = GetDocument(app.GetValue("display", new BsonDocument()));
        var appMedia = GetDocument(app.GetValue("media", new BsonDocument()));
        var appDocuments = GetDocument(app.GetValue("documents", new BsonDocument()));
        var appNotes = GetDocument(app.GetValue("notes", new BsonDocument()));
        var appOperations = GetDocument(app.GetValue("operations", new BsonDocument()));
        var circularityNotes = GetDocument(appNotes.GetValue("circularity", new BsonDocument()));

        var aspects = GetDocument(BsonHelpers.GetValue(document, "aspects"));
        var generalPayload = GetPayload(aspects, "generalProductInformation");
        var materialPayload = GetPayload(aspects, "materialComposition");
        var performancePayload = GetPayload(aspects, "performanceAndDurability");
        var carbonPayload = GetPayload(aspects, "carbonFootprintForBatteries");
        var circularityPayload = GetPayload(aspects, "circularity");
        var supplyChainPayload = GetPayload(aspects, "supplyChainDueDiligence");
        var labelingPayload = GetPayload(aspects, "labeling");

        var modelNumber = FirstNonEmpty(
            appDisplay.GetValue("modelNumber", string.Empty).ToString(),
            BsonHelpers.GetString(generalPayload, "productIdentifier"));
        var serialNumber = BatteryPassCanonicalDataCatalog.NormalizeManufacturerSerialNumber(
            BsonHelpers.GetString(appDisplay, "serialNumber"),
            passportId);
        var displayName = FirstNonEmpty(
            appDisplay.GetValue("name", string.Empty).ToString(),
            modelNumber);
        var manufacturerName = FirstNonEmpty(
            appDisplay.GetValue("manufacturerName", string.Empty).ToString(),
            BsonHelpers.GetString(generalPayload, "manufacturerInformation", "contactName"));
        var facilityId = FirstNonEmpty(
            appDisplay.GetValue("facilityId", string.Empty).ToString(),
            BsonHelpers.GetString(generalPayload, "manufacturingPlace", "streetAddress"));
        var clusterId = BsonHelpers.GetString(document, "clusterId");
        var clusterLabel = string.IsNullOrWhiteSpace(clusterId)
            ? "No cluster assigned"
            : clusterNamesById != null && clusterNamesById.TryGetValue(clusterId, out var clusterName) && !string.IsNullOrWhiteSpace(clusterName)
                ? clusterName
                : clusterId;

        var weight = NumberAt(generalPayload, "batteryMass");
        var batteryImageUrl = NormalizeAssetUrl(FirstNonEmpty(
            appMedia.GetValue("batteryImageUrl", string.Empty).ToString(),
            DefaultImage), passportId);
        var batteryCategory = FirstNonEmpty(
            BsonHelpers.GetString(generalPayload, "batteryCategory"),
            BatteryImageCatalog.CategoryForImageUrl(batteryImageUrl));
        var batteryImageAlt = FirstNonEmpty(
            appMedia.GetValue("batteryImageAlt", string.Empty).ToString(),
            $"Industrial EV battery pack for passport {modelNumber}");

        var materialSegments = WithPercentages(BuildMaterialSegmentsFromAspects(materialPayload));
        var carbonSegments = WithPercentages(BuildCarbonSegmentsFromAspects(carbonPayload));
        var recycledContentCharts = BuildRecycledContentFromAspects(circularityPayload);

        var materialRows = ReadMaterialRows(materialPayload);
        var technical = GetDocument(performancePayload.GetValue("batteryTechicalProperties", new BsonDocument()));
        var batteryCondition = GetDocument(performancePayload.GetValue("batteryCondition", new BsonDocument()));
        var locationOfUse = GetDocument(appOperations.GetValue("locationOfUse", new BsonDocument()));
        var contactPerson = GetDocument(appOperations.GetValue("contactPerson", new BsonDocument()));
        var latestTelemetry = GetDocument(appOperations.GetValue("latestTelemetry", new BsonDocument()));
        var recycledContentShareVerification = FirstNonEmpty(
            circularityNotes.GetValue("recycledContentShareVerification", string.Empty).ToString(),
            BsonHelpers.GetString(circularityPayload, "verification", "state"),
            "unverified");

        var documents = new PassportDocumentsViewModel
        {
            ConformityAssessment = ReadDocument(
                appDocuments,
                "conformityAssessment",
                "Conformity assessment",
                BsonHelpers.GetString(labelingPayload, "resultOfTestReport")),
            EuDeclarationOfConformity = ReadDocument(
                appDocuments,
                "euDeclarationOfConformity",
                "EU declaration of conformity ID",
                BsonHelpers.GetString(labelingPayload, "declarationOfConformity")),
            SustainabilityReport = ReadDocument(
                appDocuments,
                "sustainabilityReport",
                "Sustainability report",
                BsonHelpers.GetString(supplyChainPayload, "sustainabilityReport")),
            DueDiligenceReport = ReadDocument(
                appDocuments,
                "dueDiligenceReport",
                "Due diligence report",
                BsonHelpers.GetString(supplyChainPayload, "supplyChainDueDiligenceReport")),
            ThirdPartyAudit = ReadDocument(
                appDocuments,
                "thirdPartyAudit",
                "Third party audit",
                BsonHelpers.GetString(supplyChainPayload, "thirdPartyAussurances")),
            TaxonomyReport = ReadDocument(
                appDocuments,
                "taxonomyReport",
                "Taxonomy report",
                BsonHelpers.GetString(supplyChainPayload, "taxonomyReport")),
            Co2StudyReference = ReadDocument(
                appDocuments,
                "co2StudyReference",
                "CO2 study reference",
                BsonHelpers.GetString(carbonPayload, "carbonFootprintStudy"))
        };

        var carbonFootprint = BatteryPassCanonicalDataCatalog.NormalizeCarbonFootprint(NumberAt(carbonPayload, "batteryCarbonFootprint"));
        var isValid = BoolAt(document, "validation", "isValid");
        var trust = GetDocument(document.GetValue("trust", new BsonDocument()));
        var validationSummary = GetDocument(trust.GetValue("validationSummary", new BsonDocument()));
        var trustState = FirstNonEmpty(
            trust.GetValue("state", string.Empty).ToString() ?? string.Empty,
            isValid ? "signed" : "unvalidated");
        var trustBlockingErrors = NumberAt(validationSummary, "blockingErrorCount");
        var trustWarnings = NumberAt(validationSummary, "warningCount");
        var latestProof = GetDocument(trust.GetValue("latestProof", new BsonDocument()));
        var latestHash = NormalizeHashForDisplay(FirstNonEmpty(
            trust.GetValue("latestHash", string.Empty).ToString() ?? string.Empty,
            latestProof.GetValue("hash", string.Empty).ToString() ?? string.Empty));
        var latestRevisionId = trust.GetValue("latestRevisionId", string.Empty).ToString() ?? string.Empty;
        var lastSignedAt = FirstNonEmpty(
            trust.GetValue("lastSignedAt", string.Empty).ToString() ?? string.Empty,
            latestProof.GetValue("created", string.Empty).ToString() ?? string.Empty);
        var issuer = FirstNonEmpty(
            latestProof.GetValue("issuer", string.Empty).ToString() ?? string.Empty,
            verificationResult?.Issuer ?? string.Empty);
        var verificationMethod = FirstNonEmpty(
            latestProof.GetValue("verificationMethod", string.Empty).ToString() ?? string.Empty,
            verificationResult?.VerificationMethod ?? string.Empty);
        var proofValue = latestProof.GetValue("proofValue", string.Empty).ToString() ?? string.Empty;
        var proofStatus = BuildProofStatus(trustState, BoolAt(trust, "isDirty"), proofValue, verificationResult);
        var verificationMessage = FirstNonEmpty(
            verificationResult?.Message ?? string.Empty,
            BoolAt(trust, "isDirty")
                ? "Passport core has changed since the latest signature."
                : string.IsNullOrWhiteSpace(proofValue)
                    ? "No signature proof has been recorded yet."
                    : "Signature proof metadata is available.");

        return new PassportViewModel
        {
            PassportId = passportId,
            DisplayName = displayName,
            ModelNumber = modelNumber,
            SerialNumber = serialNumber,
            ManufacturerName = manufacturerName,
            FacilityId = facilityId,
            RegistryStatus = BsonHelpers.GetString(document, "registryInfo", "status"),
            ClusterId = clusterId,
            ClusterLabel = clusterLabel,
            Category = batteryCategory,
            BatteryStatus = BsonHelpers.GetString(generalPayload, "batteryStatus"),
            ManufacturedDate = DateOnly(BsonHelpers.GetString(generalPayload, "manufacturingDate")),
            Weight = weight,
            WeightLabel = $"{weight:F2}kg",
            IsValid = isValid,
            VerificationState = isValid ? "verified" : "unverified",
            TrustState = trustState,
            TrustIsDirty = BoolAt(trust, "isDirty"),
            TrustLastValidatedAt = trust.GetValue("lastValidatedAt", string.Empty).ToString() ?? string.Empty,
            TrustBlockingErrorCount = (int)trustBlockingErrors,
            TrustWarningCount = (int)trustWarnings,
            TrustLatestHash = latestHash,
            TrustLatestRevisionId = latestRevisionId,
            TrustLastSignedAt = lastSignedAt,
            TrustIssuer = issuer,
            TrustVerificationMethod = verificationMethod,
            TrustProofStatus = proofStatus,
            TrustVerificationMessage = verificationMessage,
            BatteryImageUrl = batteryImageUrl,
            BatteryImageAlt = batteryImageAlt,
            CarbonFootprint = carbonFootprint,
            CarbonFootprintLabel = $"{carbonFootprint:F2}gCO2e/kWh",
            PerformanceClass = BatteryPassCanonicalDataCatalog.NormalizePerformanceClass(BsonHelpers.GetString(carbonPayload, "carbonFootprintPerformanceClass")),
            Performance = new PassportPerformanceViewModel
            {
                RatedEnergy = NumberAt(technical, "ratedEnergy"),
                RatedCapacity = NumberAt(technical, "ratedCapacity"),
                RatedMaximumPower = NumberAt(technical, "ratedMaximumPower"),
                NominalVoltage = NumberAt(technical, "nominalVoltage"),
                ExpectedLifetime = NumberAt(technical, "expectedLifetime"),
                ExpectedNumberOfCycles = NumberAt(technical, "expectedNumberOfCycles"),
                StateOfCharge = NumberAt(GetDocument(batteryCondition.GetValue("stateOfCharge", new BsonDocument())), "stateOfChargeValue"),
                RemainingCapacity = NumberAt(GetDocument(batteryCondition.GetValue("remainingCapacity", new BsonDocument())), "remainingCapacityValue"),
                RemainingEnergy = NumberAt(GetDocument(batteryCondition.GetValue("remainingEnergy", new BsonDocument())), "remainingEnergyValue"),
                Cycles = NumberAt(GetDocument(batteryCondition.GetValue("numberOfFullCycles", new BsonDocument())), "numberOfFullCyclesValue")
            },
            Operations = new PassportOperationsViewModel
            {
                IsActive = appOperations.GetValue("isActive", true).ToBoolean(),
                LocationSiteName = BsonHelpers.GetString(locationOfUse, "siteName"),
                LocationAddress = BsonHelpers.GetString(locationOfUse, "address"),
                LocationCity = BsonHelpers.GetString(locationOfUse, "city"),
                LocationCountry = BsonHelpers.GetString(locationOfUse, "country"),
                LocationLatitude = NullableNumberAt(locationOfUse, "latitude"),
                LocationLongitude = NullableNumberAt(locationOfUse, "longitude"),
                ContactName = BsonHelpers.GetString(contactPerson, "name"),
                ContactEmail = BsonHelpers.GetString(contactPerson, "email"),
                ContactPhone = BsonHelpers.GetString(contactPerson, "phone"),
                CurrentConsumptionKwh = NullableNumberAt(latestTelemetry, "currentConsumptionKwh"),
                CurrentChargeLevelPct = NullableNumberAt(latestTelemetry, "currentChargeLevelPct"),
                CurrentVoltageV = NullableNumberAt(latestTelemetry, "currentVoltageV"),
                CurrentCurrentA = NullableNumberAt(latestTelemetry, "currentCurrentA"),
                LatestMeasuredAt = BsonHelpers.GetString(latestTelemetry, "measuredAt")
            },
            Circularity = new PassportCircularityViewModel
            {
                SeparateCollection = FirstNonEmpty(
                    circularityNotes.GetValue("separateCollection", string.Empty).ToString(),
                    BsonHelpers.GetString(circularityPayload, "endOfLifeInformation", "separateCollection")),
                WastePrevention = FirstNonEmpty(
                    circularityNotes.GetValue("wastePrevention", string.Empty).ToString(),
                    BsonHelpers.GetString(circularityPayload, "endOfLifeInformation", "wastePrevention")),
                RecycledContentShareVerification = recycledContentShareVerification
            },
            Documents = documents,
            MaterialCompositionSegments = materialSegments,
            CarbonFootprintSegments = carbonSegments,
            RecycledContentCharts = recycledContentCharts,
            BatteryMaterials = materialRows,
            MaterialCompositionTotal = materialSegments.Sum(segment => segment.Value),
            SupplyChainIndex = BatteryPassCanonicalDataCatalog.NormalizeSupplyChainIndex(NumberAt(supplyChainPayload, "supplyChainIndicies"))
        };
    }

    private static PassportDocumentLinkViewModel ReadDocument(BsonDocument appDocuments, string key, string fallbackLabel, string fallbackUrl)
    {
        var document = GetDocument(appDocuments.GetValue(key, new BsonDocument()));
        var fileId = ValueText(document.GetValue("fileId", string.Empty));
        var label = FirstNonEmpty(ValueText(document.GetValue("label", string.Empty)), fallbackLabel);
        var url = NormalizeDocumentUrl(FirstNonEmpty(ValueText(document.GetValue("url", string.Empty)), fallbackUrl), fileId);
        return new PassportDocumentLinkViewModel(
            label,
            url,
            fileId,
            ValueText(document.GetValue("contentType", string.Empty)),
            ValueText(document.GetValue("visibility", "private")),
            ValueText(document.GetValue("sha256", string.Empty)),
            ValueText(document.GetValue("uploadedAt", string.Empty)));
    }

    private static string NormalizeDocumentUrl(string url, string fileId)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return string.IsNullOrWhiteSpace(fileId) ? string.Empty : $"/api/files/{fileId}";
        }

        if (Uri.TryCreate(url, UriKind.Absolute, out var absoluteUri))
        {
            if (absoluteUri.AbsolutePath.StartsWith("/api/files/", StringComparison.OrdinalIgnoreCase))
            {
                var query = string.IsNullOrWhiteSpace(absoluteUri.Query) ? string.Empty : absoluteUri.Query;
                return absoluteUri.AbsolutePath + query;
            }

            return url;
        }

        if (url.StartsWith("/api/files/", StringComparison.OrdinalIgnoreCase))
        {
            return url;
        }

        return url;
    }

    private static string NormalizeAssetUrl(string url, string passportId)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return DefaultImage;
        }

        return BatteryImageCatalog.NormalizeKnownImageUrl(url, passportId);
    }

    private static BsonDocument GetPayload(BsonDocument aspects, string aspectKey)
    {
        var aspect = GetDocument(aspects.GetValue(aspectKey, new BsonDocument()));
        return GetDocument(aspect.GetValue("payload", new BsonDocument()));
    }

    private static BsonDocument GetDocument(BsonValue? value)
    {
        return value is BsonDocument document ? document : new BsonDocument();
    }

    private static string FirstNonEmpty(params string?[] values)
    {
        foreach (var value in values)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }

        return string.Empty;
    }

    private static string ValueText(BsonValue? value)
    {
        return value == null || value.IsBsonNull ? string.Empty : value.ToString() ?? string.Empty;
    }

    private static double NumberFromValue(BsonValue value)
    {
        if (value.IsNumeric)
        {
            return value.ToDouble();
        }

        return double.TryParse(value.ToString(), out var parsed) ? parsed : 0;
    }

    private static string BuildProofStatus(
        string trustState,
        bool isDirty,
        string proofValue,
        PassportVerificationResult? verificationResult)
    {
        if (verificationResult != null)
        {
            return verificationResult.IsValid ? "Valid signature" : verificationResult.State;
        }

        if (isDirty)
        {
            return "Dirty";
        }

        if (string.IsNullOrWhiteSpace(proofValue))
        {
            return "No proof";
        }

        return string.Equals(trustState, TrustState.Signed, StringComparison.OrdinalIgnoreCase)
            ? "Signed"
            : "Proof retained";
    }

    private static string NormalizeHashForDisplay(string hash)
    {
        if (string.IsNullOrWhiteSpace(hash))
        {
            return string.Empty;
        }

        return hash.StartsWith("sha256:", StringComparison.OrdinalIgnoreCase) ? hash : $"sha256:{hash}";
    }

    private static bool BoolAt(BsonDocument document, params string[] path)
    {
        var value = BsonHelpers.GetValue(document, path);
        if (value == null || value.IsBsonNull)
        {
            return false;
        }

        if (value.IsBoolean)
        {
            return value.AsBoolean;
        }

        return bool.TryParse(value.ToString(), out var parsed) && parsed;
    }

    private static double NumberAt(BsonDocument document, params string[] path)
    {
        var value = BsonHelpers.GetValue(document, path);
        if (value == null || value.IsBsonNull)
        {
            return 0;
        }

        if (value.IsNumeric)
        {
            return value.ToDouble();
        }

        return double.TryParse(value.ToString(), out var parsed) ? parsed : 0;
    }

    private static double? NullableNumberAt(BsonDocument document, params string[] path)
    {
        var value = BsonHelpers.GetValue(document, path);
        if (value == null || value.IsBsonNull)
        {
            return null;
        }

        if (value.IsNumeric)
        {
            return value.ToDouble();
        }

        return double.TryParse(value.ToString(), out var parsed) ? parsed : null;
    }

    private static string DateOnly(string value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : value.Length >= 10
                ? value[..10]
                : value;
    }

    private static List<ChartSegmentViewModel> BuildMaterialSegmentsFromAspects(BsonDocument materialPayload)
    {
        var materials = materialPayload.GetValue("batteryMaterials", new BsonArray());
        if (materials is not BsonArray materialArray)
        {
            return [];
        }

        var result = new List<ChartSegmentViewModel>();
        foreach (var item in materialArray)
        {
            if (item is not BsonDocument material)
            {
                continue;
            }

            var label = ValueText(material.GetValue("batteryMaterialName", string.Empty));
            result.Add(new ChartSegmentViewModel
            {
                Label = label,
                Value = BatteryPassCanonicalDataCatalog.NormalizeMaterialMass(
                    label,
                    NumberFromValue(material.GetValue("batteryMaterialMass", 0))),
                Unit = "kg",
                Color = BatteryPassCanonicalDataCatalog.MaterialByLabel(label).Color
            });
        }

        return result.Where(segment => !string.IsNullOrWhiteSpace(segment.Label)).ToList();
    }

    private static List<ChartSegmentViewModel> BuildCarbonSegmentsFromAspects(BsonDocument carbonPayload)
    {
        var stages = carbonPayload.GetValue("carbonFootprintPerLifecycleStage", new BsonArray());
        if (stages is not BsonArray stageArray)
        {
            return [];
        }

        var result = new List<ChartSegmentViewModel>();
        foreach (var item in stageArray)
        {
            if (item is not BsonDocument stage)
            {
                continue;
            }

            var rawLabel = ValueText(stage.GetValue("lifecycleStage", string.Empty));
            result.Add(new ChartSegmentViewModel
            {
                Label = HumanizeLifecycleStage(rawLabel),
                Value = BatteryPassCanonicalDataCatalog.NormalizeCarbonStageValue(
                    rawLabel,
                    NumberFromValue(stage.GetValue("carbonFootprint", 0))),
                Unit = "gCO2e/kWh"
            });
        }

        return result.Where(segment => !string.IsNullOrWhiteSpace(segment.Label)).ToList();
    }

    private static string HumanizeLifecycleStage(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var normalized = value
            .Replace("RawMaterialExtraction", "raw material extraction", StringComparison.OrdinalIgnoreCase)
            .Replace("MainProduction", "main production", StringComparison.OrdinalIgnoreCase)
            .Replace("Distribution", "distribution", StringComparison.OrdinalIgnoreCase)
            .Replace("Recycling", "recycling", StringComparison.OrdinalIgnoreCase);
        if (normalized != value)
        {
            return normalized;
        }

        var withSpaces = System.Text.RegularExpressions.Regex.Replace(value, "([a-z])([A-Z])", "$1 $2");
        return withSpaces.Replace("_", " ").Trim().ToLowerInvariant();
    }

    private static List<RecycledContentChartViewModel> BuildRecycledContentFromAspects(BsonDocument circularityPayload)
    {
        var recycledValues = circularityPayload.GetValue("recycledContent", new BsonArray());
        if (recycledValues is not BsonArray recycledArray)
        {
            return [];
        }

        var result = new List<RecycledContentChartViewModel>();
        foreach (var item in recycledArray)
        {
            if (item is not BsonDocument recycled)
            {
                continue;
            }

            var pre = recycled.GetValue("preConsumerShare", 0).ToDouble();
            var post = recycled.GetValue("postConsumerShare", 0).ToDouble();
            result.Add(new RecycledContentChartViewModel
            {
                Material = ValueText(recycled.GetValue("recycledMaterial", string.Empty)),
                PreConsumerShare = pre,
                PostConsumerShare = post,
                PrimaryMaterialShare = Math.Max(0, 100 - pre - post)
            });
        }

        return result.Where(row => !string.IsNullOrWhiteSpace(row.Material)).ToList();
    }

    private static List<BatteryMaterialRowViewModel> ReadMaterialRows(BsonDocument materialPayload)
    {
        var rows = new List<BatteryMaterialRowViewModel>();
        var materials = materialPayload.GetValue("batteryMaterials", new BsonArray());
        if (materials is not BsonArray materialArray)
        {
            return rows;
        }

        foreach (var item in materialArray)
        {
            if (item is not BsonDocument material)
            {
                continue;
            }

            var location = GetDocument(material.GetValue("batteryMaterialLocation", new BsonDocument()));
            rows.Add(new BatteryMaterialRowViewModel
            {
                MaterialName = ValueText(material.GetValue("batteryMaterialName", string.Empty)),
                MaterialMass = BatteryPassCanonicalDataCatalog.NormalizeMaterialMass(
                    ValueText(material.GetValue("batteryMaterialName", string.Empty)),
                    NumberFromValue(material.GetValue("batteryMaterialMass", 0))),
                ComponentName = ValueText(location.GetValue("componentName", string.Empty)),
                IsCriticalRawMaterial = material.GetValue("isCriticalRawMaterial", false).ToBoolean()
            });
        }

        return rows;
    }

    private static List<ChartSegmentViewModel> WithPercentages(IReadOnlyList<ChartSegmentViewModel> segments)
    {
        var total = segments.Sum(segment => segment.Value);
        return segments
            .Select(segment => new ChartSegmentViewModel
            {
                Label = segment.Label,
                Value = segment.Value,
                Unit = segment.Unit,
                Color = segment.Color,
                Percentage = total > 0 ? Math.Round(segment.Value / total * 100, 1) : 0
            })
            .ToList();
    }
}
