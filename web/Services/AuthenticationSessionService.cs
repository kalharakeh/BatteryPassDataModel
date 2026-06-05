using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace BatteryPassWeb.Services;

public sealed class AuthenticationSessionService
{
    private readonly ApplicationSettingsService _applicationSettingsService;

    public AuthenticationSessionService(ApplicationSettingsService applicationSettingsService)
    {
        _applicationSettingsService = applicationSettingsService;
    }

    public async Task SignInAsync(
        HttpContext httpContext,
        ClaimsPrincipal principal,
        CancellationToken cancellationToken = default)
    {
        var timeout = await _applicationSettingsService.GetSessionTimeoutAsync(cancellationToken);
        var now = DateTimeOffset.UtcNow;
        var properties = new AuthenticationProperties
        {
            AllowRefresh = true,
            IssuedUtc = now,
            ExpiresUtc = now.Add(timeout)
        };

        await httpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal, properties);
    }

    public async Task ValidatePrincipalAsync(CookieValidatePrincipalContext context)
    {
        var timeout = await _applicationSettingsService.GetSessionTimeoutAsync(context.HttpContext.RequestAborted);
        var now = DateTimeOffset.UtcNow;
        var issuedUtc = context.Properties.IssuedUtc;
        var expiresUtc = context.Properties.ExpiresUtc;

        if ((issuedUtc.HasValue && now - issuedUtc.Value > timeout)
            || (expiresUtc.HasValue && expiresUtc.Value <= now))
        {
            context.RejectPrincipal();
            await context.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return;
        }

        if (!expiresUtc.HasValue || expiresUtc.Value - now <= TimeSpan.FromTicks(timeout.Ticks / 2))
        {
            context.Properties.IssuedUtc = now;
            context.Properties.ExpiresUtc = now.Add(timeout);
            context.ShouldRenew = true;
        }
    }
}
