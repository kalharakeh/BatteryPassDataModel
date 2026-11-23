using System.Text.Json;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace BatteryPassWeb.Dpp.RegistryToken;

/// <summary>What the token check found (R3, decision 94).</summary>
public enum RegistryTokenVerdict
{
    /// <summary>A valid token for this site's organisation: the EU registry reads the restricted parts.</summary>
    Authorised,

    /// <summary>Malformed, untrusted, expired or otherwise invalid, or the check is off: 401, never the public part.</summary>
    Invalid,

    /// <summary>An Authorization header of another scheme than Bearer, or more than one: 401.</summary>
    OtherScheme,

    /// <summary>A valid token whose scope isn't exactly the registry's read: 403.</summary>
    InsufficientScope,

    /// <summary>A valid token for another organisation, or with a requestor that can't be read: 403.</summary>
    RequestorRefused,
}

/// <summary>
/// The check's answer: the verdict, a short reason for the log (a fixed word, never anything from the token), and the
/// organisation when it is this site's.
/// </summary>
public sealed record RegistryTokenResult(RegistryTokenVerdict Verdict, string Reason, string? OrganisationId = null)
{
    public bool Authorised => Verdict == RegistryTokenVerdict.Authorised;

    /// <summary>The answer's status: 200 for a valid token, 401 for an invalid one, 403 for a valid one that may not read.</summary>
    public int Status => Verdict switch
    {
        RegistryTokenVerdict.Authorised => StatusCodes.Status200OK,
        RegistryTokenVerdict.InsufficientScope or RegistryTokenVerdict.RequestorRefused => StatusCodes.Status403Forbidden,
        _ => StatusCodes.Status401Unauthorized,
    };

    public static RegistryTokenResult Invalid(string reason) => new(RegistryTokenVerdict.Invalid, reason);
}

/// <summary>
/// Checks an EU registry access token as the EU's Self-Hosted Authentication Guide says (§6, §7.1, §12.1), locally, in
/// the guide's order: the form, ES384 and at+jwt, the signature by the configured key set's key, the issuer, the
/// required claims and SYSTEM, the lifetime of at most 120 seconds, the audience (all 401 when they fail); then the
/// scope and the one ORG_IDENTIFIER requestor, compared exactly with the site's organisation (403).
/// </summary>
public sealed class RegistryTokenCheck(RegistryTokenSettings settings, RegistryTokenKeys keys, TimeProvider clock)
{
    public const string Scope = "dpp:registry:read";
    public const string RequestorType = "ORG_IDENTIFIER";
    public const string Algorithm = SecurityAlgorithms.EcdsaSha384;

    /// <summary>A longer token isn't read at all.</summary>
    public const int MaxTokenLength = 4096;

    public static readonly TimeSpan LongestLifetime = TimeSpan.FromSeconds(120);

    /// <summary>The clock difference allowed between the EU's clock and the site's.</summary>
    public static readonly TimeSpan ClockSkew = TimeSpan.FromSeconds(30);

    private static readonly string[] TokenTypes = ["at+jwt", "application/at+jwt"];
    private static readonly JsonWebTokenHandler Handler = new();

