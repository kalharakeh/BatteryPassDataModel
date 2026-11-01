using BatteryPassWeb.Dpp.Assembly;
using BatteryPassWeb.Dpp.Documents;
using BatteryPassWeb.Dpp.Editing;
using BatteryPassWeb.Dpp.Publication;
using BatteryPassWeb.Dpp.Store;
using BatteryPassWeb.Services;

namespace BatteryPassWeb.Dpp.Pages;

/// <summary>A battery as its cluster admins manage it: its live values to type, its accident reports, modules and versions.</summary>
public sealed record ClusterBatteryPage(
    BatteryPack Pack,
    PassportSources Sources,
    BatteryFamily Family,
    string SerialNumber,
    string Identifier,
    string ClusterLabel,
    RecordEditorView Live,
    IReadOnlyList<VersionRow> Versions,
    IReadOnlyList<DppDocument> AccidentReports)
{
    public string BatchLabel => $"{Sources.Batch.Year} · {Sources.Site.Name}";

    public string Href => ClusterBatteryPages.BatteryHref(Pack.Id);
}

/// <summary>
/// The cluster admin's battery page (design 6.1 and 12, step 4a). A cluster admin manages only a battery of a cluster
/// they are admin of, and only once its passport is published and active: a draft is the team's (J-6). Every change
/// goes through the save pipeline as the cluster admin, which allows the live values and accident reports (design 6.1).
/// </summary>
public sealed class ClusterBatteryPages(
    DppStore store,
    RecordEditor editor,
    DocumentService documents,
    DocumentStore documentStore,
    ClusterRepository clusters)
{
    public static string BatteryHref(string packId) => $"/cluster-admin/batteries/{Uri.EscapeDataString(packId)}";

    /// <summary>Where the detail page finds the "Manage this battery" link, for the battery's cluster admins only.</summary>
    public const string ManageHrefKey = "ManageHref";

    /// <summary>The battery's page, when the sender may manage it; null otherwise.</summary>
    public async Task<string?> ManageHrefAsync(ChangeSender? sender, string packId, CancellationToken cancellationToken = default) =>
        sender is not null && await ManagedAsync(sender, packId, cancellationToken) is not null ? BatteryHref(packId) : null;

    /// <summary>The battery's page; with <paramref name="live"/>, the live form as typed after a refused save. Null when the sender may not manage it.</summary>
    public async Task<ClusterBatteryPage?> BatteryAsync(ChangeSender sender, string packId, EditorState? live = null, CancellationToken cancellationToken = default)
    {
        if (await ManagedAsync(sender, packId, cancellationToken) is not { } pack)
        {
            return null;
        }

        var sources = await store.LoadSourcesAsync(pack.Id, cancellationToken);
        var family = sources is null ? null : await store.GetAsync<BatteryFamily>(sources.Model.FamilyId, cancellationToken);
        if (sources is null || family is null)
        {
            return null;
        }

        var href = BatteryHref(pack.Id);
        var serial = BatteryPages.Text(pack, "batterySerialNumber") ?? pack.Id;
        var liveView = new RecordEditorView(
            TargetKind.Pack,
            pack.Id,
            serial,
            $"{href}/live",
            $"{href}#live",
            await editor.SectionsAsync(TargetKind.Pack, pack.Id, RecordEditor.LiveAttributes, pack.Values, live?.Post, live?.Save.Refusals(editor.Forms), cancellationToken),
            [],
            0,
            editor.Forms.Languages,
            editor.Stamp(RecordEditor.LiveAttributes, pack.Values, null),
            new RemovalView(string.Empty, null))
        {
            ForTeam = false,
        };
        var names = (await clusters.ListClustersAsync(cancellationToken)).ToDictionary(
            cluster => BsonHelpers.GetString(cluster, "clusterId"),
            cluster => BsonHelpers.GetString(cluster, "name"),
            StringComparer.OrdinalIgnoreCase);
        var versions = await store.ListVersionsAsync(pack.Id, cancellationToken);
        return new ClusterBatteryPage(
            pack,
            sources,
            family,
            serial,
            BatteryPages.Text(pack, "uniqueProductIdentifier") ?? string.Empty,
            BatteryPages.ClusterLabel(pack.ClusterId, names),
            liveView,
            versions.OrderByDescending(version => version.Number).Select(version => new VersionRow(version, PassportReader.Verify(version))).ToArray(),
            await documentStore.ListAsync(TargetKind.Pack, pack.Id, DocumentService.Accidents, cancellationToken));
    }

    /// <summary>Saves the live values typed on the page, as the cluster admin; null when the sender may not manage the battery.</summary>
    public async Task<(EditorPost Post, EditorSave Save)?> SaveLiveAsync(
        ChangeSender sender,
        string packId,
        Func<string, string?> field,
        string? stamp,
        DateTime? measuredAtUtc,
        CancellationToken cancellationToken = default)
    {
        if (await ManagedAsync(sender, packId, cancellationToken) is not { } pack)
        {
            return null;
        }

        var post = editor.Read(RecordEditor.LiveAttributes, pack.Values, field, stamp) with { MeasuredAtUtc = measuredAtUtc };
        return (post, await editor.SaveAsync(sender, TargetKind.Pack, pack.Id, post, confirmed: true, cancellationToken));
    }

    /// <summary>Adds an accident report (#92), as the cluster admin; null when the sender may not manage the battery.</summary>
    public async Task<DocumentUploadResult?> AddAccidentReportAsync(ChangeSender sender, string packId, DocumentUpload upload, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(upload);
        return await ManagedAsync(sender, packId, cancellationToken) is { } pack
            ? await documents.AddAccidentReportAsync(sender, pack.Id, upload, cancellationToken)
            : null;
    }

    /// <summary>The pack, when it belongs to a cluster the sender is admin of and its passport is published and active.</summary>
    private async Task<BatteryPack?> ManagedAsync(ChangeSender sender, string packId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(sender);
        if (sender.Kind != SenderKind.ClusterAdmin
            || await store.GetAsync<BatteryPack>(packId, cancellationToken) is not { ClusterId: { } clusterId } pack
            || !sender.ClusterIds.Contains(ClusterRepository.NormalizeClusterId(clusterId)))
        {
            return null;
        }

        return await store.GetActiveVersionAsync(pack.Id, cancellationToken) is null ? null : pack;
    }
}
