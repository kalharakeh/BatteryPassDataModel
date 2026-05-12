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
        var signTokenDocument = await _externalApiRepository.GetTokenByIdAsync(ExternalApiInitializer.SampleSignTokenId, cancellationToken);
        var sampleIds = await ResolveSampleIdsAsync(cancellationToken);

        var model = new ExternalApiHelpViewModel
        {
            BasePath = "/api/external/v1",
            SampleBatteryId = sampleIds.BatteryId,
            SamplePassportId = sampleIds.PassportId,
            SampleReadToken = readTokenDocument != null
                ? _externalApiRepository.RevealToken(readTokenDocument)
                : ExternalApiInitializer.SampleReadTokenValue,
            SampleReadWriteToken = readWriteTokenDocument != null
                ? _externalApiRepository.RevealToken(readWriteTokenDocument)
                : ExternalApiInitializer.SampleReadWriteTokenValue,
            SampleSignToken = signTokenDocument != null
                ? _externalApiRepository.RevealToken(signTokenDocument)
                : ExternalApiInitializer.SampleSignTokenValue
        };

        return View(model);
    }

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

        var preferredPassport = await _passportRepository.GetByPassportIdAsync(PreferredSamplePassportId, cancellationToken);
        if (preferredPassport != null && string.IsNullOrWhiteSpace(BsonHelpers.GetString(preferredPassport, "clusterId")))
        {
            return ToSampleIds(preferredPassport);
        }

        var passport = passports.FirstOrDefault(passport => string.IsNullOrWhiteSpace(BsonHelpers.GetString(passport, "clusterId")));
        return passport != null
            ? ToSampleIds(passport)
            : (ExternalApiInitializer.SamplePassportId, ExternalApiInitializer.SamplePassportId);
    }

    private static (string BatteryId, string PassportId) ToSampleIds(MongoDB.Bson.BsonDocument passport)
    {
        var passportId = BsonHelpers.GetString(passport, "passportId");
        var batteryId = BsonHelpers.GetString(passport, "batteryId");
        return (string.IsNullOrWhiteSpace(batteryId) ? passportId : batteryId, passportId);
    }
}
