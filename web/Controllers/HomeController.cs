using BatteryPassWeb.Models.ViewModels;
using BatteryPassWeb.Services;
using Microsoft.AspNetCore.Mvc;

namespace BatteryPassWeb.Controllers;

[Route("")]
public class HomeController : Controller
{
    private const string SamplePassportId = "did:web:acme.battery.pass:sample-customer-north-001";
    private readonly PassportRepository _passportRepository;
    private readonly PassportPublishPolicyService _passportPublishPolicyService;

    public HomeController(PassportRepository passportRepository, PassportPublishPolicyService passportPublishPolicyService)
    {
        _passportRepository = passportRepository;
        _passportPublishPolicyService = passportPublishPolicyService;
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
        var query = ExtractPassportIdFromQrPayload(q?.Trim() ?? string.Empty);
        var isAdmin = AccessControlService.IsAdmin(User);
        if (query.Length == 0)
        {
            return RedirectToAction(nameof(Index));
        }

        var documents = await _passportRepository.SearchDocumentsAsync(query, includeArchived: false, cancellationToken);
        var matches = documents
            .Where(document => isAdmin || _passportPublishPolicyService.IsPubliclyVisible(document))
            .Select(_passportRepository.ToSummaryViewModel)
            .ToList();
        var exactMatch = matches.FirstOrDefault(match => string.Equals(match.PassportId, query, StringComparison.OrdinalIgnoreCase));
        if (exactMatch != null)
        {
            return Redirect($"/{Uri.EscapeDataString(exactMatch.PassportId)}/summary");
        }

        return RedirectToAction(nameof(Index), new
        {
            q = query,
            notFound = "1"
        });
    }

    private static string ExtractPassportIdFromQrPayload(string value)
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
            foreach (var key in new[] { "q", "passportId" })
            {
                var candidate = ReadQueryValue(absoluteUri.Query, key);
                if (!string.IsNullOrWhiteSpace(candidate) && candidate.StartsWith("did:web:", StringComparison.OrdinalIgnoreCase))
                {
                    return candidate;
                }
            }

            foreach (var segment in absoluteUri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries))
            {
                var candidate = Uri.UnescapeDataString(segment);
                if (candidate.StartsWith("did:web:", StringComparison.OrdinalIgnoreCase))
                {
                    return candidate;
                }
            }
        }

        return value;
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
