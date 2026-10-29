using BatteryPassWeb.Dpp.Documents;
using BatteryPassWeb.Dpp.Editing;
using BatteryPassWeb.Dpp.Pages;
using BatteryPassWeb.Dpp.Store;
using BatteryPassWeb.Dpp.Values;
using BatteryPassWeb.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BatteryPassWeb.Controllers;

/// <summary>
/// The team's product data pages (design 12, decision 23): battery families with their models and batches, and the
/// manufacturing sites and economic operators. Passport values go through the save pipeline via <see cref="RecordEditor"/>.
/// </summary>
[Authorize(Policy = "AdminOnly")]
[Route("admin")]
public sealed class ProductDataController(ProductDataPages pages, RecordEditor editor, DppStore store, DocumentService documents) : Controller
{
    public const string StatusKey = "StatusMessage";
    public const string ErrorKey = "ErrorMessage";

    /// <summary>Messages about an editor's own save or upload, shown in the editor's header card.</summary>
    public const string EditorStatusKey = "EditorStatusMessage";
    public const string EditorErrorKey = "EditorErrorMessage";

    /// <summary>The editor's anchor: after a save or upload the page opens at the editor, not at its top.</summary>
    public const string EditorAnchor = "#editor";

    /// <summary>Neutral information on the page or the editor ("Nothing changed."): neither a success nor a problem.</summary>
    public const string InfoKey = "InfoMessage";

    public const string EditorInfoKey = "EditorInfoMessage";

    /// <summary>The editor's hidden field with the stamp of what the page showed (RecordEditor.Stamp).</summary>
    public const string StampField = "shown";

    [HttpGet("families")]
    public async Task<IActionResult> Families(CancellationToken cancellationToken) =>
        View(await pages.FamiliesAsync(cancellationToken));

    [HttpPost("families")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateFamily([FromForm] string? name, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            TempData[ErrorKey] = "Give the new battery family a name.";
            return Redirect("/admin/families");
        }

        if (await pages.NameTakenAsync(RecordKind.Family, name, cancellationToken: cancellationToken) is { } taken)
        {
            TempData[ErrorKey] = $"The battery family wasn't added: {taken}.";
            return Redirect("/admin/families");
        }

        var familyId = await pages.CreateFamilyAsync(Who, name, cancellationToken);
        TempData[StatusKey] = "Battery family added. Add its first model below.";
        return Redirect(ProductDataPages.FamilyHref(familyId));
    }

    [HttpGet("families/{familyId}")]
    public async Task<IActionResult> Family(string familyId, [FromQuery] string? model, CancellationToken cancellationToken)
    {
        var page = await pages.FamilyAsync(familyId, model, cancellationToken: cancellationToken);
        return page is null ? NotFound() : View(page);
    }

