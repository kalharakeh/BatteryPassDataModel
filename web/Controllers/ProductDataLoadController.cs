using BatteryPassWeb.Configuration;
using BatteryPassWeb.Dpp.Identifiers;
using BatteryPassWeb.Dpp.Loading;
using BatteryPassWeb.Dpp.Pages;
using BatteryPassWeb.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace BatteryPassWeb.Controllers;

/// <summary>What the "Load product data from Excel" page shows: the last check, when there is one, and what went wrong.</summary>
/// <param name="Error">Why nothing was loaded, in a sentence; null when nothing was tried or the check says it all.</param>
/// <param name="MaxUploadBytes">The largest single file the app takes, as on every upload.</param>
public sealed record ProductDataLoadPage(LoadCheck? Check, string? Error, long MaxUploadBytes)
{
    public int MaxFiles => ProductDataLoader.MaxFiles;
}

/// <summary>
/// "Load product data from Excel" (decision 83): the team's admins send the filled workbook with the files it names,
/// see what a load would do and every problem in the way, and then load all of it or none of it. Reached from the
/// product data pages. A demo account never gets in, whatever role it was given: the page makes real product data.
/// </summary>
[Authorize(Policy = "AdminOnly")]
[Route("admin/product-data-load")]
public sealed class ProductDataLoadController(ProductDataLoader loader, AccessControlService access, IOptions<BatteryPassOptions> options) : Controller
{
    /// <summary>
    /// The most one load may send: the workbook and up to twenty files of the largest size an upload may have by default.
    /// Each file still passes the upload's own size check. The request limit in Program.cs must allow this for the page's
    /// two posts (see <see cref="AppliesTo"/>); until it does, a load is held to the limit of a single upload.
    /// </summary>
    public const long MaxRequestBytes = (20 * BatteryPassOptions.DefaultMaxUploadBytes) + XlsxReader.MaxBytes + 65_536;

    public const string Path = "/admin/product-data-load";

    /// <summary>The header the page's script sends with a check, to get the check alone and show it in place.</summary>
    public const string InPlaceHeader = "X-Requested-With";

    /// <summary>True for the page's two posts, which carry several files at once.</summary>
    public static bool AppliesTo(HttpRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        return HttpMethods.IsPost(request.Method) && request.Path.StartsWithSegments(Path, StringComparison.OrdinalIgnoreCase);
    }

    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken cancellationToken) =>
        await access.IsDemoAccountAsync(User, cancellationToken) ? Forbid() : View(Page(null, null));

    /// <summary>The empty workbook, the file as it was sent to the owner.</summary>
    [HttpGet("workbook")]
    public async Task<IActionResult> Workbook(CancellationToken cancellationToken) =>
        await access.IsDemoAccountAsync(User, cancellationToken) ? Forbid() : File(LoadTemplate.Bytes(), XlsxWriter.ContentType, LoadTemplate.FileName);

    /// <summary>The check: what a load would do, and every problem. Saves nothing.</summary>
    [HttpPost("check")]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(MaxRequestBytes)]
    [RequestFormLimits(MultipartBodyLengthLimit = MaxRequestBytes)]
    public async Task<IActionResult> Check(IFormFile? workbook, List<IFormFile>? files, CancellationToken cancellationToken)
    {
        if (await access.IsDemoAccountAsync(User, cancellationToken))
        {
            return Forbid();
        }

        var page = workbook is null || workbook.Length == 0
            ? Page(null, ChooseFirst)
            : Page(await loader.CheckAsync(workbook.FileName, await ReadAsync(workbook, cancellationToken), await ReadAllAsync(files, cancellationToken), cancellationToken), null);
        return Request.Headers.ContainsKey(InPlaceHeader) ? PartialView("_Check", page) : View("Index", page);
    }

    /// <summary>The load: all of what the check showed, or nothing.</summary>
    [HttpPost("")]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(MaxRequestBytes)]
    [RequestFormLimits(MultipartBodyLengthLimit = MaxRequestBytes)]
    public async Task<IActionResult> Load(IFormFile? workbook, List<IFormFile>? files, [FromForm] string? stamp, CancellationToken cancellationToken)
    {
        if (await access.IsDemoAccountAsync(User, cancellationToken))
        {
            return Forbid();
        }

        if (workbook is null || workbook.Length == 0)
        {
            return View("Index", Page(null, ChooseFirst));
        }

        var result = await loader.LoadAsync(Who, workbook.FileName, await ReadAsync(workbook, cancellationToken), await ReadAllAsync(files, cancellationToken), stamp, cancellationToken);
        if (!result.Loaded)
        {
            return View("Index", Page(result.Check, result.Message));
        }

        TempData[ProductDataController.StatusKey] = result.Message;
        return Redirect(result.FamilyId is { Length: > 0 } familyId ? ProductDataPages.FamilyHref(familyId) : "/admin/families");
    }

    public const string ChooseFirst = "Choose the filled workbook first.";

    private string Who => AccessControlService.CurrentEmail(User) is { Length: > 0 } email ? email : "admin";

    private ProductDataLoadPage Page(LoadCheck? check, string? error) => new(check, error, options.Value.MaxUploadBytes);

    private static async Task<IReadOnlyList<SentFile>> ReadAllAsync(List<IFormFile>? files, CancellationToken cancellationToken)
    {
        var sent = new List<SentFile>();
        foreach (var file in files ?? [])
        {
            if (file.Length > 0)
            {
                sent.Add(new SentFile(file.FileName, await ReadAsync(file, cancellationToken)));
            }
        }

        return sent;
    }

    private static async Task<byte[]> ReadAsync(IFormFile file, CancellationToken cancellationToken)
    {
        using var buffer = new MemoryStream();
        await file.CopyToAsync(buffer, cancellationToken);
        return buffer.ToArray();
    }
}
