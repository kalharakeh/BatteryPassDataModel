using System.Security.Cryptography;
using System.Text.RegularExpressions;
using BatteryPassWeb.Dpp.Assembly;
using BatteryPassWeb.Dpp.Catalog;
using BatteryPassWeb.Dpp.Documents;
using BatteryPassWeb.Dpp.Editing;
using BatteryPassWeb.Dpp.Publication;
using BatteryPassWeb.Dpp.Store;
using BatteryPassWeb.Dpp.Values;
using BatteryPassWeb.Services;

namespace BatteryPassWeb.Dpp.Pages;

/// <summary>A registered cluster a battery can belong to.</summary>
public sealed record ClusterChoice(string Id, string Name);

/// <summary>One battery in the list: what it is, where it belongs and how far its passport is.</summary>
public sealed record BatteryRow(
    string PackId,
    string SerialNumber,
    string Identifier,
    string FamilyName,
    string ModelName,
    string BatchLabel,
    string? ClusterId,
    string ClusterLabel,
    PassportVersion? Active)
{
    /// <summary>The newest version, whatever its status: the active one, or an inactive one (step 4c); null for a draft.</summary>
    public PassportVersion? Latest { get; init; }

    public bool IsDraft => Active is null && Latest is null;

    /// <summary>The passport's DPP status (#2), or "draft".</summary>
    public string StatusName => (Active ?? Latest)?.DppStatus ?? "draft";
}

public sealed record BatteriesPage(IReadOnlyList<BatteryRow> Rows, string Query, int Total);

/// <summary>A batch a new battery can be made in, named by its family, model, year and site.</summary>
public sealed record BatchChoice(string BatchId, string FamilyName, string ModelName, int Year, string SiteName)
{
    public string Label => $"{ModelName} · {Year} · {SiteName}";
}

/// <summary>The form for a new battery: where it comes from, and its own values as typed.</summary>
public sealed record NewBatteryPage(
    IReadOnlyList<BatchChoice> Batches,
    IReadOnlyList<EconomicOperator> Operators,
    IReadOnlyList<ClusterChoice> Clusters,
    RecordEditorView Values,
    string? BatchId,
    string? OperatorId,
    string? ClusterId,
    bool LinksConfigured);

/// <summary>Values the passport still needs, grouped by the record that holds them, with a link to its editor.</summary>
public sealed record MissingGroup(string Record, string Href, IReadOnlyList<AttributeDefinition> Attributes);

/// <summary>An uploaded file the passport uses, and whether its bytes still match the SHA-256 taken at upload.</summary>
public sealed record FileCheck(DppDocument Document, string Owner, bool Unchanged);

/// <summary>Where the battery's passport stands: published or not, what blocks it, what is needed later, and its files.</summary>
public sealed record PublicationView(
    PassportVersion? Active,
    PassportVersion? Latest,
    IReadOnlyList<MissingGroup> Missing,
    IReadOnlyList<string> Problems,
    IReadOnlyList<AttributeDefinition> NeededLater,
    IReadOnlyList<FileCheck> Files)
{
    public bool IsPublished => Latest is not null;

    /// <summary>Published once and no longer active: inactive, invalid or marked for deletion (step 4c).</summary>
    public bool NoLongerActive => Active is null && Latest is not null;

    public bool Ready => Missing.Count == 0 && Problems.Count == 0;

    public int MissingCount => Missing.Sum(group => group.Attributes.Count);
}

/// <summary>The team's "Passport status" card (S5): the passport's DPP status, its mark, whether the battery is waste.</summary>
/// <param name="Status">The DPP status (#2) of the passport's newest version; null for a draft.</param>
public sealed record PassportMarkView(string? Status, PassportMark? Mark, bool Waste, string Action)
{
    public bool Active => Status == DppStatuses.Active;

    public bool Marked => Status is DppStatuses.Invalid or DppStatuses.MarkedForDeletion;
}

