using BatteryPassWeb.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BatteryPassWeb.Controllers;

[Authorize]
[ApiController]
[Route("api/passports")]
public class PassportsApiController : ControllerBase
{
    private readonly PassportRepository _passportRepository;

    public PassportsApiController(PassportRepository passportRepository)
    {
        _passportRepository = passportRepository;
    }

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] string? q, CancellationToken cancellationToken)
    {
        var passports = await _passportRepository.SearchAsync(q ?? string.Empty, includeArchived: true, cancellationToken);
        return Ok(new { passports });
    }

    [HttpGet("{passportId}")]
    public async Task<IActionResult> Detail(string passportId, CancellationToken cancellationToken)
    {
        var passport = await _passportRepository.GetByPassportIdAsync(passportId, cancellationToken);
        if (passport == null)
        {
            return NotFound(new { error = "Passport does not exist" });
        }

        return Ok(new { passport = BsonHelpers.ToDotNet(passport) });
    }

    [HttpPost]
    public IActionResult Create()
    {
        return StatusCode(StatusCodes.Status501NotImplemented, new { error = "Create passport rewrite in progress" });
    }

    [HttpPut("{passportId}")]
    public IActionResult Update(string passportId)
    {
        return StatusCode(StatusCodes.Status501NotImplemented, new { error = "Update passport rewrite in progress", passportId });
    }

    [HttpDelete("{passportId}")]
    public IActionResult Archive(string passportId)
    {
        return StatusCode(StatusCodes.Status501NotImplemented, new { error = "Archive passport rewrite in progress", passportId });
    }
}
