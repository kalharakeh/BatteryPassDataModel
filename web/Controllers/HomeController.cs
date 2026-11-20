using BatteryPassWeb.Configuration;
using BatteryPassWeb.Dpp.Access;
using BatteryPassWeb.Dpp.Pages;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

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

    /// <summary>Whether more passports match than are offered (signed-in readers only).</summary>
    public const string NearMatchesMoreKey = "NearMatchesMore";

    /// <summary>The search text the near matches belong to, handed from the search to the home page once.</summary>
    private const string NearMatchesQueryKey = "NearMatchesQuery";

    /// <summary>How many characters a search needs before near matches are offered, and how many at most.</summary>
    public const int NearMatchMinimum = 3;

    public const int NearMatchLimit = 3;

    [HttpGet("")]
    [QueryPresent("q", present: false)]
    public async Task<IActionResult> Index([FromQuery] string? q, [FromQuery] string? notFound, [FromServices] BatteryPages batteries, CancellationToken cancellationToken, [FromServices] BatteryPassWeb.Dpp.Demo.DemoDataSwitch? demo = null)
    {
        ViewData["SearchQuery"] = q?.Trim() ?? string.Empty;
        ViewData["SearchNotFound"] = string.Equals(notFound, "1", StringComparison.OrdinalIgnoreCase);
        // Near matches only as the search worked them out and handed them over (review 1, point 1): a query flag alone offers nothing.
        if (ViewData["SearchNotFound"] is true
            && TempData?[NearMatchesQueryKey] is string handed && handed == IdFromSearch(q)
            && TempData[NearMatchesKey] is string[] lines)
        {
            ViewData[NearMatchesKey] = lines.Select(line => line.Split('\t')).Where(parts => parts.Length == 3).Select(parts => new NearMatch(parts[0], parts[1], parts[2])).ToArray();
            ViewData[NearMatchesMoreKey] = TempData[NearMatchesMoreKey] is true;
        }

        // A sample to open, as the old app had: a published demo battery, never a real one (as the API help, 30 Oct 2026);
        // DEMO-C7M-0001, the easiest to read, and none while the demo is switched off (finding U06).
        ViewData[SampleKey] = demo is { Enabled: false } ? string.Empty : await SampleBatteryIdAsync(batteries, cancellationToken);
        return View(nameof(Index)); // also for IndexWithQuery
    }

    /// <summary>The home page with a search text: as <see cref="Index"/>, rate limited as the public reads are (review 1, point 1).</summary>
    [HttpGet("")]
    [QueryPresent("q")]
    [EnableRateLimiting(SecurityRateLimitPolicyNames.PublicDataRead)]
    public Task<IActionResult> IndexWithQuery([FromQuery] string? q, [FromQuery] string? notFound, [FromServices] BatteryPages batteries, CancellationToken cancellationToken, [FromServices] BatteryPassWeb.Dpp.Demo.DemoDataSwitch? demo = null) =>
        Index(q, notFound, batteries, cancellationToken, demo);

    /// <summary>The home page's sample (finding U06): the demo's simplest passport, one model and one version.</summary>
    public const string SampleSerialNumber = "DEMO-C7M-0001";

    /// <summary>The sample's battery ID: DEMO-C7M-0001 while it is a published demo battery, else the API help's example.</summary>
    private static async Task<string> SampleBatteryIdAsync(BatteryPages batteries, CancellationToken cancellationToken)
    {
        var sample = (await batteries.ListOnePerBatteryAsync(SampleSerialNumber, row => row.IsDemo && row.Active is not null, matchClusters: false, cancellationToken)).Rows
            .FirstOrDefault(row => row.SerialNumber == SampleSerialNumber && row.Identifier.Length > 0);
        return sample?.Identifier ?? await batteries.ExampleBatteryIdAsync(cancellationToken);
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
    [EnableRateLimiting(SecurityRateLimitPolicyNames.PublicDataRead)]
    public async Task<IActionResult> Search([FromQuery] string? q, CancellationToken cancellationToken, [FromServices] BatteryPages? batteries = null, [FromServices] DppReaders? readers = null)
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

        if (batteries is not null && query.Length >= NearMatchMinimum)
        {
            // One read for the exact steps and the near matches (review 2, point 5).
            var rows = await batteries.SearchRowsAsync(query, IsPublished, cancellationToken);
            var list = rows.Where(row => row.Matches).Select(row => row.Row).ToArray();

            // In other capitals (finding U02): the one published passport whose serial number, battery ID or passport ID it is.
            var same = list.Where(row => string.Equals(row.SerialNumber, query, StringComparison.OrdinalIgnoreCase) || string.Equals(row.Identifier, query, StringComparison.OrdinalIgnoreCase)).ToArray();
            if (same.Length == 1 && same[0].Identifier.Length > 0)
            {
                return Redirect(PageOf(same[0].Identifier));
            }

            if (list.Length == 1 && list[0].Active?.DigitalProductPassportId is { } passportId && string.Equals(passportId, query, StringComparison.OrdinalIgnoreCase))
            {
                return Redirect(PageOf(list[0].Identifier));
            }

            if (TempData is { } handOver)
            {
                var reader = readers is null ? Reader.Anonymous : await readers.ForAsync(User, cancellationToken);
                var (matches, more) = NearMatches(rows, query, reader);
                handOver[NearMatchesQueryKey] = query;
                handOver[NearMatchesKey] = matches.Select(match => $"{match.SerialNumber}\t{match.ModelName}\t{match.Href}").ToArray();
                handOver[NearMatchesMoreKey] = more;
            }
        }

        return RedirectToAction(nameof(Index), new { q = query, notFound = "1" });
    }

    /// <summary>A near match on the home page: the battery's serial number, its model, and its passport's address.</summary>
    public sealed record NearMatch(string SerialNumber, string ModelName, string Href);

    /// <summary>
    /// The published passports near the search text, one per battery, from the rows the search already read; at most
    /// <see cref="NearMatchLimit"/> of them, and whether more match. Nobody lists through the search what their Registry
    /// doesn't list (review 2, point 1): the batteries the reader may list there (the Registry's own <see cref="DemoScope.Lists"/>
    /// and <see cref="RegistryList.MayList"/>) by the Registry's matching (finding U02), clusters aside, and only they count
    /// for "Type more"; any published battery also by the visitor's rule, its serial number from the start, by its letters
    /// and digits (review 1, point 1; decision 80), so a signed-in reader gets at least what a visitor gets. A visitor lists
    /// nothing in the Registry, so a visitor gets the visitor's rule alone.
    /// </summary>
    private (IReadOnlyList<NearMatch> Matches, bool More) NearMatches(IReadOnlyList<SearchRow> rows, string query, Reader reader)
    {
        var listed = DemoScope.Lists(reader, rows, row => row.Row.IsDemo)
            .Where(row => RegistryList.MayList(reader, row.Row))
            .Select(row => row.Row.PackId)
            .ToHashSet(StringComparer.Ordinal);
        var byRegistry = rows.Where(row => listed.Contains(row.Row.PackId) && row.Matches).Select(row => row.Row).Where(row => row.Identifier.Length > 0).ToArray();
        // Serial numbers compared as decision 80 compares them, by their letters and digits (review 2, point 7).
        var key = BatteryPassWeb.Dpp.Identifiers.IdentifierForms.Clean(query);
        var byPrefix = rows
            .Where(row => key.Length >= NearMatchMinimum && !(listed.Contains(row.Row.PackId) && row.Matches)
                && BatteryPassWeb.Dpp.Identifiers.IdentifierForms.Clean(row.Row.SerialNumber).StartsWith(key, StringComparison.Ordinal))
            .Select(row => row.Row)
            .Where(row => row.Identifier.Length > 0);
        var shown = byRegistry.Concat(byPrefix).Take(NearMatchLimit).ToArray();
        var matches = shown.Select(row => new NearMatch(row.SerialNumber, row.ModelName, PageOf(row.Identifier))).ToArray();
        return (matches, byRegistry.Length > shown.Count(row => listed.Contains(row.PackId)));
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
