using BatteryPassWeb.Models.ViewModels;
using BatteryPassWeb.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BatteryPassWeb.Controllers;

[Authorize]
[Route("help")]
public class HelpController : Controller
{
    private readonly ExternalApiRepository _externalApiRepository;
    private readonly PassportRepository _passportRepository;
    private readonly BatteryIdService _batteryIdService;

    public HelpController(
        ExternalApiRepository externalApiRepository,
        PassportRepository passportRepository,
        BatteryIdService batteryIdService)
    {
        _externalApiRepository = externalApiRepository;
        _passportRepository = passportRepository;
        _batteryIdService = batteryIdService;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var readTokenDocument = await _externalApiRepository.GetTokenByIdAsync(ExternalApiInitializer.SampleReadTokenId, cancellationToken);
        var readWriteTokenDocument = await _externalApiRepository.GetTokenByIdAsync(ExternalApiInitializer.SampleReadWriteTokenId, cancellationToken);
        var lifecycleTokenDocument = await _externalApiRepository.GetTokenByIdAsync(ExternalApiInitializer.SampleLifecycleTokenId, cancellationToken);
        var sampleIds = await ResolveSampleIdsAsync(cancellationToken);

        var model = new ExternalApiHelpViewModel
        {
            BasePath = "/api/external/v1",
            PublicBaseUrl = RequestBaseUrl(),
            SampleBatteryId = sampleIds.BatteryId,
            SamplePassportId = sampleIds.PassportId,
            SampleReadToken = readTokenDocument != null
                ? _externalApiRepository.RevealToken(readTokenDocument)
                : ExternalApiInitializer.SampleReadTokenValue,
            SampleReadWriteToken = readWriteTokenDocument != null
                ? _externalApiRepository.RevealToken(readWriteTokenDocument)
                : ExternalApiInitializer.SampleReadWriteTokenValue,
            SampleLifecycleToken = lifecycleTokenDocument != null
                ? _externalApiRepository.RevealToken(lifecycleTokenDocument)
                : ExternalApiInitializer.SampleLifecycleTokenValue
        };

        return View(model);
    }

    private string RequestBaseUrl() => $"{Request.Scheme}://{Request.Host}";

    private async Task<(string BatteryId, string PassportId)> ResolveSampleIdsAsync(CancellationToken cancellationToken)
    {
        var passports = await _passportRepository.SearchDocumentsAsync(string.Empty, includeArchived: false, cancellationToken);
        var clusterPassport = passports
            .Where(passport => BsonHelpers.GetString(passport, "clusterId").Equals(ExternalApiInitializer.SampleApiClusterId, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(passport => passport.GetValue("isLatestForBattery", false).ToBoolean())
            .ThenByDescending(passport => BsonHelpers.GetString(passport, "registryInfo", "updatedAt"))
            .FirstOrDefault();
        if (clusterPassport != null)
        {
            return ToSampleIds(clusterPassport);
        }

        var passport = passports.FirstOrDefault(passport => string.IsNullOrWhiteSpace(BsonHelpers.GetString(passport, "clusterId")));
        return passport != null
            ? ToSampleIds(passport)
            : (ExternalApiInitializer.CreateSampleBatteryId(_batteryIdService), ExternalApiInitializer.SamplePassportId);
    }

    private static (string BatteryId, string PassportId) ToSampleIds(MongoDB.Bson.BsonDocument passport)
    {
        var passportId = BsonHelpers.GetString(passport, "passportId");
        var batteryId = BsonHelpers.GetString(passport, "batteryId");
        return (string.IsNullOrWhiteSpace(batteryId) ? passportId : batteryId, passportId);
    }
}
