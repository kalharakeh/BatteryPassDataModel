using BatteryPassWeb.Dpp.Access;
using BatteryPassWeb.Dpp.Documents;
using BatteryPassWeb.Dpp.Editing;
using BatteryPassWeb.Dpp.Pages;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BatteryPassWeb.Controllers;

/// <summary>
/// The cluster admin's battery page (design 6.1 and 12, step 4a), opened from the Registry: the live values and accident
/// reports of a published battery of a cluster they are admin of, and its modules and versions to read. Anyone else
/// finds nothing here; the team is sent to its own battery page.
/// </summary>
[Authorize(Policy = "ClusterAdminOrAdmin")]
[Route("cluster-admin/batteries")]
public sealed class ClusterBatteriesController(ClusterBatteryPages pages, DppReaders readers) : Controller
{
    private const string PageView = "~/Views/ClusterAdmin/Battery.cshtml";

    [HttpGet("{packId}")]
    public async Task<IActionResult> Battery(string packId, CancellationToken cancellationToken)
    {
        var sender = await readers.EditorForAsync(User, cancellationToken);
        if (sender?.Kind == SenderKind.Team)
        {
            return Redirect(BatteryPages.BatteryHref(packId));
        }

        var page = sender is null ? null : await pages.BatteryAsync(sender, packId, cancellationToken: cancellationToken);
        return page is null ? NotFound() : View(PageView, page);
    }

    [HttpPost("{packId}/live")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveLiveValues(string packId, CancellationToken cancellationToken)
    {
        if (await ClusterAdminAsync(cancellationToken) is not { } sender)
        {
            return NotFound();
        }

        var form = Request.Form;
        var saved = await pages.SaveLiveAsync(
            sender,
            packId,
            name => form.TryGetValue(name, out var value) ? value.ToString() : null,
            form[ProductDataController.StampField].ToString(),
            BatteriesController.MeasuredAt(form[BatteriesController.MeasuredAtField].ToString()),
            cancellationToken);
        if (saved is not { } result)
        {
            return NotFound();
        }

        var (post, save) = result;
        var done = save.SavedCount > 0 ? $"{Words.Count(save.SavedCount, "value")} saved." : string.Empty;
        switch (save.Outcome)
        {
            case EditorOutcome.NotFound:
                return NotFound();
            case EditorOutcome.Refused:
                ViewData[BatteriesController.CardKey] = "live";
                ViewData[BatteriesController.CardErrorKey] = $"{done} {Words.Count(save.RefusedCount, "value")} not saved; {(save.RefusedCount == 1 ? "it's" : "they're")} marked below.".TrimStart();
                return View(PageView, await pages.BatteryAsync(sender, packId, new EditorState(post, save), cancellationToken));
            case EditorOutcome.NothingChanged:
                TempData[BatteriesController.CardKey] = "live";
                TempData[BatteriesController.CardInfoKey] = "Nothing changed.";
                break;
            default:
                Card("live", $"{done} Live values change the passport in place, without a new version.", null);
                break;
        }

        return Redirect($"{ClusterBatteryPages.BatteryHref(packId)}#live");
    }

    [HttpPost("{packId}/accidents")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddAccidentReport(string packId, IFormFile? file, [FromForm] string? title, [FromForm] string? language, CancellationToken cancellationToken)
    {
        if (await ClusterAdminAsync(cancellationToken) is not { } sender)
        {
            return NotFound();
        }

        if (file is null || file.Length == 0)
        {
            Card("accidents", null, "Choose a file first.");
            return Redirect($"{ClusterBatteryPages.BatteryHref(packId)}#accidents");
        }

        using var buffer = new MemoryStream();
        await file.CopyToAsync(buffer, cancellationToken);
        if (await pages.AddAccidentReportAsync(sender, packId, new DocumentUpload(file.FileName, buffer.ToArray(), title, language), cancellationToken) is not { } added)
        {
            return NotFound();
        }

        Card("accidents", added.Problem is null ? $"{added.Document!.FileName} added." : null, added.Problem is null ? null : $"The report wasn't added: {added.Problem}.");
        return Redirect($"{ClusterBatteryPages.BatteryHref(packId)}#accidents");
    }

    /// <summary>The signed-in user as a cluster admin; null for the team (it has its own page) and for anyone else.</summary>
    private async Task<ChangeSender?> ClusterAdminAsync(CancellationToken cancellationToken) =>
        await readers.EditorForAsync(User, cancellationToken) is { Kind: SenderKind.ClusterAdmin } sender ? sender : null;

    private void Card(string card, string? status, string? error)
    {
        TempData[BatteriesController.CardKey] = card;
        TempData[BatteriesController.CardStatusKey] = status;
        TempData[BatteriesController.CardErrorKey] = error;
    }
}
