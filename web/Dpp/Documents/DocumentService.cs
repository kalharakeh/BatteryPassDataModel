using System.Security.Cryptography;
using System.Text.Json.Nodes;
using BatteryPassWeb.Configuration;
using BatteryPassWeb.Dpp.Access;
using BatteryPassWeb.Dpp.Catalog;
using BatteryPassWeb.Dpp.Editing;
using BatteryPassWeb.Dpp.Store;
using BatteryPassWeb.Services;
using Microsoft.Extensions.Options;
using MongoDB.Bson;

namespace BatteryPassWeb.Dpp.Documents;

/// <summary>A file to upload. The content type is found from the bytes, so none is asked for.</summary>
public sealed record DocumentUpload(string FileName, byte[] Content, string? Title = null, string? Language = null);

public sealed record DocumentUploadResult(DppDocument? Document, string? Problem);

public sealed record DocumentFile(DppDocument Document, byte[] Content);

/// <summary>
/// Restricted documents (design 9): uploads for #29 (batch) and #45–48 (model) by the team, accident reports for #92
/// by the team or the battery's cluster admins, and downloads only for the attribute's audience. Public documents
/// are plain links saved through the pipeline and need nothing here.
/// </summary>
public sealed class DocumentService(
    DocumentStore documents,
    DppStore store,
    SavePipeline pipeline,
    DppLinks links,
    IOptions<BatteryPassOptions> options,
    ApplicationAuditService audit,
    TimeProvider clock)
{
    public const string UploadedEvent = "dpp.document-uploaded";
    public const string DeniedEvent = "dpp.document-denied";
    public const string WithdrawnEvent = "dpp.accident-report-withdrawn";

    public const string OnlyTheTeamWithdraws = "only the team withdraws an accident report";
    public const string WithdrawNeedsReason = "give the reason first";
    public const string NoSuchReport = "there is no such accident report on this battery";

    /// <summary>The longest withdrawal reason kept, in characters.</summary>
    public const int MaxReason = 500;

    /// <summary>#92: a pack's accident reports.</summary>
    public const string Accidents = "informationOnAccidents";

    /// <summary>Uploads the file for a restricted document of a model or batch and links the attribute to it.</summary>
    public async Task<DocumentUploadResult> UploadAsync(ChangeSender sender, TargetKind owner, string ownerId, string property, DocumentUpload upload, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(sender);
        ArgumentNullException.ThrowIfNull(upload);
        if (property == Accidents)
        {
            return Failed("accident reports are added with AddAccidentReportAsync");
        }

        if (!DppLinks.RestrictedDocumentProperties.Contains(property))
        {
            return Failed($"{property} isn't an uploaded document; public documents are links");
        }

        if (sender.Kind != SenderKind.Team)
        {
            return Failed("only the team uploads these documents");
        }

        var documentId = $"doc-{Guid.NewGuid():N}";
        var title = string.IsNullOrWhiteSpace(upload.Title) ? SafeFileName(upload.FileName) : upload.Title.Trim();
        return await StoreAndLinkAsync(sender, owner, ownerId, property, documentId, title, upload, contentType => DocumentLink(contentType, documentId, title, upload.Language), cancellationToken);
    }

    /// <summary>
    /// Adds an accident report to a pack (#92). #92 keeps pointing at the pack's accident page; saving it again counts
    /// as a live update, so a published passport's #4 moves (J-14).
    /// </summary>
    public Task<DocumentUploadResult> AddAccidentReportAsync(ChangeSender sender, string packId, DocumentUpload upload, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(sender);
        ArgumentNullException.ThrowIfNull(upload);
        var title = string.IsNullOrWhiteSpace(upload.Title) ? SafeFileName(upload.FileName) : upload.Title.Trim();
        return StoreAndLinkAsync(sender, TargetKind.Pack, packId, Accidents, $"doc-{Guid.NewGuid():N}", title, upload, _ => links.AccidentPageLink(packId), cancellationToken, byTheApp: true);
    }

    /// <summary>
    /// The team withdraws an accident report (S07): the record and its stored file are deleted, so the accident page, the
    /// passport's accident information and every address of the file lose it. The audit log keeps who, when, why and the
    /// file's name. Null when done; otherwise why not.
    /// </summary>
    public async Task<string?> WithdrawAccidentReportAsync(ChangeSender sender, string packId, string documentId, string? reason, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(sender);
        if (sender.Kind != SenderKind.Team)
        {
            return OnlyTheTeamWithdraws;
        }

        var why = reason?.Trim() ?? string.Empty;
        if (why.Length == 0)
        {
            return WithdrawNeedsReason;
        }

        if (why.Length > MaxReason)
        {
            why = why[..MaxReason];
        }

        if (await documents.GetAsync(documentId, cancellationToken) is not { } report
            || report.OwnerKind != TargetKind.Pack || report.OwnerId != packId || report.Property != Accidents
            || await store.GetAsync<BatteryPack>(packId, cancellationToken) is not { } pack)
        {
            return NoSuchReport;
        }

        // The trace first: a failure while deleting never leaves a deleted file without an entry.
        var serial = pack.Values.GetValueOrDefault("batterySerialNumber")?.Value?.GetValue<string>() ?? packId;
        await audit.AppendApplicationAuditEventAsync(
            WithdrawnEvent,
            sender.Who,
            sender.Kind.ToString(),
            "form",
            $"Accident report {report.FileName} of battery {serial} withdrawn: {why}",
            new BsonDocument
            {
                ["documentId"] = report.Id,
                ["fileName"] = report.FileName,
                ["title"] = report.Title,
                ["sha256"] = report.Sha256,
                ["uploadedBy"] = report.UploadedBy,
                ["reason"] = why,
            },
            entityType: "dpp-pack",
            entityId: packId,
            cancellationToken: cancellationToken);
        await documents.DeleteAsync(report, cancellationToken);
        return null;
    }

    /// <summary>The file, when the reader may see its attribute; otherwise null, and the refusal is logged.</summary>
    public async Task<DocumentFile?> ReadAsync(Reader reader, string documentId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(reader);
        var document = await documents.GetAsync(documentId, cancellationToken);
        if (document is null)
        {
            return null;
        }

        if (!await MaySeeAsync(reader, document.OwnerKind, document.OwnerId, document.Property, cancellationToken))
        {
            await audit.AppendApplicationAuditEventAsync(
                DeniedEvent,
                reader.Who,
                string.Join(",", reader.Roles),
                "documents",
                $"Download of document {documentId} refused",
                entityType: "dpp-document",
                entityId: documentId,
                cancellationToken: cancellationToken);
            return null;
        }

        return new DocumentFile(document, await documents.DownloadAsync(document, cancellationToken));
    }

    /// <summary>A pack's accident reports, newest first, when the reader may see #92; otherwise null.</summary>
    public async Task<IReadOnlyList<DppDocument>?> AccidentReportsAsync(Reader reader, string packId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(reader);
        if (await store.GetAsync<BatteryPack>(packId, cancellationToken) is null
            || !await MaySeeAsync(reader, TargetKind.Pack, packId, Accidents, cancellationToken))
        {
            return null;
        }

        return await documents.ListAsync(TargetKind.Pack, packId, Accidents, cancellationToken);
    }

    private async Task<DocumentUploadResult> StoreAndLinkAsync(
        ChangeSender sender,
        TargetKind owner,
        string ownerId,
        string property,
        string documentId,
        string title,
        DocumentUpload upload,
        Func<string, JsonObject> linkFor,
        CancellationToken cancellationToken,
        bool byTheApp = false)
    {
        if (!links.IsConfigured)
        {
            return Failed(DppLinks.NotConfigured);
        }

        var (contentType, problem) = Inspect(upload);
        if (problem is not null)
        {
            return Failed(problem);
        }

        // Check the link first (record exists, sender may change it, value valid), so nothing is stored for a refusal.
        var change = new ChangeSet(owner, ownerId, new Dictionary<string, JsonNode?> { [property] = linkFor(contentType!) }, "form") { ByTheApp = byTheApp };
        var preview = await pipeline.SaveAsync(sender, change, preview: true, cancellationToken);
        if (!preview.Found)
        {
            return Failed($"{owner.ToString().ToLowerInvariant()} {ownerId} not found");
        }

        var outcome = preview.Keys.Single();
        if (outcome.Outcome != KeyOutcome.Saved)
        {
            return Failed(outcome.Message);
        }

        var document = new DppDocument
        {
            Id = documentId,
            OwnerKind = owner,
            OwnerId = ownerId,
            Property = property,
            FileName = SafeFileName(upload.FileName),
            ContentType = contentType!,
            Length = upload.Content.LongLength,
            Sha256 = Convert.ToHexStringLower(SHA256.HashData(upload.Content)),
            FileId = await documents.UploadFileAsync(SafeFileName(upload.FileName), upload.Content, cancellationToken),
            Title = title,
            Language = upload.Language,
            UploadedAtUtc = clock.GetUtcNow().UtcDateTime,
            UploadedBy = sender.Who,
        };
        await documents.InsertAsync(document, cancellationToken);
        await pipeline.SaveAsync(sender, change, cancellationToken: cancellationToken);
        await audit.AppendApplicationAuditEventAsync(
            UploadedEvent,
            sender.Who,
            sender.Kind.ToString(),
            "documents",
            $"{document.FileName} uploaded for {property} of {owner.ToString().ToLowerInvariant()} {ownerId}",
            new BsonDocument { ["documentId"] = document.Id, ["sha256"] = document.Sha256, ["contentType"] = document.ContentType },
            entityType: "dpp-document",
            entityId: document.Id,
            cancellationToken: cancellationToken);
        return new DocumentUploadResult(document, null);
    }

    private async Task<bool> MaySeeAsync(Reader reader, TargetKind owner, string ownerId, string property, CancellationToken cancellationToken)
    {
        var attribute = AttributeCatalog.FindByProperty(property);
        if (attribute is null)
        {
            return false;
        }

        if (owner is not (TargetKind.Model or TargetKind.Batch or TargetKind.Pack))
        {
            return false;
        }

        var modelId = owner == TargetKind.Model ? ownerId : null;
        var batchId = owner == TargetKind.Batch ? ownerId : null;
        var packId = owner == TargetKind.Pack ? ownerId : null;

        // Nothing unpublished leaves the team (J-6): until a published passport uses the record, its documents are the team's.
        if (!reader.Roles.Contains(ReaderRole.Team)
            && !await store.HasPublishedPackUsingAsync(modelId, batchId, packId, cancellationToken))
        {
            return false;
        }

        var clusters = await store.FindPublishedClusterIdsUsingAsync(modelId, batchId, packId, cancellationToken);
        return AudienceRules.MaySee(reader, attribute, clusters);
    }

    /// <summary>Only PDF, PNG and JPEG, recognised by their first bytes; empty and oversized files are refused.</summary>
    private (string? ContentType, string? Problem) Inspect(DocumentUpload upload)
    {
        if (upload.Content.Length == 0)
        {
            return (null, "the file is empty");
        }

        var limit = options.Value.MaxUploadBytes;
        if (limit > 0 && upload.Content.LongLength > limit)
        {
            return (null, $"the file is larger than the {limit}-byte upload limit");
        }

        var contentType = FileTypes.Detect(upload.Content);
        return contentType is null
            ? (null, "only PDF, PNG and JPEG files are accepted, recognised by their content")
            : (contentType, null);
    }

    private JsonObject DocumentLink(string contentType, string documentId, string title, string? language)
    {
        var link = new JsonObject
        {
            ["contentType"] = contentType,
            ["url"] = links.DocumentUrl(documentId),
            ["resourceTitle"] = title,
        };
        if (!string.IsNullOrWhiteSpace(language))
        {
            link["language"] = language;
        }

        return link;
    }

    private static string SafeFileName(string fileName)
    {
        var name = Path.GetFileName(fileName ?? string.Empty).Trim();
        return name.Length == 0 ? "document" : name;
    }

    private static DocumentUploadResult Failed(string problem) => new(null, problem);
}
