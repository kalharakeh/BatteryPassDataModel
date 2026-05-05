using BatteryPassWeb.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using System.Text.Json;

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
    public async Task<IActionResult> Create([FromBody] JsonElement payload, CancellationToken cancellationToken)
    {
        if (!User.IsInRole("admin"))
        {
            return Forbid();
        }

        if (!TryReadPassportDocument(payload, out var document))
        {
            return BadRequest(new { error = "Request body must contain a passport object." });
        }

        var passportId = BsonHelpers.GetString(document, "passportId");
        if (string.IsNullOrWhiteSpace(passportId))
        {
            return BadRequest(new { error = "passportId is required." });
        }

        var existing = await _passportRepository.GetByPassportIdAsync(passportId, cancellationToken);
        if (existing != null)
        {
            return Conflict(new { error = "Passport already exists.", passportId });
        }

        document.Remove("_id");
        var now = DateTime.UtcNow.ToString("O");
        EnsureDocument(document, "registryInfo")["createdAt"] = now;
        EnsureDocument(document, "registryInfo")["updatedAt"] = now;
        EnsureDocument(document, "registryInfo")["status"] = FirstNonEmpty(BsonHelpers.GetString(document, "registryInfo", "status"), "draft");

        await _passportRepository.ReplaceAsync(passportId, document, cancellationToken);
        return Created($"/api/passports/{Uri.EscapeDataString(passportId)}", new { passport = BsonHelpers.ToDotNet(document) });
    }

    [HttpPut("{passportId}")]
    public async Task<IActionResult> Update(string passportId, [FromBody] JsonElement payload, CancellationToken cancellationToken)
    {
        if (!User.IsInRole("admin"))
        {
            return Forbid();
        }

        var existing = await _passportRepository.GetByPassportIdAsync(passportId, cancellationToken);
        if (existing == null)
        {
            return NotFound(new { error = "Passport does not exist", passportId });
        }

        if (!TryReadPassportDocument(payload, out var document))
        {
            return BadRequest(new { error = "Request body must contain a passport object." });
        }

        document.Remove("_id");
        document["passportId"] = passportId;
        var registryInfo = EnsureDocument(document, "registryInfo");
        if (!registryInfo.Contains("createdAt"))
        {
            registryInfo["createdAt"] = DateTime.UtcNow.ToString("O");
        }
        registryInfo["updatedAt"] = DateTime.UtcNow.ToString("O");

        await _passportRepository.ReplaceAsync(passportId, document, cancellationToken);
        return Ok(new { passport = BsonHelpers.ToDotNet(document) });
    }

    [HttpDelete("{passportId}")]
    public async Task<IActionResult> Archive(string passportId, CancellationToken cancellationToken)
    {
        if (!User.IsInRole("admin"))
        {
            return Forbid();
        }

        await _passportRepository.ArchivePassportAsync(passportId, cancellationToken);
        return Ok(new { archived = true, passportId });
    }

    private static bool TryReadPassportDocument(JsonElement payload, out BsonDocument document)
    {
        document = new BsonDocument();
        try
        {
            if (payload.ValueKind != JsonValueKind.Object)
            {
                return false;
            }

            if (payload.TryGetProperty("passport", out var passportElement))
            {
                document = BsonSerializer.Deserialize<BsonDocument>(passportElement.GetRawText());
                return true;
            }

            document = BsonSerializer.Deserialize<BsonDocument>(payload.GetRawText());
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static BsonDocument EnsureDocument(BsonDocument parent, string key)
    {
        if (!parent.TryGetValue(key, out var value) || value is not BsonDocument document)
        {
            document = new BsonDocument();
            parent[key] = document;
        }

        return document;
    }

    private static string FirstNonEmpty(params string[] values)
    {
        foreach (var value in values)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }

        return string.Empty;
    }
}
