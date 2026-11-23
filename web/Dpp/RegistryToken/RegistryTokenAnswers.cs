using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Net.Http.Headers;

namespace BatteryPassWeb.Dpp.RegistryToken;

/// <summary>
/// The answers to a refused token, as the EU's guide writes them (§8.3 to §8.5): 401 with
/// <c>WWW-Authenticate: Bearer error="invalid_token"</c>, 403 with <c>error="insufficient_scope"</c> for the wrong
/// scope, a plain 403 for another organisation. A problem in JSON, never a passport value, never the organisation the
/// site expects.
/// </summary>
public static partial class RegistryTokenAnswers
{
    public const string ProblemType = "application/problem+json";

    public const string InvalidToken = "The presented DPP Registry access token is invalid.";
    public const string NotBearer = "Send the DPP Registry access token in one Authorization header, with the Bearer scheme.";
    public const string WrongScope = "The access token does not grant the required operation.";
    public const string NotAuthorised = "The requested resource is not authorized for this token.";

    /// <summary>The answer for a refused token; its caching and Vary headers are the caller's.</summary>
    public static IActionResult Refusal(HttpContext context, RegistryTokenResult result)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(result);
        var (status, title, detail, challenge) = result.Verdict switch
        {
            RegistryTokenVerdict.InsufficientScope => (StatusCodes.Status403Forbidden, "Forbidden", WrongScope, $"Bearer error=\"insufficient_scope\", scope=\"{RegistryTokenCheck.Scope}\""),
            RegistryTokenVerdict.RequestorRefused => (StatusCodes.Status403Forbidden, "Forbidden", NotAuthorised, null),
            RegistryTokenVerdict.OtherScheme => (StatusCodes.Status401Unauthorized, "Unauthorized", NotBearer, "Bearer error=\"invalid_request\""),
            _ => (StatusCodes.Status401Unauthorized, "Unauthorized", InvalidToken, "Bearer error=\"invalid_token\""),
        };
        if (challenge is not null)
        {
            context.Response.Headers[HeaderNames.WWWAuthenticate] = challenge;
        }

        var problem = new JsonObject
        {
            ["title"] = title,
            ["status"] = status,
            ["detail"] = detail,
            ["correlationId"] = CorrelationId(context),
        };
        return new ContentResult { StatusCode = status, Content = problem.ToJsonString(), ContentType = ProblemType };
    }

    /// <summary>The caller's X-Correlation-Id when it is a short plain ID, else the request's own trace ID.</summary>
    public static string CorrelationId(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var given = context.Request.Headers["X-Correlation-Id"].ToString();
        return PlainId().IsMatch(given) ? given : context.TraceIdentifier;
    }

    [GeneratedRegex("^[A-Za-z0-9._:-]{1,128}$")]
    private static partial Regex PlainId();
}
