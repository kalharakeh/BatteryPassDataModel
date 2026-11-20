using BatteryPassWeb.Dpp.Identifiers;
using BatteryPassWeb.Dpp.Publication;
using BatteryPassWeb.Dpp.Store;

namespace BatteryPassWeb.Dpp.Pages;

/// <summary>One line of a "To do" box: what, a few words, and the place to do it.</summary>
public sealed record ToDoLine(string Title, string Note, string Href);

/// <summary>One box of the team's "To do" page: its count, up to four lines and a link to all of them.</summary>
/// <param name="Clear">The calm line shown when there is nothing to do.</param>
public sealed record ToDoBox(string Key, string Title, string Icon, int Count, string Clear, IReadOnlyList<ToDoLine> Lines, string? MoreHref, string? MoreText)
{
    /// <summary>The count stopped at a cap, so the real number is at least this (review 2 of N1, point 2).</summary>
    public bool AtLeast { get; init; }
}

/// <summary>The team's "To do" page: the six boxes and how many batteries there are.</summary>
public sealed record ToDoPage(IReadOnlyList<ToDoBox> Boxes, int Batteries);

/// <summary>
/// The team's "To do" page (finding U05, the owner's approved picture B): what needs doing across the batteries, each
/// line opening the place to do it. Every box comes from what the battery list loads or from one query; nothing is read
/// per battery.
/// </summary>
public sealed class ToDoPages(BatteryPages batteries, PublicationService publication, DppStore store, BatteryIdPool pool, DateTime nowUtc)
{
    /// <summary>A model is running low on battery IDs below this many available (decision 75).</summary>
    public const int LowIds = 20;

    /// <summary>How far back the uploads box looks.</summary>
    public const int UploadDays = 14;

    /// <summary>How many lines a box lists; its link opens the rest.</summary>
    public const int Shown = 4;

    public async Task<ToDoPage> BuildAsync(CancellationToken cancellationToken = default)
    {
        var list = await batteries.TeamListAsync(null, cancellationToken);
        var rows = list.Rows;
        var byPack = rows.ToDictionary(row => row.PackId, StringComparer.Ordinal);
        var boxes = new List<ToDoBox>
        {
            await DraftsAsync(rows, cancellationToken),
            Waiting(list),
            await HandOversAsync(byPack, cancellationToken),
            await IdsAsync(cancellationToken),
            await UploadsAsync(byPack, cancellationToken),
            Registry(rows, list.NotInTheRegistry),
        };
        return new ToDoPage(boxes, rows.Count);
    }

    private async Task<ToDoBox> DraftsAsync(IReadOnlyList<BatteryRow> rows, CancellationToken cancellationToken)
    {
        var drafts = rows.Where(row => row.IsDraft).ToArray();
        var reports = await publication.CheckDraftsAsync(drafts.Select(row => row.PackId).ToArray(), cancellationToken);
        var lines = drafts
            .Select(row => (Row: row, Missing: reports.TryGetValue(row.PackId, out var report) ? BatteryPages.MissingCount(report) : 0))
            .Where(item => item.Missing > 0)
            .Select(item => new ToDoLine(item.Row.SerialNumber, $"{item.Row.ModelName} · {Words.Count(item.Missing, "value")} missing", BatteryPages.BatteryHref(item.Row.PackId)))
            .ToArray();
        return new ToDoBox("drafts", "Drafts with missing values", "values", lines.Length, "Every draft has its values.", lines.Take(Shown).ToArray(),
            "/admin/batteries", "Open the battery list");
    }

    /// <summary>Published batteries whose saved change waits for its public passport (the battery list's own marker, wave 2).</summary>
    private static ToDoBox Waiting(BatteriesPage list)
    {
        var lines = list.Waiting
            .Select(row => new ToDoLine(row.SerialNumber, $"{row.ModelName} · saved, not yet public", BatteryPages.BatteryHref(row.PackId)))
            .ToArray();
        return new ToDoBox("waiting", "Saved changes not yet public", "publish", lines.Length, "Every saved change is public.", lines.Take(Shown).ToArray(),
            "/admin/batteries", "Open the battery list");
    }

