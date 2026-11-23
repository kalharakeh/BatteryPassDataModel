using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace BatteryPassWeb.Dpp.RegistryToken;

/// <summary>
/// Marks a passport's data address that the EU registry reads with its token (R3, decision 94): <c>/dpp/{id}</c> asked
/// for data, <c>/dpp/{id}.json</c> and the two version addresses. Only these check a token; the battery ID's address
/// <c>/25S/…</c> is a resolver that redirects whatever the headers say.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class RegistryReadableAttribute : Attribute;

/// <summary>What a request to a passport's data address carries, and what the token check made of it.</summary>
public static class RegistryTokenRequest
{
    /// <summary>The name a token would have in a query string (RFC 6750 §2.3) or a cookie; never accepted there.</summary>
    public const string TokenName = "access_token";

    /// <summary>The data addresses answer by these two headers (the guide's §4.3).</summary>
    public const string Vary = "Accept, Authorization";

    /// <summary>The reader's name in the access log.</summary>
    public const string RoleLabel = "EU registry";

    /// <summary>
    /// Whether the request carries a credential: an Authorization header of any scheme, or a token in the query or a cookie.
    /// Such a request is never anonymous (the guide's §2.4): it gets the restricted parts, or 401 or 403, never the public part.
    /// </summary>
    public static bool CarriesCredential(HttpRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        return request.Headers.Authorization.Count > 0 || TokenOutsideTheHeader(request);
    }

    private static bool TokenOutsideTheHeader(HttpRequest request) =>
        request.Query.ContainsKey(TokenName) || request.Cookies.ContainsKey(TokenName);

    /// <summary>
    /// The address as the log may hold it: control characters made '?', at most 120 characters, so a crafted address can't
    /// write a line of its own into the log.
    /// </summary>
    public static string ForLog(string? id)
    {
        var text = new string((id ?? string.Empty).Select(c => char.IsControl(c) ? '?' : c).ToArray());
        return text.Length <= 120 ? text : text[..120] + "...";
    }

    /// <summary>The token check's answer for this request, once it has run; null when it hasn't.</summary>
    public static RegistryTokenResult? Outcome(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return context.Features.Get<RegistryTokenResult>();
    }

    /// <summary>
    /// Checks the request's credential: exactly one Authorization header, of the Bearer scheme, with a token the check
    /// accepts. A token in the query or a cookie, another scheme or a second header are refused (the guide's §4.3).
    /// </summary>
    public static async Task<RegistryTokenResult> CheckAsync(HttpRequest request, RegistryTokenCheck check, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(check);
        if (TokenOutsideTheHeader(request))
        {
            return RegistryTokenResult.Invalid("token-outside-the-header");
        }

        var headers = request.Headers.Authorization;
        if (headers.Count != 1)
        {
            return new RegistryTokenResult(RegistryTokenVerdict.OtherScheme, headers.Count == 0 ? "no-header" : "several-headers");
        }

        var value = headers[0] ?? string.Empty;
        const string bearer = "Bearer ";
        if (!value.StartsWith(bearer, StringComparison.OrdinalIgnoreCase))
        {
            return new RegistryTokenResult(RegistryTokenVerdict.OtherScheme, "other-scheme");
        }

        return await check.CheckAsync(value[bearer.Length..].Trim(' '), cancellationToken);
    }
}

public sealed class RegistryTokenOptions : AuthenticationSchemeOptions;

/// <summary>
/// The EU registry's token as an authentication scheme of its own (R3, decision 7): it runs before the read limit, so the
/// limit knows a valid token. Cookies stay the app's default scheme; this one is asked only at the data addresses, by
/// <see cref="RegistryTokenAuthentication.UseRegistryToken"/>. Its answer is kept on the request for the passport's
/// controller, which writes the 401 or 403. The log gets the result's fixed word, never the token.
/// </summary>
public sealed class RegistryTokenHandler(IOptionsMonitor<RegistryTokenOptions> options, ILoggerFactory logger, UrlEncoder encoder, RegistryTokenCheck check)
    : AuthenticationHandler<RegistryTokenOptions>(options, logger, encoder)
{
    /// <summary>The claim naming the organisation the registry reads for: the site's own.</summary>
    public const string OrganisationClaim = "eu_registry_org";

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!RegistryTokenRequest.CarriesCredential(Request))
        {
            return AuthenticateResult.NoResult();
        }

        var result = await RegistryTokenRequest.CheckAsync(Request, check, Context.RequestAborted);
        Context.Features.Set(result);
        if (!result.Authorised)
        {
            return AuthenticateResult.Fail(result.Reason);
        }

        var identity = new ClaimsIdentity([new Claim(OrganisationClaim, result.OrganisationId!)], Scheme.Name);
        return AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme.Name));
    }
}

/// <summary>The wiring of the EU registry's token check, the same for the app and its test hosts.</summary>
public static class RegistryTokenAuthentication
{
    public const string Scheme = "EuRegistryToken";

    /// <summary>The settings, the key set's HttpClient, the keys and the check.</summary>
    public static IServiceCollection AddRegistryToken(this IServiceCollection services, RegistryTokenSettings settings)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(settings);
        services.AddSingleton(settings);
        services.TryAddSingleton(TimeProvider.System);
        services.AddHttpClient(RegistryTokenKeys.HttpClientName, client => client.Timeout = RegistryTokenKeys.FetchTimeout)
            .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler { AllowAutoRedirect = false, UseCookies = false });
        services.AddSingleton<RegistryTokenKeys>();
        services.AddSingleton<RegistryTokenCheck>();
        return services;
    }

    /// <summary>The scheme, beside the cookies.</summary>
    public static AuthenticationBuilder AddRegistryToken(this AuthenticationBuilder authentication)
    {
        ArgumentNullException.ThrowIfNull(authentication);
        return authentication.AddScheme<RegistryTokenOptions, RegistryTokenHandler>(Scheme, "EU registry token", _ => { });
    }

    /// <summary>
    /// After routing and the cookies, before the read limit: at a data address, a request that carries a credential is
    /// checked by the scheme, and the answer is kept on the request.
    /// </summary>
    public static IApplicationBuilder UseRegistryToken(this IApplicationBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);
        return app.Use(async (context, next) =>
        {
            if (context.GetEndpoint()?.Metadata.GetMetadata<RegistryReadableAttribute>() is not null
                && RegistryTokenRequest.CarriesCredential(context.Request))
            {
                await context.AuthenticateAsync(Scheme);
                if (RegistryTokenRequest.Outcome(context) is null)
                {
                    context.Features.Set(RegistryTokenResult.Invalid("not-checked"));
                }
            }

            await next();
        });
    }
}
