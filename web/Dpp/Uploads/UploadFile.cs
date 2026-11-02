using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace BatteryPassWeb.Dpp.Uploads;

/// <summary>
/// One battery's entry in an upload, whatever the file's format: how the file names the battery, when its values were
/// measured, and the values by the schema's property names (design 8.2).
/// </summary>
/// <param name="Number">The entry's place in the file, from 1.</param>
/// <param name="NamedAs">The battery ID (#6) or serial number (#11) the file names the battery by.</param>
/// <param name="MeasuredAtUtc">When every value of the entry was generated; the upload time when null.</param>
/// <param name="Problem">Why the entry can't be used as it is; null when it can.</param>
public sealed record UploadEntry(
    int Number,
    string NamedAs,
    DateTime? MeasuredAtUtc,
    IReadOnlyDictionary<string, JsonNode?> Values,
    string? Problem = null);

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

        if (entry["values"] is not JsonObject values || values.Count == 0)
        {
            return Broken(number, namedAs, "the entry has no values (\"values\": {...})");
        }

        return new UploadEntry(
            number,
            namedAs,
            measuredAt,
            values.ToDictionary(pair => pair.Key, pair => pair.Value?.DeepClone(), StringComparer.Ordinal));
    }

    private static UploadEntry Broken(int number, string namedAs, string problem) =>
        new(number, namedAs, null, new Dictionary<string, JsonNode?>(), problem);

    private static string? Text(JsonNode? node) =>
        node is JsonValue value && value.GetValueKind() == JsonValueKind.String && value.GetValue<string>().Trim() is { Length: > 0 } text ? text : null;
}
