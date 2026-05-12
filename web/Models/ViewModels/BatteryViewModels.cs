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
}
