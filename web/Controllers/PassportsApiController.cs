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
    private readonly PassportValidationService _passportValidationService;
    private readonly PassportPublishPolicyService _passportPublishPolicyService;
    private readonly DataCompletionPolicyService _dataCompletionPolicyService;
    private readonly PassportTrustService _passportTrustService;
    private readonly AuditRevisionService _auditRevisionService;

    public PassportsApiController(
        PassportRepository passportRepository,
        PassportValidationService passportValidationService,
        PassportPublishPolicyService passportPublishPolicyService,
        DataCompletionPolicyService dataCompletionPolicyService,
        PassportTrustService passportTrustService,
        AuditRevisionService auditRevisionService)
    {
        _passportRepository = passportRepository;
        _passportValidationService = passportValidationService;
        _passportPublishPolicyService = passportPublishPolicyService;
        _dataCompletionPolicyService = dataCompletionPolicyService;
        _passportTrustService = passportTrustService;
        _auditRevisionService = auditRevisionService;
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

        var directPublishBlock = RejectDirectPublishRequest(document);
        if (directPublishBlock != null)
        {
            return directPublishBlock;
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
        _passportPublishPolicyService.SanitizeTrustClaimsForDraftSave(document);
        var now = DateTime.UtcNow.ToString("O");
        var registryInfo = EnsureDocument(document, "registryInfo");
        registryInfo["createdAt"] = now;
        registryInfo["updatedAt"] = now;
        registryInfo["status"] = NormalizeDraftRegistryStatus(BsonHelpers.GetString(document, "registryInfo", "status"));

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

        var directPublishBlock = RejectDirectPublishRequest(document);
        if (directPublishBlock != null)
        {
            return directPublishBlock;
        }

        document.Remove("_id");
        document["passportId"] = passportId;
        _passportPublishPolicyService.SanitizeTrustClaimsForDraftSave(document);
        var registryInfo = EnsureDocument(document, "registryInfo");
        if (!registryInfo.Contains("createdAt"))
        {
            registryInfo["createdAt"] = DateTime.UtcNow.ToString("O");
        }
        registryInfo["updatedAt"] = DateTime.UtcNow.ToString("O");
        registryInfo["status"] = NormalizeDraftRegistryStatus(BsonHelpers.GetString(document, "registryInfo", "status"));

        await _passportRepository.ReplaceAsync(passportId, document, cancellationToken);
        return Ok(new { passport = BsonHelpers.ToDotNet(document) });
    }

    [HttpPost("{passportId}/validate")]
    public async Task<IActionResult> Validate(string passportId, CancellationToken cancellationToken)
    {
        if (!User.IsInRole("admin"))
        {
            return Forbid();
        }

        var passport = await _passportRepository.GetByPassportIdAsync(passportId, cancellationToken);
        if (passport == null)
        {
            return NotFound(new { error = "Passport does not exist", passportId });
        }

        var dataRequirements = await _dataCompletionPolicyService.GetPolicyAsync(cancellationToken);
        var summary = _passportValidationService.Validate(passport, dataRequirements);
        await _passportRepository.UpdateTrustValidationAsync(passportId, summary, cancellationToken);
        return Ok(new
        {
            passportId,
            state = summary.State,
            blockingErrors = summary.BlockingErrorCount,
            warnings = summary.WarningCount,
            passed = summary.PassedCount,
            canSign = summary.CanSign,
            sections = summary.Sections
        });
    }

    [HttpPost("{passportId}/sign")]
    public async Task<IActionResult> Sign(string passportId, CancellationToken cancellationToken)
    {
        if (!User.IsInRole("admin"))
        {
            return Forbid();
        }

        var passport = await _passportRepository.GetByPassportIdAsync(passportId, cancellationToken);
        if (passport == null)
        {
            return NotFound(new { error = "Passport does not exist", passportId });
        }

        var dataRequirements = await _dataCompletionPolicyService.GetPolicyAsync(cancellationToken);
        var summary = _passportValidationService.Validate(passport, dataRequirements);
        if (!_passportPublishPolicyService.CanSign(summary))
        {
            await _passportRepository.UpdateTrustValidationAsync(passportId, summary, cancellationToken);
            return BadRequest(new
            {
                error = "Resolve blocking validation errors before signing.",
                passportId,
                blockingErrors = summary.BlockingErrorCount,
                warnings = summary.WarningCount,
                sections = summary.Sections
            });
        }

        var actor = CurrentActor();
        var signature = _passportTrustService.Sign(passport, actor);
        var revision = await _auditRevisionService.CreateSignedRevisionAsync(
            passportId,
            signature.Snapshot,
            signature.Hash,
            signature.Proof,
            actor,
            signature.SignedAt,
            cancellationToken);
        var revisionId = BsonHelpers.GetString(revision, "revisionId");

        await _passportRepository.UpdateTrustSignatureAsync(
            passportId,
            summary,
            signature.Hash,
            signature.Proof,
            revisionId,
            signature.SignedAt,
            cancellationToken);
        await _auditRevisionService.AppendAuditEventAsync(
            passportId,
            "passport.signed",
            actor,
            "admin",
            "api",
            "Passport signed through API.",
            new BsonDocument
            {
                ["revisionId"] = revisionId,
                ["hash"] = $"sha256:{signature.Hash}"
            },
            cancellationToken);

        return Ok(new
        {
            passportId,
            signed = true,
            revisionId,
            hash = signature.Hash,
            signedAt = signature.SignedAt,
            proof = BsonHelpers.ToDotNet(signature.Proof),
            verification = _passportTrustService.Verify(passport, signature.Hash, signature.Proof)
        });
    }

    [HttpPost("{passportId}/publish")]
    public async Task<IActionResult> Publish(string passportId, CancellationToken cancellationToken)
    {
        if (!User.IsInRole("admin"))
        {
            return Forbid();
        }

        var passport = await _passportRepository.GetByPassportIdAsync(passportId, cancellationToken);
        if (passport == null)
        {
            return NotFound(new { error = "Passport does not exist", passportId });
        }

        var dataRequirements = await _dataCompletionPolicyService.GetPolicyAsync(cancellationToken);
        var summary = _passportValidationService.Validate(passport, dataRequirements);
        if (!_passportPublishPolicyService.CanSign(summary))
        {
            await _passportRepository.UpdateTrustValidationAsync(passportId, summary, cancellationToken);
            return BadRequest(new
            {
                error = "Resolve blocking validation errors before publishing.",
                passportId,
                blockingErrors = summary.BlockingErrorCount,
                warnings = summary.WarningCount,
                sections = summary.Sections
            });
        }

        var verification = _passportTrustService.Verify(passport);
        if (!verification.IsValid)
        {
            await _auditRevisionService.AppendAuditEventAsync(
                passportId,
                "passport.publish.blocked",
                CurrentActor(),
                "admin",
                "api",
                "Passport publishing blocked by signature verification.",
                new BsonDocument
                {
                    ["state"] = verification.State,
                    ["message"] = verification.Message,
                    ["currentHash"] = verification.CurrentHash,
                    ["expectedHash"] = verification.ExpectedHash
                },
                cancellationToken);
            return BadRequest(new { error = verification.Message, verification });
        }

        var revisionId = BsonHelpers.GetString(passport, "trust", "latestRevisionId");
        if (string.IsNullOrWhiteSpace(revisionId))
        {
            return BadRequest(new { error = "Publish requires a signed revision.", passportId });
        }

        var publishedAt = DateTimeOffset.UtcNow.ToString("O");
        var publishedProof = BsonHelpers.GetValue(passport, "trust", "latestProof") as BsonDocument ?? new BsonDocument();
        await _passportRepository.PublishPassportAsync(
            passportId,
            revisionId,
            publishedAt,
            verification.CurrentHash,
            publishedProof,
            cancellationToken);
        await _auditRevisionService.MarkRevisionPublishedAsync(revisionId, publishedAt, cancellationToken);
        await _auditRevisionService.AppendAuditEventAsync(
            passportId,
            "passport.published",
            CurrentActor(),
            "admin",
            "api",
            "Passport published through API.",
            new BsonDocument
            {
                ["revisionId"] = revisionId,
                ["hash"] = verification.CurrentHash,
                ["publishedAt"] = publishedAt
            },
            cancellationToken);

        return Ok(new
        {
            passportId,
            published = true,
            revisionId,
            publishedAt,
            verification
        });
    }

    [AllowAnonymous]
    [HttpGet("{passportId}/verify")]
    public async Task<IActionResult> Verify(string passportId, CancellationToken cancellationToken)
    {
        var passport = await _passportRepository.GetByPassportIdAsync(passportId, cancellationToken);
        if (passport == null)
        {
            return NotFound(new { error = "Passport does not exist", passportId });
        }

        var verification = _passportTrustService.Verify(passport);
        return Ok(new
        {
            passportId,
            verification,
            trust = BsonHelpers.ToDotNet(passport.GetValue("trust", new BsonDocument()))
        });
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

    private IActionResult? RejectDirectPublishRequest(BsonDocument document)
    {
        var requestedStatus = BsonHelpers.GetString(document, "registryInfo", "status");
        if (!requestedStatus.Equals("published", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return BadRequest(new
        {
            error = "Direct publish is blocked. Validate, sign, and publish through the trust workflow."
        });
    }

    private static string NormalizeDraftRegistryStatus(string requestedStatus)
    {
        return requestedStatus.Equals("archived", StringComparison.OrdinalIgnoreCase) ? "archived" : "draft";
    }

    private string CurrentActor()
    {
        var actor = AccessControlService.CurrentEmail(User);
        return string.IsNullOrWhiteSpace(actor) ? "api-admin" : actor;
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
