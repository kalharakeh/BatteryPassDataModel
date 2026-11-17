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

    /// <summary>
    /// A time sent by the API or in an upload (H1 round two): one with an offset is honoured; one without is UTC, on every
    /// server, never the server's own zone.
    /// </summary>
    public static bool TryReadSent(string? text, out DateTime utc)
    {
        utc = default;
        if (string.IsNullOrWhiteSpace(text)
            || !DateTimeOffset.TryParse(text, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.AssumeUniversal | System.Globalization.DateTimeStyles.AdjustToUniversal, out var moment))
        {
            return false;
        }

        utc = moment.UtcDateTime;
        return true;
    }

    /// <summary>The widest offset a time zone has: a browser's offset beyond it isn't one.</summary>
    public const int MaxOffsetMinutes = 14 * 60;

    public static bool InTheFuture(DateTime measuredAtUtc, DateTime nowUtc) => measuredAtUtc.ToUniversalTime() > nowUtc.ToUniversalTime() + ClockTolerance;
}
