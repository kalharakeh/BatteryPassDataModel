namespace BatteryPassWeb.Models.ViewModels;

public sealed class ResetPasswordViewModel
{
    public string Email { get; set; } = string.Empty;
    public string Token { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string PasswordConfirmation { get; set; } = string.Empty;
    public string ErrorMessage { get; set; } = string.Empty;
}
