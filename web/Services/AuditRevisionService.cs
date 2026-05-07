using MongoDB.Bson;
using MongoDB.Driver;

namespace BatteryPassWeb.Services;

public sealed class AuditRevisionService
{
    private readonly MongoContext? _mongoContext;

    public AuditRevisionService(MongoContext? mongoContext = null)
    {
        _mongoContext = mongoContext;
    }

    public BsonDocument BuildRevisionDocument(
        string passportId,
        int revisionNumber,
        BsonDocument snapshot,
        string hash,
        BsonDocument proof,
        string actor,
        string signedAt,
        string publishedAt = "")
    {
        var id = ObjectId.GenerateNewId();
        var isPublished = !string.IsNullOrWhiteSpace(publishedAt);

        return new BsonDocument
        {
            ["_id"] = id,
            ["revisionId"] = id.ToString(),
            ["passportId"] = passportId,
            ["revisionNumber"] = revisionNumber,
            ["status"] = isPublished ? "published" : "signed",
            ["immutable"] = true,
            ["hash"] = NormalizeHash(hash),
            ["snapshot"] = snapshot.DeepClone(),
            ["proof"] = proof.DeepClone(),
            ["actor"] = actor,
            ["createdAt"] = signedAt,
            ["signedAt"] = signedAt,
            ["publishedAt"] = isPublished ? publishedAt : BsonNull.Value
        };
    }

    public BsonDocument BuildAuditEventDocument(
        string passportId,
        string eventType,
        string actor,
        string actorRole,
        string source,
        string message,
        BsonDocument? metadata = null,
        string createdAt = "")
    {
        var id = ObjectId.GenerateNewId();
        var timestamp = string.IsNullOrWhiteSpace(createdAt) ? DateTimeOffset.UtcNow.ToString("O") : createdAt;

        return new BsonDocument
        {
            ["_id"] = id,
            ["eventId"] = id.ToString(),
            ["passportId"] = passportId,
            ["eventType"] = eventType,
            ["actor"] = actor,
            ["actorRole"] = actorRole,
            ["source"] = source,
            ["message"] = message,
            ["metadata"] = metadata == null ? new BsonDocument() : metadata.DeepClone(),
            ["createdAt"] = timestamp
        };
    }

    public async Task<BsonDocument> CreateSignedRevisionAsync(
        string passportId,
        BsonDocument snapshot,
        string hash,
        BsonDocument proof,
        string actor,
        string signedAt,
        CancellationToken cancellationToken = default)
    {
        var collection = GetPassportRevisionsCollection();
        if (collection == null)
        {
            throw new InvalidOperationException("MongoDB persistence is unavailable; signed revision was not recorded and trust state was not changed.");
        }

        var revisionNumber = await GetNextRevisionNumberAsync(collection, passportId, cancellationToken);
        var revision = BuildRevisionDocument(passportId, revisionNumber, snapshot, hash, proof, actor, signedAt);

        await collection.InsertOneAsync(revision, cancellationToken: cancellationToken);
        return revision;
    }

    public async Task<BsonDocument> AppendAuditEventAsync(
        string passportId,
        string eventType,
        string actor,
        string actorRole,
        string source,
        string message,
        BsonDocument? metadata = null,
        CancellationToken cancellationToken = default)
    {
        var auditEvent = BuildAuditEventDocument(passportId, eventType, actor, actorRole, source, message, metadata);
        var collection = GetAuditEventsCollection();
        if (collection != null)
        {
            await collection.InsertOneAsync(auditEvent, cancellationToken: cancellationToken);
        }

        return auditEvent;
    }

    public async Task<IReadOnlyList<BsonDocument>> ListRevisionsAsync(
        string passportId,
        CancellationToken cancellationToken = default)
    {
        var collection = GetPassportRevisionsCollection();
        if (collection == null)
        {
            return [];
        }

        return await collection
            .Find(Builders<BsonDocument>.Filter.Eq("passportId", passportId))
            .Sort(Builders<BsonDocument>.Sort.Descending("revisionNumber"))
            .ToListAsync(cancellationToken);
    }

