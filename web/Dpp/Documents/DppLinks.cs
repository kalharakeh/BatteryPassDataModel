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

    public string DocumentUrl(string documentId) => $"{Base}/dpp/documents/{Uri.EscapeDataString(documentId)}";

    public string AccidentPageUrl(string packId) => $"{Base}/dpp/packs/{Uri.EscapeDataString(packId)}/accidents";

    /// <summary>
    /// The public summary's full address, which a battery's QR code carries (design 10): the battery ID (#6) as written,
    /// so the code opens the battery's current passport.
    /// </summary>
    public string PassportUrl(string identifier) => Base + BatteryPassWeb.Dpp.Pages.PassportPages.SummaryHref(identifier);

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
