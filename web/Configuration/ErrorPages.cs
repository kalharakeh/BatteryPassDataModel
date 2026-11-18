using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Hosting;

namespace BatteryPassWeb.Configuration;

/// <summary>
/// The app's error pages (finding U04): an unexpected error shows a page that says something went wrong, without any
/// detail, and an address that matches nothing shows the app's "not found" page. The same wiring serves the app and the
/// tests.
/// </summary>
public static class ErrorPages
{
    public const string ErrorPath = "/error";

    /// <summary>Outside Development, an unexpected error re-runs the request as <see cref="ErrorPath"/> (status 500).</summary>
    public static WebApplication UseErrorPages(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);
        if (!app.Environment.IsDevelopment())
        {
            app.UseExceptionHandler(ErrorPath);
        }

        return app;
    }

    /// <summary>
    /// An address no route matches gets the "not found" page (status 404). Only unmatched addresses: a page or the API
    /// that answers 404 itself, and a missing file, are left as they are.
    /// </summary>
    public static WebApplication MapNotFoundPage(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);
        app.MapFallbackToController(nameof(Controllers.ErrorsController.NotFoundPage), "Errors");
        return app;
    }
}
