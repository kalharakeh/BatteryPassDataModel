namespace BatteryPassWeb.Dpp.RegistryToken;

/// <summary>
/// The four settings of the EU registry's token check (R3, decision 94), per environment: the EU's issuer and its key set
/// (the test registry for a staging site, production for the live site), the site's own origin as the token's audience,
/// and the organisation ID the EU registry shows for Scania. All four set: the check is on. Any of them empty: it is off,
/// and a token at a passport's address still gets 401, never the public part.
/// </summary>
public sealed record RegistryTokenSettings(string? Issuer, string? JwksUrl, string? Audience, string? OrganisationId)
{
    public const string IssuerSetting = "EU_REGISTRY_ISSUER";
    public const string JwksUrlSetting = "EU_REGISTRY_JWKS_URL";
    public const string AudienceSetting = "EU_REGISTRY_AUDIENCE";
    public const string OrganisationIdSetting = "EU_REGISTRY_ORG_ID";

    /// <summary>The four settings' names, in the order the runbook lists them.</summary>
    public static IReadOnlyList<string> Names { get; } = [IssuerSetting, JwksUrlSetting, AudienceSetting, OrganisationIdSetting];

    public static RegistryTokenSettings Off { get; } = new(null, null, null, null);

    /// <summary>The four settings, each by its name; one that is there but empty counts as not set.</summary>
    public static RegistryTokenSettings Read(Func<string, string?> setting)
    {
        ArgumentNullException.ThrowIfNull(setting);
        static string? Value(string? text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim();
        return new RegistryTokenSettings(Value(setting(IssuerSetting)), Value(setting(JwksUrlSetting)), Value(setting(AudienceSetting)), Value(setting(OrganisationIdSetting)));
    }

    /// <summary>Whether the check is on: all four are set.</summary>
    public bool IsOn => Missing.Count == 0;

    /// <summary>The names of the settings that aren't set.</summary>
    public IReadOnlyList<string> Missing =>
        new[] { (IssuerSetting, Issuer), (JwksUrlSetting, JwksUrl), (AudienceSetting, Audience), (OrganisationIdSetting, OrganisationId) }
            .Where(pair => pair.Item2 is null)
            .Select(pair => pair.Item1)
            .ToArray();

    /// <summary>Some of the four are set, not all.</summary>
    public bool PartlySet => Missing.Count is > 0 and < 4;

    /// <summary>
    /// Why the app can't start with these settings; null when it can. Production needs all four or none. Outside Development
    /// the issuer and the key set must be https addresses and the audience the site's https origin (decision 8); in
    /// Development a local http address is allowed, for a check with a test key.
    /// </summary>
    public string? StartProblem(bool production, bool development)
    {
        if (production && PartlySet)
        {
            return $"The EU registry's token check needs all four of {string.Join(", ", Names)}, or none. Not set: {string.Join(", ", Missing)}.";
        }

        if (!IsOn)
        {
            return null;
        }

        if (!Address(Issuer!, development))
        {
            return $"{IssuerSetting} must be a whole https address, such as https://auth.product-passport.ec.europa.eu.";
        }

        if (!Address(JwksUrl!, development))
        {
            return $"{JwksUrlSetting} must be a whole https address, such as https://auth.product-passport.ec.europa.eu/oauth2/jwks.";
        }

        if (!Origin(Audience!, development))
        {
            return $"{AudienceSetting} must be the site's own origin, its https address without a path or a closing slash, such as https://www.sib-scania-eubp.eu.";
        }

        return null;
    }

    /// <summary>The start log's line: whether the check is on, naming no value but the issuer.</summary>
    public string StartLine => IsOn
        ? $"The EU registry's token check is on: tokens from {Issuer} are accepted at the passports' data addresses."
        : PartlySet
            ? $"The EU registry's token check is off: not set: {string.Join(", ", Missing)}. A token at a passport's address gets 401."
            : "The EU registry's token check is off: the EU_REGISTRY_ settings are not set. A token at a passport's address gets 401.";

    private static bool Address(string text, bool development) =>
        Uri.TryCreate(text, UriKind.Absolute, out var uri)
        && (uri.Scheme == Uri.UriSchemeHttps || (development && uri.Scheme == Uri.UriSchemeHttp))
        && uri.UserInfo.Length == 0
        && uri.Fragment.Length == 0;

    /// <summary>Scheme, host and port only, written as the EU compares it: no path, query, fragment or closing slash.</summary>
    private static bool Origin(string text, bool development) =>
        Address(text, development)
        && Uri.TryCreate(text, UriKind.Absolute, out var uri)
        && uri.AbsolutePath == "/"
        && uri.Query.Length == 0
        && !text.EndsWith('/')
        && string.Equals(text, uri.GetLeftPart(UriPartial.Authority), StringComparison.Ordinal);
}
