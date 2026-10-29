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
    public async Task<IActionResult> Document(string documentId, CancellationToken cancellationToken)
    {
        var reader = await readers.ForAsync(User, cancellationToken);
        var file = await documents.ReadAsync(reader, documentId, cancellationToken);
        if (file is null)
        {
            return Refused("document");
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
    public async Task<IActionResult> Accidents(string packId, CancellationToken cancellationToken)
    {
        var reader = await readers.ForAsync(User, cancellationToken);
        var page = await pages.AccidentsAsync(reader, packId, cancellationToken);
        if (page is null)
        {
            return Refused("accidents");
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
