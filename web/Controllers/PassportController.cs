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
    public async Task<IActionResult> Summary(string id, CancellationToken cancellationToken)
    {
        var reader = await readers.ForAsync(User, cancellationToken);
        var page = await pages.SummaryAsync(reader, Uri.UnescapeDataString(id), cancellationToken);
        return page is null ? NotFoundPage() : View(page);
    }

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
        if (liveHistory is not null && page.IsCurrent)
        {
            ViewData["LiveHistory"] = await liveHistory.HistoryAsync(reader, page.Version.PackId, BatteryApi.MaxHistoryHours, cancellationToken);
        }

        // The battery's cluster admins manage it from here too (step 4a).
        if (manage is not null && page.IsCurrent)
        {
            ViewData[ClusterBatteryPages.ManageHrefKey] = await manage.ManageHrefAsync(await readers.EditorForAsync(User, cancellationToken), page.Version.PackId, cancellationToken);
        }

        Response.Headers[HeaderNames.CacheControl] = "private, no-store";
        return View(page);
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
