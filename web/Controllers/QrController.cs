using System.Text;
using BatteryPassWeb.Dpp.Documents;
using BatteryPassWeb.Dpp.Pages;
using BatteryPassWeb.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BatteryPassWeb.Controllers;

/// <summary>
/// The QR code of a battery with a published passport (design 10). It carries the public summary's address with the
/// battery ID (#6), built from APP_BASE_URL, so a printed code doesn't depend on where the page was opened. The
/// address takes the same IDs as the passport pages.
/// </summary>
[AllowAnonymous]
[Route("qr")]
public sealed class QrController(PassportPages pages, DppLinks links, PassportQrCodeService qrCodes) : Controller
{
    [HttpGet("{id}/svg")]
    public async Task<IActionResult> QrCode(string id, CancellationToken cancellationToken)
    {
        var battery = await BatteryAsync(id, cancellationToken);
        return battery is null
            ? NotFound()
            : Content(qrCodes.GenerateSvg(links.PassportUrl(battery.Identifier)), "image/svg+xml", Encoding.UTF8);
    }

    [HttpGet("{id}/download")]
    public async Task<IActionResult> Download(string id, CancellationToken cancellationToken)
    {
        var battery = await BatteryAsync(id, cancellationToken);
        return battery is null
            ? NotFound()
            : File(qrCodes.GeneratePng(links.PassportUrl(battery.Identifier)), "image/png", Path.ChangeExtension(qrCodes.BuildFileName(battery.SerialNumber), ".png"));
    }

    /// <summary>The battery of a published passport; null for a draft, an unknown ID, or while APP_BASE_URL isn't set.</summary>
    private async Task<PassportLocation?> BatteryAsync(string id, CancellationToken cancellationToken) =>
        links.IsConfigured ? await pages.LocateAsync(Uri.UnescapeDataString(id), cancellationToken) : null;
}
