namespace BatteryPassWeb.Services;

/// <summary>
/// Finding W12: when a live value was measured can't lie in the future. The pages, uploads and the API all refuse a time
/// more than <see cref="ClockTolerance"/> ahead of the app's clock, so a slow or fast clock elsewhere does no harm.
/// </summary>
public static class MeasuredAtRule
{
    public static readonly TimeSpan ClockTolerance = TimeSpan.FromMinutes(5);

    /// <summary>What the page says next to the values.</summary>
    public const string Words = "The time measured is in the future. Use the time the values were measured.";

    public static bool InTheFuture(DateTime measuredAtUtc, DateTime nowUtc) => measuredAtUtc.ToUniversalTime() > nowUtc.ToUniversalTime() + ClockTolerance;
}