/// <summary>One published version of the passport and whether its signature still holds.</summary>
public sealed record VersionRow(PassportVersion Version, bool SignatureHolds);

/// <summary>Everything the battery page shows.</summary>
public sealed record BatteryPage(
    BatteryPack Pack,
    PassportSources Sources,
    BatteryFamily Family,
    string SerialNumber,
    string Identifier,
    string ClusterLabel,
    IReadOnlyList<ClusterChoice> Clusters,
    RecordEditorView Values,
    RecordEditorView Live,
    PublicationView Publication,
    IReadOnlyList<VersionRow> Versions,
    IReadOnlyList<DppDocument> AccidentReports,
    RemovalView Removal)
{
    public string BatchLabel => $"{Sources.Batch.Year} · {Sources.Site.Name}";

    /// <summary>The battery status card (step 4b), shared with the cluster admin's page.</summary>
    public StatusCardView? Status { get; init; }

    /// <summary>The team's "Passport status" card (mock-up S5, step 4c-2): invalid, marked for deletion, and their undo.</summary>
    public PassportMarkView Marks => new(
        Publication.Active is not null ? DppStatuses.Active : Publication.Latest?.DppStatus,
        Pack.Mark,
        StatusChangeService.CurrentStatus(Pack) == "waste",
        $"{BatteryPages.BatteryHref(Pack.Id)}/mark");

    /// <summary>The cards this page shares with the cluster admin's battery page (step 4a).</summary>
    public BatteryCards Cards => new(
        Pack,
        BatteryPages.BatteryHref(Pack.Id),
        Live,
        AccidentReports,
        Versions,
        Values.Languages,
        ForTeam: true,
        $"{ProductDataPages.FamilyHref(Sources.Model.FamilyId, Sources.Model.Id)}#modules",
        !string.IsNullOrWhiteSpace(Sources.Model.ModuleDescription),
        Frozen: Publication.NoLongerActive);
}

