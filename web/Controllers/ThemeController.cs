using Microsoft.AspNetCore.Mvc;

namespace BatteryPassWeb.Controllers;

[Route("theme")]
public sealed class ThemeController : Controller
{
    public const string ThemeCookieName = "battery-pass-theme";

    [HttpPost("")]
    [ValidateAntiForgeryToken]
    public IActionResult SetTheme([FromForm] string? theme, [FromForm] string? returnUrl)
    {
        var normalizedTheme = NormalizeTheme(theme);
        Response.Cookies.Append(
            ThemeCookieName,
            normalizedTheme,
            new CookieOptions
            {
                HttpOnly = false,
                IsEssential = true,
                SameSite = SameSiteMode.Lax,
                Secure = Request.IsHttps,
                Expires = DateTimeOffset.UtcNow.AddYears(1)
            });

        return LocalRedirect(Url.IsLocalUrl(returnUrl) ? returnUrl! : "/");
    }

    public static string NormalizeTheme(string? theme) =>
        string.Equals(theme, "dark", StringComparison.OrdinalIgnoreCase) ? "dark" : "light";
}
