using Microsoft.AspNetCore.Mvc.ActionConstraints;
using Microsoft.Extensions.Primitives;
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

    /// <summary>
    /// True when the Accept header rates application/json above zero and above text/html, a tie going to the page. Each
    /// type is rated by the most specific range that covers it (C3 round two, point 7): application/json by itself, else
    /// application/*, else */*; the page by text/html, else text/*, else */*. So "application/json;q=0.5, text/*" and
    /// "application/json, */*" get the page, and application/ld+json, which isn't application/json, gets the page too.
    /// </summary>
    public static bool PrefersJson(string? accept)
    {
        if (string.IsNullOrWhiteSpace(accept) || !MediaTypeHeaderValue.TryParseList(accept.Split(','), out var types))
        {
            return false;
        }

        double Quality(string type, string subtype)
        {
            foreach (var range in new[] { $"{type}/{subtype}", $"{type}/*", "*/*" })
            {
                var matches = types.Where(media => StringSegment.Equals(media.MediaType, range, StringComparison.OrdinalIgnoreCase)).ToArray();
                if (matches.Length > 0)
                {
                    return matches.Max(media => media.Quality ?? 1.0);
                }
            }

            return 0.0;
        }

        var json = Quality("application", "json");
        return json > 0 && json > Quality("text", "html");
    }
}
