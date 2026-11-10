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
    /// battery ID names DPP_ID_BASE_URL, so both must be on the same host (and port). Null when either is missing or
    /// unreadable; other checks speak about those.
    /// </summary>
    public static string? HostMismatch(string? appBaseUrl, string? idBaseUrl)
    {
        if (!Uri.TryCreate(appBaseUrl?.Trim(), UriKind.Absolute, out var app) || !Uri.TryCreate(idBaseUrl?.Trim(), UriKind.Absolute, out var id)
            || app.Scheme is not ("http" or "https") || id.Scheme is not ("http" or "https"))
        {
            return null;
        }

        return string.Equals(app.IdnHost, id.IdnHost, StringComparison.OrdinalIgnoreCase) && app.Port == id.Port
            ? null
            : $"APP_BASE_URL ({app.Authority}) and DPP_ID_BASE_URL ({id.Authority}) are on different hosts. A battery's QR code opens the first and its battery ID names the second, so both must be the same site. Set them to the same address.";
    }

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
