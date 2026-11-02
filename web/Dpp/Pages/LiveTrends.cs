using System.Globalization;
using System.Text.Json.Nodes;
using BatteryPassWeb.Dpp.Catalog;
using BatteryPassWeb.Dpp.Editing;
using BatteryPassWeb.Dpp.Store;
using BatteryPassWeb.Services;
using MongoDB.Bson;

namespace BatteryPassWeb.Dpp.Pages;

/// <summary>One reading of a tile's trend line: when it was measured, and the value.</summary>
public sealed record TrendPoint(DateTime AtUtc, decimal Value);

/// <summary>
/// One of the battery overview's tiles (redesign R4, the owner's improvement 4): the value now, and how it went over the
/// last 7 days, drawn as a small line.
/// </summary>
/// <param name="Note">What the small print says under the value: when it was measured, or how the value changes.</param>
public sealed record LiveTile(string Title, string Property, decimal? Value, string Unit, string Note, IReadOnlyList<TrendPoint> Points)
{
    /// <summary>The value with its unit, or a dash before there is one.</summary>
    public string Shown => Value is { } value ? $"{ValueWords.Number(value)}{(Unit.Length == 0 ? string.Empty : $" {Unit}")}" : "—";

    /// <summary>The trend in words, for readers who can't see the line.</summary>
    public string TrendWords => Points.Count < 2
        ? "No change in the last 7 days"
        : $"From {ValueWords.Number(Points[0].Value)} to {ValueWords.Number(Points[^1].Value)} over the last 7 days";

    /// <summary>
    /// The line's points in a <paramref name="width"/> × <paramref name="height"/> box, oldest on the left and the highest
    /// value at the top; null with fewer than two readings, when there is no line to draw.
    /// </summary>
    public string? Line(int width, int height)
    {
        if (Points.Count < 2)
        {
            return null;
        }

        var first = Points[0].AtUtc.Ticks;
        var span = Math.Max(1, Points[^1].AtUtc.Ticks - first);
        var low = Points.Min(point => point.Value);
        var high = Points.Max(point => point.Value);
        var range = high - low;
        return string.Join(" ", Points.Select(point =>
        {
            var x = (double)(point.AtUtc.Ticks - first) / span * width;
            var y = range == 0 ? height / 2.0 : height - 2 - (double)((point.Value - low) / range) * (height - 4);
            return $"{x.ToString("0.#", CultureInfo.InvariantCulture)},{y.ToString("0.#", CultureInfo.InvariantCulture)}";
        }));
    }
}

/// <summary>
/// The battery overview's tiles: state of charge, full cycles, temperature and capacity fade, each with its last 7 days
/// read from the change log, which keeps every saved value with its measurement time (like the live history, decision 30).
/// They are shown on the team's and the cluster admin's battery pages, whose readers may see these values.
/// </summary>
public sealed class LiveTrends(ApplicationAuditService audit, TimeProvider clock)
{
    public const int Hours = 168;

    private static readonly (int Number, string Title, string Unit)[] Tiles =
    [
        (63, "State of charge", "%"),
        (83, "Full cycles", ""),
        (87, "Temperature", "°C"),
        (59, "Capacity fade", "%"),
    ];

    public async Task<IReadOnlyList<LiveTile>> TilesAsync(BatteryPack pack, BatteryModel model, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(pack);
        ArgumentNullException.ThrowIfNull(model);
        var sinceUtc = clock.GetUtcNow().UtcDateTime.AddHours(-Hours);
        var wanted = Tiles.Select(tile => AttributeCatalog.ByNumber(tile.Number).Property).ToHashSet(StringComparer.Ordinal);
        var points = new Dictionary<string, List<TrendPoint>>(StringComparer.Ordinal);
        foreach (var change in await audit.ListEntityEventsAsync(SavePipeline.AuditEventType, "dpp-pack", pack.Id, sinceUtc, cancellationToken))
        {
            var savedAt = Moment(BsonHelpers.GetString(change, "createdAt")) ?? sinceUtc;
            var keys = change.GetValue("metadata", new BsonDocument()).AsBsonDocument.GetValue("keys", new BsonArray()).AsBsonArray;
            foreach (var key in keys.OfType<BsonDocument>())
            {
                var property = key.GetValue("key", string.Empty).ToString()!;
                if (key.GetValue("outcome", string.Empty) != KeyOutcome.Saved.ToString() || !wanted.Contains(property)
                    || key.GetValue("value", BsonNull.Value) is not BsonString text || Number(JsonNode.Parse(text.Value)) is not { } value)
                {
                    continue;
                }

                var measuredAt = Moment(key.GetValue("generatedAt", BsonNull.Value) is BsonString generated ? generated.Value : null) ?? savedAt;
                if (measuredAt >= sinceUtc)
                {
                    (points.TryGetValue(property, out var list) ? list : points[property] = []).Add(new TrendPoint(measuredAt, value));
                }
            }
        }

        return Tiles.Select(tile =>
        {
            var property = AttributeCatalog.ByNumber(tile.Number).Property;
            var stored = pack.Values.GetValueOrDefault(property) ?? model.Values.GetValueOrDefault(property);
            var note = tile.Number == 59
                ? "Re-stated at each status change"
                : stored is null ? "Not measured yet" : $"Measured {ValueWords.Time(stored.GeneratedAtUtc)}";
            var line = points.GetValueOrDefault(property)?.OrderBy(point => point.AtUtc).ToArray() ?? [];
            return new LiveTile(tile.Title, property, Number(stored?.Value), tile.Unit, note, line);
        }).ToArray();
    }

    /// <summary>
    /// A value's number: a plain number (#83, cycles), or the number inside the schema's small object, such as
    /// { "percentUnit": "%", "percentageValue": 76 } (#63, #59) or { "degreeCelsiusUnit": "°C", "celsiusValue": 23 } (#87).
    /// </summary>
    private static decimal? Number(JsonNode? node) => node switch
    {
        // Whatever .NET type holds it (int, double, decimal), a JSON number reads the same through its text.
        JsonValue value when value.GetValueKind() == System.Text.Json.JsonValueKind.Number => decimal.Parse(value.ToJsonString(), NumberStyles.Float, CultureInfo.InvariantCulture),
        JsonValue text when text.TryGetValue<string>(out var words) && decimal.TryParse(words, NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed) => parsed,
        JsonObject inner => inner.Select(pair => Number(pair.Value)).FirstOrDefault(number => number is not null),
        _ => null,
    };

    private static DateTime? Moment(string? text) =>
        DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var moment) ? moment.UtcDateTime : null;
}
