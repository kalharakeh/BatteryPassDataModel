using System.Globalization;
using System.Text;
using System.Text.Json.Nodes;

namespace BatteryPassWeb.Dpp.Uploads;

/// <summary>
/// Sample files for "Upload a file" (decision 35, Q24 and Q25): an upload file in the app's format and a battery system
/// export (mapping BMS2_ACO), for a demo battery only. The help page offers them to the team and cluster admins; the
/// demo data sends them as its two demo uploads. The export's modules are named as the demo seed names them, so it
/// leaves a demo battery's module list as it is.
/// </summary>
public static class UploadSamples
{
    /// <summary>The demo battery the help page's samples are for.</summary>
    public const string Serial = "DEMO-C7M-0001";

    public const string UploadFileName = "upload-file-DEMO-C7M-0001.json";

    public const string ExportFileName = "battery-system-export-DEMO-C7M-0001.json";

    /// <summary>The help page's upload file: live values and a repair with one re-stated value (design 8.2).</summary>
    public static string UploadFile(DateTime measuredAtUtc) => new JsonObject
    {
        ["format"] = NativeUploadFormat.Format,
        ["batteries"] = new JsonArray(new JsonObject
        {
            ["batterySerialNumber"] = Serial,
            ["measuredAt"] = Stamp(measuredAtUtc),
            ["reason"] = "repair",
            ["repairDescription"] = "Cell module 2 replaced after a cooling fault.",
            ["values"] = new JsonObject
            {
                ["stateOfChargeSoC"] = 64,
                ["temperatureInformation"] = 22,
                ["capacityFade"] = 7,
            },
        }),
    }.ToJsonString(new System.Text.Json.JsonSerializerOptions { WriteIndented = true });

    /// <summary>An upload file of live values only, as a cluster admin might send one (the demo data's second upload).</summary>
    public static string LiveValuesFile(string serial, DateTime measuredAtUtc, int stateOfCharge, int temperature) => new JsonObject
    {
        ["format"] = NativeUploadFormat.Format,
        ["batteries"] = new JsonArray(new JsonObject
        {
            ["batterySerialNumber"] = serial,
            ["measuredAt"] = Stamp(measuredAtUtc),
            ["values"] = new JsonObject { ["stateOfChargeSoC"] = stateOfCharge, ["temperatureInformation"] = temperature },
        }),
    }.ToJsonString();

    /// <summary>
    /// A battery system export of one battery, with made-up values in the shape of mapping BMS2_ACO 01.32.07: the state of
    /// charge as its readable text, the cycles, the extreme-temperature times in seconds, the remaining capacity, and the
    /// module list as the demo seed names it.
    /// </summary>
    public static JsonObject Export(string serial, DateTime exportedAtUtc, int modules)
    {
        var children = new JsonArray();
        for (var position = 1; position <= modules; position++)
        {
            children.Add(new JsonObject
            {
                ["serialNumber"] = $"{serial}-M{position:D2}",
                ["connectivityId"] = $"CAN-{serial}-{position:D2}",
                ["position"] = position,
            });
        }

        static JsonObject Value(JsonNode raw, JsonNode formatted) => new() { ["raw"] = raw, ["formatted"] = formatted };
        var produced = new JsonArray(Encoding.ASCII.GetBytes("2026-07").Select(code => (JsonNode)JsonValue.Create((int)code)!).ToArray());
        return new JsonObject
        {
            ["version"] = 1,
            ["metadata"] = new JsonObject
            {
                ["exportedAt"] = Stamp(exportedAtUtc),
                ["identifiers"] = new JsonObject
                {
                    ["deviceIdentifiers"] = new JsonObject { ["serialNumber"] = serial, ["position"] = 1, ["childrenIdentifiers"] = children },
                },
                ["mappingName"] = BmsExportFormat.Mapping,
                ["mappingVersion"] = BmsExportFormat.CheckedVersion,
            },
            ["values"] = new JsonObject
            {
                ["remainingCapacityAh"] = Value(131, "131.0 Ah"),
                ["productionDate"] = Value(produced, "2026-07"),
                ["equivalentChargeAndDischargeCycles"] = Value(418.6, "418.6"),
                ["timeSpentInExtremeHighTemperature"] = Value(0, "0.000 s"),
                ["timeSpentInExtremeLowTemperature"] = Value(0, "0.000 s"),
                ["timeSpentChargingInExtremeTemperature"] = Value(0, "0.000 s"),
                ["stateOfCharge"] = Value(47616, "74.4 %"),
            },
        };
    }

    private static string Stamp(DateTime utc) => utc.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture);
}
