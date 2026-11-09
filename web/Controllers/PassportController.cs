using BatteryPassWeb.Dpp.Api;
using BatteryPassWeb.Dpp.Access;
using BatteryPassWeb.Dpp.Editing;
using BatteryPassWeb.Dpp.Pages;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Net.Http.Headers;

namespace BatteryPassWeb.Controllers;

/// <summary>
/// The passport pages (design 12, step 3b), in place of today's: the public summary, which the battery's QR code opens
/// without signing in, and the detail page for signed-in readers. An address names a passport by its battery ID (#6),
/// a version's passport ID (#5) or the battery's serial number. Both pages show only what <see cref="PassportFilter"/>
/// leaves for the reader.
/// </summary>
[AllowAnonymous]
[Route("dpp")]
public sealed class PassportController(DppReaders readers, PassportPages pages) : Controller
{
    /// <summary>Where the detail page says what happened to "Mark as invalid".</summary>
    public const string MarkMessageKey = "PassportMarkMessage";

    /// <summary>
    /// Market surveillance marks a passport invalid from its detail page (design 7.7, mock-up S6, step 4c-2). Anyone else,
    /// signed in or not, finds nothing here.
    /// </summary>
    [HttpPost("{id}/detail/invalid")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkInvalid(string id, [FromForm] string? reason, [FromServices] PassportMarks marks, CancellationToken cancellationToken)
    {
        var reader = await readers.ForAsync(User, cancellationToken);
        if (!reader.Roles.Contains(ReaderRole.MarketSurveillance) || await pages.FindAsync(Uri.UnescapeDataString(id), cancellationToken) is not { } version)
        {
            return NotFound();
        }

        var problem = await marks.MarkInvalidAsync(ChangeSender.MarketSurveillance(reader.Who), version.PackId, reason, cancellationToken);
        TempData[MarkMessageKey] = problem is null ? "The passport is marked invalid. The team is the only one who can undo it." : $"The passport wasn't marked: {problem}.";
        return Redirect(PassportPages.DetailHref(Uri.UnescapeDataString(id)));
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> Summary(string id, CancellationToken cancellationToken, [FromServices] BatteryPassWeb.Dpp.Identifiers.BatteryIdPool? pool = null)
    {
        var reader = await readers.ForAsync(User, cancellationToken);
        var page = await pages.SummaryAsync(reader, Uri.UnescapeDataString(id), cancellationToken);
        if (page is not null)
        {
            return View(nameof(Summary), page);
        }

        // A released ID scanned before its passport is published (6d): ours, but nothing to show yet; a voided one isn't.
        return pool is not null && await pool.FindUnpublishedAsync(Uri.UnescapeDataString(id), cancellationToken) is { Status: not BatteryPassWeb.Dpp.Identifiers.ReleasedIdStatuses.Voided } released
            ? NotPublishedYet(released)
            : NotFoundPage();
    }

    /// <summary>"Not published yet": a 404, so it is never read as a passport, with the scanned ID and no battery data.</summary>
    private ViewResult NotPublishedYet(BatteryPassWeb.Dpp.Identifiers.ReleasedBatteryId released)
    {
        var view = View("~/Views/Dpp/NotPublishedYet.cshtml", released);
        view.StatusCode = StatusCodes.Status404NotFound;
        return view;
    }

    /// <summary>A version's own address (#5, step 6d): <c>/dpp/UN778474481VPC12221/v2</c>.</summary>
    [HttpGet("{id}/v{version:int}")]
    public Task<IActionResult> Version(string id, int version, [FromServices] Microsoft.Extensions.Options.IOptions<BatteryPassWeb.Configuration.BatteryPassOptions> options, CancellationToken cancellationToken) =>
        Summary(BatteryPassWeb.Dpp.Identifiers.IdentifierForms.PassportAddress(options.Value.IdBaseUrl, Uri.UnescapeDataString(id), version), cancellationToken);

    [HttpGet("{id}/v{version:int}/detail")]
    public Task<IActionResult> VersionDetail(
        string id,
        int version,
        [FromServices] Microsoft.Extensions.Options.IOptions<BatteryPassWeb.Configuration.BatteryPassOptions> options,
        CancellationToken cancellationToken,
        [FromServices] BatteryApi? liveHistory = null,
        [FromServices] ClusterBatteryPages? manage = null) =>
        Detail(BatteryPassWeb.Dpp.Identifiers.IdentifierForms.PassportAddress(options.Value.IdBaseUrl, Uri.UnescapeDataString(id), version), cancellationToken, liveHistory, manage);

    [HttpGet("{id}/detail")]
    public async Task<IActionResult> Detail(
        string id,
        CancellationToken cancellationToken,
        [FromServices] BatteryApi? liveHistory = null,
        [FromServices] ClusterBatteryPages? manage = null)
    {
        if (User.Identity?.IsAuthenticated != true)
        {
            return Challenge();
        }

        var reader = await readers.ForAsync(User, cancellationToken);
        var page = await pages.DetailAsync(reader, Uri.UnescapeDataString(id), cancellationToken);
        if (page is null)
        {
            return NotFoundPage();
        }

        // The current version's live values over the last week, for those who may see them (decision 30), from the same
        // change log and the same rule as the API's history.
        if (liveHistory is not null && page.Current is null) // the battery's latest passport, active or not (redesign R6)
        {
            // The latest week with readings (R7): the last 7 days, or the week ending at the battery's last reading.
            var lastReading = page.Audiences.SelectMany(section => section.Values)
                .Where(value => value.Attribute.Change == BatteryPassWeb.Dpp.Catalog.ChangeRule.Live && value.Attribute.Property != BatteryPassWeb.Dpp.Documents.DocumentService.Accidents && value.Origin is not null) // the accident reports' page isn't a reading
                .Select(value => (DateTime?)value.Origin!.GeneratedAtUtc)
                .Max();
            var (week, weekEnd) = await liveHistory.LatestWeekAsync(reader, page.Version.PackId, lastReading, cancellationToken);
            ViewData["LiveHistory"] = week;
            ViewData["LiveWeekEnd"] = weekEnd;
        }

        // The battery's cluster admins manage it from here too (step 4a); read it while its passport is marked (R6b).
        if (manage is not null && (page.IsCurrent || BatteryPassWeb.Dpp.Store.DppStatuses.IsMarked(page.Version.DppStatus)))
        {
            ViewData[ClusterBatteryPages.ManageHrefKey] = await manage.ManageHrefAsync(await readers.EditorForAsync(User, cancellationToken), page.Version.PackId, cancellationToken);
        }

        Response.Headers[HeaderNames.CacheControl] = "private, no-store";
        return View(nameof(Detail), page);
    }

    /// <summary>No published passport has this ID; drafts aren't shown to anyone here (J-6).</summary>
    private ViewResult NotFoundPage()
    {
        ViewData["Subject"] = "passport";
        var view = View("~/Views/Dpp/NotAvailable.cshtml");
        view.StatusCode = StatusCodes.Status404NotFound;
        return view;
    }
}
