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

    /// <summary>The overview's tiles, as on the team's page (redesign R4).</summary>
    public IReadOnlyList<LiveTile> Tiles { get; init; } = [];

    /// <summary>The battery's passports, oldest first, when it was placed on the market again (redesign R6); empty otherwise.</summary>
    public IReadOnlyList<ChainLink> Chain { get; init; } = [];

    /// <summary>The "Hand over" card (step 4d): the waiting hand-over, or the window to ask for one.</summary>
    public HandOverCardView HandOver { get; init; } = new(string.Empty, string.Empty, null, null);

    /// <summary>What happened while the battery was in this cluster, as its admin may read it (redesign R6), newest first.</summary>
    public IReadOnlyList<BatteryPassWeb.Dpp.Audit.AuditLine> History { get; init; } = [];

    /// <summary>The "Upload a file" section (step 5c); the controller adds it, never to a read-only page.</summary>
    public UploadCardView? Upload { get; init; }

    /// <summary>The mark that froze the passport (invalid, marked for deletion); null while it is active (redesign R6b).</summary>
    public string? FrozenAs { get; init; }

    /// <summary>When the passport was marked; its reason is the team's and market surveillance's (decision 34).</summary>
    public DateTime? MarkedSince { get; init; }

    /// <summary>A marked passport's page: to read only, nothing on it changes (redesign R6b, the owner's choice).</summary>
    public bool ReadOnly => FrozenAs is not null;

    /// <summary>The team page's cards, without what only the team does (step 4a); frozen while the passport is marked.</summary>
    public BatteryCards Cards => new(Pack, Href, Live, AccidentReports, Versions, Live.Languages, ForTeam: false, FrozenAs: FrozenAs);

    /// <summary>The header's thumbnail, as on the team's page.</summary>
    public string? PictureHref => BatteryPages.PictureHref(Sources.Model, Family);

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
            if (Upload is { } upload && !ReadOnly)
            {
                sections.Add(new("upload", "Upload a file", "upload", upload.Hint)); // step 5c, mock-up B4
            }

            if (!ReadOnly) // a marked passport takes no report and can't be handed over; its reports stay on the Accident page
            {
                sections.Add(new("accidents", "Accident reports", "accident", Count: AccidentReports.Count));
                sections.Add(new("handover", "Hand over", "handover", HandOver.Waiting is null ? "To another cluster" : "Waiting for their answer"));
            }

            sections.Add(new("modules", "Modules", "modules", Count: Pack.Modules.Count));
            sections.Add(new("versions", "Versions", "versions", Count: Versions.Count));
            sections.Add(new("history", "History", "history", "While it was your cluster's")); // redesign R6
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
/// A passport marked invalid or for deletion opens read-only, every change still refused (redesign R6b).
/// </summary>
public sealed class ClusterBatteryPages(
    DppStore store,
    RecordEditor editor,
    DocumentService documents,
    DocumentStore documentStore,
    ClusterRepository clusters,
    StatusPages status,
    LiveTrends? trends = null,
    BatteryPassWeb.Dpp.Audit.ClusterHistory? history = null)
{
    public static string BatteryHref(string packId) => $"/cluster-admin/batteries/{Uri.EscapeDataString(packId)}";

    /// <summary>Where the detail page finds its "Open the battery page" link, for those who may change the battery.</summary>
    public const string ManageHrefKey = "ManageHref";

    /// <summary>
    /// The battery's page for this sender: the team's battery page for the team (redesign R6: the team had no way back
    /// from the detail page), the cluster admin's for the battery's cluster admins; null for anyone else.
    /// </summary>
    public async Task<string?> ManageHrefAsync(ChangeSender? sender, string packId, CancellationToken cancellationToken = default) =>
        sender is null ? null
        : sender.Kind == SenderKind.Team ? (await store.GetAsync<BatteryPack>(packId, cancellationToken) is null ? null : BatteryPages.BatteryHref(packId))
        : await ReadableAsync(sender, packId, cancellationToken) is not null ? BatteryHref(packId) : null;

    /// <summary>The battery's page; with <paramref name="live"/>, the live form as typed after a refused save. Null when the sender may not manage it.</summary>
    public async Task<ClusterBatteryPage?> BatteryAsync(ChangeSender sender, string packId, EditorState? live = null, CancellationToken cancellationToken = default)
    {
        if (await ReadableAsync(sender, packId, cancellationToken) is not ({ } pack, var frozenAs))
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
            Status = await status.CardAsync(sources, active: frozenAs is null, href, forTeam: false, cancellationToken: cancellationToken),
            Tiles = trends is null ? [] : await trends.TilesAsync(sources.Pack, sources.Model, cancellationToken),
            Chain = await PassportChains.ForAsync(store, pack, cancellationToken),
            HandOver = new HandOverCardView($"{href}/handover", serial, waiting, waiting is null ? null : names.GetValueOrDefault(waiting.ToClusterId) ?? waiting.ToClusterId),
            History = history is null ? [] : await history.ForAsync(pack, sender.ClusterIds, cancellationToken),
            FrozenAs = frozenAs,
            MarkedSince = frozenAs is null ? null : pack.Mark?.AtUtc,
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
    /// <summary>A battery of the sender's cluster whose passport is active: the one the cluster admin may change.</summary>
    private async Task<BatteryPack?> ManagedAsync(ChangeSender sender, string packId, CancellationToken cancellationToken) =>
        await ReadableAsync(sender, packId, cancellationToken) is (var pack, null) ? pack : null;

    /// <summary>
    /// A battery of the sender's cluster whose newest passport is active, or marked invalid or for deletion; the mark, or
    /// null while it is active. A draft, or a passport a newer one replaced, gives nothing (redesign R6b).
    /// </summary>
    private async Task<(BatteryPack Pack, string? FrozenAs)?> ReadableAsync(ChangeSender sender, string packId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(sender);
        if (sender.Kind != SenderKind.ClusterAdmin
            || await store.GetAsync<BatteryPack>(packId, cancellationToken) is not { ClusterId: { } clusterId } pack
            || !sender.ClusterIds.Contains(ClusterRepository.NormalizeClusterId(clusterId))
            || !BatteryPassWeb.Dpp.Access.DemoScope.Reaches(sender, pack.IsDemo)) // a demo cluster admin manages only demo batteries (finding R03)
        {
            return null;
        }

        var status = await store.PassportStatusAsync(pack.Id, cancellationToken);
        return status == DppStatuses.Active ? (pack, null)
            : DppStatuses.IsMarked(status) ? (pack, status)
            : null;
    }
}
