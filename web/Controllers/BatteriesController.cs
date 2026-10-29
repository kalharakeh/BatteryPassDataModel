using System.Globalization;
using BatteryPassWeb.Dpp.Documents;
using BatteryPassWeb.Dpp.Editing;
using BatteryPassWeb.Dpp.Pages;
using BatteryPassWeb.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BatteryPassWeb.Controllers;

/// <summary>
/// The team's batteries pages (design 12 and 14, step 2d-2): the list, a new battery and the battery page. They replace
/// the old "Batteries" admin pages under the same menu entry. Passport values go through the save pipeline.
/// </summary>
[Authorize(Policy = "AdminOnly")]
[Route("admin/batteries")]
public sealed class BatteriesController(BatteryPages pages, DocumentService documents) : Controller
{
    /// <summary>Which card of the battery page a message belongs to, so it shows where the action was taken.</summary>
    public const string CardKey = "BatteryCard";

    public const string CardStatusKey = "BatteryCardStatus";

    public const string CardErrorKey = "BatteryCardError";

    /// <summary>Neutral information in a card ("Nothing changed."): neither a success nor a problem.</summary>
    public const string CardInfoKey = "BatteryCardInfo";

    /// <summary>The live-values form's field with the time the values were measured, in the page's time zone.</summary>
    public const string MeasuredAtField = "measuredAt";

    [HttpGet("")]
    public async Task<IActionResult> Batteries([FromQuery] string? q, CancellationToken cancellationToken) =>
        View(await pages.ListAsync(q, cancellationToken));

    [HttpGet("new")]
    public async Task<IActionResult> NewBattery([FromQuery] string? batch, CancellationToken cancellationToken) =>
        View(await pages.NewAsync(batchId: batch, cancellationToken: cancellationToken));

