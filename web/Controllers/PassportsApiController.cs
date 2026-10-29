using BatteryPassWeb.Dpp.Access;
using BatteryPassWeb.Dpp.Api;
using BatteryPassWeb.Dpp.Pages;
using BatteryPassWeb.Dpp.Publication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BatteryPassWeb.Controllers;

/// <summary>
/// The list of all passports, for the team only (design 5.2), on the new model (step 3c-1). Passports are created,
/// changed and published on the team's pages; the external API publishes for team tokens in 3c-2. Anyone may check a
/// published version's signature.
/// </summary>
[ApiController]
[Authorize(Policy = "AdminOnly")]
[Route("api/passports")]
public sealed class PassportsApiController(DppReaders readers, BatteryPages batteries, PassportApi api, PassportPages pages) : ControllerBase
{
    /// <summary>Every battery with its current passport, found by part of a passport ID, battery ID, serial number or cluster.</summary>
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] string? q, CancellationToken cancellationToken)
    {
        var list = await batteries.ListAsync(q, cancellationToken);
        return Ok(new
        {
            query = list.Query,
            total = list.Total,
            passports = list.Rows.Select(row => new
            {
                batteryId = row.Identifier,
                serialNumber = row.SerialNumber,
                batteryFamily = row.FamilyName,
                batteryModel = row.ModelName,
                batch = row.BatchLabel,
                clusterId = row.ClusterId,
                passportId = row.Active?.DigitalProductPassportId,
                version = row.Active?.Number,
                dppStatus = row.Active?.DppStatus ?? "draft",
            }).ToArray(),
        });
    }

    /// <summary>A published passport in full: the team sees every attribute.</summary>
    [HttpGet("{id}")]
    public async Task<IActionResult> Detail(string id, CancellationToken cancellationToken)
    {
        var passport = await api.ReadAsync(await readers.ForAsync(User, cancellationToken), id, cancellationToken);
        if (passport is null)
        {
            return NotFound(new { error = DppApiController.NoPassport, id });
        }

        return Ok(new
        {
            batteryId = passport.Location.Identifier,
            passportId = passport.Version.DigitalProductPassportId,
            version = passport.Version.Number,
            dppStatus = passport.Version.DppStatus,
            signatureVerified = passport.SignatureVerified,
            passport = passport.Passport,
        });
    }

    /// <summary>Whether a published version's stored core is still the one signed. Its IDs and status are public data.</summary>
    [AllowAnonymous]
    [HttpGet("{id}/verify")]
    public async Task<IActionResult> Verify(string id, CancellationToken cancellationToken)
    {
        var version = await pages.FindAsync(id, cancellationToken);
        if (version is null)
        {
            return NotFound(new { error = DppApiController.NoPassport, id });
        }

        return Ok(new
        {
            passportId = version.DigitalProductPassportId,
            version = version.Number,
            dppStatus = version.DppStatus,
            signatureVerified = PassportReader.Verify(version),
        });
    }
}
