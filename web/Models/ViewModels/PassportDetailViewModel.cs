namespace BatteryPassWeb.Models.ViewModels;

public sealed class PassportDetailViewModel
{
    public required PassportSummaryViewModel Summary { get; init; }
    public required string CanonicalJson { get; init; }
}
