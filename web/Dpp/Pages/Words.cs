using System.Globalization;

namespace BatteryPassWeb.Dpp.Pages;

/// <summary>Counts in plain English on the team pages: "1 value", "3 values", never "value(s)".</summary>
public static class Words
{
    /// <summary>The count and the word, singular for 1: <c>Count(3, "value")</c> is "3 values".</summary>
    /// <param name="many">The plural when it isn't <paramref name="one"/> plus "s".</param>
    public static string Count(int count, string one, string? many = null) =>
        $"{count.ToString(CultureInfo.InvariantCulture)} {(count == 1 ? one : many ?? one + "s")}";

    /// <summary>"is" or "are", to agree with a count.</summary>
    public static string Is(int count) => count == 1 ? "is" : "are";
}