    [HttpPost("families/{familyId}/name")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RenameFamily(string familyId, [FromForm] string? name, CancellationToken cancellationToken)
    {
        var family = await store.GetAsync<BatteryFamily>(familyId, cancellationToken);
        if (family is null)
        {
            return NotFound();
        }

        if (!string.IsNullOrWhiteSpace(name) && await pages.NameTakenAsync(RecordKind.Family, name, exceptId: familyId, cancellationToken: cancellationToken) is { } taken)
        {
            TempData[ErrorKey] = $"The name wasn't changed: {taken}.";
            return Redirect(ProductDataPages.FamilyHref(familyId));
        }

        if (await pages.RenameAsync<BatteryFamily>(Who, familyId, family.Name, name, cancellationToken))
        {
            TempData[StatusKey] = "Name saved.";
        }
        else
        {
            TempData[InfoKey] = "Nothing changed.";
        }

        return Redirect(ProductDataPages.FamilyHref(familyId));
    }

    [HttpPost("families/{familyId}/remove")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RemoveFamily(string familyId, CancellationToken cancellationToken)
    {
        var (removed, problem, name) = await pages.RemoveAsync(Who, RecordKind.Family, familyId, cancellationToken);
        if (!removed)
        {
            TempData[ErrorKey] = $"The battery family wasn't removed: {problem}";
            return Redirect(ProductDataPages.FamilyHref(familyId));
        }

        TempData[StatusKey] = $"Battery family {name} removed.";
        return Redirect("/admin/families");
    }

    [HttpPost("families/{familyId}/picture")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UploadFamilyPicture(string familyId, IFormFile? picture, CancellationToken cancellationToken)
    {
        if (picture is null || picture.Length == 0)
        {
            TempData[ErrorKey] = "Choose a PNG or JPEG picture first.";
            return Redirect(ProductDataPages.FamilyHref(familyId));
        }

        var problem = await pages.SetFamilyPictureAsync(Who, familyId, picture.FileName, await ReadAsync(picture, cancellationToken), cancellationToken);
        TempData[problem is null ? StatusKey : ErrorKey] = problem is null ? "Picture saved." : $"The picture was not saved: {problem}.";
        return Redirect(ProductDataPages.FamilyHref(familyId));
    }

    /// <summary>The family picture is shown on the public summary (decision 20), so anyone may load it.</summary>
    [AllowAnonymous]
    [HttpGet("~/dpp/families/{familyId}/picture")]
    public async Task<IActionResult> FamilyPicture(string familyId, CancellationToken cancellationToken)
    {
        var picture = await pages.FamilyPictureAsync(familyId, cancellationToken);
        return picture is { } found ? File(found.Content, found.ContentType) : NotFound();
    }

    [HttpPost("families/{familyId}/models")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateModel(string familyId, [FromForm] string? name, [FromForm] string? copyFrom, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            TempData[ErrorKey] = "Give the new battery model a name.";
            return Redirect(ProductDataPages.FamilyHref(familyId));
        }

        if (await pages.NameTakenAsync(RecordKind.Model, name, familyId, cancellationToken: cancellationToken) is { } taken)
        {
            TempData[ErrorKey] = $"The battery model wasn't added: {taken}.";
            return Redirect(ProductDataPages.FamilyHref(familyId));
        }

        var source = string.IsNullOrEmpty(copyFrom) ? null : await store.GetAsync<BatteryModel>(copyFrom, cancellationToken);
        var modelId = await pages.CreateModelAsync(Who, familyId, name, source?.Id, cancellationToken);
        if (modelId is null)
        {
            return NotFound();
        }

        TempData[EditorStatusKey] = source is null
            ? "Battery model added. Fill in its passport values below."
            : $"Battery model added with the values of {source.Name}. Check them and change what differs; uploaded documents weren't copied.";
        return Redirect(ProductDataPages.FamilyHref(familyId, modelId) + EditorAnchor);
    }

    [HttpPost("models/{modelId}/remove")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RemoveModel(string modelId, CancellationToken cancellationToken)
    {
        var model = await store.GetAsync<BatteryModel>(modelId, cancellationToken);
        if (model is null)
        {
            return NotFound();
        }

        var (removed, problem, name) = await pages.RemoveAsync(Who, RecordKind.Model, modelId, cancellationToken);
        if (!removed)
        {
            TempData[EditorErrorKey] = $"The battery model wasn't removed: {problem}";
            return Redirect(ProductDataPages.FamilyHref(model.FamilyId, modelId) + EditorAnchor);
        }

        TempData[StatusKey] = $"Battery model {name} removed.";
        return Redirect(ProductDataPages.FamilyHref(model.FamilyId));
    }

    [HttpPost("models/{modelId}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveModel(string modelId, [FromForm] bool confirmed, CancellationToken cancellationToken)
    {
        var model = await store.GetAsync<BatteryModel>(modelId, cancellationToken);
        if (model is null)
        {
            return NotFound();
        }

        return await SaveAsync<BatteryModel>(
            TargetKind.Model,
            modelId,
            model.Name,
            model.FamilyId,
            model.Values,
            confirmed,
            ProductDataPages.FamilyHref(model.FamilyId, modelId),
            async state => View("Family", await pages.FamilyAsync(model.FamilyId, modelId, state, cancellationToken)),
            cancellationToken);
    }

    [HttpPost("models/{modelId}/module-description")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SetModuleDescription(string modelId, [FromForm] string? description, CancellationToken cancellationToken)
    {
        var model = await store.GetAsync<BatteryModel>(modelId, cancellationToken);
        if (model is null)
        {
            return NotFound();
        }

        if (await pages.SetModuleDescriptionAsync(Who, model, description, cancellationToken) is { } problem)
        {
            TempData[ErrorKey] = $"The module description wasn't saved: {problem}.";
        }
        else
        {
            TempData[StatusKey] = "Module description saved. The passport didn't change: it isn't passport data.";
        }

        return Redirect($"{ProductDataPages.FamilyHref(model.FamilyId, modelId)}#modules");
    }

    [HttpPost("models/{modelId}/batches")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateBatch(string modelId, [FromForm] int? year, [FromForm] string? siteId, CancellationToken cancellationToken)
    {
        var model = await store.GetAsync<BatteryModel>(modelId, cancellationToken);
        if (model is null)
        {
            return NotFound();
        }

        var (batchId, problem) = await pages.CreateBatchAsync(Who, modelId, year ?? 0, siteId ?? string.Empty, cancellationToken);
        if (problem is not null)
        {
            TempData[ErrorKey] = $"The batch was not added: {problem}.";
            return Redirect(ProductDataPages.FamilyHref(model.FamilyId, modelId));
        }

        TempData[StatusKey] = "Batch added.";
        return Redirect($"/admin/batches/{Uri.EscapeDataString(batchId!)}");
    }

    [HttpPost("models/{modelId}/documents/{property}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UploadModelDocument(string modelId, string property, IFormFile? file, [FromForm] string? title, [FromForm] string? language, [FromForm] bool confirmed, CancellationToken cancellationToken)
    {
        var model = await store.GetAsync<BatteryModel>(modelId, cancellationToken);
        return model is null
            ? NotFound()
            : await UploadAsync(TargetKind.Model, modelId, property, file, title, language, confirmed, ProductDataPages.FamilyHref(model.FamilyId, modelId), cancellationToken);
    }

    [HttpGet("batches/{batchId}")]
    public async Task<IActionResult> Batch(string batchId, CancellationToken cancellationToken)
    {
        var page = await pages.BatchAsync(batchId, cancellationToken: cancellationToken);
        return page is null ? NotFound() : View(page);
    }

    [HttpPost("batches/{batchId}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveBatch(string batchId, [FromForm] bool confirmed, CancellationToken cancellationToken)
    {
        var batch = await store.GetAsync<ProductionBatch>(batchId, cancellationToken);
        if (batch is null)
        {
            return NotFound();
        }

        return await SaveAsync<ProductionBatch>(
            TargetKind.Batch,
            batchId,
            null,
            null,
            batch.Values,
            confirmed,
            $"/admin/batches/{Uri.EscapeDataString(batchId)}",
            async state => View("Batch", await pages.BatchAsync(batchId, state, cancellationToken)),
            cancellationToken);
    }

    [HttpPost("batches/{batchId}/documents/{property}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UploadBatchDocument(string batchId, string property, IFormFile? file, [FromForm] string? title, [FromForm] string? language, [FromForm] bool confirmed, CancellationToken cancellationToken) =>
        await store.GetAsync<ProductionBatch>(batchId, cancellationToken) is null
            ? NotFound()
            : await UploadAsync(TargetKind.Batch, batchId, property, file, title, language, confirmed, $"/admin/batches/{Uri.EscapeDataString(batchId)}", cancellationToken);

    [HttpPost("batches/{batchId}/remove")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RemoveBatch(string batchId, CancellationToken cancellationToken)
    {
        var batch = await store.GetAsync<ProductionBatch>(batchId, cancellationToken);
        var model = batch is null ? null : await store.GetAsync<BatteryModel>(batch.ModelId, cancellationToken);
        if (batch is null || model is null)
        {
            return NotFound();
        }

        var (removed, problem, name) = await pages.RemoveAsync(Who, RecordKind.Batch, batchId, cancellationToken);
        if (!removed)
        {
            TempData[EditorErrorKey] = $"The batch wasn't removed: {problem}";
            return Redirect($"/admin/batches/{Uri.EscapeDataString(batchId)}{EditorAnchor}");
        }

        TempData[StatusKey] = $"{name} removed.";
        return Redirect(ProductDataPages.FamilyHref(model.FamilyId, model.Id));
    }

    [HttpGet("sites-and-operators")]
    public async Task<IActionResult> SitesAndOperators([FromQuery] string? site, [FromQuery(Name = "operator")] string? economicOperator, CancellationToken cancellationToken) =>
        View(await pages.SitesAndOperatorsAsync(site, economicOperator, cancellationToken: cancellationToken));

    [HttpPost("sites")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateSite([FromForm] string? name, [FromForm] string? returnTo, CancellationToken cancellationToken)
    {
        var back = ProductDataPages.SafeReturn(returnTo);
        var listHref = back is null ? "/admin/sites-and-operators" : $"/admin/sites-and-operators?returnTo={Uri.EscapeDataString(back)}";
        if (string.IsNullOrWhiteSpace(name))
        {
            TempData[ErrorKey] = "Give the new manufacturing site a name.";
            return Redirect(listHref);
        }

        if (await pages.NameTakenAsync(RecordKind.Site, name, cancellationToken: cancellationToken) is { } taken)
        {
            TempData[ErrorKey] = $"The manufacturing site wasn't added: {taken}.";
            return Redirect(listHref);
        }

        var siteId = await pages.CreateSiteAsync(Who, name, cancellationToken);
        TempData[EditorStatusKey] = "Manufacturing site added. Fill in its passport values below.";
        return Redirect(ProductDataPages.SitesHref("site", siteId) + (back is null ? string.Empty : $"&returnTo={Uri.EscapeDataString(back)}") + EditorAnchor);
    }

    [HttpPost("sites/{siteId}/remove")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RemoveSite(string siteId, CancellationToken cancellationToken)
    {
        var (removed, problem, name) = await pages.RemoveAsync(Who, RecordKind.Site, siteId, cancellationToken);
        if (!removed)
        {
            TempData[EditorErrorKey] = $"The manufacturing site wasn't removed: {problem}";
            return Redirect(ProductDataPages.SitesHref("site", siteId) + EditorAnchor);
        }

        TempData[StatusKey] = $"Manufacturing site {name} removed.";
        return Redirect("/admin/sites-and-operators");
    }

    [HttpPost("sites/{siteId}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveSite(string siteId, [FromForm] bool confirmed, CancellationToken cancellationToken)
    {
        var site = await store.GetAsync<ManufacturingSite>(siteId, cancellationToken);
        if (site is null)
        {
            return NotFound();
        }

        return await SaveAsync<ManufacturingSite>(
            TargetKind.Site,
            siteId,
            site.Name,
            null,
            site.Values,
            confirmed,
            ProductDataPages.SitesHref("site", siteId),
            async state => View("SitesAndOperators", await pages.SitesAndOperatorsAsync(siteId, null, state, cancellationToken)),
            cancellationToken);
    }

    [HttpPost("operators")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateOperator([FromForm] string? name, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            TempData[ErrorKey] = "Give the new economic operator a name.";
            return Redirect("/admin/sites-and-operators");
        }

        if (await pages.NameTakenAsync(RecordKind.Operator, name, cancellationToken: cancellationToken) is { } taken)
        {
            TempData[ErrorKey] = $"The economic operator wasn't added: {taken}.";
            return Redirect("/admin/sites-and-operators");
        }

        var operatorId = await pages.CreateOperatorAsync(Who, name, cancellationToken);
        TempData[EditorStatusKey] = "Economic operator added. Fill in its passport values below.";
        return Redirect(ProductDataPages.SitesHref("operator", operatorId) + EditorAnchor);
    }

    [HttpPost("operators/{operatorId}/remove")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RemoveOperator(string operatorId, CancellationToken cancellationToken)
    {
        var (removed, problem, name) = await pages.RemoveAsync(Who, RecordKind.Operator, operatorId, cancellationToken);
        if (!removed)
        {
            TempData[EditorErrorKey] = $"The economic operator wasn't removed: {problem}";
            return Redirect(ProductDataPages.SitesHref("operator", operatorId) + EditorAnchor);
        }

        TempData[StatusKey] = $"Economic operator {name} removed.";
        return Redirect("/admin/sites-and-operators");
    }

    [HttpPost("operators/{operatorId}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveOperator(string operatorId, [FromForm] bool confirmed, CancellationToken cancellationToken)
    {
        var item = await store.GetAsync<EconomicOperator>(operatorId, cancellationToken);
        if (item is null)
        {
            return NotFound();
        }

        return await SaveAsync<EconomicOperator>(
            TargetKind.Operator,
            operatorId,
            item.Name,
            null,
            item.Values,
            confirmed,
            ProductDataPages.SitesHref("operator", operatorId),
            async state => View("SitesAndOperators", await pages.SitesAndOperatorsAsync(null, operatorId, state, cancellationToken)),
            cancellationToken);
    }

    private string Who => AccessControlService.CurrentEmail(User) is { Length: > 0 } email ? email : "admin";

    /// <summary>
    /// Saves an editor post: the record's name (when it has one) and its changed values. Refused values and the
    /// republish question show the page again with what was typed; anything else redirects back to it.
    /// </summary>
    private async Task<IActionResult> SaveAsync<T>(
        TargetKind kind,
        string id,
        string? currentName,
        string? familyId,
        IReadOnlyDictionary<string, AttributeValue> stored,
        bool confirmed,
        string pageHref,
        Func<EditorState, Task<IActionResult>> showAgain,
        CancellationToken cancellationToken)
        where T : class, IDppRecord
    {
        var form = Request.Form;
        var stamp = form[StampField].ToString();
        var post = editor.Read(kind, stored, name => form.TryGetValue(name, out var value) ? value.ToString() : null, stamp);
        var postedName = form["name"].ToString();
        string? nameTaken = null;
        var renamed = false;
        if (currentName is not null && !string.IsNullOrWhiteSpace(postedName) && RecordEditor.NameEdited(stamp, postedName))
        {
            var nameKind = kind switch { TargetKind.Model => RecordKind.Model, TargetKind.Site => RecordKind.Site, _ => RecordKind.Operator };
            nameTaken = await pages.NameTakenAsync(nameKind, postedName, familyId, id, cancellationToken);
            renamed = nameTaken is null && await pages.RenameAsync<T>(Who, id, currentName, postedName, cancellationToken);
        }

        var save = await editor.SaveAsync(ChangeSender.Team(Who), kind, id, post, confirmed, cancellationToken);
        var nameNote = nameTaken is null ? string.Empty : $" The name wasn't changed: {nameTaken}.";
        var notes = (renamed ? " Name saved." : string.Empty)
            + (save.Affected.Count > 0 && save.Outcome is EditorOutcome.Saved or EditorOutcome.Refused
                ? $" {Words.Count(save.Affected.Count, "passport")} got a new version."
                : string.Empty);
        var done = (save.SavedCount, save.RemovedCount) switch
        {
            (> 0, > 0) => $"{Words.Count(save.SavedCount, "value")} saved and {save.RemovedCount} removed.",
            (> 0, _) => $"{Words.Count(save.SavedCount, "value")} saved.",
            (_, > 0) => $"{Words.Count(save.RemovedCount, "value")} removed.",
            _ => string.Empty,
        };
        switch (save.Outcome)
        {
            case EditorOutcome.NotFound:
                return NotFound();
            case EditorOutcome.NeedsConfirmation:
                return await showAgain(new EditorState(post, save));
            case EditorOutcome.Refused:
                ViewData[EditorErrorKey] = $"{done} {Words.Count(save.RefusedCount, "value")} not saved; {(save.RefusedCount == 1 ? "it's" : "they're")} marked below.{notes}{nameNote}".TrimStart();
                return await showAgain(new EditorState(post, save));
            case EditorOutcome.NothingChanged:
                if (nameTaken is not null)
                {
                    TempData[EditorErrorKey] = nameNote.Trim();
                }
                else
                {
                    TempData[renamed ? EditorStatusKey : EditorInfoKey] = renamed ? "Name saved." : "Nothing changed.";
                }

                return Redirect(pageHref + EditorAnchor);
            default:
                TempData[EditorStatusKey] = $"{done}{notes}";
                if (nameTaken is not null)
                {
                    TempData[EditorErrorKey] = nameNote.Trim();
                }

                return Redirect(pageHref + EditorAnchor);
        }
    }

    private async Task<IActionResult> UploadAsync(TargetKind kind, string id, string property, IFormFile? file, string? title, string? language, bool confirmed, string pageHref, CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
        {
            TempData[EditorErrorKey] = "Choose a file first.";
            return Redirect(pageHref + EditorAnchor);
        }

        // Like a typed change, an upload that publishes new versions of published passports is only made once confirmed (design 4.2).
        var published = await editor.AffectedAsync(kind, id, cancellationToken);
        if (published.Count > 0 && !confirmed)
        {
            TempData[EditorErrorKey] = $"The file was not uploaded: it would publish new versions of {Words.Count(published.Count, "passport")}. Tick \"Publish new versions\" next to the file, choose it again and upload.";
            return Redirect(pageHref + EditorAnchor);
        }

        var result = await documents.UploadAsync(ChangeSender.Team(Who), kind, id, property, new DocumentUpload(file.FileName, await ReadAsync(file, cancellationToken), title, language), cancellationToken);
        TempData[result.Problem is null ? EditorStatusKey : EditorErrorKey] = result.Problem is null
            ? $"{result.Document!.FileName} uploaded."
            : $"The file was not uploaded: {result.Problem}.";
        return Redirect(pageHref + EditorAnchor);
    }

    private static async Task<byte[]> ReadAsync(IFormFile file, CancellationToken cancellationToken)
    {
        using var buffer = new MemoryStream();
        await file.CopyToAsync(buffer, cancellationToken);
        return buffer.ToArray();
    }
}
