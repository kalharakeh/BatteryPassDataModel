namespace BatteryPassWeb.Models.ViewModels;

public sealed class PassportSummaryPageViewModel
{
    public required PassportViewModel Passport { get; init; }
    public bool CanOpenDetail { get; init; }
    public string DetailActionUrl { get; init; } = string.Empty;
    public string DetailAccessNotice { get; init; } = string.Empty;
}
