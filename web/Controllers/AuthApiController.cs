using BatteryPassWeb.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;

namespace BatteryPassWeb.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthApiController : ControllerBase
{
    private readonly AuthService _authService;
    private readonly AuthenticationSessionService _authenticationSessionService;

    public AuthApiController(AuthService authService, AuthenticationSessionService authenticationSessionService)
    {
        _authService = authService;
        _authenticationSessionService = authenticationSessionService;
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromForm] string? email, [FromForm] string? password, [FromForm] string? next, CancellationToken cancellationToken)
    {
        var safeNext = SafeInteractiveReturnUrl(next);
        var principal = await _authService.AuthenticateAsync(email ?? string.Empty, password ?? string.Empty, cancellationToken);
        if (principal == null)
        {
            return Redirect($"/login?error=invalid{(string.IsNullOrWhiteSpace(safeNext) ? string.Empty : $"&next={Uri.EscapeDataString(safeNext)}")}");
        }

        await _authenticationSessionService.SignInAsync(HttpContext, principal, cancellationToken);

        if (!string.IsNullOrWhiteSpace(safeNext))
        {
            return Redirect(safeNext);
        }

        return Redirect("/admin");
    }

    [HttpPost("logout")]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return Redirect("/");
    }

    private string? SafeInteractiveReturnUrl(string? returnUrl)
    {
        if (string.IsNullOrWhiteSpace(returnUrl) || !Url.IsLocalUrl(returnUrl))
        {
            return null;
        }

        var pathOnly = returnUrl.Split('?', '#')[0];
        if (pathOnly.Equals("/api", StringComparison.OrdinalIgnoreCase)
            || pathOnly.StartsWith("/api/", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return returnUrl;
    }
}
