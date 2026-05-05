namespace BatteryPassWeb.Models.ViewModels;

public sealed class PassportDetailViewModel
{
    public required PassportViewModel Passport { get; init; }
    public string AccessNotice { get; init; } = string.Empty;
}
