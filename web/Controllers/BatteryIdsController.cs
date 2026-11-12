using System.Globalization;
using BatteryPassWeb.Dpp.Identifiers;
using BatteryPassWeb.Dpp.Store;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BatteryPassWeb.Controllers;

/// <summary>The release form as typed, kept when a release is refused.</summary>
public sealed record ReleaseForm(string? Reference, string? ModelId, IReadOnlyList<IdRange> Ranges, string? List, bool IsTest = false)
{
    public static readonly ReleaseForm Empty = new(null, null, [new IdRange(null, null, null)], null);
}

/// <summary>The "Battery IDs" page's tabs (6d-2b): the releases (first), all IDs, and the release form.</summary>
public static class BatteryIdsTabs
{
    public const string Releases = "releases";
    public const string Ids = "ids";
    public const string New = "new";

    public static string Of(string? tab) => tab is Ids or New ? tab : Releases;
}

/// <summary>
/// What every tab of the "Battery IDs" page shows: the summary of the real IDs and the settings' state, then the chosen
/// tab's content. Unless "Show voided" is ticked (6d-4), <see cref="Releases"/> leaves out the releases whose IDs are all
/// voided (<see cref="HiddenReleases"/>), and All IDs its voided IDs (<see cref="HiddenVoidedIds"/>).
/// </summary>
public sealed record BatteryIdsPage(
    string Tab,
    IReadOnlyDictionary<string, int> Counts,
    IReadOnlyList<string> MissingSettings,
    IReadOnlyList<ReleaseSummary> Releases,
    IReadOnlyDictionary<string, string> Models,
    ReleaseForm Form,
    ReleaseResult? Refusal,
    IReadOnlyList<ReleasedBatteryId> Items,
    int Total,
    ReleasedIdFilter Filter,
    int HiddenVoidedIds = 0,
    int HiddenReleases = 0)
{
    /// <summary>The most rows a list shows; the export has them all.</summary>
    public const int MaxRows = 200;

    public string ModelName(string? modelId) => modelId is null ? "Any model" : Models.GetValueOrDefault(modelId, modelId);
}

/// <summary>One release's page (6d-2b): what it is, its export, and its IDs.</summary>
public sealed record ReleasePage(ReleaseSummary Release, IReadOnlyList<ReleasedBatteryId> Items, IReadOnlyDictionary<string, string> Models, bool JustReleased)
{
    public string ModelName(string? modelId) => modelId is null ? "Any model" : Models.GetValueOrDefault(modelId, modelId);
}

/// <summary>
/// The team's "Battery IDs" page (step 6d; reorganised in 6d-2b after the owner's trial): serial numbers released in
/// advance, each with its battery ID, so labels can be printed before the batteries are made. Team only.
/// </summary>
[Authorize(Policy = "AdminOnly")]
[Route("admin/battery-ids")]
public sealed class BatteryIdsController(BatteryIdPool pool, DppStore store) : Controller
{
    public const string ReleaseDialog = "void-unused-dialog";

    [HttpGet("")]
    public async Task<IActionResult> Index(
        [FromQuery] string? tab,
        [FromQuery] string? status,
        [FromQuery] string? release,
        [FromQuery] string? model,
        [FromQuery] string? q,
        [FromQuery] bool demo,
        [FromQuery] bool voided,
        CancellationToken cancellationToken) =>
        View(await PageAsync(BatteryIdsTabs.Of(tab), ReleaseForm.Empty, null, new ReleasedIdFilter(status, release, model, q, demo, voided), cancellationToken));

    [HttpGet("releases/{releaseId}")]
    public async Task<IActionResult> Release(string releaseId, [FromQuery] bool released, CancellationToken cancellationToken)
    {
        if (await pool.ReleaseAsync(releaseId, cancellationToken) is not { } summary)
        {
            return NotFound();
        }

        var items = await pool.ListAsync(new ReleasedIdFilter(Reference: releaseId, Voided: true), cancellationToken); // the full record
        return View(new ReleasePage(summary, items, await ModelsAsync(cancellationToken), released));
    }

