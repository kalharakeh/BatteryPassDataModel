using System.Globalization;
using BatteryPassWeb.Dpp.Identifiers;
using BatteryPassWeb.Dpp.Store;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BatteryPassWeb.Controllers;

/// <summary>The release form as typed, kept when a release is refused.</summary>
public sealed record ReleaseForm(string? Reference, string? ModelId, IReadOnlyList<IdRange> Ranges, string? List)
{
    public static readonly ReleaseForm Empty = new(null, null, [new IdRange(null, null, null)], null);
}

/// <summary>The "Battery IDs" page: the release form, its refusals, the releases and the released IDs.</summary>
public sealed record BatteryIdsPage(
    ReleaseForm Form,
    ReleaseResult? Refusal,
    IReadOnlyList<ReleaseSummary> Releases,
    IReadOnlyList<ReleasedBatteryId> Items,
    ReleasedIdFilter Filter,
    IReadOnlyDictionary<string, string> Models,
    IReadOnlyDictionary<string, string> Batteries,
    int Total)
{
    /// <summary>The most rows the page lists; the export has them all.</summary>
    public const int MaxRows = 200;

    public string ModelName(string? modelId) => modelId is null ? "Any model" : Models.GetValueOrDefault(modelId, modelId);
}

/// <summary>
/// The team's "Battery IDs" page (step 6d, the owner, 6 Nov 2026): serial numbers released in advance, each with its
/// battery ID, so labels can be printed before the batteries are made. Team only.
/// </summary>
[Authorize(Policy = "AdminOnly")]
[Route("admin/battery-ids")]
public sealed class BatteryIdsController(BatteryIdPool pool, DppStore store) : Controller
{
    public const string ReleaseDialog = "void-unused-dialog";

    [HttpGet("")]
    public async Task<IActionResult> Index([FromQuery] string? status, [FromQuery] string? release, [FromQuery] string? model, [FromQuery] string? q, CancellationToken cancellationToken) =>
        View(await PageAsync(ReleaseForm.Empty, null, new ReleasedIdFilter(status, release, model, q), cancellationToken));

    [HttpPost("")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Release(
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
        var result = await pool.ReleaseAsync(Who, new ReleaseRequest(reference, modelId, ranges, list), cancellationToken);
        if (result.Done)
        {
            TempData[ProductDataController.StatusKey] = $"Released {result.Released.ToString(CultureInfo.InvariantCulture)} battery {(result.Released == 1 ? "ID" : "IDs")} ({reference?.Trim()}). They are listed below; export them for the labels.";
            return Redirect($"/admin/battery-ids?release={Uri.EscapeDataString(result.ReleaseId!)}");
        }

        if (result.Problems.Count == 0)
        {
            ViewData[ProductDataController.ErrorKey] = $"Nothing was released: {result.Problem}."; // a listed refusal says it in its own box
        }
        var form = new ReleaseForm(reference, modelId, ranges.Length == 0 ? ReleaseForm.Empty.Ranges : ranges, list);
        var page = await PageAsync(form, result, new ReleasedIdFilter(), cancellationToken);
        Response.StatusCode = StatusCodes.Status422UnprocessableEntity;
        return View(nameof(Index), page);
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

    [HttpGet("export")]
    public async Task<IActionResult> Export([FromQuery] string? status, [FromQuery] string? release, [FromQuery] string? model, [FromQuery] string? q, CancellationToken cancellationToken)
    {
        var items = await pool.ListAsync(new ReleasedIdFilter(status, release, model, q), cancellationToken);
        var models = await ModelsAsync(cancellationToken);
        return File(BatteryIdPool.Export(items, models), XlsxWriter.ContentType, $"battery-ids-{DateTime.UtcNow:yyyy-MM-dd}.xlsx");
    }

    private async Task<BatteryIdsPage> PageAsync(ReleaseForm form, ReleaseResult? refusal, ReleasedIdFilter filter, CancellationToken cancellationToken)
    {
        var all = await pool.ListAsync(filter, cancellationToken);
        var items = all.Take(BatteryIdsPage.MaxRows).ToArray();
        var batteries = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var packId in items.Select(item => item.PackId).OfType<string>().Distinct())
        {
            batteries[packId] = Dpp.Pages.BatteryPages.BatteryHref(packId);
        }

        return new BatteryIdsPage(form, refusal, await pool.ReleasesAsync(cancellationToken), items, filter, await ModelsAsync(cancellationToken), batteries, all.Count);
    }

    private async Task<IReadOnlyDictionary<string, string>> ModelsAsync(CancellationToken cancellationToken) =>
        (await store.ListAsync<BatteryModel>(cancellationToken)).OrderBy(model => model.Name, StringComparer.OrdinalIgnoreCase).ToDictionary(model => model.Id, model => model.Name, StringComparer.Ordinal);

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
