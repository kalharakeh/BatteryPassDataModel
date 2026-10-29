using BatteryPassWeb.Dpp.Access;
using BatteryPassWeb.Dpp.Pages;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BatteryPassWeb.Controllers;

/// <summary>
/// The Registry (decision 30): a read-only list of the published batteries a signed-in reader may open, searched like the
/// team's battery list, each opening its passport's detail page.
/// </summary>
[Authorize]
[Route("registry")]
public sealed class RegistryController(DppReaders readers, BatteryPages batteries) : Controller
{
    [HttpGet("")]
    public async Task<IActionResult> Index([FromQuery] string? q, CancellationToken cancellationToken)
    {
        var reader = await readers.ForAsync(User, cancellationToken);
        return View(RegistryList.Build(reader, await batteries.ListAsync(q, cancellationToken)));
    }
}
