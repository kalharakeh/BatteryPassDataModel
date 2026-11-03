using System.Globalization;
using System.Text.Json.Nodes;
using BatteryPassWeb.Dpp.Api;
using BatteryPassWeb.Dpp.Catalog;
using BatteryPassWeb.Dpp.Editing;
using BatteryPassWeb.Dpp.Store;
using BatteryPassWeb.Services;
using MongoDB.Bson;

namespace BatteryPassWeb.Dpp.Pages;

/// <summary>The detail page's Live values tab: the reader's latest live values and the last week's readings (redesign R6).</summary>
public sealed record LiveTab(IReadOnlyList<ShownValue> Latest, IReadOnlyList<LiveHistoryEntry> History);

/// <summary>One reading of a tile's trend line: when it was measured, and the value.</summary>
public sealed record TrendPoint(DateTime AtUtc, decimal Value);

/// <summary>
/// One of the battery overview's tiles (redesign R4, the owner's improvement 4): the value now, and how it went over the
/// last 7 days, drawn as a small line.
/// </summary>
/// <param name="Note">What the small print says under the value: when it was measured, or how the value changes.</param>
public sealed record LiveTile(string Title, string Property, decimal? Value, string Unit, string Note, IReadOnlyList<TrendPoint> Points)
{
    /// <summary>The value with its unit; the latest reading in words for a value that isn't a number; a dash before there is one.</summary>
    public string Shown => Value is { } value ? $"{ValueWords.Number(value)}{(Unit.Length == 0 ? string.Empty : $" {Unit}")}" : Latest ?? "—";

    /// <summary>The latest reading in words (the detail page's charts, redesign R5); null on the overview.</summary>
    public string? Latest { get; init; }

    /// <summary>How many readings the last 7 days had (the detail page's charts).</summary>
    public int Readings { get; init; }

    /// <summary>The time axis's ends, the last 7 days (the detail page's charts); null: the readings' own first and last.</summary>
    public DateTime? FromUtc { get; init; }

    public DateTime? ToUtc { get; init; }

    /// <summary>The lowest and highest reading of the last 7 days; null without a number.</summary>
    public decimal? Low => Points.Count > 0 ? Points.Min(point => point.Value) : null;

    public decimal? High => Points.Count > 0 ? Points.Max(point => point.Value) : null;

    /// <summary>The axis's days: where each day starts across the box, and its label ("2 Nov" for the first, then the day).</summary>
    public IReadOnlyList<(double X, string Label)> Days(int width)
    {
        if (FromUtc is not { } from || ToUtc is not { } to || to <= from)
        {
            return [];
        }

        var days = new List<(double X, string Label)>();
        for (var day = from.Date.AddDays(1); day < to; day = day.AddDays(1))
        {
            var x = (double)(day.Ticks - from.Ticks) / (to.Ticks - from.Ticks) * width;
            days.Add((x, days.Count == 0 ? day.ToString("d MMM", CultureInfo.InvariantCulture) : day.Day.ToString(CultureInfo.InvariantCulture)));
        }

        return days;
    }

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

        var first = (FromUtc ?? Points[0].AtUtc).Ticks;
        var span = Math.Max(1, (ToUtc ?? Points[^1].AtUtc).Ticks - first);
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

    /// <summary>
    /// The detail page's live values as charts (redesign R5, the owner's item 7): one per value in the reader's live
    /// history (so who sees what is unchanged), in the catalogue's order, each with the last 7 days on a fixed time axis.
    /// A value that isn't a number (a document) shows its latest reading in words.
    /// </summary>
    /// <param name="words">A reading in words, as the readings table shows it.</param>
    public static IReadOnlyList<LiveTile> Charts(IReadOnlyList<LiveHistoryEntry> history, DateTime nowUtc, Func<LiveHistoryEntry, string> words)
    {
        ArgumentNullException.ThrowIfNull(history);
        ArgumentNullException.ThrowIfNull(words);
        var order = AttributeCatalog.All.Select((attribute, index) => (attribute.Number, index)).ToDictionary(pair => pair.Number, pair => pair.index);
        return history
            .GroupBy(entry => entry.Attribute.Number)
            .OrderBy(group => order.GetValueOrDefault(group.Key, int.MaxValue))
            .Select(group =>
            {
                var attribute = group.First().Attribute;
                var latest = group.MaxBy(entry => entry.MeasuredAtUtc)!;
                var unit = attribute.Unit ?? string.Empty;
                var name = attribute.IsExtension ? $"{attribute.Label} {attribute.Name}" : attribute.Name;
                var points = group
                    .Select(entry => (entry.MeasuredAtUtc, Value: Number(entry.Value)))
                    .Where(point => point.Value is not null)
                    .Select(point => new TrendPoint(point.MeasuredAtUtc, point.Value!.Value))
                    .OrderBy(point => point.AtUtc)
                    .ToArray();
                return new LiveTile(
                    unit.Length == 0 ? name : $"{name} ({unit})",
                    attribute.Property,
                    Number(latest.Value),
                    unit,
                    $"Measured {ValueWords.Time(latest.MeasuredAtUtc)} · {PassportPages.SourceWord(latest.Source)}",
                    points)
                {
                    Latest = words(latest),
                    Readings = group.Count(),
                    FromUtc = nowUtc.AddHours(-Hours),
                    ToUtc = nowUtc,
                };
            })
            .ToArray();
    }

    /// <summary>
    /// The detail page's Live values tab (redesign R6, the managing session's rule): one tile per live value the reader
    /// may see (<paramref name="latest"/>, from their passport tabs), each with its latest reading and its date, even when
    /// that is older than the last 7 days, and the week's line when the week had readings (<paramref name="history"/>).
    /// </summary>
    public static IReadOnlyList<LiveTile> ForTab(IReadOnlyList<ShownValue> latest, IReadOnlyList<LiveHistoryEntry> history, DateTime nowUtc, Func<LiveHistoryEntry, string> words)
    {
        ArgumentNullException.ThrowIfNull(latest);
        ArgumentNullException.ThrowIfNull(history);
        var week = Charts(history.Where(entry => entry.MeasuredAtUtc >= nowUtc.AddHours(-Hours)).ToArray(), nowUtc, words)
            .ToDictionary(chart => chart.Property, StringComparer.Ordinal);
        var order = AttributeCatalog.All.Select((attribute, index) => (attribute.Number, index)).ToDictionary(pair => pair.Number, pair => pair.index);
        return latest
            .Where(value => value.Attribute.Change == ChangeRule.Live)
            .OrderBy(value => order.GetValueOrDefault(value.Attribute.Number, int.MaxValue))
            .Select(value =>
            {
                var unit = value.Attribute.Unit ?? string.Empty;
                var name = value.Attribute.IsExtension ? $"{value.Attribute.Label} {value.Attribute.Name}" : value.Attribute.Name;
                var note = value.Origin is { } origin
                    ? $"{(origin.Measured ? "Measured" : "Entered")} {ValueWords.Time(origin.GeneratedAtUtc)} · {PassportPages.SourceWord(origin.Source)}"
                    : "When it was measured isn't known";
                var shown = string.Join(", ", value.Lines.Select(line => line.Text));
                return week.TryGetValue(value.Attribute.Property, out var chart)
                    ? chart with { Value = null, Note = note, Latest = shown }
                    : new LiveTile(unit.Length == 0 ? name : $"{name} ({unit})", value.Attribute.Property, null, unit, note, [])
                    {
                        Latest = shown,
                        Readings = 0,
                        FromUtc = nowUtc.AddHours(-Hours),
                        ToUtc = nowUtc,
                    };
            })
            .ToArray();
    }

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
