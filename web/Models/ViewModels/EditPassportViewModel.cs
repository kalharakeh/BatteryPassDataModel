namespace BatteryPassWeb.Models.ViewModels;

public sealed class EditPassportViewModel
{
    public required PassportViewModel Passport { get; init; }
    public string Mode { get; init; } = "edit";
    public string StatusMessage { get; init; } = string.Empty;
    public string ErrorMessage { get; init; } = string.Empty;
}
