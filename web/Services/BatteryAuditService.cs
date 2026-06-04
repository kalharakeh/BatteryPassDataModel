using MongoDB.Bson;
using MongoDB.Driver;

namespace BatteryPassWeb.Services;

public sealed class BatteryAuditService
{
    private readonly MongoContext? _mongoContext;

    public BatteryAuditService(MongoContext? mongoContext = null)
    {
        _mongoContext = mongoContext;
    }

    public BsonDocument BuildBatteryAuditEventDocument(
        string batteryId,
        string eventType,
        string actor,
        string actorType,
        string source,
        string message,
        BsonDocument? metadata = null,
        string actorTokenId = "",
        string createdAt = "")
    {
        var id = ObjectId.GenerateNewId();
        var timestamp = string.IsNullOrWhiteSpace(createdAt) ? DateTimeOffset.UtcNow.ToString("O") : createdAt;

        return new BsonDocument
        {
            ["_id"] = id,
            ["eventId"] = id.ToString(),
            ["batteryId"] = batteryId,
            ["eventType"] = eventType,
            ["actor"] = actor,
            ["actorType"] = actorType,
            ["actorTokenId"] = string.IsNullOrWhiteSpace(actorTokenId) ? BsonNull.Value : actorTokenId,
            ["source"] = source,
            ["message"] = message,
            ["metadata"] = metadata == null ? new BsonDocument() : metadata.DeepClone(),
            ["createdAt"] = timestamp
        };
    }

    public async Task<BsonDocument> AppendBatteryAuditEventAsync(
        string batteryId,
        string eventType,
        string actor,
        string actorType,
        string source,
        string message,
        BsonDocument? metadata = null,
        string actorTokenId = "",
        CancellationToken cancellationToken = default)
    {
        var auditEvent = BuildBatteryAuditEventDocument(
            batteryId,
            eventType,
            actor,
            actorType,
            source,
            message,
            metadata,
            actorTokenId);

        var collection = GetBatteryAuditEventsCollection();
        if (collection != null)
        {
            await collection.InsertOneAsync(auditEvent, cancellationToken: cancellationToken);
        }

        return auditEvent;
    }

    public async Task<IReadOnlyList<BsonDocument>> ListBatteryAuditEventsAsync(
        string batteryId,
        CancellationToken cancellationToken = default)
    {
        var collection = GetBatteryAuditEventsCollection();
        if (collection == null || string.IsNullOrWhiteSpace(batteryId))
        {
            return [];
        }

        return await collection
            .Find(Builders<BsonDocument>.Filter.Eq("batteryId", batteryId.Trim()))
            .Sort(Builders<BsonDocument>.Sort.Descending("createdAt"))
            .ToListAsync(cancellationToken);
    }

    private IMongoCollection<BsonDocument>? GetBatteryAuditEventsCollection()
    {
        return _mongoContext?.Database?.GetCollection<BsonDocument>("batteryAuditEvents");
    }
}
