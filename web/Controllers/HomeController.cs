using BatteryPassWeb.Services;
using Microsoft.AspNetCore.Mvc;

namespace BatteryPassWeb.Controllers;

[Route("")]
public class HomeController : Controller
{
    private readonly PassportRepository _passportRepository;

    public HomeController(PassportRepository passportRepository)
    {
        _passportRepository = passportRepository;
    }

    [HttpGet("")]
    public IActionResult Index()
    {
        return View();
    }

    [HttpGet("search")]
    public async Task<IActionResult> Search([FromQuery] string? q, CancellationToken cancellationToken)
    {
        var query = q?.Trim() ?? string.Empty;
        if (query.Length == 0)
        {
            return View(model: Array.Empty<Models.ViewModels.PassportSummaryViewModel>());
        }

        var matches = await _passportRepository.SearchAsync(query, includeArchived: true, cancellationToken);
        if (matches.Count == 1 && string.Equals(matches[0].PassportId, query, StringComparison.OrdinalIgnoreCase))
        {
            return Redirect($"/{Uri.EscapeDataString(matches[0].PassportId)}/summary");
        }

        return View(model: matches);
    }
}
