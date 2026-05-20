using System.Text;
using BatteryPassWeb.Services;
using Microsoft.AspNetCore.Mvc;

namespace BatteryPassWeb.Controllers;

[Route("qr")]
public sealed class QrController : Controller
{
    private readonly BatteryRepository _batteryRepository;
    private readonly PassportRepository _passportRepository;
    private readonly PassportPublishPolicyService _passportPublishPolicyService;
    private readonly AccessControlService _accessControlService;
    private readonly PassportQrCodeService _passportQrCodeService;

    public QrController(
        BatteryRepository batteryRepository,
        PassportRepository passportRepository,
        PassportPublishPolicyService passportPublishPolicyService,
        AccessControlService accessControlService,
        PassportQrCodeService passportQrCodeService)
    {
        _batteryRepository = batteryRepository;
        _passportRepository = passportRepository;
        _passportPublishPolicyService = passportPublishPolicyService;
        _accessControlService = accessControlService;
        _passportQrCodeService = passportQrCodeService;
    }

    [HttpGet("{batteryId}/svg")]
    public async Task<IActionResult> QrCode(string batteryId, CancellationToken cancellationToken)
    {
        var decodedBatteryId = Uri.UnescapeDataString(batteryId);
        if (!await CanAccessQrAsync(decodedBatteryId, cancellationToken))
        {
            return NotFound();
        }

        var payload = _passportQrCodeService.BuildPayloadUrl(Request, decodedBatteryId);
        var svg = _passportQrCodeService.GenerateSvg(payload);
        return Content(svg, "image/svg+xml", Encoding.UTF8);
    }

    [HttpGet("{batteryId}/download")]
    public async Task<IActionResult> Download(string batteryId, CancellationToken cancellationToken)
    {
        var decodedBatteryId = Uri.UnescapeDataString(batteryId);
        if (!await CanAccessQrAsync(decodedBatteryId, cancellationToken))
        {
            return NotFound();
        }

        var payload = _passportQrCodeService.BuildPayloadUrl(Request, decodedBatteryId);
        var fileName = _passportQrCodeService.BuildFileName(decodedBatteryId);
        var png = _passportQrCodeService.GeneratePng(payload);
        return File(png, "image/png", Path.ChangeExtension(fileName, ".png"));
    }

    private async Task<bool> CanAccessQrAsync(string batteryId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(batteryId))
        {
            return false;
        }

        var battery = await _batteryRepository.GetByBatteryIdAsync(batteryId, cancellationToken);
        if (battery == null)
        {
            return false;
        }

        var latestPublicPassport = await _passportRepository.GetLatestPublicByBatteryIdAsync(
            batteryId,
            _passportPublishPolicyService,
            cancellationToken);
        if (latestPublicPassport != null)
        {
            return true;
        }

        if (User.Identity?.IsAuthenticated != true)
        {
            return false;
        }

        return await _accessControlService.CanOpenPassportDetailAsync(
            User,
            BsonHelpers.GetString(battery, "clusterId"),
            cancellationToken);
    }
}
