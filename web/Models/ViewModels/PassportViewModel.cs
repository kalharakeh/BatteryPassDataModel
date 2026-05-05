namespace BatteryPassWeb.Models.ViewModels;

public sealed class PassportViewModel
{
    public string PassportId { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public string ModelNumber { get; init; } = string.Empty;
    public string SerialNumber { get; init; } = string.Empty;
    public string ManufacturerName { get; init; } = string.Empty;
    public string FacilityId { get; init; } = string.Empty;
    public string RegistryStatus { get; init; } = string.Empty;
    public string ClusterId { get; init; } = string.Empty;
    public string ClusterLabel { get; init; } = "No cluster assigned";
    public string Category { get; init; } = string.Empty;
    public string BatteryStatus { get; init; } = string.Empty;
    public string ManufacturedDate { get; init; } = string.Empty;
    public double Weight { get; init; }
    public string WeightLabel { get; init; } = string.Empty;
    public bool IsValid { get; init; }
    public string VerificationState { get; init; } = "unverified";
    public string BatteryImageUrl { get; init; } = "/sample-battery.png";
    public string BatteryImageAlt { get; init; } = "Industrial EV battery pack";
    public double CarbonFootprint { get; init; }
    public string CarbonFootprintLabel { get; init; } = string.Empty;
    public string PerformanceClass { get; init; } = string.Empty;
    public PassportPerformanceViewModel Performance { get; init; } = new();
    public PassportCircularityViewModel Circularity { get; init; } = new();
    public PassportDocumentsViewModel Documents { get; init; } = new();
    public IReadOnlyList<ChartSegmentViewModel> MaterialCompositionSegments { get; init; } = [];
    public IReadOnlyList<ChartSegmentViewModel> CarbonFootprintSegments { get; init; } = [];
    public IReadOnlyList<RecycledContentChartViewModel> RecycledContentCharts { get; init; } = [];
    public IReadOnlyList<BatteryMaterialRowViewModel> BatteryMaterials { get; init; } = [];
    public double MaterialCompositionTotal { get; init; }
    public double SupplyChainIndex { get; init; }
    public IReadOnlyList<string> Sections { get; init; } =
    [
        "General",
        "Material composition",
        "Performance",
        "Compliance",
        "Supply chain",
        "Circularity",
        "Carbon Footprint"
    ];
}

public sealed class PassportPerformanceViewModel
{
    public double RatedEnergy { get; init; }
    public double RatedCapacity { get; init; }
    public double RatedMaximumPower { get; init; }
    public double NominalVoltage { get; init; }
    public double ExpectedLifetime { get; init; }
    public double ExpectedNumberOfCycles { get; init; }
    public double StateOfCharge { get; init; }
    public double RemainingCapacity { get; init; }
    public double RemainingEnergy { get; init; }
    public double Cycles { get; init; }
}

public sealed class PassportCircularityViewModel
{
    public string SeparateCollection { get; init; } = string.Empty;
    public string WastePrevention { get; init; } = string.Empty;
    public string RecycledContentShareVerification { get; init; } = "unverified";
}

public sealed class PassportDocumentsViewModel
{
    public PassportDocumentLinkViewModel ConformityAssessment { get; init; } = new("Conformity assessment", string.Empty);
    public PassportDocumentLinkViewModel EuDeclarationOfConformity { get; init; } = new("EU declaration of conformity ID", string.Empty);
    public PassportDocumentLinkViewModel SustainabilityReport { get; init; } = new("Sustainability report", string.Empty);
    public PassportDocumentLinkViewModel DueDiligenceReport { get; init; } = new("Due diligence report", string.Empty);
    public PassportDocumentLinkViewModel ThirdPartyAudit { get; init; } = new("Third party audit", string.Empty);
    public PassportDocumentLinkViewModel TaxonomyReport { get; init; } = new("Taxonomy report", string.Empty);
    public PassportDocumentLinkViewModel Co2StudyReference { get; init; } = new("CO2 study reference", string.Empty);
}

public sealed record PassportDocumentLinkViewModel(string Label, string Url, string FileId = "", string ContentType = "");

public sealed class ChartSegmentViewModel
{
    public string Label { get; init; } = string.Empty;
    public double Value { get; init; }
    public string Unit { get; init; } = string.Empty;
    public string Color { get; init; } = string.Empty;
    public double Percentage { get; init; }
}

public sealed class RecycledContentChartViewModel
{
    public string Material { get; init; } = string.Empty;
    public double PreConsumerShare { get; init; }
    public double PostConsumerShare { get; init; }
    public double PrimaryMaterialShare { get; init; }
}

public sealed class BatteryMaterialRowViewModel
{
    public string MaterialName { get; init; } = string.Empty;
    public double MaterialMass { get; init; }
    public string ComponentName { get; init; } = string.Empty;
    public bool IsCriticalRawMaterial { get; init; }
}