    private async Task<ToDoBox> HandOversAsync(IReadOnlyDictionary<string, BatteryRow> byPack, CancellationToken cancellationToken)
    {
        var clusters = (await batteries.ClustersAsync(cancellationToken)).ToDictionary(cluster => cluster.Id, cluster => cluster.Name, StringComparer.OrdinalIgnoreCase);
        var lines = (await store.ListWaitingHandOversAsync(cancellationToken))
            .Select(handOver => new ToDoLine(
                byPack.TryGetValue(handOver.PackId, out var row) ? row.SerialNumber : handOver.PackId,
                $"to {(clusters.TryGetValue(handOver.ToClusterId, out var name) && name.Length > 0 ? name : handOver.ToClusterId)}",
                BatteryPages.BatteryHref(handOver.PackId)))
            .ToArray();
        return new ToDoBox("handovers", "Hand-overs waiting", "handover", lines.Length, "No hand-over is waiting.", lines.Take(Shown).ToArray(),
            "/admin/batteries", "Open the battery list");
    }

    /// <summary>
    /// The real models, and "Any model", with fewer than <see cref="LowIds"/> real IDs available: as the Battery IDs page
    /// counts them, demo models and demo or test IDs aside.
    /// </summary>
    private async Task<ToDoBox> IdsAsync(CancellationToken cancellationToken)
    {
        var available = await pool.AvailableAsync(demo: false, cancellationToken: cancellationToken);
        var models = (await store.ListAsync<BatteryModel>(cancellationToken)).Where(model => !model.IsDemo).ToArray();
        var lines = models
            .Select(model => (Name: model.Name, Left: available.Count(item => item.ModelId == model.Id)))
            .OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
            .Append((Name: "Any model", Left: available.Count(item => item.ModelId is null)))
            .Where(item => item.Left < LowIds)
            .OrderBy(item => item.Left)
            .Select(item => new ToDoLine(item.Name, item.Left == 0 ? "no IDs left" : $"{Words.Count(item.Left, "ID")} left", "/admin/battery-ids"))
            .ToArray();
        return new ToDoBox("ids", "Battery IDs running low", "passport", lines.Length, $"Every model has {LowIds} or more IDs left.", lines.Take(Shown).ToArray(),
            "/admin/battery-ids", "Battery IDs");
    }

    private async Task<ToDoBox> UploadsAsync(IReadOnlyDictionary<string, BatteryRow> byPack, CancellationToken cancellationToken)
    {
        var read = await store.ListRefusedUploadsSinceAsync(nowUtc.AddDays(-UploadDays), cancellationToken);
        var lines = read
            .SelectMany(upload => upload.Batteries
                .Where(battery => battery.Outcome == "refused" || battery.Values.Any(value => value.Outcome == "Rejected"))
                .Select(battery => new ToDoLine(
                    battery.PackId is not null && byPack.TryGetValue(battery.PackId, out var row) ? row.SerialNumber : battery.NamedAs,
                    $"{(battery.Outcome == "refused" ? "file refused" : "a value refused")} · {ValueWords.Time(upload.AtUtc)}",
                    battery.PackId is null ? "/admin/audit" : $"{BatteryPages.BatteryHref(battery.PackId)}?section=upload")))
            .ToArray();
        return new ToDoBox("uploads", "Uploads with refused values", "upload", lines.Length, $"No value was refused in the last {UploadDays} days.", lines.Take(Shown).ToArray(),
            "/admin/audit", "Audit log")
        {
            AtLeast = read.Count >= DppStore.RefusedUploadsRead, // the read stops at the newest 200 (point 5 of review 1)
        };
    }

    /// <summary>Published real batteries not recorded in the EU registry yet (wave 2's count, finding R02).</summary>
    private static ToDoBox Registry(IReadOnlyList<BatteryRow> rows, int count)
    {
        var lines = rows.Where(row => row.NotInTheRegistry)
            .Select(row => new ToDoLine(row.SerialNumber, $"{row.ModelName} · not recorded yet", $"{BatteryPages.BatteryHref(row.PackId)}?section=registry"))
            .ToArray();
        return new ToDoBox("registry", "Not yet in the EU registry", "globe", count, "Every published battery is in the EU registry.", lines.Take(Shown).ToArray(),
            "/admin/batteries?registry=missing", "All of them");
    }
}
