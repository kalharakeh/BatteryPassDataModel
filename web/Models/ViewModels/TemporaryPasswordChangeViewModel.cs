namespace BatteryPassWeb.Models.ViewModels;

public sealed class TemporaryPasswordChangeViewModel
{
    public string Email { get; set; } = string.Empty;
    public string TemporaryPassword { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string PasswordConfirmation { get; set; } = string.Empty;
    public string StatusMessage { get; set; } = string.Empty;
    public string ErrorMessage { get; set; } = string.Empty;
}
