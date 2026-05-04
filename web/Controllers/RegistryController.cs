using BatteryPassWeb.Models.ViewModels;
using BatteryPassWeb.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Bson;

namespace BatteryPassWeb.Controllers;

[Authorize]
[Route("registry")]
public class RegistryController : Controller
{
    private readonly PassportRepository _passportRepository;

    public RegistryController(PassportRepository passportRepository)
    {
        _passportRepository = passportRepository;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index([FromQuery] string? q, CancellationToken cancellationToken)
    {
        var passports = await _passportRepository.SearchAsync(q ?? string.Empty, includeArchived: true, cancellationToken);
        return View(passports);
    }
}