    [HttpPost("")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ReleaseIds(
        [FromForm] string? reference,
        [FromForm] string? modelId,
        [FromForm] string[]? start,
        [FromForm] string[]? end,
        [FromForm] string[]? quantity,
        [FromForm] string? list,
        CancellationToken cancellationToken)
    {
        var count = new[] { start?.Length ?? 0, end?.Length ?? 0, quantity?.Length ?? 0 }.Max();
        var ranges = Enumerable.Range(0, count)
            .Select(index => new IdRange(At(start, index), At(end, index), At(quantity, index)))
            .ToArray();
        // Decision 67: "Test IDs" ticked releases IDs marked TEST, never real ones, which resetting or clearing the demo data removes.
        var isTest = Request.HasFormContentType && Request.Form["isTest"] == "true";
        var result = await pool.ReleaseAsync(Who, new ReleaseRequest(reference, isTest ? null : modelId, ranges, list, IsDemo: isTest), cancellationToken); // test IDs are for any model (round two E)
        if (result.Done && isTest)
        {
            TempData[ProductDataController.StatusKey] = $"Released {result.Released.ToString(CultureInfo.InvariantCulture)} test battery {(result.Released == 1 ? "ID" : "IDs")}. They contain TEST, aren't counted, and go when the demo data is reset or cleared.";
            return Redirect($"/admin/battery-ids/releases/{Uri.EscapeDataString(result.ReleaseId!)}?released=true");
        }

        if (result.Done)
        {
            // Straight to the new release, its export at the top, for the labels (the owner, 6d-2b).
            TempData[ProductDataController.StatusKey] = $"Released {result.Released.ToString(CultureInfo.InvariantCulture)} battery {(result.Released == 1 ? "ID" : "IDs")}. Export them to Excel for the labels.";
            return Redirect($"/admin/battery-ids/releases/{Uri.EscapeDataString(result.ReleaseId!)}?released=true");
        }

        if (result.Problems.Count == 0)
        {
            ViewData[ProductDataController.ErrorKey] = $"Nothing was released: {result.Problem}."; // a listed refusal says it in its own box
        }

        var form = new ReleaseForm(reference, modelId, ranges.Length == 0 ? ReleaseForm.Empty.Ranges : ranges, list, isTest);
        Response.StatusCode = StatusCodes.Status422UnprocessableEntity;
        return View(nameof(Index), await PageAsync(BatteryIdsTabs.New, form, result, new ReleasedIdFilter(), cancellationToken));
    }

    [HttpPost("void")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Void([FromForm] string? serialNumber, [FromForm] string? reason, [FromForm] string? returnTo, CancellationToken cancellationToken)
    {
        var problem = await pool.VoidAsync(Who, serialNumber ?? string.Empty, reason, cancellationToken);
        Message(problem is null ? $"Battery ID {serialNumber} is voided. It stays listed and is never used again." : null, problem is null ? null : $"It wasn't voided: {problem}.");
        return Redirect(Back(returnTo));
    }

    [HttpPost("void-unused")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> VoidUnused([FromForm] string? releaseId, [FromForm] string? reason, [FromForm] string? returnTo, CancellationToken cancellationToken)
    {
        var (voided, problem) = await pool.VoidUnusedAsync(Who, releaseId ?? string.Empty, reason, cancellationToken);
        Message(problem is null ? $"{voided.ToString(CultureInfo.InvariantCulture)} unused battery {(voided == 1 ? "ID is" : "IDs are")} voided." : null, problem is null ? null : $"Nothing was voided: {problem}.");
        return Redirect(Back(returnTo));
    }

    /// <summary>
    /// The IDs as an Excel file: one release's (<paramref name="release"/>, with <paramref name="voided"/> for all of
    /// them), or All IDs as shown.
    /// </summary>
    [HttpGet("export")]
    public async Task<IActionResult> Export(
        [FromQuery] string? status,
        [FromQuery] string? release,
        [FromQuery] string? model,
        [FromQuery] string? q,
        [FromQuery] bool demo,
        [FromQuery] bool voided,
        CancellationToken cancellationToken)
    {
        var items = await pool.ListAsync(new ReleasedIdFilter(status, release, model, q, demo, voided), cancellationToken);
        var name = release is { Length: > 0 } && await pool.ReleaseAsync(release, cancellationToken) is { } summary ? FileWord(summary.Reference) : "all";
        return File(BatteryIdPool.Export(items, await ModelsAsync(cancellationToken)), XlsxWriter.ContentType, $"battery-ids-{name}-{DateTime.UtcNow:yyyy-MM-dd}.xlsx");
    }

    private async Task<BatteryIdsPage> PageAsync(string tab, ReleaseForm form, ReleaseResult? refusal, ReleasedIdFilter filter, CancellationToken cancellationToken)
    {
        var all = tab == BatteryIdsTabs.Ids ? await pool.ListAsync(filter, cancellationToken) : [];
        var hiddenIds = tab == BatteryIdsTabs.Ids && !filter.ShowsVoided ? (await pool.ListAsync(filter with { Voided = true }, cancellationToken)).Count - all.Count : 0;
        var releases = await pool.ReleasesAsync(cancellationToken);
        var shown = filter.ShowsVoided ? releases : releases.Where(release => !release.AllVoided).ToArray();
        return new BatteryIdsPage(
            tab,
            await pool.RealCountsAsync(cancellationToken),
            pool.MissingSettings,
            shown,
            await ModelsAsync(cancellationToken),
            form,
            refusal,
            all.Take(BatteryIdsPage.MaxRows).ToArray(),
            all.Count,
            filter,
            hiddenIds,
            releases.Count - shown.Count);
    }

    private async Task<IReadOnlyDictionary<string, string>> ModelsAsync(CancellationToken cancellationToken) =>
        (await store.ListAsync<BatteryModel>(cancellationToken)).OrderBy(model => model.Name, StringComparer.OrdinalIgnoreCase).ToDictionary(model => model.Id, model => model.Name, StringComparer.Ordinal);

    /// <summary>A reference as part of a file name: letters, digits and dashes.</summary>
    private static string FileWord(string reference) =>
        new string(reference.Select(letter => char.IsAsciiLetterOrDigit(letter) ? letter : '-').ToArray()).Trim('-') is { Length: > 0 } word ? word : "release";

    private void Message(string? status, string? error)
    {
        if (status is not null)
        {
            TempData[ProductDataController.StatusKey] = status;
        }

        if (error is not null)
        {
            TempData[ProductDataController.ErrorKey] = error;
        }
    }

    private static string Back(string? returnTo) =>
        returnTo is { Length: > 0 } back && back.StartsWith("/admin/battery-ids", StringComparison.Ordinal) && !back.StartsWith("//", StringComparison.Ordinal) ? back : "/admin/battery-ids";

    private static string? At(string[]? values, int index) => values is not null && index < values.Length ? values[index] : null;

    private string Who => Services.AccessControlService.CurrentEmail(User) is { Length: > 0 } email ? email : "admin";
}
