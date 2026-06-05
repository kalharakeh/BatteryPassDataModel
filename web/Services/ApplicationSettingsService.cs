using BatteryPassWeb.Configuration;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;

namespace BatteryPassWeb.Services;

public sealed class ApplicationSettingsService
{
    private const string SettingsId = "global";

    private readonly MongoContext _mongoContext;
    private readonly BatteryPassOptions _options;

    public ApplicationSettingsService(MongoContext mongoContext, IOptions<BatteryPassOptions> options)
    {
        _mongoContext = mongoContext;
        _options = options.Value;
    }

    public async Task<TimeSpan> GetSessionTimeoutAsync(CancellationToken cancellationToken = default)
    {
        return TimeSpan.FromMinutes(await GetSessionTimeoutMinutesAsync(cancellationToken));
    }

    public async Task<int> GetSessionTimeoutMinutesAsync(CancellationToken cancellationToken = default)
    {
        var fallback = BatteryPassOptions.NormalizeSessionTimeoutMinutes(_options.SessionTimeoutMinutes);
        var collection = GetCollection();
        if (collection == null)
        {
            return fallback;
        }

        var document = await collection
            .Find(Builders<BsonDocument>.Filter.Eq("_id", SettingsId))
            .FirstOrDefaultAsync(cancellationToken);
        if (document == null)
        {
            return fallback;
        }

        var value = document.GetValue("sessionTimeoutMinutes", fallback);
        return BatteryPassOptions.NormalizeSessionTimeoutMinutes(ToInt32(value, fallback));
    }

    public async Task<int> SetSessionTimeoutMinutesAsync(
        int minutes,
        string updatedBy,
        CancellationToken cancellationToken = default)
    {
        var normalized = BatteryPassOptions.NormalizeSessionTimeoutMinutes(minutes);
        var collection = GetCollection();
        if (collection == null)
        {
            return normalized;
        }

        var now = DateTime.UtcNow.ToString("O");
        await collection.UpdateOneAsync(
            Builders<BsonDocument>.Filter.Eq("_id", SettingsId),
            Builders<BsonDocument>.Update
                .Set("sessionTimeoutMinutes", normalized)
                .Set("updatedAt", now)
                .Set("updatedBy", string.IsNullOrWhiteSpace(updatedBy) ? "admin" : updatedBy.Trim().ToLowerInvariant())
                .SetOnInsert("createdAt", now),
            new UpdateOptions { IsUpsert = true },
            cancellationToken);

        return normalized;
    }

    private IMongoCollection<BsonDocument>? GetCollection() =>
        _mongoContext.Database?.GetCollection<BsonDocument>("applicationSettings");

    private static int ToInt32(BsonValue value, int fallback)
    {
        if (value.IsInt32)
        {
            return value.AsInt32;
        }

        if (value.IsInt64)
        {
            return (int)Math.Clamp(value.AsInt64, int.MinValue, int.MaxValue);
        }

        if (int.TryParse(value.ToString(), out var parsed))
        {
            return parsed;
        }

        return fallback;
    }
}
