using System.Text.Json.Nodes;
using BatteryPassWeb.Configuration;
using Microsoft.Extensions.Options;

namespace BatteryPassWeb.Dpp.Documents;

/// <summary>
/// The app's own addresses for restricted documents and #92's accident page (design 9), built from APP_BASE_URL.
/// Restricted documents may only link here, so they are always served behind the app's access checks.
/// </summary>
public sealed class DppLinks(IOptions<BatteryPassOptions> options)
{
    public const string NotConfigured = "APP_BASE_URL is not set: the app can't issue document links or create packs without it.";

    /// <summary>The document attributes that are uploaded to the app and served only to their audience (design 9).</summary>
    public static IReadOnlySet<string> RestrictedDocumentProperties { get; } = new HashSet<string>(StringComparer.Ordinal)
    {
        "resultsOfTestReportsProvingCompliance",
        "dismantlingInformation",
        "partNumbersForComponents",
        "informationOnSourcesOfSpareParts",
        "safetyMeasures",
        "informationOnAccidents",
    };

    public bool IsConfigured => BaseOrNull is not null;

    /// <summary>
    /// Why the two site addresses don't fit together, or null (finding D18): a battery's QR code opens APP_BASE_URL, its
    /// battery ID names DPP_ID_BASE_URL, so both must be the same site (scheme, host and port), and the message says which
    /// of those differ. Null when either is missing or unreadable; <see cref="ProductionAddressProblem"/> speaks about those.
    /// </summary>
    public static string? HostMismatch(string? appBaseUrl, string? idBaseUrl)
    {
        if (WholeAddress(appBaseUrl) is not { } app || WholeAddress(idBaseUrl) is not { } id)
        {
            return null;
        }

        var differences = new List<string>();
        if (app.Scheme != id.Scheme)
        {
            differences.Add($"the scheme differs ({app.Scheme} against {id.Scheme})");
        }

        if (SiteHost(app) != SiteHost(id))
        {
            differences.Add($"the host differs ({SiteHost(app)} against {SiteHost(id)})");
        }

        // A scheme change alone moves the default port too; that is the scheme's difference, not the port's.
        if (app.Port != id.Port && !(app.Scheme != id.Scheme && app.IsDefaultPort && id.IsDefaultPort))
        {
            differences.Add($"the port differs ({app.Port.ToString(System.Globalization.CultureInfo.InvariantCulture)} against {id.Port.ToString(System.Globalization.CultureInfo.InvariantCulture)})");
        }

        return differences.Count == 0
            ? null
            : $"APP_BASE_URL ({app.GetLeftPart(UriPartial.Authority)}) and DPP_ID_BASE_URL ({id.GetLeftPart(UriPartial.Authority)}) aren't the same site: {string.Join("; ", differences)}. A battery's QR code opens the first and its battery ID names the second, so both must be the same site. Set them to the same address.";
    }

    /// <summary>
    /// Why Production can't start with these addresses, or null (the manager's decision 61): both must be whole http or
    /// https addresses with a host. Elsewhere always null.
    /// </summary>
    public static string? ProductionAddressProblem(bool production, string? appBaseUrl, string? idBaseUrl)
    {
        if (!production)
        {
            return null;
        }

        return AddressProblem("APP_BASE_URL", appBaseUrl) ?? AddressProblem("DPP_ID_BASE_URL", idBaseUrl);

        static string? AddressProblem(string name, string? value) =>
            WholeAddress(value) is not null
                ? null
                : string.IsNullOrWhiteSpace(value)
                    ? $"{name} isn't set. In Production it must be the site's whole http or https address, such as https://www.example.com."
                    : $"{name} ({value.Trim()}) isn't a whole http or https address. In Production it must be the site's address, such as https://www.example.com.";
    }

    /// <summary>
    /// A site's host as one name: lowercase, without the trailing dot a fully qualified name may carry
    /// ("www.example.com." is www.example.com).
    /// </summary>
    public static string SiteHost(Uri address)
    {
        ArgumentNullException.ThrowIfNull(address);
        return address.IdnHost.TrimEnd('.').ToLowerInvariant();
    }

    /// <summary>The setting as an absolute http or https address with a host; null when it isn't one.</summary>
    public static Uri? WholeAddress(string? value) =>
        Uri.TryCreate(value?.Trim(), UriKind.Absolute, out var address) && address.Scheme is "http" or "https" && SiteHost(address).Length > 0
            ? address
            : null;

    public string DocumentUrl(string documentId) => $"{Base}/dpp/documents/{Uri.EscapeDataString(documentId)}";

    public string AccidentPageUrl(string packId) => $"{Base}/dpp/packs/{Uri.EscapeDataString(packId)}/accidents";

    /// <summary>
    /// The public summary's full address, which a battery's QR code carries (design 10): the battery ID (#6) as written,
    /// so the code opens the battery's current passport.
    /// </summary>
    public string PassportUrl(string identifier) =>
        Identifiers.IdentifierForms.Short(identifier) is { } item && Identifiers.IdentifierForms.IsItem(item)
            ? $"{Base}/{Identifiers.IdentifierForms.DataIdentifier}/{item}"
            : Base + BatteryPassWeb.Dpp.Pages.PassportPages.SummaryHref(identifier);

    /// <summary>#92's value: the pack's accident page, which lists its reports or says none were reported (J-14).</summary>
    public JsonObject AccidentPageLink(string packId) => new()
    {
        ["contentType"] = "text/html",
        ["url"] = AccidentPageUrl(packId),
        ["language"] = "en-GB",
        ["resourceTitle"] = "Accident information",
    };

    /// <summary>True for an address the app serves itself, behind its access checks.</summary>
    public bool IsAppUrl(string url) =>
        BaseOrNull is { } baseUrl && url.StartsWith(baseUrl + "/dpp/", StringComparison.Ordinal);

    private string? BaseOrNull =>
        string.IsNullOrWhiteSpace(options.Value.AppBaseUrl) ? null : options.Value.AppBaseUrl.Trim().TrimEnd('/');

    private string Base => BaseOrNull ?? throw new InvalidOperationException(NotConfigured);
}
