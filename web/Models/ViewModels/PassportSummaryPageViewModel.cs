namespace BatteryPassWeb.Models.ViewModels;

public sealed class PassportSummaryPageViewModel
{
    public required PassportViewModel Passport { get; init; }
    public string DetailAccessNotice { get; init; } = string.Empty;
}
