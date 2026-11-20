using Microsoft.AspNetCore.RateLimiting;

namespace BatteryPassWeb.Configuration;

/// <summary>
/// The answer over a rate limit (review 2 of N1, point 6): the API and the passports' .json addresses keep their JSON
/// error; a person on a page (the home search, the log-in page) gets a short sentence instead of bare JSON.
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
        if (path.StartsWithSegments("/api") || (path.Value ?? string.Empty).EndsWith(".json", StringComparison.OrdinalIgnoreCase))
        {
            await response.WriteAsJsonAsync(new { error = "Too many requests." }, cancellationToken);
            return;
        }

        response.ContentType = "text/plain; charset=utf-8";
        var searching = path.StartsWithSegments("/search") || path == "/";
        await response.WriteAsync(searching ? SearchWords : PageWords, cancellationToken);
    }
}
