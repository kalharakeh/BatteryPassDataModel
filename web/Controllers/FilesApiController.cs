using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using MongoDB.Bson;
using MongoDB.Driver;
using MongoDB.Driver.GridFS;
using BatteryPassWeb.Services;

namespace BatteryPassWeb.Controllers;

[ApiController]
[Route("api/files")]
public class FilesApiController : ControllerBase
{
    private readonly MongoContext _mongoContext;

    public FilesApiController(MongoContext mongoContext)
    {
        _mongoContext = mongoContext;
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

        var contentType = string.IsNullOrWhiteSpace(file.ContentType) ? "application/octet-stream" : file.ContentType;
        if (contentType != "application/pdf" && !contentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(new { error = "Only PDF and image uploads are supported." });
        }

        await using var memory = new MemoryStream();
        await file.CopyToAsync(memory, cancellationToken);
        var metadata = new BsonDocument
        {
            ["uploadedAt"] = DateTime.UtcNow.ToString("O"),
            ["source"] = "batterypass-mvc-upload",
            ["contentType"] = contentType
        };
        if (!string.IsNullOrWhiteSpace(passportId))
        {
            metadata["passportId"] = passportId.Trim();
        }
        if (!string.IsNullOrWhiteSpace(documentKey))
        {
            metadata["documentKey"] = documentKey.Trim();
        }
        if (!string.IsNullOrWhiteSpace(publicAccess))
        {
            metadata["publicAccess"] = publicAccess.Trim();
        }

        var bucket = new GridFSBucket(_mongoContext.Database, new GridFSBucketOptions
        {
            BucketName = "passportFiles"
        });
        var uploadId = await bucket.UploadFromBytesAsync(
            string.IsNullOrWhiteSpace(file.FileName) ? "upload.bin" : file.FileName,
            memory.ToArray(),
            new GridFSUploadOptions
            {
                Metadata = metadata
            },
            cancellationToken);

        return Ok(new
        {
            fileId = uploadId.ToString(),
            url = $"/api/files/{uploadId}",
            contentType
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
