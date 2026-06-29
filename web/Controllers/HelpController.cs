using BatteryPassWeb.Configuration;
using BatteryPassWeb.Models.ViewModels;
using BatteryPassWeb.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace BatteryPassWeb.Controllers;

[Authorize]
[Route("help")]
public class HelpController : Controller
{
    private readonly ExternalApiRepository _externalApiRepository;
    private readonly PassportRepository _passportRepository;
    private readonly BatteryIdService _batteryIdService;
    private readonly AccessControlService _accessControlService;
    private readonly BatteryPassOptions _options;

    public HelpController(
        ExternalApiRepository externalApiRepository,
        PassportRepository passportRepository,
        BatteryIdService batteryIdService,
        AccessControlService accessControlService,
        IOptions<BatteryPassOptions> options)
    {
        _externalApiRepository = externalApiRepository;
        _passportRepository = passportRepository;
        _batteryIdService = batteryIdService;
        _accessControlService = accessControlService;
        _options = options.Value;
    }

    [AllowAnonymous]
    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var readTokenDocument = await _externalApiRepository.GetTokenByIdAsync(ExternalApiInitializer.SampleReadTokenId, cancellationToken);
        var readWriteTokenDocument = await _externalApiRepository.GetTokenByIdAsync(ExternalApiInitializer.SampleReadWriteTokenId, cancellationToken);
        var lifecycleTokenDocument = await _externalApiRepository.GetTokenByIdAsync(ExternalApiInitializer.SampleLifecycleTokenId, cancellationToken);
        var sampleIds = await ResolveSampleIdsAsync(cancellationToken);
        var canUsePrivilegedDemoTokens = await CanUsePrivilegedDemoTokensAsync(cancellationToken);

        var model = new ExternalApiHelpViewModel
        {
            BasePath = "/api/external/v1",
            PublicBaseUrl = RequestBaseUrl(),
            SampleBatteryId = sampleIds.BatteryId,
            SamplePassportId = sampleIds.PassportId,
            SampleReadToken = _options.EnableDemoData && _options.EnablePublicDemoReadToken && readTokenDocument != null
                ? ExternalApiInitializer.SampleReadTokenValue
                : string.Empty,
            SampleReadWriteToken = canUsePrivilegedDemoTokens && readWriteTokenDocument != null
                ? ExternalApiInitializer.SampleReadWriteTokenValue
                : string.Empty,
            SampleLifecycleToken = canUsePrivilegedDemoTokens && lifecycleTokenDocument != null
                ? ExternalApiInitializer.SampleLifecycleTokenValue
                : string.Empty,
            DemoWriteSignTestingEnabled = _options.EnableDemoData && _options.EnableDemoWriteSignTesting,
            CanUsePrivilegedDemoTokens = canUsePrivilegedDemoTokens,
            PrivilegedDemoTokenMessage = PrivilegedDemoTokenMessage()
        };

        return View(model);
    }

    private string RequestBaseUrl() => $"{Request.Scheme}://{Request.Host}";

    private async Task<bool> CanUsePrivilegedDemoTokensAsync(CancellationToken cancellationToken)
    {
        if (!_options.EnableDemoData || !_options.EnableDemoWriteSignTesting)
        {
            return false;
        }

        if (User.IsInRole(AccessControlService.RoleAdmin))
        {
            return true;
        }

        return User.IsInRole(AccessControlService.RoleClusterAdmin)
            && await _accessControlService.CanAdministerClusterAsync(User, ExternalApiInitializer.SampleApiClusterId, cancellationToken);
    }

    private string PrivilegedDemoTokenMessage()
    {
        if (!_options.EnableDemoData)
        {
            return "Demo data is disabled for this environment.";
        }

        if (!_options.EnableDemoWriteSignTesting)
        {
            return "Demo write/sign testing is disabled for this environment.";
        }

        return "Use an approved tester account: global admin or demo-cluster cluster admin.";
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
