using BatteryPassWeb.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BatteryPassWeb.Controllers;

[Authorize(Policy = "ClusterAdminOrAdmin")]
[Route("cluster-admin")]
public class ClusterAdminController : Controller
{
    private readonly PassportRepository _passportRepository;

    public ClusterAdminController(PassportRepository passportRepository)
    {
        _passportRepository = passportRepository;
    }

    [HttpGet("")]
    public IActionResult Index()
    {
        return View();
    }

    [HttpGet("passports")]
    public async Task<IActionResult> Passports([FromQuery] string? q, CancellationToken cancellationToken)
    {
        var passports = await _passportRepository.SearchAsync(q ?? string.Empty, includeArchived: true, cancellationToken);
        return View("~/Views/Admin/Passports.cshtml", passports);
    }

    [HttpGet("passports/{passportId}/edit")]
    public async Task<IActionResult> EditPassport(string passportId, CancellationToken cancellationToken)
    {
        var passport = await _passportRepository.GetSummaryAsync(passportId, cancellationToken);
        if (passport == null)
        {
            return NotFound();
        }

        return View("~/Views/Admin/EditPassport.cshtml", passport);
    }

    [HttpGet("users")]
    public IActionResult Users()
    {
        return View();
    }
}