/// <summary>
/// The team's batteries pages (design 12 and 14, step 2d-2): the list with its search, a new battery, and the battery
/// page with its values, live values, cluster, publication checks, versions and accident reports. Values are saved by
/// <see cref="RecordEditor"/> through the save pipeline; this class loads the pages and makes the changes that aren't
/// passport values (the cluster, publishing, removing a draft).
/// </summary>
public sealed class BatteryPages(
    DppStore store,
    RecordEditor editor,
    PackCreationService creation,
    PublicationService publication,
    DocumentStore documents,
    DppLinks links,
    ClusterRepository clusters,
    ApplicationAuditService audit,
    StatusPages status)
{
    public const string ClusterChangedEvent = "dpp.pack-cluster-changed";
    public const string ModulesSetEvent = "dpp.pack-modules-set";

    public static string BatteryHref(string packId) => $"/admin/batteries/{Uri.EscapeDataString(packId)}";

    /// <summary>
    /// Every battery, newest model first by name, filtered by <paramref name="query"/>: a passport ID (#5, any version), a
    /// battery ID (#6), a serial number (#11) or a cluster's ID or name, ignoring case; part of one is enough.
    /// </summary>
    public async Task<BatteriesPage> ListAsync(string? query, CancellationToken cancellationToken = default)
    {
        var packs = await store.ListAsync<BatteryPack>(cancellationToken);
        var batches = (await store.ListAsync<ProductionBatch>(cancellationToken)).ToDictionary(batch => batch.Id, StringComparer.Ordinal);
        var models = (await store.ListAsync<BatteryModel>(cancellationToken)).ToDictionary(model => model.Id, StringComparer.Ordinal);
        var families = (await store.ListAsync<BatteryFamily>(cancellationToken)).ToDictionary(family => family.Id, StringComparer.Ordinal);
        var sites = (await store.ListAsync<ManufacturingSite>(cancellationToken)).ToDictionary(site => site.Id, StringComparer.Ordinal);
        var clusterNames = (await ClustersAsync(cancellationToken)).ToDictionary(cluster => cluster.Id, cluster => cluster.Name, StringComparer.OrdinalIgnoreCase);
        var versions = (await store.ListAsync<PassportVersion>(cancellationToken)).ToLookup(version => version.PackId, StringComparer.Ordinal);

        var rows = new List<BatteryRow>();
        foreach (var pack in packs)
        {
            var batch = batches.GetValueOrDefault(pack.BatchId);
            var model = batch is null ? null : models.GetValueOrDefault(batch.ModelId);
            var family = model is null ? null : families.GetValueOrDefault(model.FamilyId);
            var site = batch is null ? null : sites.GetValueOrDefault(batch.SiteId);
            var active = versions[pack.Id].Where(version => version.DppStatus == DppStatuses.Active).MaxBy(version => version.Number);
            rows.Add(new BatteryRow(
                pack.Id,
                Text(pack, "batterySerialNumber") ?? pack.Id,
                Text(pack, "uniqueProductIdentifier") ?? string.Empty,
                family?.Name ?? "Unknown family",
                model?.Name ?? "Unknown model",
                batch is null ? "Unknown batch" : $"{batch.Year} · {site?.Name ?? batch.SiteId}",
                pack.ClusterId,
                ClusterLabel(pack.ClusterId, clusterNames),
                active)
            {
                Latest = versions[pack.Id].MaxBy(version => version.Number),
            });
        }

        var wanted = query?.Trim() ?? string.Empty;
        var shown = rows
            .Where(row => wanted.Length == 0 || Matches(row, wanted, versions[row.PackId]))
            .OrderBy(row => row.FamilyName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(row => row.ModelName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(row => row.SerialNumber, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        return new BatteriesPage(shown, wanted, rows.Count);
    }

    /// <summary>The API help's example battery: a published demo battery's ID, or none. The help is open to everyone, so it
    /// never shows a real battery's ID (product owner, 30 Oct 2026).</summary>
    public async Task<string> ExampleBatteryIdAsync(CancellationToken cancellationToken = default)
    {
        var published = (await store.ListAsync<PassportVersion>(cancellationToken))
            .Where(version => version.DppStatus == DppStatuses.Active)
            .Select(version => version.PackId)
            .ToHashSet(StringComparer.Ordinal);
        return (await store.ListAsync<BatteryPack>(cancellationToken))
            .Where(pack => pack.IsDemo && published.Contains(pack.Id))
            .Select(pack => Text(pack, "uniqueProductIdentifier") ?? string.Empty)
            .Where(identifier => identifier.Length > 0)
            .Order(StringComparer.Ordinal)
            .FirstOrDefault() ?? string.Empty;
    }

    /// <summary>True when the search text is part of the battery's passport IDs, battery ID, serial number or cluster.</summary>
    public static bool Matches(BatteryRow row, string query, IEnumerable<PassportVersion> versions)
    {
        ArgumentNullException.ThrowIfNull(row);
        ArgumentNullException.ThrowIfNull(versions);
        bool Has(string? text) => text is not null && text.Contains(query, StringComparison.OrdinalIgnoreCase);
        return Has(row.SerialNumber)
            || Has(row.Identifier)
            || Has(row.ClusterId)
            || (row.ClusterId is not null && Has(row.ClusterLabel))
            || versions.Any(version => Has(version.DigitalProductPassportId));
    }

    /// <summary>The new-battery form; with <paramref name="post"/>, as typed, with the refused values marked.</summary>
    public async Task<NewBatteryPage> NewAsync(
        string? batchId = null,
        string? operatorId = null,
        string? clusterId = null,
        EditorState? state = null,
        CancellationToken cancellationToken = default)
    {
        var batches = await store.ListAsync<ProductionBatch>(cancellationToken);
        var models = (await store.ListAsync<BatteryModel>(cancellationToken)).ToDictionary(model => model.Id, StringComparer.Ordinal);
        var families = (await store.ListAsync<BatteryFamily>(cancellationToken)).ToDictionary(family => family.Id, StringComparer.Ordinal);
        var sites = (await store.ListAsync<ManufacturingSite>(cancellationToken)).ToDictionary(site => site.Id, StringComparer.Ordinal);
        var choices = batches
            .Where(batch => models.ContainsKey(batch.ModelId))
            .Select(batch =>
            {
                var model = models[batch.ModelId];
                return new BatchChoice(
                    batch.Id,
                    families.GetValueOrDefault(model.FamilyId)?.Name ?? "Unknown family",
                    model.Name,
                    batch.Year,
                    sites.GetValueOrDefault(batch.SiteId)?.Name ?? batch.SiteId);
            })
            .OrderBy(choice => choice.FamilyName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(choice => choice.ModelName, StringComparer.OrdinalIgnoreCase)
            .ThenByDescending(choice => choice.Year)
            .ThenBy(choice => choice.SiteName, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var operators = (await store.ListAsync<EconomicOperator>(cancellationToken)).OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase).ToArray();
        var empty = new Dictionary<string, AttributeValue>(StringComparer.Ordinal);
        var sections = await editor.SectionsAsync(TargetKind.Pack, string.Empty, empty, state?.Post, state?.Save.Refusals(editor.Forms), cancellationToken);
        var values = new RecordEditorView(TargetKind.Pack, string.Empty, string.Empty, "/admin/batteries", "/admin/batteries", sections, [], 0, editor.Forms.Languages, string.Empty, new RemovalView(string.Empty, null));
        return new NewBatteryPage(choices, operators, await ClustersAsync(cancellationToken), values, batchId, operatorId, clusterId, links.IsConfigured);
    }

    /// <summary>
    /// Creates a draft battery (design 7.1). The cluster is optional (decision of 29 Oct 2026) but must exist; the
    /// values go through the same checks as any save, and a serial number another battery has is refused.
    /// </summary>
    public async Task<(PackCreationResult Result, EditorPost Post)> CreateAsync(
        string who,
        string? batchId,
        string? operatorId,
        string? clusterId,
        Func<string, string?> field,
        CancellationToken cancellationToken = default)
    {
        var post = editor.Read(TargetKind.Pack, new Dictionary<string, AttributeValue>(StringComparer.Ordinal), field);
        if (string.IsNullOrWhiteSpace(batchId))
        {
            return (Problem("choose the batch the battery was made in"), post);
        }

        if (string.IsNullOrWhiteSpace(operatorId))
        {
            return (Problem("choose the economic operator that places it on the market"), post);
        }

        var cluster = string.IsNullOrWhiteSpace(clusterId) ? null : clusterId.Trim();
        if (cluster is not null && await clusters.GetClusterByIdAsync(cluster, cancellationToken) is null)
        {
            return (Problem($"cluster {cluster} doesn't exist; choose a registered cluster or none"), post);
        }

        var values = post.Changed.ToDictionary(entry => entry.Key, entry => (System.Text.Json.Nodes.JsonNode?)entry.Value.DeepClone(), StringComparer.Ordinal);
        return (await creation.CreateAsync(ChangeSender.Team(who), new NewPack(batchId, operatorId, cluster, values), cancellationToken), post);

        static PackCreationResult Problem(string problem) => new(null, [], problem);
    }

    /// <summary>The battery page; with <paramref name="values"/> or <paramref name="live"/>, that form as typed after a refused save.</summary>
    public async Task<BatteryPage?> BatteryAsync(string packId, EditorState? values = null, EditorState? live = null, CancellationToken cancellationToken = default)
    {
        var sources = await store.LoadSourcesAsync(packId, cancellationToken);
        var family = sources is null ? null : await store.GetAsync<BatteryFamily>(sources.Model.FamilyId, cancellationToken);
        if (sources is null || family is null)
        {
            return null;
        }

        var pack = sources.Pack;
        var versions = await store.ListVersionsAsync(packId, cancellationToken);
        var active = versions.Where(version => version.DppStatus == DppStatuses.Active).MaxBy(version => version.Number);
        var href = BatteryHref(packId);
        var valueAttributes = ValueAttributes(published: versions.Count > 0);
        var valueSections = await editor.SectionsAsync(TargetKind.Pack, packId, valueAttributes, pack.Values, values?.Post, values?.Save.Refusals(editor.Forms), cancellationToken);
        var valuesView = new RecordEditorView(
            TargetKind.Pack,
            packId,
            Text(pack, "batterySerialNumber") ?? packId,
            href,
            href,
            valueSections,
            values?.Save.Outcome == EditorOutcome.NeedsConfirmation ? values.Save.Affected : [],
            active is null ? 0 : 1,
            editor.Forms.Languages,
            editor.Stamp(valueAttributes, pack.Values, null),
            new RemovalView(string.Empty, null));
        var liveSections = await editor.SectionsAsync(TargetKind.Pack, packId, RecordEditor.LiveAttributes, pack.Values, live?.Post, live?.Save.Refusals(editor.Forms), cancellationToken);
        var liveView = new RecordEditorView(
            TargetKind.Pack,
            packId,
            Text(pack, "batterySerialNumber") ?? packId,
            $"{href}/live",
            $"{href}#live",
            liveSections,
            [],
            0,
            editor.Forms.Languages,
            editor.Stamp(RecordEditor.LiveAttributes, pack.Values, null),
            new RemovalView(string.Empty, null));
        var clusterChoices = await ClustersAsync(cancellationToken);
        return new BatteryPage(
            pack,
            sources,
            family,
            Text(pack, "batterySerialNumber") ?? packId,
            Text(pack, "uniqueProductIdentifier") ?? string.Empty,
            ClusterLabel(pack.ClusterId, clusterChoices.ToDictionary(cluster => cluster.Id, cluster => cluster.Name, StringComparer.OrdinalIgnoreCase)),
            clusterChoices,
            valuesView,
            liveView,
            await PublicationAsync(sources, active, cancellationToken),
            versions.OrderByDescending(version => version.Number).Select(version => new VersionRow(version, PassportReader.Verify(version))).ToArray(),
            await documents.ListAsync(TargetKind.Pack, packId, DocumentService.Accidents, cancellationToken),
            new RemovalView($"{href}/remove", versions.Count > 0 ? "its passport is published, so it stays with its versions." : null))
        {
            Status = await status.CardAsync(sources, active is not null, href, forTeam: true, cancellationToken: cancellationToken),
        };
    }

    /// <summary>
    /// The pack values the battery page edits: all four while the passport is a draft; once published, only those not
    /// fixed (#13), since #11, #17 and #19 can no longer change.
    /// </summary>
    public static IReadOnlyList<AttributeDefinition> ValueAttributes(bool published) =>
        RecordEditor.AttributesOf(TargetKind.Pack).Where(attribute => !published || !SavePipeline.IsFixedPackValue(attribute)).ToArray();

    /// <summary>Saves the battery's own values or its live values, typed on its page.</summary>
    public async Task<(EditorPost Post, EditorSave Save)?> SaveAsync(
        string who,
        string packId,
        bool liveValues,
        Func<string, string?> field,
        string? stamp,
        DateTime? measuredAtUtc,
        bool confirmed,
        CancellationToken cancellationToken = default)
    {
        var pack = await store.GetAsync<BatteryPack>(packId, cancellationToken);
        if (pack is null)
        {
            return null;
        }

        var attributes = liveValues ? RecordEditor.LiveAttributes : ValueAttributes(await store.GetLatestVersionNumberAsync(packId, cancellationToken) > 0);
        var post = editor.Read(attributes, pack.Values, field, stamp) with { MeasuredAtUtc = liveValues ? measuredAtUtc : null };
        return (post, await editor.SaveAsync(ChangeSender.Team(who), TargetKind.Pack, packId, post, confirmed, cancellationToken));
    }

    /// <summary>
    /// Moves a battery to another registered cluster, or out of any (design 7.8, step 4: the team moves a pack directly).
    /// The cluster isn't passport data, so no new version is made; the move is logged. Null when it moved.
    /// </summary>
    public async Task<string?> SetClusterAsync(string who, string packId, string? clusterId, CancellationToken cancellationToken = default)
    {
        var pack = await store.GetAsync<BatteryPack>(packId, cancellationToken);
        if (pack is null)
        {
            return "the battery was not found";
        }

        var wanted = string.IsNullOrWhiteSpace(clusterId) ? null : clusterId.Trim();
        if (string.Equals(wanted, pack.ClusterId, StringComparison.OrdinalIgnoreCase))
        {
            return "it is already there";
        }

        var names = (await ClustersAsync(cancellationToken)).ToDictionary(cluster => cluster.Id, cluster => cluster.Name, StringComparer.OrdinalIgnoreCase);
        if (wanted is not null && !names.ContainsKey(wanted))
        {
            return $"cluster {wanted} doesn't exist";
        }

        await store.SetPackClusterAsync(packId, wanted, cancellationToken);
        var serial = Text(pack, "batterySerialNumber") ?? packId;
        await audit.AppendApplicationAuditEventAsync(
            ClusterChangedEvent,
            who,
            "Team",
            "form",
            $"Battery {serial} moved from {ClusterLabel(pack.ClusterId, names)} to {ClusterLabel(wanted, names)}",
            new MongoDB.Bson.BsonDocument
            {
                ["from"] = pack.ClusterId is null ? MongoDB.Bson.BsonNull.Value : pack.ClusterId,
                ["to"] = wanted is null ? MongoDB.Bson.BsonNull.Value : wanted,
            },
            entityType: "dpp-pack",
            entityId: packId,
            cancellationToken: cancellationToken);
        return null;
    }

    /// <summary>The battery's module list as the team typed it (decision 31). Null when it was saved.</summary>
    public Task<string?> SetModulesAsync(string who, string packId, string? text, CancellationToken cancellationToken = default) =>
        SaveModulesAsync(who, packId, _ => ModuleList.FromText(text), "typed", cancellationToken);

    /// <summary>The battery's module list from the battery system's JSON export of this battery (decision 31). Null when it was saved.</summary>
    public Task<string?> ImportModulesAsync(string who, string packId, byte[] export, CancellationToken cancellationToken = default) =>
        SaveModulesAsync(who, packId, serial => serial is null
            ? ([], "type the battery's serial number first; the export is matched to it")
            : ModuleList.FromExport(export, serial), "from the battery system's export", cancellationToken);

    /// <summary>
    /// The module list isn't passport data (decision 31), so no new version is made; the change is logged with the old
    /// and the new count.
    /// </summary>
    private async Task<string?> SaveModulesAsync(
        string who,
        string packId,
        Func<string?, (IReadOnlyList<BatteryModule> Modules, string? Problem)> read,
        string how,
        CancellationToken cancellationToken)
    {
        var pack = await store.GetAsync<BatteryPack>(packId, cancellationToken);
        if (pack is null)
        {
            return "the battery was not found";
        }

        var serial = Text(pack, "batterySerialNumber");
        var (modules, problem) = read(serial);
        if (problem is not null)
        {
            return problem;
        }

        await store.SetPackModulesAsync(packId, modules, cancellationToken);
        await audit.AppendApplicationAuditEventAsync(
            ModulesSetEvent,
            who,
            "Team",
            "form",
            $"Modules of battery {serial ?? packId} set {how}: {Words.Count(modules.Count, "module")}",
            new MongoDB.Bson.BsonDocument { ["from"] = pack.Modules.Count, ["to"] = modules.Count },
            entityType: "dpp-pack",
            entityId: packId,
            cancellationToken: cancellationToken);
        return null;
    }

    /// <summary>
    /// Removes a draft battery, e.g. one made in the wrong batch, with its accident reports. A battery whose passport has
    /// been published stays (design 14). Returns the problem, or null when it was removed.
    /// </summary>
    public async Task<(string? Problem, string SerialNumber)> RemoveDraftAsync(string who, string packId, CancellationToken cancellationToken = default)
    {
        var pack = await store.GetAsync<BatteryPack>(packId, cancellationToken);
        if (pack is null)
        {
            return ("it was not found", string.Empty);
        }

        var serial = Text(pack, "batterySerialNumber") ?? packId;
        if (await store.GetLatestVersionNumberAsync(packId, cancellationToken) > 0)
        {
            return ("its passport is published, so it stays with its versions", serial);
        }

        await store.DeleteAsync<BatteryPack>(packId, cancellationToken);
        await documents.DeleteOwnedAsync(TargetKind.Pack, packId, cancellationToken);
        await audit.AppendApplicationAuditEventAsync(
            ProductDataPages.RemovedEvent,
            who,
            "Team",
            "form",
            $"Battery {serial} removed",
            entityType: "dpp-pack",
            entityId: packId,
            cancellationToken: cancellationToken);
        return (null, serial);
    }

    /// <summary>Publishes the draft as version 1 (design 7.1); the result says what blocked it, if anything.</summary>
    public Task<PublicationResult> PublishAsync(string who, string packId, CancellationToken cancellationToken = default) =>
        publication.PublishAsync(ChangeSender.Team(who), packId, cancellationToken);

    public async Task<IReadOnlyList<ClusterChoice>> ClustersAsync(CancellationToken cancellationToken = default) =>
        (await clusters.ListClustersAsync(cancellationToken))
            .Select(cluster => new ClusterChoice(BsonHelpers.GetString(cluster, "clusterId"), BsonHelpers.GetString(cluster, "name")))
            .Where(cluster => cluster.Id.Length > 0)
            .OrderBy(cluster => cluster.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();

    /// <summary>
    /// The publication checks in the team's terms: each missing value under the record that holds it (this battery, its
    /// model, batch, site or operator) with a link to that editor, then what the schema refuses, then what is needed only
    /// after February 2027, then the uploaded files the passport uses, checked against their SHA-256.
    /// </summary>
    private async Task<PublicationView> PublicationAsync(PassportSources sources, PassportVersion? active, CancellationToken cancellationToken)
    {
        var report = await publication.CheckAsync(sources.Pack.Id, cancellationToken) ?? new PublicationReport([], []);
        var missing = new List<AttributeDefinition>();
        var problems = new List<string>();
        foreach (var item in report.Blocking)
        {
            if (Numbered(item) is { } attribute)
            {
                missing.Add(attribute);
            }
            else
            {
                problems.Add(item);
            }
        }

        var groups = missing
            .GroupBy(Holder)
            .OrderBy(group => group.Key)
            .Select(group => Group(group.Key, sources, group.OrderBy(attribute => attribute.Number).ToArray()))
            .ToArray();
        var later = report.Warnings.Select(Numbered).OfType<AttributeDefinition>().OrderBy(attribute => attribute.Number).ToArray();
        return new PublicationView(active, await store.GetLatestVersionAsync(sources.Pack.Id, cancellationToken), groups, problems, later, await FilesAsync(sources, cancellationToken));
    }

    /// <summary>The attribute a report line such as "#21 Battery mass is missing" is about.</summary>
    private static AttributeDefinition? Numbered(string item) =>
        Regex.Match(item, @"^(#\d+|E\d+) ") is { Success: true } match
            ? AttributeCatalog.All.FirstOrDefault(attribute => attribute.Label == match.Groups[1].Value)
            : null;

    private enum Record
    {
        Battery,
        Live,
        Model,
        Batch,
        Site,
        Operator,
    }

    private static Record Holder(AttributeDefinition attribute) => attribute.Storage switch
    {
        StorageLocation.Model or StorageLocation.ModelWithPackOverride => Record.Model,
        StorageLocation.Batch => Record.Batch,
        StorageLocation.Site => Record.Site,
        StorageLocation.Operator => Record.Operator,
        _ => RecordEditor.LiveAttributes.Contains(attribute) ? Record.Live : Record.Battery,
    };

    private static MissingGroup Group(Record holder, PassportSources sources, IReadOnlyList<AttributeDefinition> attributes)
    {
        var href = BatteryHref(sources.Pack.Id);
        return holder switch
        {
            Record.Model => new($"Model {sources.Model.Name}", $"{ProductDataPages.FamilyHref(sources.Model.FamilyId, sources.Model.Id)}#editor", attributes),
            Record.Batch => new($"Batch {sources.Batch.Year} · {sources.Site.Name}", $"/admin/batches/{Uri.EscapeDataString(sources.Batch.Id)}#editor", attributes),
            Record.Site => new($"Site {sources.Site.Name}", $"{ProductDataPages.SitesHref("site", sources.Site.Id)}#editor", attributes),
            Record.Operator => new($"Operator {sources.Operator.Name}", $"{ProductDataPages.SitesHref("operator", sources.Operator.Id)}#editor", attributes),
            Record.Live => new("This battery's live values", $"{href}#live", attributes),
            _ => new("This battery", $"{href}#values", attributes),
        };
    }

    /// <summary>
    /// The uploaded files the passport links to: the model's and batch's restricted documents (#45–48, #29) and the
    /// battery's accident reports, each checked against the SHA-256 taken when it was uploaded (the old Conformance page
    /// did the same for its evidence).
    /// </summary>
    private async Task<IReadOnlyList<FileCheck>> FilesAsync(PassportSources sources, CancellationToken cancellationToken)
    {
        var checks = new List<FileCheck>();
        foreach (var (owner, label, values) in new (TargetKind, string, IReadOnlyDictionary<string, AttributeValue>)[]
        {
            (TargetKind.Model, $"Model {sources.Model.Name}", sources.Model.Values),
            (TargetKind.Batch, $"Batch {sources.Batch.Year} · {sources.Site.Name}", sources.Batch.Values),
        })
        {
            foreach (var property in DppLinks.RestrictedDocumentProperties.Where(values.ContainsKey).Order(StringComparer.Ordinal))
            {
                var url = values[property].Value["url"]?.GetValue<string>() ?? string.Empty;
                var id = url.Contains("/dpp/documents/", StringComparison.Ordinal) ? Uri.UnescapeDataString(url[(url.LastIndexOf('/') + 1)..]) : null;
                if (id is not null && await documents.GetAsync(id, cancellationToken) is { } document && document.OwnerKind == owner)
                {
                    checks.Add(new FileCheck(document, label, await UnchangedAsync(document, cancellationToken)));
                }
            }
        }

        foreach (var report in await documents.ListAsync(TargetKind.Pack, sources.Pack.Id, DocumentService.Accidents, cancellationToken))
        {
            checks.Add(new FileCheck(report, "This battery", await UnchangedAsync(report, cancellationToken)));
        }

        return checks;
    }

    /// <summary>True when the stored bytes still have the SHA-256 recorded at upload; a missing file counts as changed.</summary>
    public async Task<bool> UnchangedAsync(DppDocument document, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(document);
        try
        {
            var bytes = await documents.DownloadAsync(document, cancellationToken);
            return string.Equals(Convert.ToHexStringLower(SHA256.HashData(bytes)), document.Sha256, StringComparison.Ordinal);
        }
        catch (Exception exception) when (exception is MongoDB.Driver.GridFS.GridFSFileNotFoundException or FormatException)
        {
            return false;
        }
    }

    internal static string ClusterLabel(string? clusterId, IReadOnlyDictionary<string, string> names) =>
        clusterId is null ? "No cluster yet" : names.TryGetValue(clusterId, out var name) && name.Length > 0 ? name : clusterId;

    internal static string? Text(BatteryPack pack, string property) =>
        pack.Values.GetValueOrDefault(property)?.Value is System.Text.Json.Nodes.JsonValue value && value.TryGetValue<string>(out var text) ? text : null;
}
