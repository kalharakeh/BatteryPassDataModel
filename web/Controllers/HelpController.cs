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

    /// <summary>The one Help place (step 3e): each reader sees only the parts for their role.</summary>
    [AllowAnonymous]
    [HttpGet("")]
    public IActionResult Guide() => View("Guide");

    /// <summary>The team's workflow is a part of the Help page now (this address, and /admin/help before it, lead there).</summary>
    [Authorize(Policy = "AdminOnly")]
    [HttpGet("team")]
    public IActionResult Team() => Redirect("/help#workflow");

    /// <summary>The API reference and request workbench.</summary>
    [AllowAnonymous]
    [HttpGet("api")]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var model = new ExternalApiHelpViewModel
        {
            BasePath = "/api/external/v1",
            PublicBaseUrl = RequestBaseUrl(),
            ExampleBatteryId = await _batteryPages.ExampleBatteryIdAsync(cancellationToken),
        };

        return View(model);
    }

    private string RequestBaseUrl() => $"{Request.Scheme}://{Request.Host}";
}
