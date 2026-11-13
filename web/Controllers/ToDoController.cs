using BatteryPassWeb.Dpp.Identifiers;
using BatteryPassWeb.Dpp.Pages;
using BatteryPassWeb.Dpp.Publication;
using BatteryPassWeb.Dpp.Store;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BatteryPassWeb.Controllers;

/// <summary>
/// The team's "To do" page (finding U05, the owner's approved picture B): where the team lands after signing in, with
/// what needs doing and the place to do each thing. The battery list is one click away.
/// </summary>
[Authorize(Policy = "AdminOnly")]
[Route("admin/start")]
public sealed class ToDoController(BatteryPages batteries, PublicationService publication, DppStore store, BatteryIdPool pool, TimeProvider clock) : Controller
{
    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken cancellationToken) =>
        View("~/Views/Admin/Start.cshtml", await new ToDoPages(batteries, publication, store, pool, clock.GetUtcNow().UtcDateTime).BuildAsync(cancellationToken));
}
