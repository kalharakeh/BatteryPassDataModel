using BatteryPassWeb.Models.Trust;

namespace BatteryPassWeb.Models.ViewModels;

public sealed class AdminClusterViewModel
{
    public string SelectedTab { get; init; } = "passports";
    public string PassportsQuery { get; init; } = string.Empty;
    public IReadOnlyList<ClusterViewModel> Clusters { get; init; } = [];
    public IReadOnlyList<PassportSummaryViewModel> Passports { get; init; } = [];
    public IReadOnlyList<UserViewModel> Users { get; init; } = [];
    public IReadOnlyList<ClusterMembershipViewModel> Memberships { get; init; } = [];
    public IReadOnlyList<ApiTokenViewModel> ApiTokens { get; init; } = [];
    public IReadOnlyList<BatterySecretViewModel> BatterySecrets { get; init; } = [];
    public IReadOnlyList<ProductTemplateSummaryViewModel> ProductTemplates { get; init; } = [];
    public string SamplePassportId { get; init; } = string.Empty;
    public string SampleReadToken { get; init; } = string.Empty;
    public string SampleReadWriteToken { get; init; } = string.Empty;
    public string StatusMessage { get; init; } = string.Empty;
    public string ErrorMessage { get; init; } = string.Empty;
    public string GeneratedCredential { get; init; } = string.Empty;
}

public sealed class ProductTemplateSummaryViewModel
{
    public string ProductId { get; init; } = string.Empty;
    public string ProductName { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string ImageUrl { get; init; } = string.Empty;
    public int ModuleCount { get; init; }
    public int SoftwareVersionCount { get; init; }
    public int RequiredFieldCount { get; init; }
    public int DocumentCount { get; init; }
}

public sealed class ProductSoftwareVersionViewModel
{
    public string Version { get; init; } = string.Empty;
    public string ReleaseDate { get; init; } = string.Empty;
    public string LatestUpdate { get; init; } = string.Empty;
}

public sealed class ProductTemplateFormCatalogItemViewModel
{
    public string ProductId { get; init; } = string.Empty;
    public string ProductName { get; init; } = string.Empty;
    public string ImageUrl { get; init; } = string.Empty;
    public int ModuleCount { get; init; }
    public double BatteryMassKg { get; init; }
    public double RatedEnergyKwh { get; init; }
    public double RatedCapacityAh { get; init; }
    public double RatedMaximumPowerKw { get; init; }
    public double NominalVoltageV { get; init; }
    public double ExpectedLifetimeYears { get; init; }
    public double ExpectedCycles { get; init; }
    public double SupplyChainIndex { get; init; }
    public double CarbonFootprint { get; init; }
    public string PerformanceClass { get; init; } = string.Empty;
    public IReadOnlyDictionary<string, double> MaterialMassesKg { get; init; } = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
    public IReadOnlyDictionary<string, double> CarbonStages { get; init; } = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
    public IReadOnlyDictionary<string, ProductTemplateRecycledContentViewModel> RecycledContent { get; init; } = new Dictionary<string, ProductTemplateRecycledContentViewModel>(StringComparer.OrdinalIgnoreCase);
    public IReadOnlyList<ProductSoftwareVersionViewModel> SoftwareVersions { get; init; } = [];
}

public sealed class ProductTemplateRecycledContentViewModel
{
    public double PreConsumerShare { get; init; }
    public double PostConsumerShare { get; init; }
}

public sealed class ProductTemplateEditViewModel
{
    public string ProductId { get; init; } = string.Empty;
    public string ProductName { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string ImageUrl { get; init; } = string.Empty;
    public int ModuleCount { get; init; }
    public double BatteryMassKg { get; init; }
    public double RatedEnergyKwh { get; init; }
    public double RatedCapacityAh { get; init; }
    public double RatedMaximumPowerKw { get; init; }
    public double NominalVoltageV { get; init; }
    public double ExpectedLifetimeYears { get; init; }
    public double ExpectedCycles { get; init; }
    public double SupplyChainIndex { get; init; }
    public double CarbonFootprint { get; init; }
    public string PerformanceClass { get; init; } = string.Empty;
    public IReadOnlyDictionary<string, double> MaterialMassesKg { get; init; } = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
    public IReadOnlyDictionary<string, double> CarbonStages { get; init; } = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
    public IReadOnlyDictionary<string, ProductTemplateRecycledContentViewModel> RecycledContent { get; init; } = new Dictionary<string, ProductTemplateRecycledContentViewModel>(StringComparer.OrdinalIgnoreCase);
    public IReadOnlyList<ProductSoftwareVersionViewModel> SoftwareVersions { get; init; } = [];
    public DataCompletionPolicySnapshot DataRequirements { get; init; } = new();
    public string StatusMessage { get; init; } = string.Empty;
    public string ErrorMessage { get; init; } = string.Empty;
}

public sealed class ClusterViewModel
{
    public string ClusterId { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string CreatedAt { get; init; } = string.Empty;
    public string UpdatedAt { get; init; } = string.Empty;
}

public sealed class ClusterMembershipViewModel
{
    public string Email { get; init; } = string.Empty;
    public string ClusterId { get; init; } = string.Empty;
    public string Role { get; init; } = "member";
    public string CreatedAt { get; init; } = string.Empty;
    public string UpdatedAt { get; init; } = string.Empty;
}

public sealed class UserViewModel
{
    public string Email { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public IReadOnlyList<string> Roles { get; init; } = [];
}

public sealed class ApiTokenViewModel
{
    public string TokenId { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string AccessMode { get; init; } = "read";
    public bool GlobalAccess { get; init; }
    public bool AllowUnassigned { get; init; }
    public bool IsActive { get; init; } = true;
    public bool IsSample { get; init; }
    public string ClusterIdsLabel { get; init; } = string.Empty;
    public string CreatedAt { get; init; } = string.Empty;
    public string UpdatedAt { get; init; } = string.Empty;
    public string LastUsedAt { get; init; } = string.Empty;
}

public sealed class BatterySecretViewModel
{
    public string PassportId { get; init; } = string.Empty;
    public string ClusterId { get; init; } = string.Empty;
    public string ClusterLabel { get; init; } = string.Empty;
    public bool IsActive { get; init; } = true;
    public string CreatedAt { get; init; } = string.Empty;
    public string UpdatedAt { get; init; } = string.Empty;
}
