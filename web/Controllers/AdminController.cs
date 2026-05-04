using BatteryPassWeb.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BatteryPassWeb.Controllers;

[Authorize(Policy = "AdminOnly")]
[Route("admin")]
public class AdminController : Controller
{
    private readonly PassportRepository _passportRepository;
    private readonly ClusterRepository _clusterRepository;

    public AdminController(PassportRepository passportRepository, ClusterRepository clusterRepository)
    {
        _passportRepository = passportRepository;
        _clusterRepository = clusterRepository;
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
        return View(passports);
    }

    [HttpGet("passports/new")]
    public IActionResult NewPassport()
    {
        return View();
    }

    [HttpGet("passports/{passportId}/edit")]
    public async Task<IActionResult> EditPassport(string passportId, CancellationToken cancellationToken)
    {
        var passport = await _passportRepository.GetSummaryAsync(passportId, cancellationToken);
        if (passport == null)
        {
            return NotFound();
        }

        return View(passport);
    }

    [HttpGet("clusters")]
    public async Task<IActionResult> Clusters(CancellationToken cancellationToken)
    {
        var clusters = await _clusterRepository.ListClustersAsync(cancellationToken);
        return View(clusters);
    }
}
