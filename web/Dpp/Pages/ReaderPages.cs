using System.Text.Json.Nodes;
using BatteryPassWeb.Dpp.Access;
using BatteryPassWeb.Dpp.Documents;
using BatteryPassWeb.Dpp.Store;

namespace BatteryPassWeb.Dpp.Pages;

/// <summary>The accident page (#92, design 9 and J-14): the battery, and its accident reports newest first.</summary>
public sealed record AccidentsPage(string PackId, string SerialNumber, string Identifier, IReadOnlyList<DppDocument> Reports);

/// <summary>
/// The pages passport readers open, cut to what the reader may see (step 3). The accident page comes first, because
/// every passport's #92 already links to it.
/// </summary>
public sealed class ReaderPages(DppStore store, DocumentService documents)
{
    /// <summary>The app's own address of an uploaded document, behind its access check (design 9).</summary>
    public static string DocumentHref(string documentId) => $"/dpp/documents/{Uri.EscapeDataString(documentId)}";

    /// <summary>The pack's accident page, the address #92 links to (<see cref="DppLinks.AccidentPageUrl"/>).</summary>
    public static string AccidentsHref(string packId) => $"/dpp/packs/{Uri.EscapeDataString(packId)}/accidents";

    /// <summary>The accident page when the reader may see #92 for this battery; otherwise null, whether or not it exists.</summary>
    public async Task<AccidentsPage?> AccidentsAsync(Reader reader, string packId, CancellationToken cancellationToken = default)
    {
        var reports = await documents.AccidentReportsAsync(reader, packId, cancellationToken);
        var pack = reports is null ? null : await store.GetAsync<BatteryPack>(packId, cancellationToken);
        return pack is null
            ? null
            : new AccidentsPage(pack.Id, Text(pack, "batterySerialNumber") ?? pack.Id, Text(pack, "uniqueProductIdentifier") ?? string.Empty, reports!);
    }

    /// <summary>"PDF", "PNG picture" or "JPEG picture": the stored, byte-checked type in words.</summary>
    public static string FileKind(string contentType) => contentType switch
    {
        "application/pdf" => "PDF",
        "image/png" => "PNG picture",
        "image/jpeg" => "JPEG picture",
        _ => contentType,
    };

    private static string? Text(BatteryPack pack, string property) =>
        pack.Values.GetValueOrDefault(property)?.Value is JsonValue value && value.TryGetValue<string>(out var text) ? text : null;
}
