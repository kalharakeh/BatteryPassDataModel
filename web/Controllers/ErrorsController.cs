using BatteryPassWeb.Configuration;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BatteryPassWeb.Controllers;

/// <summary>The app's "not found" and error pages (finding U04), in its layout, open to everyone, for any method.</summary>
[AllowAnonymous]
[IgnoreAntiforgeryToken]
public sealed class ErrorsController : Controller
{
    /// <summary>The API's answer to an unexpected error, as its other errors are worded: no detail (review 1, point 4).</summary>
    public const string ApiError = "Something went wrong. Try again later.";

    /// <summary>
    /// An unexpected error, re-run here by the exception handler: no detail is shown (status 500). A call to the API gets
    /// the API's own form, <c>{"error": ...}</c>, never the page.
    /// </summary>
    [Route(ErrorPages.ErrorPath)]
    public IActionResult Error()
    {
        if (ErrorPages.OriginalPath(HttpContext).StartsWithSegments("/api"))
        {
            return new ObjectResult(new { error = ApiError }) { StatusCode = StatusCodes.Status500InternalServerError };
        }

        Response.StatusCode = StatusCodes.Status500InternalServerError;
        return View("~/Views/Shared/Error.cshtml");
    }

    /// <summary>
    /// The "not found" page with the search box (status 404), for an address no route matched: a 404 re-run here by
    /// <see cref="ErrorPages.UseErrorPages"/>; the API, files and wrong methods never come here.
    /// </summary>
    [HttpGet(ErrorPages.NotFoundPath)]
    [HttpHead(ErrorPages.NotFoundPath)]
    public IActionResult NotFoundPage()
    {
        Response.StatusCode = StatusCodes.Status404NotFound;
        return View("~/Views/Errors/NotFound.cshtml");
    }
}
