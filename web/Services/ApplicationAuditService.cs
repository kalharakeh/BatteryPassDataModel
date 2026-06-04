using MongoDB.Bson;
using MongoDB.Driver;

namespace BatteryPassWeb.Services;

public sealed record ApplicationAuditFilter(
    string Search = "",
    string EntityType = "",
    string Category = "",
    string Actor = "",
    string Source = "",
    string From = "",
    string To = "",
    int Limit = 500);

public sealed class ApplicationAuditService
{
    private readonly MongoContext? _mongoContext;

    public ApplicationAuditService(MongoContext? mongoContext = null)
    {
        _mongoContext = mongoContext;
    }

    public BsonDocument BuildApplicationAuditEventDocument(
        string eventType,
        string actor,
        string actorRole,
        string source,
        string message,
        BsonDocument? metadata = null,
        string entityType = "",
        string entityId = "",
        string passportId = "",
        string batteryId = "",
        string createdAt = "")
    {
        var id = ObjectId.GenerateNewId();
        var normalizedEntityType = FirstNonEmpty(entityType, EventRoot(eventType));
        var normalizedEntityId = FirstNonEmpty(entityId, passportId, batteryId);
        var timestamp = string.IsNullOrWhiteSpace(createdAt) ? DateTimeOffset.UtcNow.ToString("O") : createdAt;

        return new BsonDocument
        {
            ["_id"] = id,
            ["eventId"] = id.ToString(),
            ["eventType"] = eventType,
            ["category"] = CategoryFromEventType(eventType),
            ["entityType"] = normalizedEntityType,
            ["entityId"] = normalizedEntityId,
            ["passportId"] = string.IsNullOrWhiteSpace(passportId) ? BsonNull.Value : passportId,
            ["batteryId"] = string.IsNullOrWhiteSpace(batteryId) ? BsonNull.Value : batteryId,
            ["actor"] = actor,
            ["actorRole"] = actorRole,
            ["actorType"] = actorRole,
            ["source"] = source,
            ["message"] = message,
            ["metadata"] = metadata == null ? new BsonDocument() : metadata.DeepClone(),
            ["createdAt"] = timestamp,
            ["collection"] = "applicationAuditEvents"
        };
    }

    public async Task<BsonDocument> AppendApplicationAuditEventAsync(
        string eventType,
        string actor,
        string actorRole,
        string source,
        string message,
        BsonDocument? metadata = null,
        string entityType = "",
        string entityId = "",
        string passportId = "",
        string batteryId = "",
        CancellationToken cancellationToken = default)
    {
        var auditEvent = BuildApplicationAuditEventDocument(
            eventType,
            actor,
            actorRole,
            source,
            message,
            metadata,
            entityType,
            entityId,
            passportId,
            batteryId);

        var collection = GetApplicationAuditEventsCollection();
        if (collection != null)
        {
            await collection.InsertOneAsync(auditEvent, cancellationToken: cancellationToken);
        }

        return auditEvent;
    }

