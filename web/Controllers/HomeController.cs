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
    private static readonly HashSet<string> RouteWords = new(StringComparer.OrdinalIgnoreCase) { "dpp", "detail", "summary", "latest", "25S" };

    /// <summary>Where the home page finds the sample battery's ID: a published demo battery, or none (redesign R6).</summary>
    public const string SampleKey = "SampleBatteryId";

    /// <summary>The near matches the home page offers (finding U02): serial number, model and address.</summary>
    public const string NearMatchesKey = "NearMatches";

    /// <summary>Whether more passports match than are offered.</summary>
    public const string NearMatchesMoreKey = "NearMatchesMore";

    /// <summary>How many characters a search needs before near matches are offered, and how many at most.</summary>
    public const int NearMatchMinimum = 3;

    public const int NearMatchLimit = 3;

    [HttpGet("")]
    public async Task<IActionResult> Index([FromQuery] string? q, [FromQuery] string? notFound, [FromServices] BatteryPages batteries, CancellationToken cancellationToken)
    {
        ViewData["SearchQuery"] = q?.Trim() ?? string.Empty;
        ViewData["SearchNotFound"] = string.Equals(notFound, "1", StringComparison.OrdinalIgnoreCase);
        if (ViewData["SearchNotFound"] is true && IdFromSearch(q) is { Length: >= NearMatchMinimum } typed)
        {
            // Near matches (finding U02): the Registry's own matching, among the published passports only.
            var (matches, total) = await NearMatchesAsync(batteries, typed, cancellationToken);
            ViewData[NearMatchesKey] = matches;
            ViewData[NearMatchesMoreKey] = total > matches.Count;
        }

        // A sample to open, as the old app had: a published demo battery, never a real one (as the API help, 30 Oct 2026).
        ViewData[SampleKey] = await batteries.ExampleBatteryIdAsync(cancellationToken);
        return View();
    }

    /// <summary>
    /// Where a signed-in user lands when their role may not open a page. It used to be the sign-in page, which sent a
    /// signed-in user straight back: an endless redirect.
    /// </summary>
    [HttpGet("not-allowed")]
    public IActionResult NotAllowed()
    {
        Response.StatusCode = StatusCodes.Status403Forbidden;
        return View();
    }

    [HttpGet("search")]
    public async Task<IActionResult> Search([FromQuery] string? q, CancellationToken cancellationToken, [FromServices] BatteryPages? batteries = null)
    {
        var query = IdFromSearch(q);
        if (query.Length == 0)
        {
            return RedirectToAction(nameof(Index));
        }

        if (await pages.LocateAsync(query, cancellationToken) is { } passport)
        {
            return Redirect(PageOf(passport.Address));
        }

        // In other capitals (finding U02): the one published passport whose serial number, battery ID or passport ID it is.
        if (batteries is not null && query.Length >= NearMatchMinimum)
        {
            var list = await batteries.ListOnePerBatteryAsync(query, IsPublished, matchClusters: false, cancellationToken);
            var same = list.Rows.Where(row => string.Equals(row.SerialNumber, query, StringComparison.OrdinalIgnoreCase) || string.Equals(row.Identifier, query, StringComparison.OrdinalIgnoreCase)).ToArray();
            if (same.Length == 1 && same[0].Identifier.Length > 0)
            {
                return Redirect(PageOf(same[0].Identifier));
            }

            if (list.Rows.Count == 1 && list.Rows[0].Active?.DigitalProductPassportId is { } passportId && string.Equals(passportId, query, StringComparison.OrdinalIgnoreCase))
            {
                return Redirect(PageOf(list.Rows[0].Identifier));
            }
        }

        return RedirectToAction(nameof(Index), new { q = query, notFound = "1" });
    }

    /// <summary>A near match on the home page: the battery's serial number, its model, and its passport's address.</summary>
    public sealed record NearMatch(string SerialNumber, string ModelName, string Href);

    /// <summary>
    /// The published passports the search text is part of, with the Registry's own matching (finding U02), one per
    /// battery; at most <see cref="NearMatchLimit"/> of them, and the total. Clusters don't count, as for a visitor.
    /// </summary>
    private async Task<(IReadOnlyList<NearMatch> Matches, int Total)> NearMatchesAsync(BatteryPages batteries, string query, CancellationToken cancellationToken)
    {
        var list = await batteries.ListOnePerBatteryAsync(query, IsPublished, matchClusters: false, cancellationToken);
        var matches = list.Rows
            .Where(row => row.Identifier.Length > 0)
            .Take(NearMatchLimit)
            .Select(row => new NearMatch(row.SerialNumber, row.ModelName, PageOf(row.Identifier)))
            .ToArray();
        return (matches, list.Rows.Count);
    }

    /// <summary>A passport the public may open: the battery's active one.</summary>
    private static bool IsPublished(BatteryRow row) => row.Active is not null;

    /// <summary>The detail page when signed in, the public page otherwise.</summary>
    private string PageOf(string address) =>
        User.Identity?.IsAuthenticated == true ? PassportPages.DetailHref(address) : PassportPages.SummaryHref(address);

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
