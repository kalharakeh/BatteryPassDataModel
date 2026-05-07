using System.Text;
using BatteryPassWeb.Services;
using Microsoft.AspNetCore.Mvc;

namespace BatteryPassWeb.Controllers;

[Route("qr")]
public sealed class QrController : Controller
{
    private readonly PassportRepository _passportRepository;
    private readonly PassportPublishPolicyService _passportPublishPolicyService;
    private readonly AccessControlService _accessControlService;
    private readonly PassportQrCodeService _passportQrCodeService;

    public QrController(
        PassportRepository passportRepository,
        PassportPublishPolicyService passportPublishPolicyService,
        AccessControlService accessControlService,
        PassportQrCodeService passportQrCodeService)
    {
        _passportRepository = passportRepository;
        _passportPublishPolicyService = passportPublishPolicyService;
        _accessControlService = accessControlService;
        _passportQrCodeService = passportQrCodeService;
    }

    [HttpGet("{passportId}/svg")]
    public async Task<IActionResult> QrCode(string passportId, CancellationToken cancellationToken)
    {
        var decodedPassportId = Uri.UnescapeDataString(passportId);
        if (!await CanAccessQrAsync(decodedPassportId, cancellationToken))
        {
            return NotFound();
        }

        var payload = _passportQrCodeService.BuildPayloadUrl(Request, decodedPassportId);
        var svg = _passportQrCodeService.GenerateSvg(payload);
        return Content(svg, "image/svg+xml", Encoding.UTF8);
    }

    [HttpGet("{passportId}/download")]
    public async Task<IActionResult> Download(string passportId, CancellationToken cancellationToken)
    {
        var decodedPassportId = Uri.UnescapeDataString(passportId);
        if (!await CanAccessQrAsync(decodedPassportId, cancellationToken))
        {
            return NotFound();
        }

        var payload = _passportQrCodeService.BuildPayloadUrl(Request, decodedPassportId);
        var svg = _passportQrCodeService.GenerateSvg(payload);
        var fileName = _passportQrCodeService.BuildFileName(decodedPassportId);
        return File(Encoding.UTF8.GetBytes(svg), "image/svg+xml", fileName);
    }

    private async Task<bool> CanAccessQrAsync(string passportId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(passportId))
        {
            return false;
        }

        var passport = await _passportRepository.GetByPassportIdAsync(passportId, cancellationToken);
        if (passport == null)
        {
            return false;
        }

        if (_passportPublishPolicyService.IsPubliclyVisible(passport))
        {
            return true;
        }

        if (User.Identity?.IsAuthenticated != true)
        {
            return false;
        }

        return await _accessControlService.CanOpenPassportDetailAsync(
            User,
            BsonHelpers.GetString(passport, "clusterId"),
            cancellationToken);
    }
}
