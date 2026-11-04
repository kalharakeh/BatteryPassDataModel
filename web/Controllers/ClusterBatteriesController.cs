using BatteryPassWeb.Dpp.Access;
using BatteryPassWeb.Dpp.Documents;
using BatteryPassWeb.Dpp.Editing;
using BatteryPassWeb.Dpp.Pages;
using BatteryPassWeb.Dpp.Uploads;
using BatteryPassWeb.Services;
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
public sealed class ClusterBatteriesController(ClusterBatteryPages pages, DppReaders readers, UploadPages uploads) : Controller
{
    private const string PageView = "~/Views/ClusterAdmin/Battery.cshtml";

    /// <summary>The cluster ID typed in the "Hand over" window, and the name it was found by (step 4d).</summary>
    public const string HandOverToKey = "HandOverTo";

    public const string HandOverNameKey = "HandOverName";

    [HttpGet("{packId}")]
    public async Task<IActionResult> Battery(string packId, CancellationToken cancellationToken)
    {
        var sender = await readers.EditorForAsync(User, cancellationToken);
        if (sender?.Kind == SenderKind.Team)
        {
            return Redirect(BatteryPages.BatteryHref(packId));
        }

        var page = sender is null ? null : await WithUploadAsync(await pages.BatteryAsync(sender, packId, cancellationToken: cancellationToken), cancellationToken);
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
                return View(PageView, await WithUploadAsync(await pages.BatteryAsync(sender, packId, new EditorState(post, save), cancellationToken), cancellationToken));
            case EditorOutcome.NothingChanged:
                CardMessages.Put(TempData, packId, BatteriesController.CardKey, "live");
                CardMessages.Put(TempData, packId, BatteriesController.CardInfoKey, "Nothing changed.");
                break;
            default:
                Card(packId, "live", $"{done} Live values change the passport in place, without a new version.", null);
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
            Card(packId, "accidents", null, "Choose a file first.");
            return Redirect($"{ClusterBatteryPages.BatteryHref(packId)}#accidents");
        }

        using var buffer = new MemoryStream();
        await file.CopyToAsync(buffer, cancellationToken);
        if (await pages.AddAccidentReportAsync(sender, packId, new DocumentUpload(file.FileName, buffer.ToArray(), title, language), cancellationToken) is not { } added)
        {
            return NotFound();
        }

