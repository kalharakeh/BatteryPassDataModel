using BatteryPassWeb.Configuration;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BatteryPassWeb.Controllers;

/// <summary>The app's "not found" and error pages (finding U04), in its layout, open to everyone, for any method.</summary>
[AllowAnonymous]
[IgnoreAntiforgeryToken]
public sealed class ErrorsController : Controller
{
    /// <summary>An unexpected error, re-run here by the exception handler: no detail is shown (status 500).</summary>
    [Route(ErrorPages.ErrorPath)]
    public IActionResult Error()
    {
        Response.StatusCode = StatusCodes.Status500InternalServerError;
        return View("~/Views/Shared/Error.cshtml");
    }

    /// <summary>
    /// An address no route matches (the fallback, <see cref="ErrorPages.MapNotFoundPage"/>): the "not found" page with the
    /// search box (status 404). An address under /api keeps its plain 404 for the systems that call it.
    /// </summary>
    public IActionResult NotFoundPage()
    {
        if (Request.Path.StartsWithSegments("/api"))
        {
            return NotFound();
        }

        Response.StatusCode = StatusCodes.Status404NotFound;
        return View("~/Views/Errors/NotFound.cshtml");
    }
}
