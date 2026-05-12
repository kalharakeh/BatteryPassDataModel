using BatteryPassWeb.Services;
using Microsoft.AspNetCore.Mvc;

namespace BatteryPassWeb.Controllers;

[Route("")]
public class HomeController : Controller
{
    private const string SamplePassportId = "did:web:acme.battery.pass:sample-customer-north-001";
    private readonly BatteryRouteResolutionService _batteryRouteResolutionService;

    public HomeController(BatteryRouteResolutionService batteryRouteResolutionService)
    {
        _batteryRouteResolutionService = batteryRouteResolutionService;
    }

    [HttpGet("")]
    public IActionResult Index([FromQuery] string? q, [FromQuery] string? notFound)
    {
        ViewData["SamplePassportId"] = SamplePassportId;
        ViewData["SearchQuery"] = q?.Trim() ?? string.Empty;
        ViewData["SearchNotFound"] = string.Equals(notFound, "1", StringComparison.OrdinalIgnoreCase);
        ViewData["IsAdminSearch"] = AccessControlService.IsAdmin(User);
        return View();
    }

    [HttpGet("search")]
    public async Task<IActionResult> Search([FromQuery] string? q, CancellationToken cancellationToken)
    {
        var query = ExtractIdFromQrPayload(q?.Trim() ?? string.Empty);
        if (query.Length == 0)
        {
            return RedirectToAction(nameof(Index));
        }

        var resolution = await _batteryRouteResolutionService.ResolveAsync(query, cancellationToken);
        if (resolution.Kind is BatteryRouteTargetKind.Battery or BatteryRouteTargetKind.Passport)
        {
            return Redirect($"/{Uri.EscapeDataString(query)}");
        }

        return RedirectToAction(nameof(Index), new
        {
            q = query,
            notFound = "1"
        });
    }

    private static string ExtractIdFromQrPayload(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        if (value.StartsWith("did:web:", StringComparison.OrdinalIgnoreCase))
        {
            return value;
        }

        if (Uri.TryCreate(value, UriKind.Absolute, out var absoluteUri)
            || Uri.TryCreate($"https://local.test{(value.StartsWith('/') ? string.Empty : "/")}{value}", UriKind.Absolute, out absoluteUri))
        {
            foreach (var key in new[] { "q", "batteryId", "passportId", "id" })
            {
                var candidate = ReadQueryValue(absoluteUri.Query, key);
                if (!string.IsNullOrWhiteSpace(candidate))
                {
                    return candidate;
                }
            }

            foreach (var segment in absoluteUri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries))
            {
                var candidate = Uri.UnescapeDataString(segment);
                if (!IsRouteSuffix(candidate))
                {
                    return candidate;
                }
            }
        }

        return value;
    }

    private static bool IsRouteSuffix(string value)
    {
        return value.Equals("latest", StringComparison.OrdinalIgnoreCase)
            || value.Equals("summary", StringComparison.OrdinalIgnoreCase);
    }

    private static string ReadQueryValue(string query, string key)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return string.Empty;
        }

        foreach (var pair in query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = pair.Split('=', 2);
            if (parts.Length == 2 && Uri.UnescapeDataString(parts[0]).Equals(key, StringComparison.OrdinalIgnoreCase))
            {
                return Uri.UnescapeDataString(parts[1].Replace("+", " "));
            }
        }

        return string.Empty;
    }
}