        Card(packId, "accidents", added.Problem is null ? $"{added.Document!.FileName} added." : null, added.Problem is null ? null : $"The report wasn't added: {added.Problem}.");
        return Redirect($"{ClusterBatteryPages.BatteryHref(packId)}#accidents");
    }

    /// <summary>The "Change status" window (design 7.4, step 4b), as the battery's cluster admin.</summary>
    [HttpPost("{packId}/status")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangeStatus(string packId, [FromForm] string? what, [FromForm] string? repairDate, [FromForm] string? repairDescription, CancellationToken cancellationToken)
    {
        if (await ClusterAdminAsync(cancellationToken) is not { } sender)
        {
            return NotFound();
        }

        var form = Request.Form;
        var before = (await pages.BatteryAsync(sender, packId, cancellationToken: cancellationToken))?.Versions.Count ?? 0;
        if (await pages.ChangeStatusAsync(sender, packId, what, name => form.TryGetValue(name, out var value) ? value.ToString() : null, repairDate, repairDescription, cancellationToken) is not { } result)
        {
            return NotFound();
        }

        var (done, problem) = pages.StatusMessage(result, before);
        Card(packId, "status", done, problem);
        return Redirect($"{ClusterBatteryPages.BatteryHref(packId)}#status");
    }

    /// <summary>"Add a note to the history" (decision 34), as the battery's cluster admin.</summary>
    [HttpPost("{packId}/status/note")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddHistoryNote(string packId, [FromForm] string? note, CancellationToken cancellationToken)
    {
        if (await ClusterAdminAsync(cancellationToken) is not { } sender
            || await pages.AddHistoryNoteAsync(sender, packId, note, cancellationToken) is not { } message)
        {
            return NotFound();
        }

        Card(packId, "status", message.Done, message.Problem);
        return Redirect($"{ClusterBatteryPages.BatteryHref(packId)}#status");
    }

    /// <summary>Another operator places the battery on the market again (design 7.5), as its cluster admin.</summary>
    [HttpPost("{packId}/successor/other")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> OtherOperator(string packId, [FromForm] string? operatorIdentifier, [FromForm] string? operatorInfo, [FromForm] string? passport, [FromForm] bool link, CancellationToken cancellationToken)
    {
        if (await ClusterAdminAsync(cancellationToken) is not { } sender
            || await pages.OtherOperatorAsync(sender, packId, new OtherOperator(operatorIdentifier, operatorInfo, passport), link, cancellationToken) is not { } message)
        {
            return NotFound();
        }

        Card(packId, "status", message.Done, message.Problem);
        return Redirect(link && message.Problem is null ? "/registry" : $"{ClusterBatteryPages.BatteryHref(packId)}#status");
    }

    /// <summary>
    /// "Hand over…" (design 7.8, step 4d): first names the receiving cluster, then "Ask them to accept" asks for the ID
    /// whose name was shown. A changed ID is named again instead of asked for.
    /// </summary>
    [HttpPost("{packId}/handover")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> HandOver(string packId, [FromForm] string? toClusterId, [FromForm] string? checkedId, [FromServices] HandOverService handOvers, CancellationToken cancellationToken)
    {
        if (await ClusterAdminAsync(cancellationToken) is not { } sender)
        {
            return NotFound();
        }

        var typed = string.IsNullOrWhiteSpace(toClusterId) ? string.Empty : ClusterRepository.NormalizeClusterId(toClusterId);
        var confirm = typed.Length > 0 && !string.IsNullOrWhiteSpace(checkedId) && ClusterRepository.NormalizeClusterId(checkedId) == typed;
        var asked = await handOvers.AskAsync(sender, packId, typed, confirm, cancellationToken);
        if (asked.Problem is { } problem)
        {
            Card(packId, "handover", null, $"Not asked: {problem}.");
        }
        else if (asked.HandOver is null)
        {
            TempData[HandOverToKey] = typed;
            TempData[HandOverNameKey] = asked.ReceiverName;
        }
        else
        {
            Card(packId, "handover", $"Asked. The battery stays with your cluster until {asked.ReceiverName} accepts.", null);
        }

        return Redirect($"{ClusterBatteryPages.BatteryHref(packId)}#handover");
    }

    /// <summary>The sender cancels its waiting hand-over (step 4d).</summary>
    [HttpPost("{packId}/handover/cancel")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CancelHandOver(string packId, [FromServices] HandOverService handOvers, CancellationToken cancellationToken)
    {
        if (await ClusterAdminAsync(cancellationToken) is not { } sender)
        {
            return NotFound();
        }

        var problem = await handOvers.CancelAsync(sender, packId, cancellationToken);
        Card(packId, "handover", problem is null ? "Hand-over cancelled. The battery stays with your cluster." : null, problem is null ? null : $"Not cancelled: {problem}.");
        return Redirect($"{ClusterBatteryPages.BatteryHref(packId)}#handover");
    }

    /// <summary>The signed-in user as a cluster admin; null for the team (it has its own page) and for anyone else.</summary>
    /// <summary>"Check the file" in the "Upload a file" section (decision 35, step 5c), as the battery's cluster admin.</summary>
    [HttpPost("{packId}/upload")]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(UploadService.MaxBytes + 65536)]
    public async Task<IActionResult> CheckUpload(string packId, IFormFile? file, CancellationToken cancellationToken)
    {
        if (await ClusterAdminAsync(cancellationToken) is not { } sender || await pages.BatteryAsync(sender, packId, cancellationToken: cancellationToken) is not { ReadOnly: false })
        {
            return NotFound();
        }

        var (checkId, problem) = await uploads.CheckAsync(sender, packId, file?.FileName, file?.OpenReadStream(), file?.Length ?? 0, cancellationToken);
        CardMessages.Put(TempData, packId, UploadPages.CheckKey, checkId);
        Card(packId, "upload", null, problem is null ? null : $"Not checked: {problem}.");
        return Redirect($"{ClusterBatteryPages.BatteryHref(packId)}?section=upload");
    }

    /// <summary>"Save" after the check (step 5c): the kept file goes through the save pipeline as the cluster admin.</summary>
    [HttpPost("{packId}/upload/save")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveUpload(string packId, [FromForm] string? checkId, CancellationToken cancellationToken)
    {
        if (await ClusterAdminAsync(cancellationToken) is not { } sender || await pages.BatteryAsync(sender, packId, cancellationToken: cancellationToken) is not { ReadOnly: false })
        {
            return NotFound();
        }

        var (done, uploadId, problem) = await uploads.SaveAsync(sender, packId, checkId, cancellationToken);
        CardMessages.Put(TempData, packId, UploadPages.OpenKey, uploadId);
        Card(packId, "upload", done, problem is null ? null : $"Not saved: {problem}.");
        return Redirect($"{ClusterBatteryPages.BatteryHref(packId)}?section=upload");
    }

    /// <summary>The page with its "Upload a file" section; a read-only page (a marked passport) gets none.</summary>
    /// <remarks>The battery's one-time messages move from TempData into ViewData here, on every path that shows the page.</remarks>
    private async Task<ClusterBatteryPage?> WithUploadAsync(ClusterBatteryPage? page, CancellationToken cancellationToken)
    {
        if (page is null)
        {
            return null;
        }

        CardMessages.Take(TempData, ViewData, page.Pack.Id);
        return page.ReadOnly ? page : page with
        {
            Upload = await uploads.CardAsync(page.Pack, ClusterBatteryPages.BatteryHref(page.Pack.Id), forTeam: false, ViewData[UploadPages.CheckKey] as string, ViewData[UploadPages.OpenKey] as string, cancellationToken,
                viewer: AccessControlService.CurrentEmail(User).Trim().ToLowerInvariant()),
        };
    }

    private async Task<ChangeSender?> ClusterAdminAsync(CancellationToken cancellationToken) =>
        await readers.EditorForAsync(User, cancellationToken) is { Kind: SenderKind.ClusterAdmin } sender ? sender : null;

    private void Card(string packId, string card, string? status, string? error)
    {
        CardMessages.Put(TempData, packId, BatteriesController.CardKey, card);
        CardMessages.Put(TempData, packId, BatteriesController.CardStatusKey, status);
        CardMessages.Put(TempData, packId, BatteriesController.CardErrorKey, error);
    }
}
