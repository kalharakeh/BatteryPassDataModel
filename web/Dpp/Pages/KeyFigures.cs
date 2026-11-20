using System.Text.Json.Nodes;

namespace BatteryPassWeb.Dpp.Pages;

/// <summary>
/// One key figure: what it is, its value, the value's small words, a line under it, and the attribute whose full entry it
/// links to on the detail page.
/// </summary>
public sealed record KeyFigure(string Icon, string Label, string Value, string? Small = null, string? Under = null, int Number = 0)
{
    /// <summary>The full entry's anchor in the detail page's data sections.</summary>
    public string Anchor => KeyFigures.Anchor(Number);
}

/// <summary>
/// "Key figures" (redesign R5, the owner's item 4, decision 37): the public values a reader wants first, as tiles, on the
/// public summary and on the detail page under the hero. They replace the summary's "Summary report" card (power, carbon
/// pie, recycled content). Public attributes only, read from the passport as everyone sees it, so the card is the same
/// for every reader on both pages. Nothing is worked out: power is the stored watts shown in kW. A tile shows only when
/// its main value is there.
/// </summary>
public sealed record KeyFigures(
    IReadOnlyList<KeyFigure> Using,
    KeyFigure? Carbon,
    string? CarbonClass,
    IReadOnlyList<ChartPart> CarbonStages,
    IReadOnlyList<ChartPart> Recycled,
    decimal? Renewable,
    IReadOnlyList<KeyFigure> Safety)
{
    /// <summary>The carbon stages' colours, in the order of <see cref="CarbonStages"/> (the old pie's).</summary>
    public static IReadOnlyList<string> StageColours { get; } = ["#22577a", "#38a3a5", "#f2a541", "#57cc99"];

    /// <summary>A value's entry in the detail page's data sections (its id there).</summary>
    public static string Anchor(int number) => $"value-{number}";

    /// <summary>Where the recycled content tile links: the first share the passport has (#49 to #52), else the renewable share.</summary>
    public int RecycledNumber => Recycled.Count > 0 ? RecycledFirst : 53;

    private int RecycledFirst { get; init; } = 49;

    /// <summary>Whether there is anything to show.</summary>
    public bool Any => Using.Count > 0 || Carbon is not null || Recycled.Count > 0 || Renewable is not null || Safety.Count > 0;

    /// <param name="find">A public attribute's value in the passport, by its number.</param>
    /// <param name="words">A public attribute's first line in words, by its number (e.g. #19's "8 years").</param>
    public static KeyFigures From(Func<int, JsonNode?> find, Func<int, string?> words, IReadOnlyList<ChartPart> carbonStages, IReadOnlyList<ChartPart> recycled)
    {
        ArgumentNullException.ThrowIfNull(find);
        ArgumentNullException.ThrowIfNull(words);
        decimal? Number(int attribute, string? name = null) => ValueWords.Decimal(name is null ? find(attribute) : find(attribute)?[name]);
        string N(decimal value) => ValueWords.Number(value);
        string Watts(decimal watts) => watts >= 1000 ? $"{N(Math.Round(watts / 1000, 1))} kW" : $"{N(watts)} W";
        string? Join(params string?[] parts) => parts.Any(part => part is not null) ? string.Join(" · ", parts.OfType<string>()) : null;

        var used = new List<KeyFigure>();
        if (Number(57, "amperehourMiliamperehourValue") is { } capacity)
        {
            var nominal = Number(66, "voltValue");
            var (low, high) = (Number(64, "voltValue"), Number(65, "voltValue"));
            used.Add(new("battery", "Capacity and voltage", $"{N(capacity)} Ah", nominal is { } volts ? $"at {N(volts)} V" : null,
                low is { } min && high is { } max ? $"Works between {N(min)} and {N(max)} V" : null, 57));
        }

        if (Number(67, "wattValueAt80PercentStateOfCharge") is { } at80)
        {
            var at20 = Number(67, "wattValueAt20PercentStateOfCharge");
            var most = Number(70, "wattValue");
            used.Add(new("bolt", "Power", Watts(at80), "at 80 % charge",
                Join(at20 is { } low ? $"{Watts(low)} at 20 % charge" : null, most is { } max ? $"at most {Watts(max)}" : null), 67));
        }

        if (Number(82) is { } cycles)
        {
            var rate = Number(85, "amperePerAmpereHourValue");
            used.Add(new("reuse", "Expected lifetime", N(cycles), "cycles",
                Join(rate is { } c ? $"Tested at {N(c)} C" : null, words(19) is { } warranty ? $"warranty {warranty}" : null), 82));
        }

        if (Number(71, "percentageValue") is { } efficiency)
        {
            used.Add(new("gauge", "Round-trip efficiency", $"{N(efficiency)} %", null,
                Number(72, "percentageValue") is { } half ? $"{N(half)} % at half its cycle life" : null, 71));
        }

        var carbon = Number(30, "kgCO2EquivalentPerKilowattHourValue") is { } perKwh
            ? new KeyFigure("leaf", "Carbon footprint", N(perKwh), "kg CO₂e per kWh", Number: 30)
            : null;

        var safety = new List<KeyFigure>();
        if (words(41) is { } chemistry)
        {
            var critical = find(42) is JsonArray materials ? materials.Select(material => material?.GetValue<string>()).OfType<string>().ToArray() : [];
            safety.Add(new("flask", "Chemistry", chemistry, null,
                critical.Length > 0 ? $"Critical raw materials: {string.Join(", ", critical.Select(material => material.ToLowerInvariant()))}" : null, 41));
        }

        if (Number(88, "celsiusValue") is { } idleLow && Number(89, "celsiusValue") is { } idleHigh)
        {
            var agent = find(26) is JsonArray agents ? agents.FirstOrDefault() : find(26);
            var fire = agent?["extinguishingAgent"]?.GetValue<string>();
            var fireClass = agent?["agentFireClass"]?.GetValue<string>();
            safety.Add(new("thermo", "Storage and fire", $"{N(idleLow).Replace("-", "−", StringComparison.Ordinal)} to {N(idleHigh)} °C", "when idle",
                fire is null ? null : $"In a fire: {fire.ToLowerInvariant()}{(fireClass is null ? null : $" (fire class {fireClass})")}", 88));
        }

        var carbonClass = find(35) is JsonValue value && value.TryGetValue<string>(out var text) ? text : null;
        var firstShare = new[] { 49, 50, 51, 52 }.FirstOrDefault(number => Number(number, "percentageValue") is not null, 49);
        return new KeyFigures(used, carbon, carbonClass, carbonStages, recycled, Number(53, "percentageValue"), safety) { RecycledFirst = firstShare };
    }
}
