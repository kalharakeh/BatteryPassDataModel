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
        var principal = await _authService.AuthenticateAsync(model.Email, model.Password, cancellationToken);
        if (principal == null)
        {
            model.ErrorMessage = "Invalid email or password.";
            return View(model);
        }

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
        TempData["ForgotPasswordMessage"] = "If an account exists, reset instructions have been sent.";
        return Redirect("/login");
    }

    [HttpGet("reset-password")]
    public IActionResult ResetPassword([FromQuery] string? email, [FromQuery] string? token)
    {
        return View(new ResetPasswordViewModel
        {
            Email = email ?? string.Empty,
            Token = token ?? string.Empty
        });
    }

    [HttpPost("reset-password")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResetPassword(ResetPasswordViewModel model, CancellationToken cancellationToken)
    {
        if (!model.Password.Equals(model.PasswordConfirmation, StringComparison.Ordinal))
        {
            model.ErrorMessage = "Passwords do not match.";
            model.Password = string.Empty;
            model.PasswordConfirmation = string.Empty;
            return View(model);
        }

        var success = await _authService.ConsumePasswordResetTokenAsync(
            model.Email,
            model.Token,
            model.Password,
            cancellationToken);
        if (!success)
        {
            model.ErrorMessage = "The reset link is invalid or expired.";
            model.Password = string.Empty;
            model.PasswordConfirmation = string.Empty;
            return View(model);
        }

        TempData["ForgotPasswordMessage"] = "Password reset complete. Sign in with your new password.";
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
