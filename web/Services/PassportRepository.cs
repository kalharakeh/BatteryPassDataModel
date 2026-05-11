using BatteryPassWeb.Models.Trust;
using BatteryPassWeb.Models.ViewModels;
using MongoDB.Bson;
using MongoDB.Driver;

namespace BatteryPassWeb.Services;

public sealed class PassportRepository
{
    private readonly MongoContext _mongoContext;

    public PassportRepository(MongoContext mongoContext)
    {
        _mongoContext = mongoContext;
    }

    public async Task<IReadOnlyList<PassportSummaryViewModel>> SearchAsync(string query, bool includeArchived = false, CancellationToken cancellationToken = default)
    {
        var documents = await SearchDocumentsAsync(query, includeArchived, cancellationToken);
        return documents.Select(ToSummary).ToList();
    }

    public async Task<IReadOnlyList<BsonDocument>> SearchDocumentsAsync(string query, bool includeArchived = false, CancellationToken cancellationToken = default)
    {
        var collection = GetCollection();
        if (collection == null)
        {
            return [];
        }

        var builder = Builders<BsonDocument>.Filter;
        var filters = new List<FilterDefinition<BsonDocument>>();

        if (!includeArchived)
        {
            filters.Add(builder.Ne("registryInfo.status", "archived"));
        }

        if (!string.IsNullOrWhiteSpace(query))
        {
            var regex = new BsonRegularExpression(query.Trim(), "i");
            filters.Add(builder.Or(
                builder.Regex("passportId", regex),
                builder.Regex("registryInfo.registryId", regex),
                builder.Regex("app.display.name", regex),
                builder.Regex("app.display.modelNumber", regex),
                builder.Regex("app.display.serialNumber", regex),
                builder.Regex("app.display.manufacturerName", regex),
                builder.Regex("clusterId", regex),
                builder.Regex("aspects.generalProductInformation.payload.productIdentifier", regex),
                builder.Regex("aspects.generalProductInformation.payload.batteryPassportIdentifier", regex)
            ));
        }

        var filter = filters.Count switch
        {
            0 => builder.Empty,
            1 => filters[0],
            _ => builder.And(filters)
        };

        return await collection
            .Find(filter)
            .SortByDescending(document => document["registryInfo"]["updatedAt"])
            .Limit(500)
            .ToListAsync(cancellationToken);
    }