    public async Task<IReadOnlyList<BsonDocument>> ListPassportTimelineEventsAsync(
        string passportId,
        string batteryId,
        CancellationToken cancellationToken = default)
    {
        var events = new List<BsonDocument>();

        var passportCollection = GetPassportAuditEventsCollection();
        if (passportCollection != null && !string.IsNullOrWhiteSpace(passportId))
        {
            var passportEvents = await passportCollection
                .Find(Builders<BsonDocument>.Filter.Eq("passportId", passportId))
                .Sort(Builders<BsonDocument>.Sort.Descending("createdAt"))
                .Limit(500)
                .ToListAsync(cancellationToken);
            events.AddRange(passportEvents.Select(NormalizePassportAuditEvent));
        }

        var batteryCollection = GetBatteryAuditEventsCollection();
        if (batteryCollection != null && !string.IsNullOrWhiteSpace(batteryId))
        {
            var batteryEvents = await batteryCollection
                .Find(Builders<BsonDocument>.Filter.Eq("batteryId", batteryId))
                .Sort(Builders<BsonDocument>.Sort.Descending("createdAt"))
                .Limit(500)
                .ToListAsync(cancellationToken);
            events.AddRange(batteryEvents.Select(NormalizeBatteryAuditEvent));
        }

        var appCollection = GetApplicationAuditEventsCollection();
        if (appCollection != null && (!string.IsNullOrWhiteSpace(passportId) || !string.IsNullOrWhiteSpace(batteryId)))
        {
            var filters = new List<FilterDefinition<BsonDocument>>();
            if (!string.IsNullOrWhiteSpace(passportId))
            {
                filters.Add(Builders<BsonDocument>.Filter.Eq("passportId", passportId));
            }

            if (!string.IsNullOrWhiteSpace(batteryId))
            {
                filters.Add(Builders<BsonDocument>.Filter.Eq("batteryId", batteryId));
            }

            var relatedEvents = await appCollection
                .Find(Builders<BsonDocument>.Filter.Or(filters))
                .Sort(Builders<BsonDocument>.Sort.Descending("createdAt"))
                .Limit(500)
                .ToListAsync(cancellationToken);
            events.AddRange(relatedEvents.Select(NormalizeApplicationAuditEvent));
        }

        return events
            .OrderByDescending(document => BsonHelpers.GetString(document, "createdAt"), StringComparer.OrdinalIgnoreCase)
            .ThenByDescending(document => BsonHelpers.GetString(document, "eventId"), StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public async Task<IReadOnlyList<BsonDocument>> ListApplicationAuditEventsAsync(
        ApplicationAuditFilter filter,
        CancellationToken cancellationToken = default)
    {
        var events = new List<BsonDocument>();
        var readLimit = Math.Clamp(filter.Limit <= 0 ? 500 : filter.Limit, 50, 1000);

        if (GetApplicationAuditEventsCollection() is { } appCollection)
        {
            var appEvents = await appCollection
                .Find(Builders<BsonDocument>.Filter.Empty)
                .Sort(Builders<BsonDocument>.Sort.Descending("createdAt"))
                .Limit(readLimit)
                .ToListAsync(cancellationToken);
            events.AddRange(appEvents.Select(NormalizeApplicationAuditEvent));
        }

        if (GetPassportAuditEventsCollection() is { } passportCollection)
        {
            var passportEvents = await passportCollection
                .Find(Builders<BsonDocument>.Filter.Empty)
                .Sort(Builders<BsonDocument>.Sort.Descending("createdAt"))
                .Limit(readLimit)
                .ToListAsync(cancellationToken);
            events.AddRange(passportEvents.Select(NormalizePassportAuditEvent));
        }

        if (GetBatteryAuditEventsCollection() is { } batteryCollection)
        {
            var batteryEvents = await batteryCollection
                .Find(Builders<BsonDocument>.Filter.Empty)
                .Sort(Builders<BsonDocument>.Sort.Descending("createdAt"))
                .Limit(readLimit)
                .ToListAsync(cancellationToken);
            events.AddRange(batteryEvents.Select(NormalizeBatteryAuditEvent));
        }

        return events
            .Where(document => MatchesFilter(document, filter))
            .OrderByDescending(document => BsonHelpers.GetString(document, "createdAt"), StringComparer.OrdinalIgnoreCase)
            .ThenByDescending(document => BsonHelpers.GetString(document, "eventId"), StringComparer.OrdinalIgnoreCase)
            .Take(readLimit)
            .ToList();
    }

    public static BsonDocument NormalizeApplicationAuditEvent(BsonDocument document)
    {
        var normalized = document.DeepClone().AsBsonDocument;
        normalized["category"] = FirstNonEmpty(BsonHelpers.GetString(normalized, "category"), CategoryFromEventType(BsonHelpers.GetString(normalized, "eventType")));
        normalized["entityType"] = FirstNonEmpty(BsonHelpers.GetString(normalized, "entityType"), EventRoot(BsonHelpers.GetString(normalized, "eventType")));
        normalized["entityId"] = FirstNonEmpty(
            BsonHelpers.GetString(normalized, "entityId"),
            BsonHelpers.GetString(normalized, "passportId"),
            BsonHelpers.GetString(normalized, "batteryId"));
        normalized["actorType"] = FirstNonEmpty(BsonHelpers.GetString(normalized, "actorType"), BsonHelpers.GetString(normalized, "actorRole"));
        normalized["collection"] = "applicationAuditEvents";
        EnsureMetadata(normalized);
        return normalized;
    }

    public static BsonDocument NormalizePassportAuditEvent(BsonDocument document)
    {
        var normalized = document.DeepClone().AsBsonDocument;
        var passportId = BsonHelpers.GetString(normalized, "passportId");
        normalized["category"] = CategoryFromEventType(BsonHelpers.GetString(normalized, "eventType"));
        normalized["entityType"] = "passport";
        normalized["entityId"] = passportId;
        normalized["batteryId"] = FirstNonEmpty(
            BsonHelpers.GetString(normalized, "batteryId"),
            BsonHelpers.GetString(normalized, "metadata", "batteryId"));
        normalized["actorType"] = FirstNonEmpty(BsonHelpers.GetString(normalized, "actorType"), BsonHelpers.GetString(normalized, "actorRole"));
        normalized["collection"] = "auditEvents";
        EnsureMetadata(normalized);
        return normalized;
    }

    public static BsonDocument NormalizeBatteryAuditEvent(BsonDocument document)
    {
        var normalized = document.DeepClone().AsBsonDocument;
        var batteryId = BsonHelpers.GetString(normalized, "batteryId");
        normalized["category"] = CategoryFromEventType(BsonHelpers.GetString(normalized, "eventType"));
        normalized["entityType"] = "battery";
        normalized["entityId"] = batteryId;
        normalized["passportId"] = FirstNonEmpty(
            BsonHelpers.GetString(normalized, "passportId"),
            BsonHelpers.GetString(normalized, "metadata", "passportId"),
            BsonHelpers.GetString(normalized, "metadata", "latestPassportId"));
        normalized["actorRole"] = FirstNonEmpty(BsonHelpers.GetString(normalized, "actorRole"), BsonHelpers.GetString(normalized, "actorType"));
        normalized["actorType"] = FirstNonEmpty(BsonHelpers.GetString(normalized, "actorType"), BsonHelpers.GetString(normalized, "actorRole"));
        normalized["collection"] = "batteryAuditEvents";
        EnsureMetadata(normalized);
        return normalized;
    }

    private static bool MatchesFilter(BsonDocument document, ApplicationAuditFilter filter)
    {
        return MatchesText(BsonHelpers.GetString(document, "entityType"), filter.EntityType)
            && MatchesText(BsonHelpers.GetString(document, "category"), filter.Category)
            && MatchesText(BsonHelpers.GetString(document, "actor"), filter.Actor)
            && MatchesText(BsonHelpers.GetString(document, "source"), filter.Source)
            && MatchesDateRange(BsonHelpers.GetString(document, "createdAt"), filter.From, filter.To)
            && MatchesSearch(document, filter.Search);
    }

    private static bool MatchesSearch(BsonDocument document, string search)
    {
        if (string.IsNullOrWhiteSpace(search))
        {
            return true;
        }

        var needle = search.Trim();
        return SearchFields(document).Any(field => field.Contains(needle, StringComparison.OrdinalIgnoreCase));
    }

    private static IEnumerable<string> SearchFields(BsonDocument document)
    {
        yield return BsonHelpers.GetString(document, "eventType");
        yield return BsonHelpers.GetString(document, "entityType");
        yield return BsonHelpers.GetString(document, "entityId");
        yield return BsonHelpers.GetString(document, "passportId");
        yield return BsonHelpers.GetString(document, "batteryId");
        yield return BsonHelpers.GetString(document, "actor");
        yield return BsonHelpers.GetString(document, "actorRole");
        yield return BsonHelpers.GetString(document, "source");
        yield return BsonHelpers.GetString(document, "message");
        yield return document.GetValue("metadata", new BsonDocument()).ToString() ?? string.Empty;
    }

    private static bool MatchesText(string value, string filter)
    {
        return string.IsNullOrWhiteSpace(filter)
            || value.Equals(filter.Trim(), StringComparison.OrdinalIgnoreCase);
    }

    private static bool MatchesDateRange(string createdAt, string from, string to)
    {
        if (!DateTimeOffset.TryParse(createdAt, out var created))
        {
            return true;
        }

        if (!string.IsNullOrWhiteSpace(from)
            && DateTimeOffset.TryParse(from, out var fromDate)
            && created < fromDate)
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(to)
            && DateTimeOffset.TryParse(to, out var toDate)
            && created > toDate.AddDays(1).AddTicks(-1))
        {
            return false;
        }

        return true;
    }

    private static void EnsureMetadata(BsonDocument document)
    {
        if (!document.TryGetValue("metadata", out var metadata) || metadata is not BsonDocument)
        {
            document["metadata"] = new BsonDocument();
        }
    }

    private static string CategoryFromEventType(string eventType)
    {
        if (eventType.StartsWith("passport.file.", StringComparison.OrdinalIgnoreCase))
        {
            return "file";
        }

        if (eventType.StartsWith("apiToken.", StringComparison.OrdinalIgnoreCase)
            || eventType.StartsWith("security.", StringComparison.OrdinalIgnoreCase)
            || eventType.StartsWith("account.", StringComparison.OrdinalIgnoreCase))
        {
            return "security";
        }

        if (eventType.StartsWith("cluster.", StringComparison.OrdinalIgnoreCase)
            || eventType.StartsWith("editablePolicy.", StringComparison.OrdinalIgnoreCase)
            || eventType.StartsWith("product.", StringComparison.OrdinalIgnoreCase))
        {
            return "admin";
        }

        return EventRoot(eventType);
    }

    private static string EventRoot(string eventType)
    {
        var parts = eventType.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return parts.Length == 0 ? "application" : parts[0];
    }

    private static string FirstNonEmpty(params string[] values)
    {
        return values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;
    }

    private IMongoCollection<BsonDocument>? GetApplicationAuditEventsCollection()
    {
        return _mongoContext?.Database?.GetCollection<BsonDocument>("applicationAuditEvents");
    }

    private IMongoCollection<BsonDocument>? GetPassportAuditEventsCollection()
    {
        return _mongoContext?.Database?.GetCollection<BsonDocument>("auditEvents");
    }

    private IMongoCollection<BsonDocument>? GetBatteryAuditEventsCollection()
    {
        return _mongoContext?.Database?.GetCollection<BsonDocument>("batteryAuditEvents");
    }
}
