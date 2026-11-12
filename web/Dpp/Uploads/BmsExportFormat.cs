using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using BatteryPassWeb.Dpp.Pages;
using BatteryPassWeb.Dpp.Store;

namespace BatteryPassWeb.Dpp.Uploads;

/// <summary>
/// The battery system's JSON export, mapping <c>BMS2_ACO</c> (design 8.3, decisions 17, 28 and 31; step 5b). One export is
/// one battery: its serial number and export time from <c>metadata</c>, its live values from <c>values</c>, and its
/// module list from <c>metadata.identifiers.deviceIdentifiers.childrenIdentifiers</c>. Everything else in the export
/// isn't in the passport and is ignored.
/// </summary>
public sealed partial class BmsExportFormat : IUploadAdapter
{
    public const string Mapping = "BMS2_ACO";

    /// <summary>The mapping version the adapter was checked against; another version is read the same way, with a note.</summary>
    public const string CheckedVersion = "01.32.07";

    public const string NoTemperature = "the export has no pack temperature yet, so #87 is left as it is";
    public const string ChargingAboveZero = "the export's time spent charging in extreme temperatures is above zero and can't be split into above and below the boundary, so E3 and E4 are left as they are";

    public bool Reads(JsonObject file) => file["metadata"] is JsonObject metadata && metadata["mappingName"] is JsonValue;

    public UploadDocument Read(JsonObject file)
    {
        ArgumentNullException.ThrowIfNull(file);
        var metadata = (JsonObject)file["metadata"]!;
        var mapping = Text(metadata["mappingName"]);
        if (mapping != Mapping)
        {
            return UploadDocument.Unreadable($"the export's mapping \"{mapping}\" isn't one this app reads; it reads {Mapping}");
        }

        var format = $"{Mapping} {Text(metadata["mappingVersion"]) ?? "(no version)"}";
        var device = metadata["identifiers"]?["deviceIdentifiers"] as JsonObject;
        var serial = Text(device?["serialNumber"]);
        if (serial is null)
        {
            return new UploadDocument(format, [new UploadEntry(1, string.Empty, null, new Dictionary<string, JsonNode?>(), "the export has no serial number (metadata.identifiers.deviceIdentifiers.serialNumber)")]);
        }

        DateTime? measuredAt = null;
        if (metadata["exportedAt"] is { } exported)
        {
            if (Text(exported) is not { } stamp || !DateTimeOffset.TryParse(stamp, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
            {
                return new UploadDocument(format, [new UploadEntry(1, serial, null, new Dictionary<string, JsonNode?>(), "the export's exportedAt isn't a time")]);
            }

            measuredAt = parsed.UtcDateTime;
        }

        var notes = new List<string>();
        if (Text(metadata["mappingVersion"]) != CheckedVersion)
        {
            notes.Add($"read as mapping version {CheckedVersion}, the one this app was checked against");
        }

        var values = new Dictionary<string, JsonNode?>(StringComparer.Ordinal);
        var exportValues = file["values"] as JsonObject ?? new JsonObject();

        // #63: the readable text, e.g. "27.1 %", as a whole percent; the raw number is on another scale.
        if (Formatted(exportValues, "stateOfCharge") is { } charge)
        {
            if (PercentText().Match(charge) is { Success: true } percent)
            {
                values["stateOfChargeSoC"] = (int)Math.Round(double.Parse(percent.Groups[1].Value, CultureInfo.InvariantCulture), MidpointRounding.AwayFromZero);
            }
            else
            {
                notes.Add($"the export's state of charge \"{charge}\" isn't a percentage, so #63 is left as it is");
            }
        }

        // #83: whole cycles completed.
        if (Raw(exportValues, "equivalentChargeAndDischargeCycles") is { } cycles)
        {
            values["numberOfFullChargingAndDischargingCycles"] = (int)Math.Floor(cycles);
        }

        notes.Add(NoTemperature);

        // E1 and E2: seconds, as whole minutes completed (decision 28).
        if (Raw(exportValues, "timeSpentInExtremeHighTemperature") is { } high)
        {
            values["timeSpentInExtremeTemperaturesAboveBoundary"] = Minutes(high);
        }

        if (Raw(exportValues, "timeSpentInExtremeLowTemperature") is { } low)
        {
            values["timeSpentInExtremeTemperaturesBelowBoundary"] = Minutes(low);
        }

        // E3 and E4: one charging time that is always zero (charging in extreme temperatures is prevented).
        if (Raw(exportValues, "timeSpentChargingInExtremeTemperature") is { } charging)
        {
            if (charging == 0)
            {
                values["timeSpentChargingDuringExtremeTemperaturesAboveBoundary"] = 0;
                values["timeSpentChargingDuringExtremeTemperaturesBelowBoundary"] = 0;
            }
            else
            {
                notes.Add(ChargingAboveZero);
            }
        }

        // #17: year and month (decision 18), readable only when the export has it. It only fills a battery that has none
        // (a new draft): once set, the team corrects it on the battery's page (decision 35, Q15).
        if (Formatted(exportValues, "productionDate") is { } produced && YearMonth().IsMatch(produced))
        {
            values["manufacturingDate"] = produced;
        }

        // The remaining capacity isn't passport data; it is kept to suggest the capacity fade (J-11, Q16).
        decimal? remaining = null;
        if (Formatted(exportValues, "remainingCapacityAh") is { } capacity && AmpereHours().Match(capacity) is { Success: true } ah)
        {
            remaining = decimal.Parse(ah.Groups[1].Value, CultureInfo.InvariantCulture);
        }

        IReadOnlyList<BatteryModule> modules = [];
        string? problem = null;
        if (device is not null)
        {
            (modules, problem) = ModuleList.FromDevice(device);
        }

        if (problem is not null)
        {
            notes.Add($"the module list wasn't read: {problem}");
        }

        return new UploadDocument(format, [new UploadEntry(1, serial, measuredAt, values, Modules: modules, Notes: notes, FillOnly: ["manufacturingDate"], RemainingCapacityAh: remaining)]);
    }

    private static int Minutes(double seconds) => (int)Math.Floor(seconds / 60);

    private static string? Formatted(JsonObject values, string name) => Text(values[name]?["formatted"]);

    private static double? Raw(JsonObject values, string name) =>
        values[name]?["raw"] is JsonValue raw && raw.GetValueKind() == JsonValueKind.Number
            && double.Parse(raw.ToJsonString(), CultureInfo.InvariantCulture) is var number && number >= 0
            ? number
            : null;

    private static string? Text(JsonNode? node) =>
        node is JsonValue value && value.GetValueKind() == JsonValueKind.String && value.GetValue<string>().Trim() is { Length: > 0 } text ? text : null;

    [GeneratedRegex(@"^(\d{1,3}(?:\.\d+)?)\s*%$")]
    private static partial Regex PercentText();

    [GeneratedRegex(@"^\d{4}-(0[1-9]|1[0-2])$")]
    private static partial Regex YearMonth();

    [GeneratedRegex(@"^(\d{1,6}(?:\.\d+)?)\s*Ah$")]
    private static partial Regex AmpereHours();
}
