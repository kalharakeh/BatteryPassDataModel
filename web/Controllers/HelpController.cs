using BatteryPassWeb.Models.ViewModels;
using BatteryPassWeb.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BatteryPassWeb.Controllers;

[Authorize]
[Route("help")]
public class HelpController : Controller
{
    private const string PreferredSamplePassportId = "did:web:acme.battery.pass:cba0c455d7bc4a8cb5ffc1ff6c60dd6f";

    private readonly ExternalApiRepository _externalApiRepository;
    private readonly PassportRepository _passportRepository;

    public HelpController(ExternalApiRepository externalApiRepository, PassportRepository passportRepository)
    {
        _externalApiRepository = externalApiRepository;
        _passportRepository = passportRepository;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var readTokenDocument = await _externalApiRepository.GetTokenByIdAsync(ExternalApiInitializer.SampleReadTokenId, cancellationToken);
        var readWriteTokenDocument = await _externalApiRepository.GetTokenByIdAsync(ExternalApiInitializer.SampleReadWriteTokenId, cancellationToken);
        var samplePassportId = await ResolveSamplePassportIdAsync(cancellationToken);

        var model = new ExternalApiHelpViewModel
        {
            BasePath = "/api/external/v1",
            SamplePassportId = samplePassportId,
            SampleReadToken = readTokenDocument != null
                ? _externalApiRepository.RevealToken(readTokenDocument)
                : ExternalApiInitializer.SampleReadTokenValue,
            SampleReadWriteToken = readWriteTokenDocument != null
                ? _externalApiRepository.RevealToken(readWriteTokenDocument)
                : ExternalApiInitializer.SampleReadWriteTokenValue
        };

        return View(model);
    }

    private async Task<string> ResolveSamplePassportIdAsync(CancellationToken cancellationToken)
    {
        var preferredPassport = await _passportRepository.GetSummaryAsync(PreferredSamplePassportId, cancellationToken);
        if (preferredPassport != null && string.IsNullOrWhiteSpace(preferredPassport.ClusterId))
        {
            return preferredPassport.PassportId;
        }

        var passports = await _passportRepository.SearchAsync(string.Empty, includeArchived: false, cancellationToken);
        return passports
            .FirstOrDefault(passport => string.IsNullOrWhiteSpace(passport.ClusterId))
            ?.PassportId
            ?? ExternalApiInitializer.SamplePassportId;
    }
}
