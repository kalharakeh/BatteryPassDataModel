namespace BatteryPassWeb.Dpp.Demo;

/// <summary>
/// ENABLE_DEMO_DATA (finding R03): whether this site has demo data. Outside Production it is on unless set to false; in
/// Production it is off unless set to true. Off: "Reset demo data" isn't offered and refuses, demo accounts can't sign
/// in and demo tokens are refused; "Clear demo data" stays, so a site can get rid of what it has.
/// </summary>
public sealed class DemoDataSwitch(bool enabled)
{
    public const string Off = "Demo data is switched off on this site.";
    public const string TokenOff = "Demo tokens are switched off on this site.";

    /// <summary>Where the team's Clusters page finds whether the switch is on.</summary>
    public const string ViewDataKey = "DemoDataEnabled";

    public static DemoDataSwitch On { get; } = new(true);

    public bool Enabled { get; } = enabled;

    public static bool Parse(string? value, bool isProduction)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return !isProduction;
        }

        var normalized = value.Trim();
        return normalized.Equals("true", StringComparison.OrdinalIgnoreCase)
            || normalized.Equals("1", StringComparison.OrdinalIgnoreCase)
            || normalized.Equals("yes", StringComparison.OrdinalIgnoreCase)
            || normalized.Equals("on", StringComparison.OrdinalIgnoreCase);
    }
}