    public async Task<BsonDocument?> GetLatestSignedRevisionAsync(
        string passportId,
        CancellationToken cancellationToken = default)
    {
        var collection = GetPassportRevisionsCollection();
        if (collection == null || string.IsNullOrWhiteSpace(passportId))
        {
            return null;
        }

        var filter = Builders<BsonDocument>.Filter.And(
            Builders<BsonDocument>.Filter.Eq("passportId", passportId),
            Builders<BsonDocument>.Filter.In("status", new[] { "signed", "published" }));

        return await collection
            .Find(filter)
            .Sort(Builders<BsonDocument>.Sort.Descending("revisionNumber"))
            .Limit(1)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<BsonDocument>> ListAuditEventsAsync(
        string passportId,
        CancellationToken cancellationToken = default)
    {
        var collection = GetAuditEventsCollection();
        if (collection == null)
        {
            return [];
        }

        return await collection
            .Find(Builders<BsonDocument>.Filter.Eq("passportId", passportId))
            .Sort(Builders<BsonDocument>.Sort.Descending("createdAt"))
            .ToListAsync(cancellationToken);
    }

    public async Task<bool> MarkRevisionPublishedAsync(
        string revisionId,
        string publishedAt,
        CancellationToken cancellationToken = default)
    {
        var collection = GetPassportRevisionsCollection();
        if (collection == null || string.IsNullOrWhiteSpace(revisionId))
        {
            return false;
        }

        var timestamp = string.IsNullOrWhiteSpace(publishedAt) ? DateTimeOffset.UtcNow.ToString("O") : publishedAt;
        var result = await collection.UpdateOneAsync(
            Builders<BsonDocument>.Filter.Eq("revisionId", revisionId),
            Builders<BsonDocument>.Update
                .Set("status", "published")
                .Set("publishedAt", timestamp),
            cancellationToken: cancellationToken);
        return result.MatchedCount > 0;
    }

    public async Task DeleteDemoLedgerAsync(
        IReadOnlyCollection<string> passportIds,
        CancellationToken cancellationToken = default)
    {
        var filteredPassportIds = passportIds
            .Where(passportId => !string.IsNullOrWhiteSpace(passportId))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (filteredPassportIds.Length == 0)
        {
            return;
        }

        var revisionCollection = GetPassportRevisionsCollection();
        if (revisionCollection != null)
        {
            await revisionCollection.DeleteManyAsync(
                Builders<BsonDocument>.Filter.In("passportId", filteredPassportIds),
                cancellationToken);
        }

        var auditCollection = GetAuditEventsCollection();
        if (auditCollection != null)
        {
            await auditCollection.DeleteManyAsync(
                Builders<BsonDocument>.Filter.In("passportId", filteredPassportIds),
                cancellationToken);
        }
    }

    private async Task<int> GetNextRevisionNumberAsync(
        IMongoCollection<BsonDocument> collection,
        string passportId,
        CancellationToken cancellationToken)
    {
        var latest = await collection
            .Find(Builders<BsonDocument>.Filter.Eq("passportId", passportId))
            .Sort(Builders<BsonDocument>.Sort.Descending("revisionNumber"))
            .Limit(1)
            .FirstOrDefaultAsync(cancellationToken);

        return latest == null ? 1 : latest.GetValue("revisionNumber", 0).ToInt32() + 1;
    }

    private IMongoCollection<BsonDocument>? GetPassportRevisionsCollection()
    {
        return _mongoContext?.Database?.GetCollection<BsonDocument>("passportRevisions");
    }

    private IMongoCollection<BsonDocument>? GetAuditEventsCollection()
    {
        return _mongoContext?.Database?.GetCollection<BsonDocument>("auditEvents");
    }

    private static string NormalizeHash(string hash)
    {
        return hash.StartsWith("sha256:", StringComparison.OrdinalIgnoreCase) ? hash : $"sha256:{hash}";
    }
}
