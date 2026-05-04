namespace BatteryPassWeb.Models.ViewModels;

public sealed class LoginViewModel
{
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string ReturnUrl { get; set; } = string.Empty;
    public string ErrorMessage { get; set; } = string.Empty;
}
