namespace BatteryPassWeb.Models.ViewModels;

public sealed class PassportSummaryViewModel
{
    public string PassportId { get; init; } = string.Empty;
    public string BatteryId { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public string ModelNumber { get; init; } = string.Empty;
    public string ManufacturerName { get; init; } = string.Empty;
    public string SerialNumber { get; init; } = string.Empty;
    public string RegistryStatus { get; init; } = string.Empty;
    public string ClusterId { get; init; } = string.Empty;
    public string ClusterLabel { get; init; } = "No cluster assigned";
    public string BatteryFamily { get; init; } = string.Empty;
    public string BatteryVersion { get; init; } = string.Empty;
    public string BatterySerialNumber { get; init; } = string.Empty;
    public string PassportStatus { get; init; } = "Draft";
    public string BatteryImageUrl { get; init; } = string.Empty;
    public string UpdatedDate { get; init; } = string.Empty;
}
