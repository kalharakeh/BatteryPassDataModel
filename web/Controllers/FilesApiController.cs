using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using MongoDB.Bson;
using MongoDB.Driver;
using MongoDB.Driver.GridFS;
using BatteryPassWeb.Services;
using System.Security.Cryptography;

namespace BatteryPassWeb.Controllers;

[ApiController]
[Route("api/files")]
public class FilesApiController : ControllerBase
{
    private readonly MongoContext _mongoContext;
    private readonly PassportRepository _passportRepository;
    private readonly AccessControlService _accessControlService;
    private readonly PassportPublishPolicyService _passportPublishPolicyService;
    private readonly AuditRevisionService _auditRevisionService;

    public FilesApiController(
        MongoContext mongoContext,
        PassportRepository passportRepository,
        AccessControlService accessControlService,
        PassportPublishPolicyService passportPublishPolicyService,
        AuditRevisionService auditRevisionService)
    {
        _mongoContext = mongoContext;
        _passportRepository = passportRepository;
        _accessControlService = accessControlService;
        _passportPublishPolicyService = passportPublishPolicyService;
        _auditRevisionService = auditRevisionService;
    }

    [HttpPost("")]
    [Authorize(Policy = "ClusterAdminOrAdmin")]
    public async Task<IActionResult> Upload([FromForm] IFormFile? file, [FromForm] string? passportId, [FromForm] string? documentKey, [FromForm] string? publicAccess, CancellationToken cancellationToken)
    {
        if (_mongoContext.Database == null)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new { error = "Database is not connected." });
        }

        if (file == null || file.Length == 0)
        {
            return BadRequest(new { error = "File is required." });
        }

        var normalizedPassportId = passportId?.Trim() ?? string.Empty;
        var normalizedDocumentKey = documentKey?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(normalizedPassportId) || string.IsNullOrWhiteSpace(normalizedDocumentKey))
        {
            return BadRequest(new { error = "passportId and documentKey are required for secure passport file uploads." });
        }

        var passport = await _passportRepository.GetByPassportIdAsync(normalizedPassportId, cancellationToken);
        if (passport == null)
        {
            return NotFound(new { error = "Passport not found.", passportId = normalizedPassportId });
        }

        if (!await _accessControlService.CanAdministerClusterAsync(User, BsonHelpers.GetString(passport, "clusterId"), cancellationToken))
        {
            await AppendFileDeniedAuditEventAsync(
                normalizedPassportId,
                string.Empty,
                normalizedDocumentKey,
                NormalizeVisibility(publicAccess ?? string.Empty),
                "uploadForbidden",
                cancellationToken);
            return StatusCode(StatusCodes.Status403Forbidden, new { error = "You do not have access to upload files for this passport." });
        }

        var previousReference = ResolveDocumentReference(passport, normalizedDocumentKey, string.Empty);
        var previousFileId = BsonHelpers.GetString(previousReference, "fileId");

        var contentType = string.IsNullOrWhiteSpace(file.ContentType) ? "application/octet-stream" : file.ContentType;
        if (contentType != "application/pdf" && !contentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(new { error = "Only PDF and image uploads are supported." });
        }

        await using var memory = new MemoryStream();
        await file.CopyToAsync(memory, cancellationToken);
        var fileBytes = memory.ToArray();
        var sha256 = ComputeSha256Hex(fileBytes);
        var visibility = NormalizeVisibility(publicAccess ?? string.Empty);
        var metadata = new BsonDocument
        {
            ["uploadedAt"] = DateTime.UtcNow.ToString("O"),
            ["source"] = "batterypass-mvc-upload",
            ["contentType"] = contentType,
            ["passportId"] = normalizedPassportId,
            ["documentKey"] = normalizedDocumentKey
        };
        metadata["visibility"] = visibility;
        metadata["publicAccess"] = visibility.Equals("public", StringComparison.OrdinalIgnoreCase);
        metadata["sha256"] = sha256;

        var bucket = new GridFSBucket(_mongoContext.Database, new GridFSBucketOptions
        {
            BucketName = "passportFiles"
        });
        var uploadId = await bucket.UploadFromBytesAsync(
            string.IsNullOrWhiteSpace(file.FileName) ? "upload.bin" : file.FileName,
            fileBytes,
            new GridFSUploadOptions
            {
                Metadata = metadata
            },
            cancellationToken);

        var fileUrl = $"/api/files/{uploadId}";
        await _passportRepository.UpdateDocumentReferenceAsync(
            normalizedPassportId,
            normalizedDocumentKey,
            uploadId.ToString(),
            fileUrl,
            contentType,
            sha256,
            visibility,
            cancellationToken);
        await _passportRepository.MarkCanonicalDirtyAsync(normalizedPassportId, "supportingDocumentChanged", cancellationToken);
        await AppendFileUploadedAuditEventAsync(
            normalizedPassportId,
            uploadId.ToString(),
            previousFileId,
            normalizedDocumentKey,
            visibility,
            sha256,
            contentType,
            cancellationToken);

        return Ok(new
        {
            fileId = uploadId.ToString(),
            url = fileUrl,
            contentType,
            sha256,
            visibility
        });
    }

    [HttpGet("{fileId}")]
    public async Task<IActionResult> Download(string fileId, CancellationToken cancellationToken)
    {
        if (_mongoContext.Database == null)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new { error = "Database is not connected." });
        }

        if (!ObjectId.TryParse(fileId, out var objectId))
        {
            return NotFound(new { error = "File not found", fileId });
        }

        var fileMetadata = await _mongoContext.Database
            .GetCollection<BsonDocument>("passportFiles.files")
            .Find(Builders<BsonDocument>.Filter.Eq("_id", objectId))
            .FirstOrDefaultAsync(cancellationToken);

        if (fileMetadata == null)
        {
            return NotFound(new { error = "File not found", fileId });
        }

        var metadata = fileMetadata.GetValue("metadata", new BsonDocument()) as BsonDocument ?? new BsonDocument();
        var passportId = BsonHelpers.GetString(metadata, "passportId");
        var documentKey = BsonHelpers.GetString(metadata, "documentKey");
        if (string.IsNullOrWhiteSpace(passportId))
        {
            return await DenyDownloadAsync(
                fileId,
                string.Empty,
                documentKey,
                NormalizeVisibility(BsonHelpers.GetString(metadata, "visibility")),
                "unlinkedFile",
                "File is not linked to a passport.",
                cancellationToken);
        }

        var passport = await _passportRepository.GetByPassportIdAsync(passportId, cancellationToken);
        if (passport == null)
        {
            return await DenyDownloadAsync(
                fileId,
                passportId,
                documentKey,
                NormalizeVisibility(BsonHelpers.GetString(metadata, "visibility")),
                "passportNotFound",
                "Linked passport was not found.",
                cancellationToken);
        }

        var documentReference = ResolveDocumentReference(passport, documentKey, fileId);
        var visibility = NormalizeVisibility(FirstNonEmpty(
            BsonHelpers.GetString(documentReference, "visibility"),
            BsonHelpers.GetString(metadata, "visibility"),
            BsonHelpers.GetString(metadata, "publicAccess")));
        if (documentReference.ElementCount > 0 && !DocumentReferenceMatchesFile(documentReference, fileId))
        {
            return await DenyDownloadAsync(
                fileId,
                passportId,
                documentKey,
                visibility,
                "fileReferenceMismatch",
                "File is no longer the active document reference for this passport.",
                cancellationToken);
        }

        var isPassportPublic = _passportPublishPolicyService.IsPubliclyVisible(passport);
        var canDownload = await _accessControlService.CanDownloadPassportDocumentAsync(
            User,
            BsonHelpers.GetString(passport, "clusterId"),
            visibility,
            isPassportPublic,
            cancellationToken);
        if (!canDownload)
        {
            return await DenyDownloadAsync(
                fileId,
                passportId,
                documentKey,
                visibility,
                "accessDenied",
                "You do not have access to download this passport file.",
                cancellationToken);
        }

        var contentType = FirstNonEmpty(
            BsonHelpers.GetString(fileMetadata, "contentType"),
            BsonHelpers.GetString(fileMetadata, "metadata", "contentType"),
            "application/octet-stream");
        var fileName = FirstNonEmpty(
            BsonHelpers.GetString(fileMetadata, "filename"),
            $"file-{fileId}");

        var bucket = new GridFSBucket(_mongoContext.Database, new GridFSBucketOptions
        {
            BucketName = "passportFiles"
        });
        var bytes = await bucket.DownloadAsBytesAsync(objectId, cancellationToken: cancellationToken);
        return File(bytes, contentType, fileName);
    }

    private async Task<IActionResult> DenyDownloadAsync(
        string fileId,
        string passportId,
        string documentKey,
        string visibility,
        string reason,
        string message,
        CancellationToken cancellationToken)
    {
        await AppendFileDeniedAuditEventAsync(passportId, fileId, documentKey, visibility, reason, cancellationToken);
        return StatusCode(StatusCodes.Status403Forbidden, new { error = message, fileId, reason });
    }

    private async Task AppendFileDeniedAuditEventAsync(
        string passportId,
        string fileId,
        string documentKey,
        string visibility,
        string reason,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(passportId))
        {
            return;
        }

        await _auditRevisionService.AppendAuditEventAsync(
            passportId,
            "passport.file.download.denied",
            CurrentActor(),
            CurrentActorRole(),
            "files-api",
            "Passport file download denied.",
            new BsonDocument
            {
                ["fileId"] = fileId,
                ["documentKey"] = documentKey,
                ["visibility"] = visibility,
                ["reason"] = reason
            },
            cancellationToken);
    }

    private async Task AppendFileUploadedAuditEventAsync(
        string passportId,
        string fileId,
        string previousFileId,
        string documentKey,
        string visibility,
        string sha256,
        string contentType,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(passportId))
        {
            return;
        }

        var isReplacement = !string.IsNullOrWhiteSpace(previousFileId)
            && !previousFileId.Equals(fileId, StringComparison.OrdinalIgnoreCase);

        await _auditRevisionService.AppendAuditEventAsync(
            passportId,
            isReplacement ? "passport.file.replaced" : "passport.file.uploaded",
            CurrentActor(),
            CurrentActorRole(),
            "files-api",
            isReplacement ? "Passport supporting file replaced." : "Passport supporting file uploaded.",
            new BsonDocument
            {
                ["fileId"] = fileId,
                ["previousFileId"] = previousFileId,
                ["documentKey"] = documentKey,
                ["visibility"] = visibility,
                ["sha256"] = sha256,
                ["contentType"] = contentType
            },
            cancellationToken);
    }

    private static BsonDocument ResolveDocumentReference(BsonDocument passport, string documentKey, string fileId)
    {
        var documents = BsonHelpers.GetValue(passport, "app", "documents") as BsonDocument;
        if (documents == null)
        {
            return new BsonDocument();
        }

        if (!string.IsNullOrWhiteSpace(documentKey)
            && documents.TryGetValue(documentKey, out var keyedValue)
            && keyedValue is BsonDocument keyedDocument)
        {
            return keyedDocument;
        }

        foreach (var element in documents)
        {
            if (element.Value is BsonDocument document && DocumentReferenceMatchesFile(document, fileId))
            {
                return document;
            }
        }

        return new BsonDocument();
    }

    private static bool DocumentReferenceMatchesFile(BsonDocument documentReference, string fileId)
    {
        if (string.IsNullOrWhiteSpace(fileId))
        {
            return false;
        }

        var referencedFileId = BsonHelpers.GetString(documentReference, "fileId");
        if (!string.IsNullOrWhiteSpace(referencedFileId))
        {
            return referencedFileId.Equals(fileId, StringComparison.OrdinalIgnoreCase);
        }

        var url = BsonHelpers.GetString(documentReference, "url");
        return !string.IsNullOrWhiteSpace(url)
            && url.Contains($"/api/files/{fileId}", StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeVisibility(string value)
    {
        if (value.Equals("public", StringComparison.OrdinalIgnoreCase)
            || value.Equals("true", StringComparison.OrdinalIgnoreCase)
            || value.Equals("1", StringComparison.OrdinalIgnoreCase)
            || value.Equals("yes", StringComparison.OrdinalIgnoreCase))
        {
            return "public";
        }

        if (value.Equals("restricted", StringComparison.OrdinalIgnoreCase))
        {
            return "private";
        }

        return "private";
    }

    private static string ComputeSha256Hex(byte[] bytes)
    {
        return Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    }

    private string CurrentActor()
    {
        if (User.Identity?.IsAuthenticated != true)
        {
            return "anonymous";
        }

        return AccessControlService.CurrentEmail(User);
    }

    private string CurrentActorRole()
    {
        if (User.IsInRole("admin"))
        {
            return "admin";
        }

        if (User.IsInRole("clusterAdmin"))
        {
            return "clusterAdmin";
        }

        return User.Identity?.IsAuthenticated == true ? "user" : "anonymous";
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
