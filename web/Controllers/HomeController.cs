using BatteryPassWeb.Dpp.Pages;
using Microsoft.AspNetCore.Mvc;

namespace BatteryPassWeb.Controllers;

/// <summary>
/// The home page and its search (step 3b): a battery's serial number, battery ID (#6), passport ID (#5) or the address
/// its QR code carries opens the public summary, or the detail page when signed in.
/// </summary>
[Route("")]
public class HomeController(PassportPages pages) : Controller
{
    /// <summary>Address parts that aren't an ID: the passport pages' prefix and suffixes, and today's old ones.</summary>
    private static readonly HashSet<string> RouteWords = new(StringComparer.OrdinalIgnoreCase) { "dpp", "detail", "summary", "latest" };

    [HttpGet("")]
    public IActionResult Index([FromQuery] string? q, [FromQuery] string? notFound)
    {
        ViewData["SearchQuery"] = q?.Trim() ?? string.Empty;
        ViewData["SearchNotFound"] = string.Equals(notFound, "1", StringComparison.OrdinalIgnoreCase);
        return View();
    }

    [HttpGet("search")]
    public async Task<IActionResult> Search([FromQuery] string? q, CancellationToken cancellationToken)
    {
        var query = IdFromSearch(q);
        if (query.Length == 0)
        {
            return RedirectToAction(nameof(Index));
        }

        var passport = await pages.LocateAsync(query, cancellationToken);
        if (passport is null)
        {
            return RedirectToAction(nameof(Index), new { q = query, notFound = "1" });
        }

        return Redirect(User.Identity?.IsAuthenticated == true
            ? PassportPages.DetailHref(passport.Address)
            : PassportPages.SummaryHref(passport.Address));
    }

    /// <summary>
    /// The ID in what was typed or scanned: from a web address (a QR code's), its first path part that isn't a route
    /// word, or its "q" or "id"; anything else as typed, so a battery ID such as urn:x-battery-pass:TEST-… keeps its form.
    /// </summary>
    public static string IdFromSearch(string? value)
    {
        var text = value?.Trim() ?? string.Empty;
        if (!Uri.TryCreate(text, UriKind.Absolute, out var address) || (address.Scheme != Uri.UriSchemeHttp && address.Scheme != Uri.UriSchemeHttps))
        {
            return text;
        }

        foreach (var key in new[] { "q", "id" })
        {
            var candidate = Microsoft.AspNetCore.WebUtilities.QueryHelpers.ParseQuery(address.Query).TryGetValue(key, out var found) ? found.ToString().Trim() : string.Empty;
            if (candidate.Length > 0)
            {
                return candidate;
            }
        }

        return address.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries)
            .Select(Uri.UnescapeDataString)
            .FirstOrDefault(segment => !RouteWords.Contains(segment)) ?? string.Empty;
    }
}
