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
        await _authService.StorePasswordResetRequestAsync(
            email,
            HttpContext.Connection.RemoteIpAddress?.ToString() ?? string.Empty,
            cancellationToken);
        TempData["ForgotPasswordMessage"] = "If an account exists, reset instructions will be sent when email delivery is configured.";
        return Redirect("/login");
    }

    [HttpPost("logout")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return Redirect("/");
    }
}
