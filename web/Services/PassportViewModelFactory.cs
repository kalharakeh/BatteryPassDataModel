using BatteryPassWeb.Models.ViewModels;
using MongoDB.Bson;

namespace BatteryPassWeb.Services;

public sealed class PassportViewModelFactory
{
    private const string DefaultImage = "/sample-battery.png";

    public PassportViewModel Create(BsonDocument document, IReadOnlyDictionary<string, string>? clusterNamesById = null)
    {
        var app = GetDocument(BsonHelpers.GetValue(document, "app"));
        var appDisplay = GetDocument(app.GetValue("display", new BsonDocument()));
        var appMedia = GetDocument(app.GetValue("media", new BsonDocument()));
        var appDocuments = GetDocument(app.GetValue("documents", new BsonDocument()));
        var appCharts = GetDocument(app.GetValue("charts", new BsonDocument()));
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
        var serialNumber = FirstNonEmpty(
            appDisplay.GetValue("serialNumber", string.Empty).ToString(),
            BsonHelpers.GetString(generalPayload, "batteryPassportIdentifier"));
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
            DefaultImage));
        var batteryImageAlt = FirstNonEmpty(
            appMedia.GetValue("batteryImageAlt", string.Empty).ToString(),
            $"Industrial EV battery pack for passport {modelNumber}");

        var materialSegments = WithPercentages(ReadChartSegments(appCharts.GetValue("materialComposition", new BsonArray())));
        if (materialSegments.Count == 0)
        {
            materialSegments = WithPercentages(BuildMaterialSegmentsFromAspects(materialPayload));
        }

        var carbonSegments = WithPercentages(ReadChartSegments(appCharts.GetValue("carbonFootprint", new BsonArray())));
        if (carbonSegments.Count == 0)
        {
            carbonSegments = WithPercentages(BuildCarbonSegmentsFromAspects(carbonPayload));
        }

        var recycledContentCharts = ReadRecycledContentCharts(appCharts.GetValue("recycledContent", new BsonArray()));
        if (recycledContentCharts.Count == 0)
        {
            recycledContentCharts = BuildRecycledContentFromAspects(circularityPayload);
        }

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

        var carbonFootprint = NumberAt(carbonPayload, "batteryCarbonFootprint");
        var isValid = BoolAt(document, "validation", "isValid");

        return new PassportViewModel
        {
            PassportId = BsonHelpers.GetString(document, "passportId"),
            DisplayName = displayName,
            ModelNumber = modelNumber,
            SerialNumber = serialNumber,
            ManufacturerName = manufacturerName,
            FacilityId = facilityId,
            RegistryStatus = BsonHelpers.GetString(document, "registryInfo", "status"),
            ClusterId = clusterId,
            ClusterLabel = clusterLabel,
            Category = BsonHelpers.GetString(generalPayload, "batteryCategory").ToUpperInvariant(),
            BatteryStatus = BsonHelpers.GetString(generalPayload, "batteryStatus"),
            ManufacturedDate = DateOnly(BsonHelpers.GetString(generalPayload, "manufacturingDate")),
            Weight = weight,
            WeightLabel = $"{weight:F2}kg",
            IsValid = isValid,
            VerificationState = isValid ? "verified" : "unverified",
            BatteryImageUrl = batteryImageUrl,
            BatteryImageAlt = batteryImageAlt,
            CarbonFootprint = carbonFootprint,
            CarbonFootprintLabel = $"{carbonFootprint:F2}gCO2e/kWh",
            PerformanceClass = BsonHelpers.GetString(carbonPayload, "carbonFootprintPerformanceClass"),
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
            SupplyChainIndex = NumberAt(supplyChainPayload, "supplyChainIndicies")
        };
    }

    private static PassportDocumentLinkViewModel ReadDocument(BsonDocument appDocuments, string key, string fallbackLabel, string fallbackUrl)
    {
        var document = GetDocument(appDocuments.GetValue(key, new BsonDocument()));
        var fileId = document.GetValue("fileId", string.Empty).ToString();
        var label = FirstNonEmpty(document.GetValue("label", string.Empty).ToString(), fallbackLabel);
        var url = NormalizeDocumentUrl(FirstNonEmpty(document.GetValue("url", string.Empty).ToString(), fallbackUrl), fileId);
        return new PassportDocumentLinkViewModel(
            label,
            url,
            fileId,
            document.GetValue("contentType", string.Empty).ToString());
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

    private static string NormalizeAssetUrl(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return DefaultImage;
        }

        if (Uri.TryCreate(url, UriKind.Absolute, out var absoluteUri) && absoluteUri.AbsolutePath.StartsWith("/", StringComparison.Ordinal))
        {
            return absoluteUri.AbsolutePath;
        }

        return url;
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

    private static string FirstNonEmpty(params string[] values)
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

    private static List<ChartSegmentViewModel> ReadChartSegments(BsonValue chartValue)
    {
        if (chartValue is not BsonArray chartArray)
        {
            return [];
        }

        var segments = new List<ChartSegmentViewModel>();
        foreach (var item in chartArray)
        {
            if (item is not BsonDocument segment)
            {
                continue;
            }

            segments.Add(new ChartSegmentViewModel
            {
                Label = segment.GetValue("label", string.Empty).ToString(),
                Value = segment.GetValue("value", 0).ToDouble(),
                Unit = segment.GetValue("unit", string.Empty).ToString(),
                Color = segment.GetValue("color", string.Empty).ToString()
            });
        }

        return segments.Where(segment => !string.IsNullOrWhiteSpace(segment.Label)).ToList();
    }

    private static List<ChartSegmentViewModel> BuildMaterialSegmentsFromAspects(BsonDocument materialPayload)
    {
        var materials = materialPayload.GetValue("batteryMaterials", new BsonArray());
        if (materials is not BsonArray materialArray)
        {
            return [];
        }

        var colorByIndex = new[]
        {
            "#4f6f7d", "#d76f3d", "#aeb4ba", "#27313f", "#d9b64e", "#0aa34f", "#85c7d6", "#e7d99d"
        };

        var index = 0;
        var result = new List<ChartSegmentViewModel>();
        foreach (var item in materialArray)
        {
            if (item is not BsonDocument material)
            {
                continue;
            }

            result.Add(new ChartSegmentViewModel
            {
                Label = material.GetValue("batteryMaterialName", string.Empty).ToString(),
                Value = material.GetValue("batteryMaterialMass", 0).ToDouble(),
                Unit = "kg",
                Color = colorByIndex[index % colorByIndex.Length]
            });
            index++;
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

            var rawLabel = stage.GetValue("lifecycleStage", string.Empty).ToString();
            result.Add(new ChartSegmentViewModel
            {
                Label = HumanizeLifecycleStage(rawLabel),
                Value = stage.GetValue("carbonFootprint", 0).ToDouble(),
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

    private static List<RecycledContentChartViewModel> ReadRecycledContentCharts(BsonValue recycledValue)
    {
        if (recycledValue is not BsonArray recycledArray)
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

            result.Add(new RecycledContentChartViewModel
            {
                Material = recycled.GetValue("material", string.Empty).ToString(),
                PreConsumerShare = recycled.GetValue("preConsumerShare", 0).ToDouble(),
                PostConsumerShare = recycled.GetValue("postConsumerShare", 0).ToDouble(),
                PrimaryMaterialShare = recycled.GetValue("primaryMaterialShare", 0).ToDouble()
            });
        }

        return result.Where(row => !string.IsNullOrWhiteSpace(row.Material)).ToList();
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
                Material = recycled.GetValue("recycledMaterial", string.Empty).ToString(),
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
                MaterialName = material.GetValue("batteryMaterialName", string.Empty).ToString(),
                MaterialMass = material.GetValue("batteryMaterialMass", 0).ToDouble(),
                ComponentName = location.GetValue("componentName", string.Empty).ToString(),
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