    public async Task<RegistryTokenResult> CheckAsync(string? token, CancellationToken cancellationToken = default)
    {
        try
        {
            return await CheckUnsafeAsync(token, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            return RegistryTokenResult.Invalid("unreadable"); // whatever goes wrong refuses the token; nothing of it is logged
        }
    }

    private async Task<RegistryTokenResult> CheckUnsafeAsync(string? token, CancellationToken cancellationToken)
    {
        if (!settings.IsOn)
        {
            return RegistryTokenResult.Invalid("off");
        }

        if (string.IsNullOrEmpty(token) || token.Length > MaxTokenLength || !Compact(token))
        {
            return RegistryTokenResult.Invalid("malformed");
        }

        var jwt = new JsonWebToken(token);
        using var header = JsonDocument.Parse(Base64UrlEncoder.DecodeBytes(token[..token.IndexOf('.')]));
        if (!Unique(header.RootElement))
        {
            return RegistryTokenResult.Invalid("malformed");
        }

        if (Text(header.RootElement, "alg") != Algorithm)
        {
            return RegistryTokenResult.Invalid("algorithm");
        }

        if (Text(header.RootElement, "typ") is not { } type || !TokenTypes.Contains(type, StringComparer.Ordinal))
        {
            return RegistryTokenResult.Invalid("type");
        }

        if (header.RootElement.TryGetProperty("crit", out _))
        {
            return RegistryTokenResult.Invalid("critical-header"); // no extension this check understands
        }

        // The key only by its ID, only from the configured key set: jku, x5u and any other address in the token are ignored.
        if (Text(header.RootElement, "kid") is not { Length: > 0 } keyId || await keys.FindAsync(keyId, cancellationToken) is not { } key)
        {
            return RegistryTokenResult.Invalid("unknown-key");
        }

        var signature = await Handler.ValidateTokenAsync(jwt, new TokenValidationParameters
        {
            IssuerSigningKey = key,
            ValidAlgorithms = [Algorithm],
            ValidTypes = TokenTypes,
            TryAllIssuerSigningKeys = false,
            RequireSignedTokens = true,
            ValidateIssuerSigningKey = false,
            ValidateIssuer = false,
            ValidateAudience = false,
            ValidateLifetime = false, // checked below, on the app's clock, with this check's own skew
            RequireExpirationTime = false,
            RequireAudience = false,
        });
        if (!signature.IsValid)
        {
            return RegistryTokenResult.Invalid("signature");
        }

        using var payload = JsonDocument.Parse(Base64UrlEncoder.DecodeBytes(jwt.EncodedPayload));
        var claims = payload.RootElement;
        if (claims.ValueKind != JsonValueKind.Object || !Unique(claims))
        {
            return RegistryTokenResult.Invalid("malformed");
        }

        if (!string.Equals(Text(claims, "iss"), settings.Issuer, StringComparison.Ordinal))
        {
            return RegistryTokenResult.Invalid("issuer");
        }

        if (Text(claims, "sub") is not { Length: > 0 } || Text(claims, "client_id") is not { Length: > 0 } || Text(claims, "jti") is not { Length: > 0 })
        {
            return RegistryTokenResult.Invalid("claims");
        }

        if (Text(claims, "principal_type") != "SYSTEM")
        {
            return RegistryTokenResult.Invalid("principal-type");
        }

        if (Lifetime(claims) is { } lifetime)
        {
            return RegistryTokenResult.Invalid(lifetime);
        }

        if (!Audience(claims))
        {
            return RegistryTokenResult.Invalid("audience");
        }

        if (Text(claims, "scope") != Scope)
        {
            return new RegistryTokenResult(RegistryTokenVerdict.InsufficientScope, "scope");
        }

        if (!claims.TryGetProperty("resource_requestor_identifiers", out var requestors)
            || requestors.ValueKind != JsonValueKind.Array
            || requestors.GetArrayLength() != 1
            || requestors[0] is not { ValueKind: JsonValueKind.Object } requestor
            || !Unique(requestor)
            || Text(requestor, "type") != RequestorType
            || Text(requestor, "value") is not { Length: > 0 } organisation)
        {
            return new RegistryTokenResult(RegistryTokenVerdict.RequestorRefused, "requestor-unreadable");
        }

        // Exactly, as the guide's §7.2 says: no trimming, no change of case, no suffix.
        return string.Equals(organisation, settings.OrganisationId, StringComparison.Ordinal)
            ? new RegistryTokenResult(RegistryTokenVerdict.Authorised, "authorised", settings.OrganisationId)
            : new RegistryTokenResult(RegistryTokenVerdict.RequestorRefused, "requestor-other");
    }

    /// <summary>Three base64url parts, header.payload.signature, nothing else.</summary>
    private static bool Compact(string token)
    {
        var parts = token.Split('.');
        return parts.Length == 3
            && parts.All(part => part.Length > 0 && part.All(c => c is (>= 'A' and <= 'Z') or (>= 'a' and <= 'z') or (>= '0' and <= '9') or '-' or '_'));
    }

    /// <summary>An object whose names each appear once: a claim written twice can't be read one way here and another way elsewhere.</summary>
    private static bool Unique(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        var names = new HashSet<string>(StringComparer.Ordinal);
        return element.EnumerateObject().All(property => names.Add(property.Name));
    }

    private static string? Text(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    /// <summary>Null when iat and exp hold: both there, exp after iat, at most 120 seconds apart, and now inside them give or take the skew.</summary>
    private string? Lifetime(JsonElement claims)
    {
        if (!claims.TryGetProperty("iat", out var issuedAt) || issuedAt.ValueKind != JsonValueKind.Number || !issuedAt.TryGetDouble(out var iat)
            || !claims.TryGetProperty("exp", out var expires) || expires.ValueKind != JsonValueKind.Number || !expires.TryGetDouble(out var exp))
        {
            return "lifetime";
        }

        if (exp <= iat || exp - iat > LongestLifetime.TotalSeconds)
        {
            return "lifetime";
        }

        var now = clock.GetUtcNow().ToUnixTimeMilliseconds() / 1000.0;
        if (now >= exp + ClockSkew.TotalSeconds)
        {
            return "expired";
        }

        if (iat > now + ClockSkew.TotalSeconds)
        {
            return "not-yet-valid";
        }

        if (claims.TryGetProperty("nbf", out var notBefore) && (notBefore.ValueKind != JsonValueKind.Number || !notBefore.TryGetDouble(out var nbf) || nbf > now + ClockSkew.TotalSeconds))
        {
            return "not-yet-valid";
        }

        return null;
    }

    /// <summary>The audience is exactly the site's origin: one text, or a list of that one text.</summary>
    private bool Audience(JsonElement claims)
    {
        if (!claims.TryGetProperty("aud", out var audience))
        {
            return false;
        }

        var value = audience.ValueKind switch
        {
            JsonValueKind.String => audience.GetString(),
            JsonValueKind.Array when audience.GetArrayLength() == 1 && audience[0].ValueKind == JsonValueKind.String => audience[0].GetString(),
            _ => null,
        };
        return string.Equals(value, settings.Audience, StringComparison.Ordinal);
    }
}
