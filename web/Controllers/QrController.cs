using System.Text;
using BatteryPassWeb.Dpp.Documents;
using BatteryPassWeb.Dpp.Pages;
using BatteryPassWeb.Dpp.Store;
using BatteryPassWeb.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BatteryPassWeb.Controllers;

/// <summary>
/// The QR code of a battery with a published passport (design 10). It carries the public summary's address with the
/// battery ID (#6), built from APP_BASE_URL, so a printed code doesn't depend on where the page was opened. The
/// address takes the same IDs as the passport pages. The team also gets a draft's code, to print labels before
/// publishing (decision 30); it opens the summary once the passport is published.
/// </summary>
[AllowAnonymous]
[Route("qr")]
public sealed class QrController(PassportPages pages, DppLinks links, PassportQrCodeService qrCodes, DppStore store) : Controller
{
    [HttpGet("{id}/svg")]
    public async Task<IActionResult> QrCode(string id, CancellationToken cancellationToken)
    {
        var battery = await BatteryAsync(id, cancellationToken);
        return battery is null
            ? NotFound()
            : Content(qrCodes.GenerateSvg(links.PassportUrl(battery.Value.Identifier)), "image/svg+xml", Encoding.UTF8);
    }

    [HttpGet("{id}/download")]
    public async Task<IActionResult> Download(string id, CancellationToken cancellationToken)
    {
        var battery = await BatteryAsync(id, cancellationToken);
        return battery is null
            ? NotFound()
            : File(qrCodes.GeneratePng(links.PassportUrl(battery.Value.Identifier)), "image/png", Path.ChangeExtension(qrCodes.BuildFileName(battery.Value.SerialNumber), ".png"));
    }

    /// <summary>
    /// The battery of a published passport, or, for the team, a draft's by its battery ID; null for anything else, and
    /// while APP_BASE_URL isn't set.
    /// </summary>
    private async Task<(string Identifier, string SerialNumber)?> BatteryAsync(string id, CancellationToken cancellationToken)
    {
        if (!links.IsConfigured)
        {
            return null;
        }

        var wanted = Uri.UnescapeDataString(id);
        if (await pages.LocateAsync(wanted, cancellationToken) is { } published)
        {
            return (published.Identifier, published.SerialNumber);
        }

        return User.IsInRole(AccessControlService.RoleAdmin) && await store.FindPackByUniqueProductIdentifierAsync(wanted, cancellationToken) is { } draft
            ? (wanted, Text(draft, "batterySerialNumber") ?? draft.Id)
            : null;
    }

    private static string? Text(BatteryPack pack, string property) =>
        pack.Values.GetValueOrDefault(property)?.Value is System.Text.Json.Nodes.JsonValue value && value.TryGetValue<string>(out var text) ? text : null;
}