    public async Task<BsonDocument?> GetByPassportIdAsync(string passportId, CancellationToken cancellationToken = default)
    {
        var collection = GetCollection();
        if (collection == null)
        {
            return null;
        }

        return await collection.Find(Builders<BsonDocument>.Filter.Eq("passportId", passportId)).FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<PassportSummaryViewModel?> GetSummaryAsync(string passportId, CancellationToken cancellationToken = default)
    {
        var document = await GetByPassportIdAsync(passportId, cancellationToken);
        return document == null ? null : ToSummary(document);
    }

    public PassportSummaryViewModel ToSummaryViewModel(BsonDocument document)
    {
        return ToSummary(document);
    }

    public async Task ReplaceAsync(string passportId, BsonDocument document, CancellationToken cancellationToken = default)
    {
        var collection = GetCollection();
        if (collection == null)
        {
            return;
        }

        await collection.ReplaceOneAsync(
            Builders<BsonDocument>.Filter.Eq("passportId", passportId),
            document,
            new ReplaceOptions { IsUpsert = true },
            cancellationToken);
    }

    public async Task UpdatePassportClusterAsync(string passportId, string clusterId, CancellationToken cancellationToken = default)
    {
        var collection = GetCollection();
        if (collection == null)
        {
            return;
        }

        var now = DateTime.UtcNow.ToString("O");
        await collection.UpdateOneAsync(
            Builders<BsonDocument>.Filter.Eq("passportId", passportId),
            Builders<BsonDocument>.Update
                .Set("clusterId", clusterId)
                .Set("registryInfo.updatedAt", now),
            cancellationToken: cancellationToken);
    }

    public async Task ClearPassportClusterAsync(string clusterId, CancellationToken cancellationToken = default)
    {
        var collection = GetCollection();
        if (collection == null)
        {
            return;
        }

        var now = DateTime.UtcNow.ToString("O");
        await collection.UpdateManyAsync(
            Builders<BsonDocument>.Filter.Eq("clusterId", clusterId),
            Builders<BsonDocument>.Update
                .Unset("clusterId")
                .Set("registryInfo.updatedAt", now),
            cancellationToken: cancellationToken);
    }

    public async Task ArchivePassportAsync(string passportId, CancellationToken cancellationToken = default)
    {
        var collection = GetCollection();
        if (collection == null)
        {
            return;
        }

        var now = DateTime.UtcNow.ToString("O");
        await collection.UpdateOneAsync(
            Builders<BsonDocument>.Filter.Eq("passportId", passportId),
            Builders<BsonDocument>.Update
                .Set("registryInfo.status", "archived")
                .Set("registryInfo.updatedAt", now),
            cancellationToken: cancellationToken);
    }

    public async Task<bool> UpdateFieldsAsync(string passportId, IReadOnlyDictionary<string, BsonValue> setValues, CancellationToken cancellationToken = default)
    {
        var collection = GetCollection();
        if (collection == null || string.IsNullOrWhiteSpace(passportId) || setValues.Count == 0)
        {
            return false;
        }

        var updates = new List<UpdateDefinition<BsonDocument>>();
        foreach (var pair in setValues)
        {
            if (string.IsNullOrWhiteSpace(pair.Key))
            {
                continue;
            }

            updates.Add(Builders<BsonDocument>.Update.Set(pair.Key, pair.Value));
        }

        updates.Add(Builders<BsonDocument>.Update.Set("registryInfo.updatedAt", DateTime.UtcNow.ToString("O")));
        if (updates.Count == 0)
        {
            return false;
        }

        var result = await collection.UpdateOneAsync(
            Builders<BsonDocument>.Filter.Eq("passportId", passportId),
            Builders<BsonDocument>.Update.Combine(updates),
            cancellationToken: cancellationToken);

        return result.MatchedCount > 0;
    }

    public async Task<bool> UpdateDocumentReferenceAsync(
        string passportId,
        string documentKey,
        string fileId,
        string url,
        string contentType,
        string sha256,
        string visibility,
        CancellationToken cancellationToken = default)
    {
        var collection = GetCollection();
        if (collection == null
            || string.IsNullOrWhiteSpace(passportId)
            || string.IsNullOrWhiteSpace(documentKey)
            || !IsSafeDocumentKey(documentKey)
            || string.IsNullOrWhiteSpace(fileId))
        {
            return false;
        }

        var now = DateTime.UtcNow.ToString("O");
        var update = Builders<BsonDocument>.Update
            .Set($"app.documents.{documentKey}.fileId", fileId)
            .Set($"app.documents.{documentKey}.url", url)
            .Set($"app.documents.{documentKey}.contentType", contentType)
            .Set($"app.documents.{documentKey}.sha256", sha256)
            .Set($"app.documents.{documentKey}.visibility", visibility)
            .Set($"app.documents.{documentKey}.uploadedAt", now)
            .Set("registryInfo.updatedAt", now);

        var result = await collection.UpdateOneAsync(
            Builders<BsonDocument>.Filter.Eq("passportId", passportId),
            update,
            cancellationToken: cancellationToken);

        return result.MatchedCount > 0;
    }

    public async Task UpdateTrustValidationAsync(string passportId, TrustValidationSummary summary, CancellationToken cancellationToken = default)
    {
        var collection = GetCollection();
        if (collection == null || string.IsNullOrWhiteSpace(passportId))
        {
            return;
        }

        var now = DateTime.UtcNow.ToString("O");
        await collection.UpdateOneAsync(
            Builders<BsonDocument>.Filter.Eq("passportId", passportId),
            Builders<BsonDocument>.Update
                .Set("validation.isValid", summary.BlockingErrorCount == 0)
                .Set("validation.status", ValidationStatus(summary))
                .Set("validation.validatedAt", summary.ValidatedAt)
                .Set("validation.state", summary.State)
                .Set("validation.validationSummary", ToBsonDocument(summary))
                .Set("validation.blockingErrorCount", summary.BlockingErrorCount)
                .Set("validation.warningCount", summary.WarningCount)
                .Set("validation.passedCount", summary.PassedCount)
                .Set("validation.canSign", summary.CanSign)
                .Set("trust.state", summary.State)
                .Set("trust.isDirty", false)
                .Set("trust.lastValidatedAt", summary.ValidatedAt)
                .Set("trust.validationSummary", ToBsonDocument(summary))
                .Set("registryInfo.updatedAt", now),
            cancellationToken: cancellationToken);
    }

    public async Task MarkCanonicalDirtyAsync(string passportId, string reason = "canonicalDataChanged", CancellationToken cancellationToken = default)
    {
        var collection = GetCollection();
        if (collection == null || string.IsNullOrWhiteSpace(passportId))
        {
            return;
        }

        var now = DateTime.UtcNow.ToString("O");
        await collection.UpdateOneAsync(
            Builders<BsonDocument>.Filter.Eq("passportId", passportId),
            Builders<BsonDocument>.Update
                .Set("trust.state", TrustState.Dirty)
                .Set("trust.isDirty", true)
                .Set("trust.dirtyAt", now)
                .Set("trust.dirtyReason", reason)
                .Set("registryInfo.updatedAt", now),
            cancellationToken: cancellationToken);
    }

    public async Task<bool> UpdateTrustSignatureAsync(
        string passportId,
        TrustValidationSummary summary,
        string hash,
        BsonDocument proof,
        string revisionId,
        string signedAt,
        CancellationToken cancellationToken = default)
    {
        var collection = GetCollection();
        if (collection == null || string.IsNullOrWhiteSpace(passportId))
        {
            return false;
        }

        var now = DateTime.UtcNow.ToString("O");
        var proofValue = ProofValue(proof);
        var result = await collection.UpdateOneAsync(
            Builders<BsonDocument>.Filter.Eq("passportId", passportId),
            Builders<BsonDocument>.Update
                .Set("validation.isValid", summary.BlockingErrorCount == 0)
                .Set("validation.status", TrustState.Signed)
                .Set("validation.validatedAt", summary.ValidatedAt)
                .Set("validation.state", TrustState.Signed)
                .Set("validation.validationSummary", ToBsonDocument(summary))
                .Set("validation.blockingErrorCount", summary.BlockingErrorCount)
                .Set("validation.warningCount", summary.WarningCount)
                .Set("validation.passedCount", summary.PassedCount)
                .Set("validation.canSign", summary.CanSign)
                .Set("validation.signedAt", signedAt)
                .Set("validation.hash", hash)
                .Set("validation.signature", proofValue)
                .Set("validation.proof", proof.DeepClone())
                .Set("validation.signedRevisionId", revisionId)
                .Set("validation.signatureProofCode", proofValue)
                .Set("trust.state", TrustState.Signed)
                .Set("trust.isDirty", false)
                .Set("trust.lastValidatedAt", summary.ValidatedAt)
                .Set("trust.validationSummary", ToBsonDocument(summary))
                .Set("trust.latestHash", hash)
                .Set("trust.latestProof", proof.DeepClone())
                .Set("trust.latestRevisionId", revisionId)
                .Set("trust.lastSignedAt", signedAt)
                .Set("registryInfo.updatedAt", now),
            cancellationToken: cancellationToken);
        return result.MatchedCount > 0;
    }

    public async Task<bool> PublishPassportAsync(
        string passportId,
        string revisionId,
        string publishedAt,
        string hash,
        BsonDocument proof,
        CancellationToken cancellationToken = default)
    {
        var collection = GetCollection();
        if (collection == null || string.IsNullOrWhiteSpace(passportId))
        {
            return false;
        }

        var timestamp = string.IsNullOrWhiteSpace(publishedAt) ? DateTime.UtcNow.ToString("O") : publishedAt;
        var proofValue = ProofValue(proof);
        var result = await collection.UpdateOneAsync(
            Builders<BsonDocument>.Filter.Eq("passportId", passportId),
            Builders<BsonDocument>.Update
                .Set("registryInfo.status", "published")
                .Set("registryInfo.hasBeenPublished", true)
                .Set("registryInfo.publishedAt", timestamp)
                .Set("registryInfo.updatedAt", timestamp)
                .Set("validation.status", "published")
                .Set("validation.publishedAt", timestamp)
                .Set("validation.publishedRevisionId", revisionId)
                .Set("validation.publishedHash", hash)
                .Set("validation.publishedProof", proof.DeepClone())
                .Set("validation.publishedSignatureProofCode", proofValue)
                .Set("validation.publishStatus", "published")
                .Set("trust.publishedRevisionId", revisionId)
                .Set("trust.publishedAt", timestamp),
            cancellationToken: cancellationToken);
        return result.MatchedCount > 0;
    }

    private IMongoCollection<BsonDocument>? GetCollection()
    {
        return _mongoContext.Database?.GetCollection<BsonDocument>("passports");
    }

    private static BsonDocument ToBsonDocument(TrustValidationSummary summary)
    {
        return new BsonDocument
        {
            ["passportId"] = summary.PassportId,
            ["state"] = summary.State,
            ["validatedAt"] = summary.ValidatedAt,
            ["blockingErrorCount"] = summary.BlockingErrorCount,
            ["warningCount"] = summary.WarningCount,
            ["passedCount"] = summary.PassedCount,
            ["canSign"] = summary.CanSign,
            ["sections"] = new BsonArray(summary.Sections.Select(section => new BsonDocument
            {
                ["sectionKey"] = section.SectionKey,
                ["sectionLabel"] = section.SectionLabel,
                ["hasBlockingErrors"] = section.HasBlockingErrors,
                ["hasWarnings"] = section.HasWarnings,
                ["issues"] = new BsonArray(section.Issues.Select(issue => new BsonDocument
                {
                    ["severity"] = issue.Severity.ToString(),
                    ["path"] = issue.Path,
                    ["message"] = issue.Message
                }))
            }))
        };
    }

    private static string ValidationStatus(TrustValidationSummary summary)
    {
        return summary.BlockingErrorCount == 0 ? "validated" : "validation_failed";
    }

    private static string ProofValue(BsonDocument proof)
    {
        return BsonHelpers.GetString(proof, "proofValue");
    }

    private static bool IsSafeDocumentKey(string documentKey)
    {
        return documentKey.All(character => char.IsLetterOrDigit(character) || character is '_' or '-');
    }

    private static PassportSummaryViewModel ToSummary(BsonDocument document)
    {
        var passportId = BsonHelpers.GetString(document, "passportId");
        var name = BsonHelpers.GetString(document, "app", "display", "name");
        var modelNumber = BsonHelpers.GetString(document, "app", "display", "modelNumber");
        var manufacturer = BsonHelpers.GetString(document, "app", "display", "manufacturerName");
        var serialNumber = BsonHelpers.GetString(document, "app", "display", "serialNumber");
        var status = BsonHelpers.GetString(document, "registryInfo", "status");
        var imageUrl = BsonHelpers.GetString(document, "app", "media", "batteryImageUrl");
        var normalizedImageUrl = BatteryImageCatalog.NormalizeKnownImageUrl(imageUrl, passportId);
        var clusterId = BsonHelpers.GetString(document, "clusterId");

        return new PassportSummaryViewModel
        {
            PassportId = passportId,
            DisplayName = string.IsNullOrWhiteSpace(name) ? modelNumber : name,
            ModelNumber = modelNumber,
            ManufacturerName = manufacturer,
            SerialNumber = serialNumber,
            RegistryStatus = status,
            ClusterId = clusterId,
            ClusterLabel = string.IsNullOrWhiteSpace(clusterId) ? "No cluster assigned" : clusterId,
            BatteryFamily = BsonHelpers.GetString(document, "app", "product", "productName"),
            BatteryVersion = BsonHelpers.GetString(document, "app", "product", "productVersion"),
            BatterySerialNumber = serialNumber,
            PassportStatus = BuildPassportStatus(document),
            BatteryImageUrl = normalizedImageUrl,
            UpdatedDate = BsonHelpers.GetString(document, "registryInfo", "updatedAt")
        };
    }

    private static string BuildPassportStatus(BsonDocument document)
    {
        var registryStatus = BsonHelpers.GetString(document, "registryInfo", "status");
        if (registryStatus.Equals("archived", StringComparison.OrdinalIgnoreCase))
        {
            return "Archived";
        }

        if (registryStatus.Equals("published", StringComparison.OrdinalIgnoreCase))
        {
            return "Published";
        }

        var trustState = BsonHelpers.GetString(document, "trust", "state");
        return trustState.Equals(TrustState.Signed, StringComparison.OrdinalIgnoreCase)
            ? "Signed"
            : "Draft";
    }
}
