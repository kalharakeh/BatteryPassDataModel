using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using BatteryPassWeb.Dpp.Editing;
using BatteryPassWeb.Dpp.Store;

namespace BatteryPassWeb.Dpp.Uploads;

/// <summary>
/// One battery's entry in an upload, whatever the file's format: how the file names the battery, when its values were
/// measured, and the values by the schema's property names (design 8.2).
/// </summary>
/// <param name="Number">The entry's place in the file, from 1.</param>
/// <param name="NamedAs">The battery ID (#6) or serial number (#11) the file names the battery by.</param>
/// <param name="MeasuredAtUtc">When every value of the entry was generated; the upload time when null.</param>
/// <param name="Problem">Why the entry can't be used as it is; null when it can.</param>
/// <param name="Modules">The battery's module list, when the file has one (the battery system's export, decision 31).</param>
/// <param name="Notes">What the file's adapter wants the sender to know, e.g. a value it couldn't read.</param>
/// <param name="FillOnly">Keys that only fill a battery without a value, left out silently otherwise (the export's #17).</param>
/// <param name="Status">A status change or repair the entry asks for (step 5d); null for values only.</param>
/// <param name="RemainingCapacityAh">The measured remaining capacity, kept as app data for the fade suggestion (J-11).</param>
public sealed record UploadEntry(
    int Number,
    string NamedAs,
    DateTime? MeasuredAtUtc,
    IReadOnlyDictionary<string, JsonNode?> Values,
    string? Problem = null,
    IReadOnlyList<BatteryModule>? Modules = null,
    IReadOnlyList<string>? Notes = null,
    IReadOnlyCollection<string>? FillOnly = null,
    decimal? RemainingCapacityAh = null,
    UploadStatus? Status = null);

/// <summary>
/// A status change in an upload entry (design 8.2, decision 35 Q20): what happened, and for a repair its day
/// (yyyy-MM-dd; the day of measuredAt when the file gives none) and what was repaired.
/// </summary>
public sealed record UploadStatus(StatusEvent What, string? RepairDate = null, string? RepairDescription = null);

/// <summary>A file read by its format's adapter: the format, its entries, or why it can't be read at all.</summary>
public sealed record UploadDocument(string? Format, IReadOnlyList<UploadEntry> Entries, string? Problem = null)
{
    public static UploadDocument Unreadable(string problem, string? format = null) => new(format, [], problem);
}

/// <summary>
/// Turns one file format into upload entries (design 8.1): the native format here, the battery system's export in
/// step 5b. Every entry then goes through the same save pipeline.
/// </summary>
public interface IUploadAdapter
{
    /// <summary>True when the file is in this adapter's format.</summary>
    bool Reads(JsonObject file);

    UploadDocument Read(JsonObject file);
}

/// <summary>
/// The app's own upload format (design 8.2): <c>{"format": "dpp-v3-upload/1", "batteries": [...]}</c>, each battery named
/// by <c>uniqueProductIdentifier</c> or <c>batterySerialNumber</c>, with an optional <c>measuredAt</c> and its
/// <c>values</c> by the schema's property names.
/// </summary>
public sealed class NativeUploadFormat : IUploadAdapter
{
    public const string Format = "dpp-v3-upload/1";

    public bool Reads(JsonObject file) => file["format"] is JsonValue;

    public UploadDocument Read(JsonObject file)
    {
        ArgumentNullException.ThrowIfNull(file);
        if (file["format"] is not JsonValue value || !value.TryGetValue<string>(out var format) || format != Format)
        {
            return UploadDocument.Unreadable($"the format {file["format"]?.ToJsonString()} isn't one this app reads; use \"{Format}\"");
        }

        if (file["batteries"] is not JsonArray batteries || batteries.Count == 0)
        {
            return UploadDocument.Unreadable("the file lists no batteries (\"batteries\": [...])", Format);
        }

        if (batteries.Count > UploadService.MaxBatteries)
        {
            return UploadDocument.Unreadable($"the file lists {batteries.Count} batteries; one upload takes at most {UploadService.MaxBatteries}", Format);
        }

        return new UploadDocument(Format, batteries.Select((battery, index) => Entry(index + 1, battery)).ToArray());
    }

    private static UploadEntry Entry(int number, JsonNode? battery)
    {
        if (battery is not JsonObject entry)
        {
            return Broken(number, string.Empty, "the entry isn't a JSON object");
        }

        var namedAs = Text(entry["uniqueProductIdentifier"]) ?? Text(entry["batterySerialNumber"]);
        if (namedAs is null)
        {
            return Broken(number, string.Empty, "name the battery by uniqueProductIdentifier or batterySerialNumber");
        }

        DateTime? measuredAt = null;
        if (entry["measuredAt"] is { } moment)
        {
            if (Text(moment) is not { } stamp
                || !DateTimeOffset.TryParse(stamp, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
            {
                return Broken(number, namedAs, "measuredAt isn't a time, such as 2026-10-27T10:02:59Z");
            }

            measuredAt = parsed.UtcDateTime;
        }

        var repair = Text(entry["reason"]) is { } reason
            ? reason == "repair" ? true : (bool?)null
            : false;
        if (repair is null)
        {
            return Broken(number, namedAs, "the only reason an entry gives is \"repair\"");
        }

        if ((entry["values"] is not JsonObject values || values.Count == 0) && repair != true)
        {
            return Broken(number, namedAs, "the entry has no values (\"values\": {...})");
        }

        var given = (entry["values"] as JsonObject ?? new JsonObject()).ToDictionary(pair => pair.Key, pair => pair.Value?.DeepClone(), StringComparer.Ordinal);
        UploadStatus? status = null;
        var named = given.Remove(StatusProperty, out var statusValue) ? StatusText(statusValue) : null;
        if (repair == true)
        {
            if (named is not null)
            {
                return Broken(number, namedAs, "a repair sets the battery status itself; leave batteryStatus out");
            }

            var date = Text(entry["repairDate"]);
            if (date is not null && !DateOnly.TryParseExact(date, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _))
            {
                return Broken(number, namedAs, "repairDate isn't a day, such as 2026-10-30");
            }

            status = new UploadStatus(StatusEvent.Repaired, date ?? measuredAt?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), Text(entry["repairDescription"]));
        }
        else if (statusValue is not null)
        {
            StatusEvent? what = named switch
            {
                "waste" => StatusEvent.Waste,
                "re-used" => StatusEvent.PlacedOnMarketAgain,
                "remanufactured" => StatusEvent.Remanufactured,
                "repurposed" => StatusEvent.Repurposed,
                _ => null,
            };
            if (what is null)
            {
                return Broken(number, namedAs, $"batteryStatus \"{named}\" can't be set by upload; it takes waste, re-used, remanufactured or repurposed, or a repair");
            }

            status = new UploadStatus(what.Value);
        }

        return new UploadEntry(number, namedAs, measuredAt, given, Status: status);
    }

    private const string StatusProperty = "batteryStatus";

    /// <summary>The battery status as a bare word or in the schema's own form.</summary>
    private static string? StatusText(JsonNode? value) => Text(value) ?? Text(value?["batteryStatusValues"]);

    private static UploadEntry Broken(int number, string namedAs, string problem) =>
        new(number, namedAs, null, new Dictionary<string, JsonNode?>(), problem);

    private static string? Text(JsonNode? node) =>
        node is JsonValue value && value.GetValueKind() == JsonValueKind.String && value.GetValue<string>().Trim() is { Length: > 0 } text ? text : null;
}
