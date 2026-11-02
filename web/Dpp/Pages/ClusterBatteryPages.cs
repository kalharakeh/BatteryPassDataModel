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

    /// <summary>The battery status card (step 4b), shared with the team's page.</summary>
    public StatusCardView? Status { get; init; }

    /// <summary>The "Hand over" card (step 4d): the waiting hand-over, or the window to ask for one.</summary>
    public HandOverCardView HandOver { get; init; } = new(string.Empty, string.Empty, null, null);

    /// <summary>The team page's cards, without what only the team does (step 4a).</summary>
    public BatteryCards Cards => new(Pack, Href, Live, AccidentReports, Versions, Live.Languages, ForTeam: false);

    /// <summary>The page's side menu (redesign R4, option B), like the team's without what only the team does.</summary>
    public IReadOnlyList<BatterySection> Sections
    {
        get
        {
            var sections = new List<BatterySection> { new(BatterySections.Overview, "Overview", "overview", HandOver.Waiting is null ? null : "A hand-over is waiting") };
            if (Status is { } status)
            {
                sections.Add(new("status", "Status & repairs", "swap", BatterySections.StatusHint(status)));
            }

            sections.Add(new("live", "Live values", "live", BatterySections.LiveHint(Pack, Live)));
            sections.Add(new("accidents", "Accident reports", "accident", Count: AccidentReports.Count));
            sections.Add(new("handover", "Hand over", "handover", HandOver.Waiting is null ? "To another cluster" : "Waiting for their answer"));
            sections.Add(new("modules", "Modules", "modules", Count: Pack.Modules.Count));
            sections.Add(new("versions", "Versions", "versions", Count: Versions.Count));
            return sections;
        }
    }
}

/// <summary>The "Hand over" card (mock-up S2 and S8): where its forms go, and the hand-over waiting with its receiver's name.</summary>
public sealed record HandOverCardView(string Action, string SerialNumber, HandOver? Waiting, string? ReceiverName);

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
    ClusterRepository clusters,
    StatusPages status)
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
        var waiting = await store.FindWaitingHandOverAsync(pack.Id, cancellationToken);
        return new ClusterBatteryPage(
            pack,
            sources,
            family,
            serial,
            BatteryPages.Text(pack, "uniqueProductIdentifier") ?? string.Empty,
            BatteryPages.ClusterLabel(pack.ClusterId, names),
            liveView,
            versions.OrderByDescending(version => version.Number).Select(version => new VersionRow(version, PassportReader.Verify(version))).ToArray(),
            await documentStore.ListAsync(TargetKind.Pack, pack.Id, DocumentService.Accidents, cancellationToken))
        {
            Status = await status.CardAsync(sources, active: true, href, forTeam: false, cancellationToken: cancellationToken),
            HandOver = new HandOverCardView($"{href}/handover", serial, waiting, waiting is null ? null : names.GetValueOrDefault(waiting.ToClusterId) ?? waiting.ToClusterId),
        };
    }

    /// <summary>Adds a note to the history as the cluster admin (decision 34); null when the sender may not manage the battery.</summary>
    public async Task<(string? Done, string? Problem)?> AddHistoryNoteAsync(ChangeSender sender, string packId, string? note, CancellationToken cancellationToken = default) =>
        await ManagedAsync(sender, packId, cancellationToken) is { } pack
            ? await status.AddNoteAsync(sender, pack.Id, note, cancellationToken)
            : null;

    /// <summary>Another operator's details or link, as the cluster admin (design 7.5); null when the sender may not manage the battery.</summary>
    public async Task<(string? Done, string? Problem)?> OtherOperatorAsync(ChangeSender sender, string packId, OtherOperator details, bool link, CancellationToken cancellationToken = default) =>
        await ManagedAsync(sender, packId, cancellationToken) is { } pack
            ? await status.OtherOperatorAsync(sender, pack.Id, details, link, cancellationToken)
            : null;

    /// <summary>What the status card says after the window was sent (the team page's words).</summary>
    public (string? Done, string? Problem) StatusMessage(StatusChangeResult result, int versionBefore) => status.Message(result, versionBefore);

    /// <summary>Makes a status change from the window, as the cluster admin; null when the sender may not manage the battery.</summary>
    public async Task<StatusChangeResult?> ChangeStatusAsync(
        ChangeSender sender,
        string packId,
        string? what,
        Func<string, string?> field,
        string? repairDate,
        string? repairDescription,
        CancellationToken cancellationToken = default) =>
        await ManagedAsync(sender, packId, cancellationToken) is { } pack
            ? await status.ChangeAsync(sender, pack.Id, what, field, repairDate, repairDescription, cancellationToken)
            : null;

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
