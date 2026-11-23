using BatteryPassWeb.Dpp.Api;
using BatteryPassWeb.Dpp.Access;
using BatteryPassWeb.Dpp.Editing;
using BatteryPassWeb.Dpp.Pages;
using BatteryPassWeb.Dpp.RegistryToken;
using BatteryPassWeb.Configuration;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
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
    public async Task<IActionResult> MarkInvalid(string id, [FromForm] string? reason, [FromServices] PassportMarks marks, CancellationToken cancellationToken, [FromServices] DemoBoundary? demo = null)
    {
        var reader = await readers.ForAsync(User, cancellationToken);
        if (!reader.Roles.Contains(ReaderRole.MarketSurveillance) || await pages.FindAsync(Uri.UnescapeDataString(id), cancellationToken) is not { } version)
        {
            return NotFound();
        }

        // Outside the demo a demo authority finds nothing here, as for an unknown passport (finding R03).
        if (demo is not null && !(await demo.OnAsync(reader, version.PackId, cancellationToken)).Roles.Contains(ReaderRole.MarketSurveillance))
        {
            return NotFound();
        }

        var problem = await marks.MarkInvalidAsync(DppReaders.SurveillanceSenderFor(reader), version.PackId, reason, cancellationToken);
        TempData[MarkMessageKey] = problem is null ? "The passport is marked invalid. The team is the only one who can undo it." : $"The passport wasn't marked: {problem}.";
        return Redirect(PassportPages.DetailHref(Uri.UnescapeDataString(id)));
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> Summary(string id, CancellationToken cancellationToken, [FromServices] BatteryPassWeb.Dpp.Identifiers.BatteryIdPool? pool = null)
    {
        Response.Headers[HeaderNames.Vary] = RegistryTokenRequest.Vary;
        if (await GoneAsync(this, Uri.UnescapeDataString(id), json: false, cancellationToken) is { } gone)
        {
            return gone; // finding R08: removed after recycling or export; asked before anything is looked up
        }

        var reader = await readers.ForAsync(User, cancellationToken);
        var page = await pages.SummaryAsync(reader, Uri.UnescapeDataString(id), cancellationToken);
        if (page is not null)
        {
            // The same passport as data (finding R07), for programs that read the page's headers.
            Response.Headers[HeaderNames.Link] = $"<{page.DataHref}>; rel=\"alternate\"; type=\"application/json\"";
            return View(nameof(Summary), page);
        }

        // A released ID scanned before its passport is published (6d): ours, but nothing to show yet; a voided one isn't.
        return pool is not null && await pool.FindUnpublishedAsync(Uri.UnescapeDataString(id), cancellationToken) is { Status: not BatteryPassWeb.Dpp.Identifiers.ReleasedIdStatuses.Voided } released
            ? NotPublishedYet(released)
            : NotFoundPage();
    }

    /// <summary>
    /// The public part as data (finding R07), at the page's own address unless the request asks for the page. No token: the
    /// public values only, the same as the page shows, found the same way; 404 wherever the page answers 404. With the EU
    /// registry's token: the restricted parts too, or 401 or 403 (R3, decision 94), never the public part instead.
    /// </summary>
    [HttpGet("{id}")]
    [PrefersJson]
    [RegistryReadable]
    [EnableRateLimiting(SecurityRateLimitPolicyNames.PublicDataRead)]
    public Task<IActionResult> SummaryData(string id, CancellationToken cancellationToken) => DataAsync(Uri.UnescapeDataString(id), cancellationToken);

    /// <summary>The same data at the page's address with ".json": <c>/dpp/UN778474481-VBC-92921.json</c>.</summary>
    [HttpGet("{id}.json")]
    [RegistryReadable]
    [EnableRateLimiting(SecurityRateLimitPolicyNames.PublicDataRead)]
    public Task<IActionResult> SummaryJson(string id, CancellationToken cancellationToken) => DataAsync(Uri.UnescapeDataString(id), cancellationToken);

    /// <summary>A version's public part as data, at its own address when the request asks for JSON.</summary>
    [HttpGet("{id}/v{version:int}")]
    [PrefersJson]
    [RegistryReadable]
    [EnableRateLimiting(SecurityRateLimitPolicyNames.PublicDataRead)]
    public Task<IActionResult> VersionData(string id, int version, [FromServices] Microsoft.Extensions.Options.IOptions<BatteryPassWeb.Configuration.BatteryPassOptions> options, CancellationToken cancellationToken) =>
        DataAsync(BatteryPassWeb.Dpp.Identifiers.IdentifierForms.PassportAddress(options.Value.IdBaseUrl, Uri.UnescapeDataString(id), version), cancellationToken);

    /// <summary>A version's public part as data at its address with ".json": <c>/dpp/UN778474481-VBC-92921/v2.json</c>.</summary>
    [HttpGet("{id}/v{version:int}.json")]
    [RegistryReadable]
    [EnableRateLimiting(SecurityRateLimitPolicyNames.PublicDataRead)]
    public Task<IActionResult> VersionJson(string id, int version, [FromServices] Microsoft.Extensions.Options.IOptions<BatteryPassWeb.Configuration.BatteryPassOptions> options, CancellationToken cancellationToken) =>
        VersionData(id, version, options, cancellationToken);

    private async Task<IActionResult> DataAsync(string id, CancellationToken cancellationToken)
    {
        Response.Headers[HeaderNames.Vary] = RegistryTokenRequest.Vary;

        // The same as the page, which gets it from its form protection (C3 round two, point 6), the 404 included.
        Response.Headers[HeaderNames.CacheControl] = "no-cache, no-store";
        Response.Headers[HeaderNames.Pragma] = "no-cache";
        if (RegistryTokenRequest.CarriesCredential(Request))
        {
            return await RegistryDataAsync(id, cancellationToken); // never the public part (the guide's section 2.4)
        }

        if (await GoneAsync(this, id, json: true, cancellationToken) is { } gone)
        {
            return gone; // finding R08: the same headers and read limit, asked before anything is looked up
        }

        var reader = await readers.ForAsync(User, cancellationToken);
        var page = await pages.SummaryAsync(reader, id, cancellationToken);
        if (page?.PublicData is not { } data)
        {
            return NotFound(new { error = "No published passport has this ID." });
        }

        return Content(data.ToJsonString(), "application/json");
    }

    /// <summary>
    /// The EU registry's read (R3, decision 94): with a valid token, the passport cut for the reader "EU registry" (all four
    /// levels, today's format), answered here without a redirect and never stored by a shared cache; the read goes into the
    /// access log. A refused token gets 401 or 403 without data. The check ran before the read limit
    /// (<see cref="RegistryTokenAuthentication.UseRegistryToken"/>); without its answer the token is refused.
    /// </summary>
    private async Task<IActionResult> RegistryDataAsync(string id, CancellationToken cancellationToken)
    {
        Response.Headers[HeaderNames.CacheControl] = "private, no-store";
        var result = RegistryTokenRequest.Outcome(HttpContext) ?? RegistryTokenResult.Invalid("not-checked");
        var log = HttpContext.RequestServices?.GetService<ILogger<PassportController>>();
        var battery = RegistryTokenRequest.ForLog(id); // the address as asked, never a line of its own
        if (!result.Authorised)
        {
            log?.LogInformation("EU registry read of {Battery}: refused, {Status} {Reason}.", battery, result.Status, result.Reason);
            return RegistryTokenAnswers.Refusal(HttpContext, result);
        }

        if (await GoneAsync(this, id, json: true, cancellationToken) is { } gone)
        {
            log?.LogInformation("EU registry read of {Battery}: 410, organisation {Organisation}.", battery, result.OrganisationId);
            return gone;
        }

        var reader = DppReaders.EuRegistry(result.OrganisationId!);
        var passport = HttpContext.RequestServices?.GetService<PassportApi>() is { } api ? await api.ReadAsync(reader, id, cancellationToken) : null;
        if (passport is null)
        {
            log?.LogInformation("EU registry read of {Battery}: 404, organisation {Organisation}.", battery, result.OrganisationId);
            return NotFound(new { error = "No published passport has this ID." });
        }

        // H1's access log: the first read of the battery's restricted data per day, by the reader "EU registry".
        var sections = BatteryPassWeb.Dpp.Catalog.AttributeCatalog.All
            .Where(attribute => attribute.Audience != BatteryPassWeb.Dpp.Catalog.Audience.Public && Holds(passport.Passport, attribute.Path))
            .Select(attribute => attribute.Audience.ToString().ToLowerInvariant())
            .Distinct()
            .ToArray();
        if (sections.Length > 0 && HttpContext.RequestServices?.GetService<RestrictedReads>() is { } restricted)
        {
            await restricted.NoteBatteryAsync(reader.Who, RegistryTokenRequest.RoleLabel, RestrictedReads.EuRegistryRead, passport.Version.PackId, passport.Location.Identifier, sections, cancellationToken);
        }

        log?.LogInformation("EU registry read of {Battery}: 200, organisation {Organisation}.", battery, result.OrganisationId);
        return Content(passport.Passport.ToJsonString(), "application/json");
    }

    /// <summary>Whether the passport holds a value at this attribute's path.</summary>
    private static bool Holds(System.Text.Json.Nodes.JsonObject passport, string path)
    {
        System.Text.Json.Nodes.JsonNode? node = passport;
        foreach (var name in path.Split('.'))
        {
            if (node is not System.Text.Json.Nodes.JsonObject parent || !parent.TryGetPropertyValue(name, out node))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>"Not published yet": a 404, so it is never read as a passport, with the scanned ID and no battery data.</summary>
    private ViewResult NotPublishedYet(BatteryPassWeb.Dpp.Identifiers.ReleasedBatteryId released)
    {
        var view = View("~/Views/Dpp/NotPublishedYet.cshtml", released);
        view.StatusCode = StatusCodes.Status404NotFound;
        return view;
    }

    /// <summary>A version's own address (#5, step 6d): <c>/dpp/UN778474481-VBC-92921/v2</c>.</summary>
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
        [FromServices] ClusterBatteryPages? manage = null,
        [FromServices] RestrictedReads? restricted = null,
        [FromServices] BatteryPassWeb.Dpp.Store.DppStore? store = null) =>
        Detail(BatteryPassWeb.Dpp.Identifiers.IdentifierForms.PassportAddress(options.Value.IdBaseUrl, Uri.UnescapeDataString(id), version), cancellationToken, liveHistory, manage, restricted, store);

    [HttpGet("{id}/detail")]
    public async Task<IActionResult> Detail(
        string id,
        CancellationToken cancellationToken,
        [FromServices] BatteryApi? liveHistory = null,
        [FromServices] ClusterBatteryPages? manage = null,
        [FromServices] RestrictedReads? restricted = null,
        [FromServices] BatteryPassWeb.Dpp.Store.DppStore? store = null)
    {
        if (User.Identity?.IsAuthenticated != true)
        {
            return Challenge();
        }

        if (await GoneAsync(this, Uri.UnescapeDataString(id), json: false, cancellationToken) is { } gone)
        {
            return gone; // finding R08
        }

        var reader = await readers.ForAsync(User, cancellationToken);
        var page = await pages.DetailAsync(reader, Uri.UnescapeDataString(id), cancellationToken);
        if (page is null)
        {
            return NotFoundPage();
        }

        // Finding R11: a reader who sees restricted sections is told how the data may be used, and the first opening of the
        // battery's restricted data today is logged (the team's own reads and a cluster admin's own batteries are not).
        var restrictedSections = page.Audiences.Where(section => section.Values.Count > 0).Select(section => section.Key)
            .Concat(page.Modules is { Count: > 0 } ? ["modules"] : []).ToArray();
        if (restricted is not null && store is not null && restrictedSections.Length > 0
            && await RestrictedReads.RoleForAsync(readers, User, (await store.GetAsync<BatteryPassWeb.Dpp.Store.BatteryPack>(page.Version.PackId, cancellationToken))?.ClusterId, cancellationToken) is { } role)
        {
            ViewData[RestrictedReads.RoleKey] = role;
            await restricted.NoteBatteryAsync(ClusterRepositoryEmail(), role, RestrictedReads.DetailPage, page.Version.PackId, page.Identifier, restrictedSections, cancellationToken);
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

    private string ClusterRepositoryEmail() => BatteryPassWeb.Services.ClusterRepository.NormalizeEmail(BatteryPassWeb.Services.AccessControlService.CurrentEmail(User));

    /// <summary>
    /// Finding R08: an address of a passport the team removed after recycling answers 410 with the plain sentence, as a
    /// page or, for the data addresses, as JSON. Null when no removed passport had this ID.
    /// </summary>
    internal static async Task<IActionResult?> GoneAsync(ControllerBase controller, string id, bool json, CancellationToken cancellationToken)
    {
        if (controller.HttpContext?.RequestServices?.GetService(typeof(PassportRemoval)) is not PassportRemoval removal
            || await removal.FindAsync(id, cancellationToken) is null)
        {
            return null;
        }

        if (json || controller is not Controller pages)
        {
            return new ObjectResult(new { error = PassportRemoval.Gone }) { StatusCode = StatusCodes.Status410Gone };
        }

        var view = pages.View("~/Views/Dpp/Removed.cshtml");
        view.StatusCode = StatusCodes.Status410Gone;
        return view;
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
