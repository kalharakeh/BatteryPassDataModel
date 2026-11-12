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

    /// <summary>
    /// A sample file for "Upload a file" (decision 35, Q24): the app's upload format or a battery system export, for the
    /// demo battery DEMO-C7M-0001, to the team and cluster admins signed in only.
    /// </summary>
    [Authorize(Policy = "ClusterAdminOrAdmin")]
    [HttpGet("samples/{name}")]
    public IActionResult Sample(string name, [FromServices] TimeProvider clock)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        var json = name switch
        {
            BatteryPassWeb.Dpp.Uploads.UploadSamples.UploadFileName => BatteryPassWeb.Dpp.Uploads.UploadSamples.UploadFile(now.AddHours(-1)),
            BatteryPassWeb.Dpp.Uploads.UploadSamples.ExportFileName => BatteryPassWeb.Dpp.Uploads.UploadSamples.Export(BatteryPassWeb.Dpp.Uploads.UploadSamples.Serial, now.AddHours(-1), 7)
                .ToJsonString(new System.Text.Json.JsonSerializerOptions { WriteIndented = true }),
            _ => null,
        };
        return json is null ? NotFound() : File(System.Text.Encoding.UTF8.GetBytes(json), "application/json", name);
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
            ExampleBatteryId = await _batteryPages.ExampleBatteryIdAsync(cancellationToken),
        };

        return View(model);
    }

    private string RequestBaseUrl() => $"{Request.Scheme}://{Request.Host}";
}
