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

    public AuthApiController(AuthService authService)
    {
        _authService = authService;
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromForm] string? email, [FromForm] string? password, [FromForm] string? next, CancellationToken cancellationToken)
    {
        var principal = await _authService.AuthenticateAsync(email ?? string.Empty, password ?? string.Empty, cancellationToken);
        if (principal == null)
        {
            return Redirect($"/login?error=invalid{(string.IsNullOrWhiteSpace(next) ? string.Empty : $"&next={Uri.EscapeDataString(next)}")}");
        }

        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal);

        if (!string.IsNullOrWhiteSpace(next) && Url.IsLocalUrl(next))
        {
            return Redirect(next);
        }

        return Redirect("/admin");
    }

    [HttpPost("logout")]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return Redirect("/");
    }
}
