using BatteryPassWeb.Dpp.Pages;
using BatteryPassWeb.Models.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BatteryPassWeb.Controllers;

[Authorize]
[Route("help")]
public class HelpController : Controller
{
    private readonly BatteryPages _batteryPages;

    public HelpController(BatteryPages batteryPages)
    {
        _batteryPages = batteryPages;
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
        var model = new ExternalApiHelpViewModel
        {
            BasePath = "/api/external/v1",
            PublicBaseUrl = RequestBaseUrl(),
            ExampleBatteryId = (await _batteryPages.ListAsync(null, cancellationToken)).Rows.FirstOrDefault(row => !row.IsDraft)?.Identifier ?? string.Empty,
        };

        return View(model);
    }

    private string RequestBaseUrl() => $"{Request.Scheme}://{Request.Host}";
}