    [HttpPost("")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateBattery([FromForm] string? batchId, [FromForm] string? operatorId, [FromForm] string? clusterId, CancellationToken cancellationToken)
    {
        var form = Request.Form;
        var (result, post) = await pages.CreateAsync(Who, batchId, operatorId, clusterId, name => form.TryGetValue(name, out var value) ? value.ToString() : null, cancellationToken);
        if (result.Pack is { } pack)
        {
            TempData[ProductDataController.StatusKey] = "Draft battery created. Fill in what its passport still needs, then publish it.";
            return Redirect(BatteryPages.BatteryHref(pack.Id));
        }

        var refused = result.Keys.Count(key => key.Outcome == KeyOutcome.Rejected);
        ViewData[ProductDataController.ErrorKey] = refused > 0
            ? $"The battery wasn't created: {Words.Count(refused, "value")} {(refused == 1 ? "was" : "were")} refused; {(refused == 1 ? "it's" : "they're")} marked below."
            : $"The battery wasn't created: {result.Problem}.";
        var state = new EditorState(post, new EditorSave(EditorOutcome.Refused, result.Keys, []));
        return View("NewBattery", await pages.NewAsync(batchId, operatorId, clusterId, state, cancellationToken));
    }

    [HttpGet("{packId}")]
    public async Task<IActionResult> Battery(string packId, CancellationToken cancellationToken)
    {
        var page = await pages.BatteryAsync(packId, cancellationToken: cancellationToken);
        return page is null ? NotFound() : View(page);
    }

    [HttpPost("{packId}")]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> SaveValues(string packId, [FromForm] bool confirmed, CancellationToken cancellationToken) =>
        SaveAsync(packId, liveValues: false, confirmed, cancellationToken);

    [HttpPost("{packId}/live")]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> SaveLiveValues(string packId, CancellationToken cancellationToken) =>
        SaveAsync(packId, liveValues: true, confirmed: true, cancellationToken);

    [HttpPost("{packId}/cluster")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SetCluster(string packId, [FromForm] string? clusterId, CancellationToken cancellationToken)
    {
        var problem = await pages.SetClusterAsync(Who, packId, clusterId, cancellationToken);
        Card("cluster", problem is null ? "Cluster changed. The passport didn't change: the cluster isn't passport data." : null, problem is null ? null : $"The cluster wasn't changed: {problem}.");
        return Redirect(BatteryPages.BatteryHref(packId));
    }

    /// <summary>The largest JSON export taken for the module list; the battery system's are a few kilobytes.</summary>
    public const long MaxExportBytes = 1024 * 1024;

    [HttpPost("{packId}/modules")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SetModules(string packId, [FromForm] string? modules, CancellationToken cancellationToken)
    {
        var problem = await pages.SetModulesAsync(Who, packId, modules, cancellationToken);
        Card("modules", problem is null ? "Modules saved. The passport didn't change: modules aren't passport data." : null, problem is null ? null : $"The modules weren't saved: {problem}.");
        return Redirect($"{BatteryPages.BatteryHref(packId)}#modules");
    }

    [HttpPost("{packId}/modules/from-export")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ImportModules(string packId, IFormFile? file, CancellationToken cancellationToken)
    {
        string? problem;
        if (file is null || file.Length == 0)
        {
            problem = "choose the battery system's JSON export first";
        }
        else if (file.Length > MaxExportBytes)
        {
            problem = "the file is larger than an export (1 MB)";
        }
        else
        {
            using var buffer = new MemoryStream();
            await file.CopyToAsync(buffer, cancellationToken);
            problem = await pages.ImportModulesAsync(Who, packId, buffer.ToArray(), cancellationToken);
        }

        Card("modules", problem is null ? "Modules filled from the export. The passport didn't change: modules aren't passport data." : null, problem is null ? null : $"The modules weren't filled: {problem}.");
        return Redirect($"{BatteryPages.BatteryHref(packId)}#modules");
    }

    [HttpPost("{packId}/publish")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Publish(string packId, CancellationToken cancellationToken)
    {
        var result = await pages.PublishAsync(Who, packId, cancellationToken);
        if (result.Version is { } version && result.Problem is null)
        {
            Card("publication", result.Unchanged ? "Nothing new to publish: the passport is already up to date." : $"Version {version.Number} published.", null);
        }
        else
        {
            Card("publication", null, $"The passport wasn't published: {result.Problem}.");
        }

        return Redirect($"{BatteryPages.BatteryHref(packId)}#publication");
    }

    [HttpPost("{packId}/accidents")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddAccidentReport(string packId, IFormFile? file, [FromForm] string? title, [FromForm] string? language, CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
        {
            Card("accidents", null, "Choose a file first.");
            return Redirect($"{BatteryPages.BatteryHref(packId)}#accidents");
        }

        using var buffer = new MemoryStream();
        await file.CopyToAsync(buffer, cancellationToken);
        var result = await documents.AddAccidentReportAsync(ChangeSender.Team(Who), packId, new DocumentUpload(file.FileName, buffer.ToArray(), title, language), cancellationToken);
        Card("accidents", result.Problem is null ? $"{result.Document!.FileName} added." : null, result.Problem is null ? null : $"The report wasn't added: {result.Problem}.");
        return Redirect($"{BatteryPages.BatteryHref(packId)}#accidents");
    }

    [HttpPost("{packId}/remove")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Remove(string packId, CancellationToken cancellationToken)
    {
        var (problem, serial) = await pages.RemoveDraftAsync(Who, packId, cancellationToken);
        if (problem is not null)
        {
            Card("remove", null, $"The battery wasn't removed: {problem}.");
            return Redirect($"{BatteryPages.BatteryHref(packId)}#remove");
        }

        TempData[ProductDataController.StatusKey] = $"Draft battery {serial} removed.";
        return Redirect("/admin/batteries");
    }

    private string Who => AccessControlService.CurrentEmail(User) is { Length: > 0 } email ? email : "admin";

    private void Card(string card, string? status, string? error)
    {
        TempData[CardKey] = card;
        TempData[CardStatusKey] = status;
        TempData[CardErrorKey] = error;
    }

    /// <summary>
    /// Saves the battery's own values (the values card, which asks before publishing a new version) or its live values
    /// (in place, with the time they were measured). Refused values show the page again as typed.
    /// </summary>
    private async Task<IActionResult> SaveAsync(string packId, bool liveValues, bool confirmed, CancellationToken cancellationToken)
    {
        var form = Request.Form;
        var measured = liveValues ? MeasuredAt(form[MeasuredAtField].ToString()) : null;
        var saved = await pages.SaveAsync(
            Who,
            packId,
            liveValues,
            name => form.TryGetValue(name, out var value) ? value.ToString() : null,
            form[ProductDataController.StampField].ToString(),
            measured,
            confirmed,
            cancellationToken);
        if (saved is not { } result)
        {
            return NotFound();
        }

        var (post, save) = result;
        var anchor = liveValues ? "#live" : "#values";
        var done = (save.SavedCount, save.RemovedCount) switch
        {
            (> 0, > 0) => $"{Words.Count(save.SavedCount, "value")} saved and {save.RemovedCount} removed.",
            (> 0, _) => $"{Words.Count(save.SavedCount, "value")} saved.",
            (_, > 0) => $"{Words.Count(save.RemovedCount, "value")} removed.",
            _ => string.Empty,
        };
        var version = !liveValues && save.Affected.Count > 0 && save.Outcome is EditorOutcome.Saved or EditorOutcome.Refused ? " The passport got a new version." : string.Empty;
        EditorState state = new(post, save);
        switch (save.Outcome)
        {
            case EditorOutcome.NotFound:
                return NotFound();
            case EditorOutcome.NeedsConfirmation:
                return View("Battery", await pages.BatteryAsync(packId, values: state, cancellationToken: cancellationToken));
            case EditorOutcome.Refused:
                var refused = $"{done} {Words.Count(save.RefusedCount, "value")} not saved; {(save.RefusedCount == 1 ? "it's" : "they're")} marked below.{version}".TrimStart();
                if (liveValues)
                {
                    ViewData[CardKey] = "live";
                    ViewData[CardErrorKey] = refused;
                    return View("Battery", await pages.BatteryAsync(packId, live: state, cancellationToken: cancellationToken));
                }

                ViewData[ProductDataController.EditorErrorKey] = refused;
                return View("Battery", await pages.BatteryAsync(packId, values: state, cancellationToken: cancellationToken));
            case EditorOutcome.NothingChanged:
                if (liveValues)
                {
                    TempData[CardKey] = "live";
                    TempData[CardInfoKey] = "Nothing changed.";
                }
                else
                {
                    TempData[ProductDataController.EditorInfoKey] = "Nothing changed.";
                }

                return Redirect(BatteryPages.BatteryHref(packId) + anchor);
            default:
                if (liveValues)
                {
                    Card("live", $"{done} Live values change the passport in place, without a new version.", null);
                }
                else
                {
                    TempData[ProductDataController.EditorStatusKey] = $"{done}{version}";
                }

                return Redirect(BatteryPages.BatteryHref(packId) + anchor);
        }
    }

    /// <summary>A measurement time typed in the page's time zone (a datetime-local input), as UTC; null when blank or unreadable.</summary>
    public static DateTime? MeasuredAt(string? typed)
    {
        if (string.IsNullOrWhiteSpace(typed)
            || !DateTime.TryParse(typed, CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces, out var local))
        {
            return null;
        }

        return TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(local, DateTimeKind.Unspecified), TimeZoneInfo.Local);
    }
}
