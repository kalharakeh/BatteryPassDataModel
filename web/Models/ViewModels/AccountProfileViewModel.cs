namespace BatteryPassWeb.Models.ViewModels;

public sealed class AccountProfileViewModel
{
    public string Email { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string RoleLabel { get; init; } = string.Empty;
    public string StatusMessage { get; init; } = string.Empty;
    public string ErrorMessage { get; init; } = string.Empty;
}
