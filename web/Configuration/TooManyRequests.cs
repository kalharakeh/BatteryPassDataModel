using Microsoft.AspNetCore.RateLimiting;

namespace BatteryPassWeb.Configuration;

/// <summary>
/// The answer over a rate limit (review 2 of N1, point 6): the API, the passports' .json addresses and a request that
/// asks for JSON keep their JSON error; a person on a page (the home search, the log-in page) gets a short sentence instead of bare JSON.
/// </summary>
public static class TooManyRequests
{
    public const string SearchWords = "Too many searches from your address. Try again in a minute.";

    public const string PageWords = "Too many tries from your address. Try again in a minute.";

    public static async ValueTask WriteAsync(OnRejectedContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        var http = context.HttpContext;
        var response = http.Response;
        if (response.HasStarted)
        {
            return;
        }

        response.StatusCode = StatusCodes.Status429TooManyRequests;
        var path = http.Request.Path;
        // JSON for the API, the .json addresses, and a passport address that answers data by the same rule its data answer
        // uses (PrefersJson; point 10 of round three). Only the passport addresses: since R3 no Accept header or "*/*" gets
        // a passport's data, but the search and the sign-in page keep their sentence.
        if (path.StartsWithSegments("/api")
            || (path.Value ?? string.Empty).EndsWith(".json", StringComparison.OrdinalIgnoreCase)
            || (path.StartsWithSegments("/dpp") && BatteryPassWeb.Dpp.Api.PrefersJsonAttribute.AnswersData(http.Request)))
        {
            await response.WriteAsJsonAsync(new { error = "Too many requests." }, cancellationToken);
            return;
        }

        response.ContentType = "text/plain; charset=utf-8";
        var searching = path.StartsWithSegments("/search") || path == "/";
        await response.WriteAsync(searching ? SearchWords : PageWords, cancellationToken);
    }
}
