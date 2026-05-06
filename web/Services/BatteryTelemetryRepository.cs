using MongoDB.Bson;
using MongoDB.Driver;

namespace BatteryPassWeb.Services;

public sealed class TelemetryWritePoint
{
    public double? CurrentConsumptionKwh { get; init; }
    public double? CurrentChargeLevelPct { get; init; }
    public double? CurrentVoltageV { get; init; }
    public double? CurrentCurrentA { get; init; }
    public DateTime MeasuredAtUtc { get; init; }
}

public sealed class BatteryTelemetryRepository
{
    private readonly MongoContext _mongoContext;

    public BatteryTelemetryRepository(MongoContext mongoContext)
    {
        _mongoContext = mongoContext;
    }

    public bool IsAvailable => _mongoContext.Database != null;

    public async Task EnsureIndexesAsync(CancellationToken cancellationToken = default)
    {
        if (_mongoContext.Database == null)
        {
            return;
        }

        var telemetry = _mongoContext.Database.GetCollection<BsonDocument>("batteryTelemetry");
        var indexes = new[]
        {
            new CreateIndexModel<BsonDocument>(Builders<BsonDocument>.IndexKeys.Ascending("passportId").Descending("measuredAt")),
            new CreateIndexModel<BsonDocument>(Builders<BsonDocument>.IndexKeys.Ascending("expiresAt"), new CreateIndexOptions { ExpireAfter = TimeSpan.Zero })
        };
        await telemetry.Indexes.CreateManyAsync(indexes, cancellationToken);
    }

    public async Task AppendTelemetryAsync(string passportId, IReadOnlyList<TelemetryWritePoint> points, CancellationToken cancellationToken = default)
    {
        if (_mongoContext.Database == null || string.IsNullOrWhiteSpace(passportId) || points.Count == 0)
        {
            return;
        }

        var documents = points.Select(point => new BsonDocument
        {
            ["passportId"] = passportId.Trim(),
            ["measuredAt"] = point.MeasuredAtUtc.ToString("O"),
            ["currentConsumptionKwh"] = point.CurrentConsumptionKwh.HasValue ? BsonValue.Create(point.CurrentConsumptionKwh.Value) : BsonNull.Value,
            ["currentChargeLevelPct"] = point.CurrentChargeLevelPct.HasValue ? BsonValue.Create(point.CurrentChargeLevelPct.Value) : BsonNull.Value,
            ["currentVoltageV"] = point.CurrentVoltageV.HasValue ? BsonValue.Create(point.CurrentVoltageV.Value) : BsonNull.Value,
            ["currentCurrentA"] = point.CurrentCurrentA.HasValue ? BsonValue.Create(point.CurrentCurrentA.Value) : BsonNull.Value,
            ["createdAt"] = DateTime.UtcNow.ToString("O"),
            ["expiresAt"] = point.MeasuredAtUtc.AddDays(30).ToString("O")
        }).ToList();

        await _mongoContext.Database.GetCollection<BsonDocument>("batteryTelemetry")
            .InsertManyAsync(documents, cancellationToken: cancellationToken);
    }

    public async Task<IReadOnlyList<BsonDocument>> ReadHistoryAsync(string passportId, DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken = default)
    {
        if (_mongoContext.Database == null || string.IsNullOrWhiteSpace(passportId))
        {
            return [];
        }

        var filter = Builders<BsonDocument>.Filter.And(
            Builders<BsonDocument>.Filter.Eq("passportId", passportId.Trim()),
            Builders<BsonDocument>.Filter.Gte("measuredAt", fromUtc.ToString("O")),
            Builders<BsonDocument>.Filter.Lte("measuredAt", toUtc.ToString("O")));

        return await _mongoContext.Database.GetCollection<BsonDocument>("batteryTelemetry")
            .Find(filter)
            .SortBy(document => document["measuredAt"])
            .ToListAsync(cancellationToken);
    }
}
