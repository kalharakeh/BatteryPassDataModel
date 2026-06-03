using System.Security.Claims;
using BatteryPassWeb.Models.ViewModels;
using BatteryPassWeb.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;

namespace BatteryPassWeb.Controllers;

[Route("login")]
public class LoginController : Controller
{
    private readonly AuthService _authService;

    public LoginController(AuthService authService)
    {
        _authService = authService;
    }

    [HttpGet("")]
    public IActionResult Index([FromQuery] string? next)
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return Redirect(next ?? "/admin");
        }

        return View(new LoginViewModel { ReturnUrl = next ?? string.Empty });
    }

    [HttpPost("")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Index(LoginViewModel model, CancellationToken cancellationToken)
    {
        var loginResult = await _authService.AuthenticateLoginAsync(model.Email, model.Password, cancellationToken);
        if (loginResult.Status == LoginAuthenticationStatus.RequiresTemporaryPasswordChange)
        {
            return View("ChangeTemporaryPassword", new TemporaryPasswordChangeViewModel
            {
                Email = loginResult.Email,
                StatusMessage = "You must choose a new password before continuing."
            });
        }

        if (loginResult.Status != LoginAuthenticationStatus.Authenticated || loginResult.Principal == null)
        {
            model.ErrorMessage = "Invalid email or password.";
            return View(model);
        }

        var principal = loginResult.Principal;
        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal);

        if (!string.IsNullOrWhiteSpace(model.ReturnUrl) && Url.IsLocalUrl(model.ReturnUrl))
        {
            return Redirect(model.ReturnUrl);
        }

        if (principal.IsInRole("admin"))
        {
            return Redirect("/admin");
        }

        return Redirect("/");
    }

    [HttpPost("forgot-password")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ForgotPassword([FromForm] string email, CancellationToken cancellationToken)
    {
        await _authService.CreatePasswordResetAsync(
            email,
            HttpContext.Connection.RemoteIpAddress?.ToString() ?? string.Empty,
            RequestBaseUrl(),
            cancellationToken);
        TempData["ForgotPasswordMessage"] = "If an account exists, a temporary password has been sent.";
        return Redirect("/login");
    }

    [HttpGet("change-temporary-password")]
    public IActionResult ChangeTemporaryPassword()
    {
        return View(new TemporaryPasswordChangeViewModel());
    }

    [HttpPost("change-temporary-password")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangeTemporaryPassword(TemporaryPasswordChangeViewModel model, CancellationToken cancellationToken)
    {
        if (!model.Password.Equals(model.PasswordConfirmation, StringComparison.Ordinal))
        {
            model.ErrorMessage = "Passwords do not match.";
            model.Password = string.Empty;
            model.PasswordConfirmation = string.Empty;
            return View(model);
        }

        var success = await _authService.ConsumeTemporaryPasswordAsync(
            model.Email,
            model.TemporaryPassword,
            model.Password,
            cancellationToken);
        if (!success)
        {
            model.ErrorMessage = "The temporary password is invalid or expired.";
            model.TemporaryPassword = string.Empty;
            model.Password = string.Empty;
            model.PasswordConfirmation = string.Empty;
            return View(model);
        }

        TempData["ForgotPasswordMessage"] = "Password changed. Sign in with your new password.";
        return Redirect("/login");
    }

    [HttpPost("logout")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return Redirect("/");
    }

    private string RequestBaseUrl() => $"{Request.Scheme}://{Request.Host}";
}
