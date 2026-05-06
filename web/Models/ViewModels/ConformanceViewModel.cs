using BatteryPassWeb.Models.Trust;

namespace BatteryPassWeb.Models.ViewModels;

public sealed class ConformanceViewModel
{
    public required PassportViewModel Passport { get; init; }
    public required TrustValidationSummary ValidationSummary { get; init; }
    public string StatusMessage { get; init; } = string.Empty;
    public string ErrorMessage { get; init; } = string.Empty;
}
