using BatteryPassWeb.Configuration;
using BatteryPassWeb.Dpp.Pages;
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
    private readonly AccessControlService _accessControlService;
    private readonly BatteryPassOptions _options;
    private readonly BatteryPages _batteryPages;

    public HelpController(
        ExternalApiRepository externalApiRepository,
        AccessControlService accessControlService,
        IOptions<BatteryPassOptions> options,
        BatteryPages batteryPages)
    {
        _batteryPages = batteryPages;
        _externalApiRepository = externalApiRepository;
        _accessControlService = accessControlService;
        _options = options.Value;
    }

    /// <summary>The guide for everyone: finding a passport and what each role sees.</summary>
    [AllowAnonymous]
    [HttpGet("")]
    public IActionResult Guide() => View("Guide");

    /// <summary>The team's workflow help (it used to be /admin/help, which now forwards here).</summary>
    [Authorize(Policy = "AdminOnly")]
    [HttpGet("team")]
    public IActionResult Team([FromQuery] string? status, [FromQuery] string? error)
    {
        ViewData["StatusMessage"] = string.IsNullOrWhiteSpace(status) ? string.Empty : Uri.UnescapeDataString(status);
        ViewData["ErrorMessage"] = string.IsNullOrWhiteSpace(error) ? string.Empty : Uri.UnescapeDataString(error);
        return View("~/Views/Admin/Help.cshtml");
    }

    /// <summary>The API reference and request workbench.</summary>
    [AllowAnonymous]
    [HttpGet("api")]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var readTokenDocument = await _externalApiRepository.GetTokenByIdAsync(ExternalApiInitializer.SampleReadTokenId, cancellationToken);
        var readWriteTokenDocument = await _externalApiRepository.GetTokenByIdAsync(ExternalApiInitializer.SampleReadWriteTokenId, cancellationToken);
        var lifecycleTokenDocument = await _externalApiRepository.GetTokenByIdAsync(ExternalApiInitializer.SampleLifecycleTokenId, cancellationToken);
        var canUsePrivilegedDemoTokens = await CanUsePrivilegedDemoTokensAsync(cancellationToken);

        var model = new ExternalApiHelpViewModel
        {
            BasePath = "/api/external/v1",
            PublicBaseUrl = RequestBaseUrl(),
            ExampleBatteryId = (await _batteryPages.ListAsync(null, cancellationToken)).Rows.FirstOrDefault(row => !row.IsDraft)?.Identifier ?? string.Empty,
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
}
