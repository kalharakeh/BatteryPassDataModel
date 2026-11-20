using Microsoft.AspNetCore.Diagnostics;

namespace BatteryPassWeb.Configuration;

/// <summary>
/// The app's error pages (finding U04): an unexpected error shows a page that says something went wrong, without any
/// detail, and an address that matches nothing shows the app's "not found" page. The same wiring serves the app and the
/// tests.
/// </summary>
/// <remarks>
/// Review 1, point 4: the "not found" page is a re-run of a 404 that no route answered, not a catch-all route. A known
/// address asked with the wrong method keeps its 405 (its endpoint is set), a page or API call that answers 404 itself
/// keeps its own answer, and so do a missing file and anything under /api.
/// </remarks>
public static class ErrorPages
{
    public const string ErrorPath = "/error";

    public const string NotFoundPath = "/not-found";

    /// <summary>Set on a request no route matched; only such a request's 404 gets the "not found" page.</summary>
    private const string NothingMatched = "bp-nothing-matched";

    /// <summary>
    /// Outside Development, an unexpected error re-runs the request as <see cref="ErrorPath"/> (status 500). Everywhere,
    /// a 404 that no route answered re-runs as <see cref="NotFoundPath"/>.
    /// </summary>
    public static WebApplication UseErrorPages(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);
        if (!app.Environment.IsDevelopment())
        {
            app.UseExceptionHandler(ErrorPath);
        }

        app.UseStatusCodePages(async status =>
        {
            var http = status.HttpContext;
            if (http.Response.StatusCode != StatusCodes.Status404NotFound || !http.Items.ContainsKey(NothingMatched) || !ShowsPage(http.Request))
            {
                return;
            }

            var path = http.Request.Path;
            var query = http.Request.QueryString;
            http.Items.Remove(NothingMatched);
            http.SetEndpoint(null);
            http.Request.RouteValues.Clear();
            http.Request.Path = NotFoundPath;
            http.Request.QueryString = QueryString.Empty;
            try
            {
                await status.Next(http);
            }
            finally
            {
                http.Request.Path = path;
                http.Request.QueryString = query;
            }
        });
        return app;
    }

    /// <summary>
    /// Marks a request no route matched (it runs after routing), so <see cref="UseErrorPages"/> may answer its 404 with the
    /// "not found" page. The name stays from when this was a catch-all route.
    /// </summary>
    public static WebApplication MapNotFoundPage(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);
        app.Use(async (http, next) =>
        {
            if (http.GetEndpoint() is null)
            {
                http.Items[NothingMatched] = true;
            }

            await next(http);
        });
        return app;
    }

    /// <summary>The address of the request the exception handler re-ran, or the request's own.</summary>
    public static PathString OriginalPath(HttpContext http)
    {
        ArgumentNullException.ThrowIfNull(http);
        return http.Features.Get<IExceptionHandlerPathFeature>()?.Path is { Length: > 0 } failed ? new PathString(failed) : http.Request.Path;
    }

    /// <summary>A page a person asked for: GET or HEAD, not the API, not a file (a dot in the last part).</summary>
    private static bool ShowsPage(HttpRequest request) =>
        (HttpMethods.IsGet(request.Method) || HttpMethods.IsHead(request.Method))
        && !request.Path.StartsWithSegments("/api")
        && !(request.Path.Value ?? string.Empty).Split('/').Last().Contains('.', StringComparison.Ordinal);
}
