using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using BatteryPassWeb.Dpp.Store;

namespace BatteryPassWeb.Dpp.Pages;

/// <summary>
/// A battery's modules as the team types them, one per line, or as the battery system's JSON export lists them under
/// metadata.identifiers.deviceIdentifiers.childrenIdentifiers (decision 31).
/// </summary>
public static class ModuleList
{
    public const int MaxModules = 200;
    public const int MaxIdLength = 100;
    public const int MaxDescriptionLength = 300;

    /// <summary>The list as the team types it: "position; serial number; connectivity ID" per line.</summary>
    public static string ToText(IEnumerable<BatteryModule> modules) =>
        string.Join("\n", modules.OrderBy(module => module.Position).Select(module =>
            $"{module.Position.ToString(CultureInfo.InvariantCulture)}; {module.SerialNumber}; {module.ConnectivityId}"));

    /// <summary>
    /// One module per line: its position, serial number and connectivity ID, separated by semicolons or tabs (as pasted
    /// from a spreadsheet). One of the two IDs may be empty. An empty text clears the list.
    /// </summary>
    public static (IReadOnlyList<BatteryModule> Modules, string? Problem) FromText(string? text)
    {
        var lines = (text ?? string.Empty).Split('\n').Select(line => line.Trim()).ToArray();
        var modules = new List<BatteryModule>();
        for (var index = 0; index < lines.Length; index++)
        {
            if (lines[index].Length == 0)
            {
                continue;
            }

            var parts = lines[index].Split(';', '\t').Select(part => part.Trim()).ToArray();
            if (parts.Length != 3)
            {
                return ([], $"line {index + 1} needs a position, a serial number and a connectivity ID, separated by semicolons");
            }

            if (!int.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out var position))
            {
                return ([], $"line {index + 1} doesn't start with a position, such as 1");
            }

            modules.Add(new BatteryModule { Position = position, SerialNumber = OrNull(parts[1]), ConnectivityId = OrNull(parts[2]) });
        }

        return Check(modules);
    }

    /// <summary>
    /// The modules in the battery system's JSON export of this battery. The export must be this battery's: its device
    /// serial number is the battery's serial number (#11).
    /// </summary>
    public static (IReadOnlyList<BatteryModule> Modules, string? Problem) FromExport(byte[] file, string batterySerialNumber)
    {
        ArgumentNullException.ThrowIfNull(file);
        JsonNode? root;
        try
        {
            ReadOnlySpan<byte> json = file;
            root = JsonNode.Parse(json.StartsWith("﻿"u8) ? json[3..] : json);
        }
        catch (JsonException)
        {
            return ([], "the file isn't a JSON export");
        }

        if (root?["metadata"]?["identifiers"]?["deviceIdentifiers"] is not JsonObject device)
        {
            return ([], "the file has no module identifiers (metadata.identifiers.deviceIdentifiers)");
        }

        var exported = Text(device["serialNumber"]);
        if (!string.Equals(exported, batterySerialNumber.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            return ([], $"the file is for battery {exported ?? "without a serial number"}, not {batterySerialNumber}");
        }

        return FromDevice(device);
    }

    /// <summary>The modules an export lists under one device (its <c>childrenIdentifiers</c>), used by uploads (step 5b).</summary>
    public static (IReadOnlyList<BatteryModule> Modules, string? Problem) FromDevice(JsonObject device)
    {
        ArgumentNullException.ThrowIfNull(device);
        if (device["childrenIdentifiers"] is not JsonArray children || children.Count == 0)
        {
            return ([], "the file lists no modules");
        }

        var modules = new List<BatteryModule>();
        foreach (var child in children)
        {
            if (child is not JsonObject module || Position(module["position"]) is not { } position)
            {
                return ([], "a module in the file has no position");
            }

            modules.Add(new BatteryModule { Position = position, SerialNumber = Text(module["serialNumber"]), ConnectivityId = Text(module["connectivityId"]) });
        }

        return Check(modules);
    }

    private static (IReadOnlyList<BatteryModule> Modules, string? Problem) Check(List<BatteryModule> modules)
    {
        if (modules.Count > MaxModules)
        {
            return ([], $"a battery has at most {MaxModules} modules here");
        }

        foreach (var module in modules)
        {
            if (module.Position < 1)
            {
                return ([], "positions start at 1");
            }

            if (module.SerialNumber is null && module.ConnectivityId is null)
            {
                return ([], $"the module at position {module.Position} needs a serial number or a connectivity ID");
            }

            if ((module.SerialNumber?.Length ?? 0) > MaxIdLength || (module.ConnectivityId?.Length ?? 0) > MaxIdLength)
            {
                return ([], $"the module at position {module.Position} has an ID longer than {MaxIdLength} characters");
            }
        }

        if (modules.GroupBy(module => module.Position).FirstOrDefault(group => group.Count() > 1) is { } twice)
        {
            return ([], $"position {twice.Key} is listed twice");
        }

        return (modules.OrderBy(module => module.Position).ToArray(), null);
    }

    private static string? OrNull(string text) => text.Length == 0 ? null : text;

    private static string? Text(JsonNode? node) =>
        node is JsonValue value && value.TryGetValue<string>(out var text) ? OrNull(text.Trim()) : null;

    private static int? Position(JsonNode? node) =>
        node is JsonValue value && (value.TryGetValue<int>(out var number)
            || (value.TryGetValue<string>(out var text) && int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out number)))
            ? number
            : null;
}
