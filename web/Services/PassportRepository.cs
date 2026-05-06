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
            BatteryImageUrl = normalizedImageUrl,
            UpdatedDate = BsonHelpers.GetString(document, "registryInfo", "updatedAt")
        };
    }
}
