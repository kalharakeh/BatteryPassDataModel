using Microsoft.AspNetCore.Mvc.ActionConstraints;
using Microsoft.Net.Http.Headers;

namespace BatteryPassWeb.Dpp.Api;

/// <summary>
/// Picks the data answer of a page address when the request asks for JSON (finding R07): its Accept header rates
/// application/json above text/html. A browser, which asks for HTML first, gets the page as before. Of two actions on the
/// same address, the one with this mark is chosen when it holds, the other one otherwise.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class PrefersJsonAttribute : Attribute, IActionConstraint
{
    public int Order => 0;

    public bool Accept(ActionConstraintContext context) =>
        PrefersJson(context.RouteContext.HttpContext.Request.Headers.Accept.ToString());

    /// <summary>True when the Accept header rates application/json above zero and above text/html (a tie goes to the page).</summary>
    public static bool PrefersJson(string? accept)
    {
        if (string.IsNullOrWhiteSpace(accept) || !MediaTypeHeaderValue.TryParseList(accept.Split(','), out var types))
        {
            return false;
        }

        double Quality(string mediaType) => types
            .Where(type => type.MediaType.Equals(mediaType, StringComparison.OrdinalIgnoreCase))
            .Select(type => type.Quality ?? 1.0)
            .DefaultIfEmpty(0.0)
            .Max();

        var json = Quality("application/json");
        return json > 0 && json > Quality("text/html");
    }
}
