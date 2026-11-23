using Microsoft.AspNetCore.Mvc.ActionConstraints;
using Microsoft.Extensions.Primitives;
using Microsoft.Net.Http.Headers;

namespace BatteryPassWeb.Dpp.Api;

/// <summary>
/// Picks the data answer of a page address unless the request asks for the page (finding R07; R3, the manager's decision
/// 6). A browser, which names text/html first, gets the page as before; a program gets the data, whatever else its Accept
/// header lists. Of two actions on the same address, the one with this mark is chosen when it holds, the other one
/// otherwise.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class PrefersJsonAttribute : Attribute, IActionConstraint
{
    public int Order => 0;

    public bool Accept(ActionConstraintContext context) => AnswersData(context.RouteContext.HttpContext.Request);

    /// <summary>Whether a passport's page address answers this request with data: an Accept header that doesn't ask for the page.</summary>
    public static bool AnswersData(HttpRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        return PrefersJson(request.Headers.Accept.ToString());
    }

    /// <summary>
    /// False only when the Accept header names the page, text/html or text/*, with a q above zero and at least as high as
    /// JSON's. True otherwise: application/json, application/ld+json, "application/json, */*", "*/*" alone and no Accept
    /// header at all get the data. Each type is rated by the most specific range that covers it (C3 round two, point 7):
    /// JSON by application/json or application/ld+json, else application/*, else */*; the page by text/html, else text/*,
    /// never by */*. A range that can't be read, or whose q isn't a number from 0 to 1, is skipped (C3 round three, point 4).
    /// </summary>
    public static bool PrefersJson(string? accept)
    {
        if (string.IsNullOrWhiteSpace(accept))
        {
            return true;
        }

        var types = accept.Split(',')
            .Select(part => MediaTypeHeaderValue.TryParse(part, out var range) ? range : null)
            .OfType<MediaTypeHeaderValue>()
            .Where(Readable)
            .ToArray();

        double? Rated(params string[] ranges)
        {
            foreach (var name in ranges)
            {
                var matches = types.Where(media => StringSegment.Equals(media.MediaType, name, StringComparison.OrdinalIgnoreCase)).ToArray();
                if (matches.Length > 0)
                {
                    return matches.Max(media => media.Quality ?? 1.0);
                }
            }

            return null;
        }

        var page = Rated("text/html", "text/*");
        if (page is not > 0)
        {
            return true; // the page isn't named, or is refused
        }

        var json = Math.Max(
            Rated("application/json", "application/*", "*/*") ?? 0.0,
            Rated("application/ld+json", "application/*", "*/*") ?? 0.0);
        return json > page;
    }

    /// <summary>A range without q, or with a q that reads as a number from 0 to 1.</summary>
    private static bool Readable(MediaTypeHeaderValue range)
    {
        var q = range.Parameters.FirstOrDefault(parameter => StringSegment.Equals(parameter.Name, "q", StringComparison.OrdinalIgnoreCase));
        return q is null || range.Quality is >= 0 and <= 1;
    }
}
