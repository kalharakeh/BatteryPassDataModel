using BatteryPassWeb.Models.ViewModels;
using BatteryPassWeb.Services;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Bson;

namespace BatteryPassWeb.Controllers;

public class PassportController : Controller
{
    private readonly PassportRepository _passportRepository;

    public PassportController(PassportRepository passportRepository)
    {
        _passportRepository = passportRepository;
    }

    [HttpGet("{passportId}/summary")]
    public async Task<IActionResult> Summary(string passportId, CancellationToken cancellationToken)
    {
        if (IsReservedSegment(passportId))
        {
            return NotFound();
        }

        var summary = await _passportRepository.GetSummaryAsync(passportId, cancellationToken);
        if (summary == null)
        {
            return NotFound();
        }

        return View(summary);
    }

    [HttpGet("{passportId}")]
    public async Task<IActionResult> Detail(string passportId, CancellationToken cancellationToken)
    {
        if (IsReservedSegment(passportId))
        {
            return NotFound();
        }

        var document = await _passportRepository.GetByPassportIdAsync(passportId, cancellationToken);
        if (document == null)
        {
            return NotFound();
        }

        var summary = await _passportRepository.GetSummaryAsync(passportId, cancellationToken);
        if (summary == null)
        {
            return NotFound();
        }

        var model = new PassportDetailViewModel
        {
            Summary = summary,
            CanonicalJson = document.ToJson(new MongoDB.Bson.IO.JsonWriterSettings { Indent = true })
        };

        return View(model);
    }

    private static bool IsReservedSegment(string value)
    {
        return value.Equals("login", StringComparison.OrdinalIgnoreCase)
            || value.Equals("registry", StringComparison.OrdinalIgnoreCase)
            || value.Equals("search", StringComparison.OrdinalIgnoreCase)
            || value.Equals("admin", StringComparison.OrdinalIgnoreCase)
            || value.Equals("cluster-admin", StringComparison.OrdinalIgnoreCase)
            || value.Equals("api", StringComparison.OrdinalIgnoreCase);
    }
}
