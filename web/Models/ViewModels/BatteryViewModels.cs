namespace BatteryPassWeb.Models.ViewModels;

public sealed class BatterySummaryViewModel
{
    public string BatteryId { get; init; } = string.Empty;
    public string BatteryFamily { get; init; } = string.Empty;
    public string BatteryModel { get; init; } = string.Empty;
    public string BatterySerialNumber { get; init; } = string.Empty;
    public string ClusterId { get; init; } = string.Empty;
    public string ClusterLabel { get; init; } = "No cluster assigned";
    public int PassportCount { get; init; }
    public string LatestPassportId { get; init; } = string.Empty;
    public string LatestPassportStatus { get; init; } = "Draft";
    public string UpdatedDate { get; init; } = string.Empty;
    public bool NewPassportRequired { get; init; }
    public IReadOnlyList<BatteryPassportHistoryRowViewModel> Passports { get; init; } = [];
    public string SummaryUrl { get; set; } = string.Empty;
    public string DetailUrl { get; set; } = string.Empty;
    public string HistoryUrl { get; set; } = string.Empty;
    public string EditUrl { get; set; } = string.Empty;
    public string ConformanceUrl { get; set; } = string.Empty;
    public string CreatePassportUrl { get; set; } = string.Empty;
    public string ReturnUrl { get; set; } = string.Empty;
    public bool CanViewHistory { get; set; }
    public bool CanEditBattery { get; set; }
    public bool CanCreatePassport { get; set; }
    public bool CanOpenConformance { get; set; }
    public bool ShowNewPassportRequired { get; set; }
}

public sealed class BatteryPassportHistoryRowViewModel
{
    public string PassportId { get; init; } = string.Empty;
    public string BatteryId { get; init; } = string.Empty;
    public string CreatedAt { get; init; } = string.Empty;
    public string PassportStatus { get; init; } = "Draft";
    public bool IsLatestForBattery { get; init; }
    public bool IsPubliclyVisible { get; init; }
}

public sealed class BatteryDetailViewModel
{
    public required BatterySummaryViewModel Battery { get; init; }
    public string AccessNotice { get; init; } = string.Empty;
}

public sealed class BatteryPassportHistoryPageViewModel
{
    public required BatterySummaryViewModel Battery { get; init; }
    public string StatusMessage { get; init; } = string.Empty;
    public string ErrorMessage { get; init; } = string.Empty;
    public string ReturnUrl { get; init; } = "/admin/clusters?tab=batteries";
    public string ReturnLabel { get; init; } = "Back to Batteries";
}

public sealed class BatteryTablePageViewModel
{
    public string Query { get; init; } = string.Empty;
    public string SearchAction { get; init; } = "/registry";
    public string SearchPlaceholder { get; init; } = "Search by battery ID, passport ID, serial, or cluster";
    public string ScopeLabel { get; init; } = string.Empty;
    public string EmptyLabel { get; init; } = "No batteries found.";
    public string AccessMessage { get; init; } = string.Empty;
    public string RedirectPath { get; init; } = string.Empty;
    public string ReturnUrl { get; init; } = "/registry";
    public bool ShowCreateBattery { get; init; }
    public bool ShowHelp { get; init; }
    public IReadOnlyList<BatterySummaryViewModel> Rows { get; init; } = [];
}
