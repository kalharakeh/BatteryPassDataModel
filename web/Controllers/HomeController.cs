using BatteryPassWeb.Models.ViewModels;
using BatteryPassWeb.Services;
using Microsoft.AspNetCore.Mvc;

namespace BatteryPassWeb.Controllers;

[Route("")]
public class HomeController : Controller
{
    private const string SamplePassportId = "did:web:acme.battery.pass:sample-customer-north-001";
    private readonly PassportRepository _passportRepository;
    private readonly PassportPublishPolicyService _passportPublishPolicyService;

    public HomeController(PassportRepository passportRepository, PassportPublishPolicyService passportPublishPolicyService)
    {
        _passportRepository = passportRepository;
        _passportPublishPolicyService = passportPublishPolicyService;
    }

    [HttpGet("")]
    public IActionResult Index()
    {
        ViewData["SamplePassportId"] = SamplePassportId;
        return View();
    }

    [HttpGet("search")]
    public async Task<IActionResult> Search([FromQuery] string? q, CancellationToken cancellationToken)
    {
        var query = q?.Trim() ?? string.Empty;
        if (query.Length == 0)
        {
            return View(new SearchPageViewModel { Query = string.Empty });
        }

        var documents = await _passportRepository.SearchDocumentsAsync(query, includeArchived: false, cancellationToken);
        var matches = documents
            .Where(_passportPublishPolicyService.IsPubliclyVisible)
            .Select(_passportRepository.ToSummaryViewModel)
            .ToList();
        var exactMatch = matches.FirstOrDefault(match => string.Equals(match.PassportId, query, StringComparison.OrdinalIgnoreCase));
        if (exactMatch != null)
        {
            return Redirect($"/{Uri.EscapeDataString(exactMatch.PassportId)}/summary");
        }

        return View(new SearchPageViewModel
        {
            Query = query,
            NotFound = true
        });
    }
}
