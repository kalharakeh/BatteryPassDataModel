namespace BatteryPassWeb.Dpp.Access;

/// <summary>
/// What is demo stays in the demo (finding R03, decision 32): a demo account's or demo token's roles and memberships
/// count only on demo batteries. On any other battery it is a visitor: public data only, no actions. The one rule;
/// <see cref="DppReaders"/> marks the reader, and whoever meets a reader with a battery asks here.
/// </summary>
public static class DemoScope
{
    public const string RealBattery = "Demo accounts and demo tokens work only on demo batteries.";

    /// <summary>Whether the reader's roles and memberships count on this battery.</summary>
    public static bool Reaches(Reader reader, bool batteryIsDemo)
    {
        ArgumentNullException.ThrowIfNull(reader);
        return !reader.DemoOnly || batteryIsDemo;
    }

    /// <summary>The reader as it counts on this battery: itself, or a visitor (who keeps its name for the logs).</summary>
    public static Reader On(Reader reader, bool batteryIsDemo) =>
        Reaches(reader, batteryIsDemo) ? reader : Reader.Anonymous with { Who = reader.Who };
}
