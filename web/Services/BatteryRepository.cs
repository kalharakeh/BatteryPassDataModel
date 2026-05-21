using BatteryPassWeb.Models.ViewModels;
using MongoDB.Bson;
using MongoDB.Driver;

namespace BatteryPassWeb.Services;

public sealed class BatteryRepository
{
    private readonly MongoContext _mongoContext;

    public BatteryRepository(MongoContext mongoContext)
    {
        _mongoContext = mongoContext;
    }

    public bool IsAvailable => _mongoContext.Database != null;

    public async Task EnsureIndexesAsync(CancellationToken cancellationToken = default)
    {
        var collection = GetCollection();
        if (collection == null)
        {
            return;
        }

        var indexes = new[]
        {
            new CreateIndexModel<BsonDocument>(
                Builders<BsonDocument>.IndexKeys.Ascending("batteryId"),
                new CreateIndexOptions { Unique = true }),
            new CreateIndexModel<BsonDocument>(
                Builders<BsonDocument>.IndexKeys.Ascending("clusterId")),
            new CreateIndexModel<BsonDocument>(
                Builders<BsonDocument>.IndexKeys.Ascending("identity.batteryFamily").Ascending("identity.serialNumber"),
                new CreateIndexOptions { Unique = true })
        };

        await collection.Indexes.CreateManyAsync(indexes, cancellationToken);
    }

    public async Task<BsonDocument?> GetByBatteryIdAsync(string batteryId, CancellationToken cancellationToken = default)
    {
        var collection = GetCollection();
        if (collection == null || string.IsNullOrWhiteSpace(batteryId))
        {
            return null;
        }

        return await collection.Find(Builders<BsonDocument>.Filter.Eq("batteryId", batteryId.Trim())).FirstOrDefaultAsync(cancellationToken);
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
            filters.Add(builder.Ne("status", "archived"));
        }

        if (!string.IsNullOrWhiteSpace(query))
        {
            var regex = new BsonRegularExpression(query.Trim(), "i");
            filters.Add(builder.Or(
                builder.Regex("batteryId", regex),
                builder.Regex("identity.batteryFamily", regex),
                builder.Regex("identity.batteryModel", regex),
                builder.Regex("identity.serialNumber", regex),
                builder.Regex("clusterId", regex)));
        }

        var filter = filters.Count == 0 ? builder.Empty : filters.Count == 1 ? filters[0] : builder.And(filters);
        return await collection.Find(filter).SortByDescending(row => row["updatedAt"]).Limit(500).ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<BsonDocument>> SearchByClusterAsync(string query, CancellationToken cancellationToken = default)
    {
        var collection = GetCollection();
        if (collection == null || string.IsNullOrWhiteSpace(query))
        {
            return [];
        }

        var regex = new BsonRegularExpression(query.Trim(), "i");
        return await collection
            .Find(Builders<BsonDocument>.Filter.Regex("clusterId", regex))
            .SortByDescending(row => row["updatedAt"])
            .Limit(500)
            .ToListAsync(cancellationToken);
    }

    public async Task CreateBatteryAsync(BsonDocument battery, CancellationToken cancellationToken = default)
    {
        var collection = GetCollection();
        if (collection == null)
        {
            return;
        }

        battery.Remove("_id");
        await collection.InsertOneAsync(battery, cancellationToken: cancellationToken);
    }

    public async Task<long> DeleteByFamilyAndSerialExceptAsync(
        string batteryFamily,
        string serialNumber,
        string batteryIdToKeep,
        CancellationToken cancellationToken = default)
    {
        var collection = GetCollection();
        if (collection == null
            || string.IsNullOrWhiteSpace(batteryFamily)
            || string.IsNullOrWhiteSpace(serialNumber)
            || string.IsNullOrWhiteSpace(batteryIdToKeep))
        {
            return 0;
        }

        var builder = Builders<BsonDocument>.Filter;
        var result = await collection.DeleteManyAsync(
            builder.And(
                builder.Eq("identity.batteryFamily", batteryFamily),
                builder.Eq("identity.serialNumber", serialNumber),
                builder.Ne("batteryId", batteryIdToKeep)),
            cancellationToken);
        return result.DeletedCount;
    }

    public async Task ReplaceAsync(string batteryId, BsonDocument battery, CancellationToken cancellationToken = default)
    {
        var collection = GetCollection();
        if (collection == null)
        {
            return;
        }

        battery.Remove("_id");
        await collection.ReplaceOneAsync(
            Builders<BsonDocument>.Filter.Eq("batteryId", batteryId),
            battery,
            new ReplaceOptions { IsUpsert = true },
            cancellationToken);
    }

    public async Task<bool> UpdateBatteryFieldsAsync(string batteryId, IReadOnlyDictionary<string, BsonValue> setValues, CancellationToken cancellationToken = default)
    {
        var collection = GetCollection();
        if (collection == null || string.IsNullOrWhiteSpace(batteryId) || setValues.Count == 0)
        {
            return false;
        }

        var updates = setValues.Select(pair => Builders<BsonDocument>.Update.Set(pair.Key, pair.Value)).ToList();
        updates.Add(Builders<BsonDocument>.Update.Set("updatedAt", DateTimeOffset.UtcNow.ToString("O")));

        var result = await collection.UpdateOneAsync(
            Builders<BsonDocument>.Filter.Eq("batteryId", batteryId),
            Builders<BsonDocument>.Update.Combine(updates),
            cancellationToken: cancellationToken);

        return result.MatchedCount > 0;
    }

    public BatterySummaryViewModel ToSummary(BsonDocument battery, IReadOnlyList<BatteryPassportHistoryRowViewModel> passportRows, string clusterLabel)
    {
        var latest = passportRows.FirstOrDefault(row => row.IsLatestForBattery) ?? passportRows.OrderByDescending(row => row.CreatedAt).FirstOrDefault();
        return new BatterySummaryViewModel
        {
            BatteryId = BsonHelpers.GetString(battery, "batteryId"),
            BatteryFamily = BsonHelpers.GetString(battery, "identity", "batteryFamily"),
            BatteryModel = BsonHelpers.GetString(battery, "identity", "batteryModel"),
            BatterySerialNumber = BsonHelpers.GetString(battery, "identity", "serialNumber"),
            ClusterId = BsonHelpers.GetString(battery, "clusterId"),
            ClusterLabel = clusterLabel,
            PassportCount = passportRows.Count,
            LatestPassportId = latest?.PassportId ?? string.Empty,
            LatestPassportStatus = latest?.PassportStatus ?? "Draft",
            UpdatedDate = BsonHelpers.GetString(battery, "updatedAt"),
            NewPassportRequired = BsonHelpers.GetValue(battery, "app", "snapshot", "newPassportRequired") is { IsBoolean: true } newPassportRequired
                && newPassportRequired.AsBoolean,
            Passports = passportRows
        };
    }

    private IMongoCollection<BsonDocument>? GetCollection()
    {
        return _mongoContext.Database?.GetCollection<BsonDocument>("batteries");
    }
}
