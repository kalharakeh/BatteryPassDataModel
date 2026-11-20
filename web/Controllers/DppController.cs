using BatteryPassWeb.Dpp.Access;
using BatteryPassWeb.Dpp.Documents;
using BatteryPassWeb.Dpp.Pages;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Net.Http.Headers;

namespace BatteryPassWeb.Controllers;

/// <summary>
/// The addresses passports link to (design 9): uploaded documents and the accident page, each served only to the
/// attribute's audience. Not signed in: the sign-in page comes first. Signed in without access: "Not available", which
/// doesn't say whether the battery or the file exists.
/// </summary>
[AllowAnonymous]
[Route("dpp")]
public sealed class DppController(DppReaders readers, DocumentService documents, ReaderPages pages) : Controller
{
    [HttpGet("documents/{documentId}")]
    public async Task<IActionResult> Document(
        string documentId,
        CancellationToken cancellationToken,
        [FromServices] RestrictedReads? restricted = null,
        [FromServices] BatteryPassWeb.Dpp.Store.DppStore? store = null)
    {
        var reader = await readers.ForAsync(User, cancellationToken);
        var file = await documents.ReadAsync(reader, documentId, cancellationToken);
        if (file is null)
        {
            return Refused("document");
        }

        // Finding R11: a restricted document comes with the use notice (a header: the file itself is shown as it is), and its
        // first opening today by this reader is logged; the team's own reads and a cluster admin's own batteries are not.
        if (restricted is not null && store is not null
            && BatteryPassWeb.Dpp.Catalog.AttributeCatalog.FindByProperty(file.Document.Property) is { Audience: not BatteryPassWeb.Dpp.Catalog.Audience.Public })
        {
            var pack = file.Document.OwnerKind == BatteryPassWeb.Dpp.Editing.TargetKind.Pack
                ? await store.GetAsync<BatteryPassWeb.Dpp.Store.BatteryPack>(file.Document.OwnerId, cancellationToken)
                : null;
            if (await RestrictedReads.RoleForAsync(readers, User, pack?.ClusterId, cancellationToken) is { } role)
            {
                Response.Headers["X-Restricted-Data-Notice"] = RestrictedReads.Notice(role);
                await restricted.NoteDocumentAsync(
                    BatteryPassWeb.Services.ClusterRepository.NormalizeEmail(BatteryPassWeb.Services.AccessControlService.CurrentEmail(User)),
                    role, documentId, file.Document.FileName, pack?.Id, cancellationToken);
            }
        }

        // The type stored at upload was read from the file's own bytes (2c), never taken from a request.
        var disposition = new ContentDispositionHeaderValue("inline");
        disposition.SetHttpFileName(file.Document.FileName);
        Response.Headers[HeaderNames.ContentDisposition] = disposition.ToString();
        Response.Headers[HeaderNames.XContentTypeOptions] = "nosniff";
        Response.Headers[HeaderNames.CacheControl] = "private, no-store";
        return File(file.Content, file.Document.ContentType);
    }

    [HttpGet("packs/{packId}/accidents")]
    public async Task<IActionResult> Accidents(
        string packId,
        CancellationToken cancellationToken,
        [FromServices] RestrictedReads? restricted = null,
        [FromServices] BatteryPassWeb.Dpp.Store.DppStore? store = null)
    {
        if (await PassportController.GoneAsync(this, packId, json: false, cancellationToken) is { } gone)
        {
            return gone; // finding R08
        }

        var reader = await readers.ForAsync(User, cancellationToken);
        var page = await pages.AccidentsAsync(reader, packId, cancellationToken);
        if (page is null)
        {
            return Refused("accidents");
        }

        // Finding R11: the accident reports are restricted data too.
        if (restricted is not null && store is not null
            && await RestrictedReads.RoleForAsync(readers, User, (await store.GetAsync<BatteryPassWeb.Dpp.Store.BatteryPack>(packId, cancellationToken))?.ClusterId, cancellationToken) is { } role)
        {
            ViewData[RestrictedReads.RoleKey] = role;
            await restricted.NoteBatteryAsync(
                BatteryPassWeb.Services.ClusterRepository.NormalizeEmail(BatteryPassWeb.Services.AccessControlService.CurrentEmail(User)),
                role, RestrictedReads.AccidentsPage, packId, page.SerialNumber, ["accident reports"], cancellationToken);
        }

        Response.Headers[HeaderNames.CacheControl] = "private, no-store";
        return View(page);
    }

    private IActionResult Refused(string subject)
    {
        if (User.Identity?.IsAuthenticated != true)
        {
            return Challenge();
        }

        ViewData["Subject"] = subject;
        var view = View("NotAvailable");
        view.StatusCode = StatusCodes.Status404NotFound;
        return view;
    }
}
