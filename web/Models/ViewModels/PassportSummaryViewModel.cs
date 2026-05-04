namespace BatteryPassWeb.Models.ViewModels;

public sealed class PassportSummaryViewModel
{
    public string PassportId { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public string ModelNumber { get; init; } = string.Empty;
    public string ManufacturerName { get; init; } = string.Empty;
    public string SerialNumber { get; init; } = string.Empty;
    public string RegistryStatus { get; init; } = string.Empty;
    public string ClusterId { get; init; } = string.Empty;
    public string BatteryImageUrl { get; init; } = string.Empty;
}
