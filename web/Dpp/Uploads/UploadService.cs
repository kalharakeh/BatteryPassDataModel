using System.Text.Json;
using System.Text.Json.Nodes;
using BatteryPassWeb.Dpp.Api;
using BatteryPassWeb.Dpp.Catalog;
using BatteryPassWeb.Dpp.Editing;
using BatteryPassWeb.Dpp.Pages;
using BatteryPassWeb.Dpp.Store;
using BatteryPassWeb.Services;
using MongoDB.Bson;

namespace BatteryPassWeb.Dpp.Uploads;

/// <summary>What happened to one battery of an upload.</summary>
/// <param name="Outcome">One of <see cref="UploadService.Saved"/>, <see cref="UploadService.NothingSaved"/> or <see cref="UploadService.Refused"/>.</param>
/// <param name="Problem">Why the battery was refused as a whole; null when it wasn't.</param>
/// <param name="Modules">What happened to the battery's module list, when the file had one; null otherwise.</param>
/// <param name="Notes">What the file's adapter wants the sender to know, e.g. a value it couldn't read.</param>
/// <param name="Sent">The values the battery's entry sent, as JSON text, by key (step 5c).</param>
/// <param name="Status">The status change or repair in words (step 5d); null when the entry asked for none.</param>
/// <param name="Kept">Re-stated values the file left out, taken from the battery's current ones (Q8).</param>
public sealed record UploadedBatteryReport(
    int Entry,
    string NamedAs,
    string? BatteryId,
    string? PackId,
    string Outcome,
    string? Problem,
    IReadOnlyList<KeyResult> Values,
    string? Modules = null,
    IReadOnlyList<string>? Notes = null,
    string? ClusterId = null,
    IReadOnlyDictionary<string, string?>? Sent = null,
    string? Status = null,
    IReadOnlyCollection<string>? Kept = null);

/// <summary>An upload's result: per battery what happened, or why the file couldn't be read at all.</summary>
/// <param name="UploadId">The log entry; null for a preview or an unreadable file, which aren't logged.</param>
/// <param name="CheckId">The checked file kept for "Save" on a battery's page (step 5c); null otherwise.</param>
public sealed record UploadReport(string? Format, bool Preview, IReadOnlyList<UploadedBatteryReport> Batteries, string? Problem, string? UploadId, string? CheckId = null)
{
    /// <summary>True when at least one battery had a value saved (or, in a preview, would have).</summary>
    public bool AnySaved => Batteries.Any(battery => battery.Outcome == UploadService.Saved);
}

/// <summary>
/// Uploads (design 8.1 and 8.2, decision 8, step 5a): a file of many batteries, read by its format's adapter, each
/// battery found as the API finds it and its values sent through the one save pipeline (design 6.2), which decides what
/// this sender may change. A battery's refused value doesn't block its other values (J-12); a battery that can't be
/// changed doesn't block the others. Each upload that isn't a preview is logged (<see cref="UploadRecord"/>).
/// </summary>
public sealed class UploadService(
    DppStore store,
    SavePipeline pipeline,
    BatteryApi batteries,
    ApplicationAuditService audit,
    TimeProvider clock,
    StatusChangeService statusChanges,
    IEnumerable<IUploadAdapter> adapters)
{
    /// <summary>Why an API upload's battery with a status change is refused (decision 35, Q7 and Q23).</summary>
    public const string StatusOnThePage = "status changes are made on the battery's page";

    public const int MaxBatteries = 500;
    public const int MaxBytes = 2 * 1024 * 1024;
    public const string UploadedEvent = "dpp.upload";

    public const string Saved = "saved";
    public const string NothingSaved = "nothing saved";
    public const string Refused = "refused";

    public const string NotAnUpload = "the file isn't an upload this app reads: give \"format\": \"dpp-v3-upload/1\" and the batteries, or send the battery system's export (mapping BMS2_ACO)";
    public const string NotFound = "no battery you may change has this ID";
    public const string NotYours = "this battery belongs to another cluster";
    public const string Ambiguous = "more than one battery has this serial number; use the battery ID";
    public const string NamedTwice = "the file names this battery more than once";
    public const string InTheFuture = "measuredAt lies in the future";
    public const string TooLarge = "the file is larger than 2 MB";
    public const string NotJson = "the file isn't a JSON file";
    public const string CheckExpired = "the check has expired; choose the file again";

    /// <summary>How long a checked file is kept for "Save" (decision 35).</summary>
    public static readonly TimeSpan CheckKeptFor = TimeSpan.FromHours(1);

    /// <summary>How many earlier uploads a battery's page lists.</summary>
    public const int HistoryLength = 5;

    /// <summary>Why a file on a battery's page is refused: it names another battery.</summary>
    public static string NotThisBattery(string namedAs) => $"the file is for battery {namedAs}, not this one";

    /// <summary>Why a file on a battery's page is refused: it names several batteries.</summary>
    public static string OneBatteryOnly(int count) => $"the file lists {count} batteries; a battery's page takes a file for that battery only";

    private readonly IUploadAdapter[] _adapters = adapters.ToArray();

    /// <summary>Reads a file's bytes as JSON (a leading byte order mark is fine); null when it isn't JSON.</summary>
    public static JsonNode? Parse(ReadOnlySpan<byte> file)
    {
        try
        {
            return JsonNode.Parse(file.StartsWith("﻿"u8) ? file[3..] : file);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <param name="channel">"page" or "api".</param>
    public async Task<UploadReport> RunAsync(ChangeSender sender, JsonNode? file, string channel, string? fileName, bool preview, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(sender);
        if (file is not JsonObject root)
        {
            return new UploadReport(null, preview, [], NotAnUpload, null);
        }

        var document = _adapters.FirstOrDefault(adapter => adapter.Reads(root))?.Read(root) ?? UploadDocument.Unreadable(NotAnUpload);
        if (document.Problem is not null)
        {
            return new UploadReport(document.Format, preview, [], document.Problem, null);
        }

        // Find every battery first, so a battery the file names twice is refused both times.
        var found = new List<(UploadEntry Entry, WriteTarget? Target, string? Problem)>();
        foreach (var entry in document.Entries)
        {
            found.Add(await FindAsync(sender, entry, cancellationToken));
        }

        var twice = found.Where(item => item.Target?.Pack is not null)
            .GroupBy(item => item.Target!.Pack!.Id)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToHashSet(StringComparer.Ordinal);

        var reports = new List<UploadedBatteryReport>();
        foreach (var (entry, target, problem) in found)
        {
            var pack = target?.Pack;
            var notes = entry.Notes ?? [];
            if (problem is not null || pack is null || twice.Contains(pack.Id))
            {
                reports.Add(new UploadedBatteryReport(entry.Number, entry.NamedAs, target?.Identifier, pack?.Id, Refused, problem ?? NamedTwice, [], Notes: notes, ClusterId: Cluster(pack)));
                continue;
            }

            if (await store.PassportStatusAsync(pack.Id, cancellationToken) is { } status && DppStatuses.IsNoLongerActive(status))
            {
                reports.Add(new UploadedBatteryReport(entry.Number, entry.NamedAs, target!.Identifier, pack.Id, Refused, SavePipeline.NoLongerChanges(status), [], Notes: notes, ClusterId: Cluster(pack)));
                continue;
            }

            // A status change or repair first (Q22): refused, nothing of the battery is saved; the API takes none (Q23).
            string? statusWords = null;
            IReadOnlyList<KeyResult> statusKeys = [];
            IReadOnlyCollection<string> kept = [];
            var restatedSent = new Dictionary<string, string?>(StringComparer.Ordinal);
            var current = entry;
            if (entry.Status is { } asked)
            {
                if (channel != "page")
                {
                    reports.Add(new UploadedBatteryReport(entry.Number, entry.NamedAs, target!.Identifier, pack.Id, Refused, StatusOnThePage, [], Notes: notes, ClusterId: Cluster(pack)));
                    continue;
                }

                var (changed, keptKeys, restated, day) = await StatusAsync(sender, pack, entry, asked, preview, cancellationToken);
                statusKeys = changed.Keys.Where(key => key.Key != StatusProperty).ToArray();
                kept = keptKeys;
                foreach (var key in restated.Keys)
                {
                    restatedSent[key] = keptKeys.Contains(key) ? null : restated[key]?.ToJsonString();
                }

                if (changed.Problem is { } refused)
                {
                    reports.Add(new UploadedBatteryReport(entry.Number, entry.NamedAs, target!.Identifier, pack.Id, Refused, refused, statusKeys, Notes: notes, ClusterId: Cluster(pack), Sent: restatedSent, Kept: kept));
                    continue;
                }

                statusWords = StatusWords(asked, day, changed.NewStatus ?? string.Empty, done: !preview);
                current = entry with { Values = entry.Values.Where(pair => !restated.ContainsKey(pair.Key)).ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal) };
            }

            var source = Source(sender);
            var (values, unused) = await FillOnlyAsync(sender, pack, current, cancellationToken);
            notes = [.. notes, .. unused];
            var result = values.Count == 0
                ? new SaveResult(true, false, [])
                : await pipeline.SaveAsync(sender, new ChangeSet(TargetKind.Pack, pack.Id, values, source, entry.MeasuredAtUtc), preview, cancellationToken);
            var modules = await ModulesAsync(sender, pack, entry.Modules, preview, cancellationToken);
            if (!preview && entry.RemainingCapacityAh is { } remaining)
            {
                await store.SetRemainingCapacityAsync(pack.Id, new RemainingCapacityReading { Ah = remaining, MeasuredAtUtc = entry.MeasuredAtUtc ?? clock.GetUtcNow().UtcDateTime }, cancellationToken);
            }

            var outcome = statusWords is not null || result.Keys.Any(key => key.Outcome == KeyOutcome.Saved) || modules is { Changed: true } ? Saved : NothingSaved;
            var sent = values.ToDictionary(pair => pair.Key, pair => pair.Value?.ToJsonString(), StringComparer.Ordinal);
            foreach (var (key, value) in restatedSent)
            {
                sent[key] = value;
            }

            reports.Add(new UploadedBatteryReport(entry.Number, entry.NamedAs, target!.Identifier, pack.Id, outcome, null, [.. statusKeys, .. result.Keys], modules?.Words, notes, Cluster(pack), sent, statusWords, kept));
        }

        string? uploadId = null;
        if (!preview)
        {
            uploadId = await LogAsync(sender, channel, fileName, document.Format!, reports, cancellationToken);
        }

        return new UploadReport(document.Format, preview, reports, null, uploadId);
    }

    /// <summary>
    /// The entry's values without the fill-only ones it may not use (decision 35, Q15): such a value (the export's #17)
    /// fills a draft without one, from a sender who may set it. Otherwise it is left out with a note, not refused, so
    /// the pipeline's rules aren't bent and a battery system's report isn't one refusal longer each time.
    /// </summary>
    private async Task<(IReadOnlyDictionary<string, JsonNode?> Values, IReadOnlyList<string> Unused)> FillOnlyAsync(
        ChangeSender sender, BatteryPack pack, UploadEntry entry, CancellationToken cancellationToken)
    {
        if (entry.FillOnly is not { Count: > 0 } fillOnly || !entry.Values.Keys.Any(fillOnly.Contains))
        {
            return (entry.Values, []);
        }

        var draft = await store.GetLatestVersionNumberAsync(pack.Id, cancellationToken) == 0;
        var values = new Dictionary<string, JsonNode?>(entry.Values, StringComparer.Ordinal);
        var unused = new List<string>();
        foreach (var key in entry.Values.Keys.Where(fillOnly.Contains))
        {
            var attribute = AttributeCatalog.FindByProperty(key)!;
            if (!draft || pack.Values.ContainsKey(key) || !EditRights.MayEdit(sender, attribute, pack.ClusterId))
            {
                values.Remove(key);
                unused.Add($"the {char.ToLowerInvariant(attribute.Name[0])}{attribute.Name[1..]} in the file isn't used");
            }
        }

        return (values, unused);
    }

    /// <summary>
    /// A file chosen on a battery's page (decision 35, step 5c): it must name that battery only, and is checked like a
    /// preview. It is kept for an hour, so the page can show the check and its checker can "Save" it.
    /// </summary>
    public async Task<UploadReport> CheckBatteryFileAsync(ChangeSender sender, string packId, string fileName, byte[] content, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(sender);
        ArgumentNullException.ThrowIfNull(content);
        if (content.Length > MaxBytes)
        {
            return new UploadReport(null, true, [], TooLarge, null);
        }

        if (Parse(content) is not { } json)
        {
            return new UploadReport(null, true, [], NotJson, null);
        }

        var document = json is JsonObject root
            ? _adapters.FirstOrDefault(adapter => adapter.Reads(root))?.Read(root) ?? UploadDocument.Unreadable(NotAnUpload)
            : UploadDocument.Unreadable(NotAnUpload);
        var problem = document.Problem
            ?? (document.Entries.Count != 1 ? OneBatteryOnly(document.Entries.Count) : null)
            ?? document.Entries[0].Problem;
        if (problem is null)
        {
            var named = await batteries.FindAsync(sender, document.Entries[0].NamedAs, cancellationToken);
            if (named.Pack?.Id != packId)
            {
                problem = NotThisBattery(document.Entries[0].NamedAs);
            }
            else if (await store.PassportStatusAsync(packId, cancellationToken) is { } status && DppStatuses.IsNoLongerActive(status))
            {
                problem = SavePipeline.NoLongerChanges(status); // a read-only page has no upload section; this guards a stale form
            }
        }

        if (problem is not null)
        {
            return new UploadReport(document.Format, true, [], problem, null);
        }

        var report = await RunAsync(sender, json, "page", fileName, preview: true, cancellationToken);
        var check = new UploadCheck
        {
            Id = Guid.NewGuid().ToString("N"),
            PackId = packId,
            Who = sender.Who,
            SentAs = sender.Kind.ToString(),
            FileName = fileName,
            Json = json.ToJsonString(),
            ExpiresAtUtc = clock.GetUtcNow().UtcDateTime + CheckKeptFor,
        };
        await store.InsertAsync(check, cancellationToken);
        return report with { CheckId = check.Id };
    }

    /// <summary>
    /// Saves a checked file (step 5c): only for the person who checked it, on the same battery, within the hour. It is
    /// run again, so the result is what the save pipeline says now. Null when it expired or isn't theirs.
    /// </summary>
    public async Task<UploadReport?> SaveCheckedAsync(ChangeSender sender, string packId, string? checkId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(sender);
        if (string.IsNullOrWhiteSpace(checkId) || await store.GetAsync<UploadCheck>(checkId, cancellationToken) is not { } check
            || check.PackId != packId || check.Who != sender.Who || check.SentAs != sender.Kind.ToString()
            || check.ExpiresAtUtc <= clock.GetUtcNow().UtcDateTime)
        {
            return null;
        }

        await store.DeleteAsync<UploadCheck>(check.Id, cancellationToken);
        return await RunAsync(sender, JsonNode.Parse(check.Json), "page", check.FileName, preview: false, cancellationToken);
    }

    /// <summary>
    /// A kept check shown again on its battery's page (step 5c): run as a preview now, as its checker (the team on the
    /// team's page, the battery's cluster admin on theirs). Null when it expired or is another battery's.
    /// </summary>
    public async Task<(UploadCheck Check, UploadReport Report)?> ShowCheckAsync(BatteryPack pack, bool forTeam, string? checkId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(pack);
        if (string.IsNullOrWhiteSpace(checkId) || await store.GetAsync<UploadCheck>(checkId, cancellationToken) is not { } check
            || check.PackId != pack.Id || check.ExpiresAtUtc <= clock.GetUtcNow().UtcDateTime
            || check.SentAs != (forTeam ? nameof(SenderKind.Team) : nameof(SenderKind.ClusterAdmin)))
        {
            return null;
        }

        var sender = forTeam ? ChangeSender.Team(check.Who) : ChangeSender.ClusterAdmin(check.Who, Cluster(pack) ?? string.Empty);
        return (check, await RunAsync(sender, JsonNode.Parse(check.Json), "page", check.FileName, preview: true, cancellationToken));
    }

    /// <summary>
    /// The battery's latest uploads, each cut to this battery's line (step 5c, Q19). The team sees them all; a cluster
    /// admin those sent for the battery's cluster (its admins and its battery system), not the team's.
    /// </summary>
    public async Task<IReadOnlyList<UploadRecord>> HistoryAsync(BatteryPack pack, bool forTeam, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(pack);
        var packId = pack.Id;
        var cluster = Cluster(pack);
        var uploads = await store.ListUploadsForPackAsync(packId, HistoryLength * 4, cancellationToken);
        return uploads
            .Where(upload => forTeam
                || (upload.SentAs != nameof(SenderKind.Team) && cluster is not null && upload.ClusterIds.Contains(cluster)))
            .Take(HistoryLength)
            .Select(upload => new UploadRecord
            {
                Id = upload.Id,
                AtUtc = upload.AtUtc,
                Who = upload.Who,
                SentAs = upload.SentAs,
                ClusterIds = upload.ClusterIds,
                Channel = upload.Channel,
                FileName = upload.FileName,
                Format = upload.Format,
                IsDemo = upload.IsDemo,
                Batteries = upload.Batteries.Where(battery => battery.PackId == packId).ToList(),
            })
            .ToArray();
    }

    private static string? Cluster(BatteryPack? pack) => pack?.ClusterId is { } cluster ? ClusterRepository.NormalizeClusterId(cluster) : null;

    private const string StatusProperty = "batteryStatus";

    /// <summary>
    /// The entry's status change through <see cref="StatusChangeService"/> (Q20–Q22), as the upload: the re-stated values
    /// it gives, and the battery's current ones for those it leaves out (Q8, "kept"). A repair's day is the file's
    /// repairDate, else the day of measuredAt, else today.
    /// </summary>
    private async Task<(StatusChangeResult Result, IReadOnlyCollection<string> Kept, IReadOnlyDictionary<string, JsonNode?> Restated, string Day)> StatusAsync(
        ChangeSender sender, BatteryPack pack, UploadEntry entry, UploadStatus asked, bool preview, CancellationToken cancellationToken)
    {
        var sources = await store.LoadSourcesAsync(pack.Id, cancellationToken);
        var restated = new Dictionary<string, JsonNode?>(StringComparer.Ordinal);
        var kept = new HashSet<string>(StringComparer.Ordinal);
        foreach (var attribute in sources is null ? [] : StatusChangeService.RestatedAttributes(sources.Model))
        {
            if (entry.Values.TryGetValue(attribute.Property, out var given) && given is not null
                && !(given.GetValueKind() == JsonValueKind.String && string.IsNullOrWhiteSpace(given.GetValue<string>())))
            {
                restated[attribute.Property] = given;
            }
            else
            {
                restated[attribute.Property] = (pack.Values.GetValueOrDefault(attribute.Property)?.Value ?? sources!.Model.Values.GetValueOrDefault(attribute.Property)?.Value)?.DeepClone();
                kept.Add(attribute.Property);
            }
        }

        var day = asked.RepairDate ?? clock.GetUtcNow().UtcDateTime.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
        var result = await statusChanges.ChangeAsync(sender, pack.Id, new StatusChange(asked.What, restated, day, asked.RepairDescription), cancellationToken, preview, "upload");
        return (result, kept, restated, day);
    }

    /// <summary>The status change in words, for the check (Q21) and, once saved, the battery's earlier uploads.</summary>
    public static string StatusWords(UploadStatus asked, string day, string to, bool done)
    {
        ArgumentNullException.ThrowIfNull(asked);
        var becomes = done ? "became" : "becomes";
        var description = asked.RepairDescription?.Trim() ?? string.Empty;
        var said = description.EndsWith('.') || description.EndsWith('!') || description.EndsWith('?') ? $"“{description}”" : $"“{description}.”";
        return asked.What switch
        {
            StatusEvent.Repaired => $"Repaired on {ValueWords.Day(day)}: {said} The status {becomes} {to}, with a new version.",
            StatusEvent.Waste => $"The status {becomes} waste, with a new version.",
            _ => $"The status {becomes} {to}, with a new version. Then name who places the battery on the market again, on this page.",
        };
    }

    private static string Source(ChangeSender sender) => sender.Kind == SenderKind.BatterySystemFeed ? BatteryApi.FeedSource : "upload";

    /// <summary>
    /// The battery's module list from the file (decision 31): app data, not passport data, so it is set beside the save
    /// pipeline, without a new version. An empty list never overwrites; an unchanged list isn't saved again.
    /// </summary>
    private async Task<(bool Changed, string Words)?> ModulesAsync(ChangeSender sender, BatteryPack pack, IReadOnlyList<BatteryModule>? modules, bool preview, CancellationToken cancellationToken)
    {
        if (modules is not { Count: > 0 })
        {
            return null;
        }

        static string Key(BatteryModule module) => $"{module.Position}|{module.SerialNumber}|{module.ConnectivityId}";
        if (pack.Modules.Select(Key).SequenceEqual(modules.OrderBy(module => module.Position).Select(Key)))
        {
            return (false, "the module list is unchanged");
        }

        var count = Words.Count(modules.Count, "module");
        if (preview)
        {
            return (true, $"the module list would be set: {count}");
        }

        await store.SetPackModulesAsync(pack.Id, modules, cancellationToken);
        var serial = pack.Values.GetValueOrDefault("batterySerialNumber")?.Value?.GetValue<string>() ?? pack.Id;
        await audit.AppendApplicationAuditEventAsync(
            BatteryPages.ModulesSetEvent,
            sender.Who,
            sender.Kind.ToString(),
            Source(sender),
            $"Modules of battery {serial} set from the battery system's export: {count}",
            new BsonDocument { ["from"] = pack.Modules.Count, ["to"] = modules.Count },
            entityType: "dpp-pack",
            entityId: pack.Id,
            cancellationToken: cancellationToken);
        return (true, $"the module list was set: {count}");
    }

    private async Task<(UploadEntry Entry, WriteTarget? Target, string? Problem)> FindAsync(ChangeSender sender, UploadEntry entry, CancellationToken cancellationToken)
    {
        if (entry.Problem is not null)
        {
            return (entry, null, entry.Problem);
        }

        if (entry.MeasuredAtUtc is { } measuredAt && batteries.InTheFuture(measuredAt))
        {
            return (entry, null, InTheFuture);
        }

        var target = await batteries.FindAsync(sender, entry.NamedAs, cancellationToken);
        return target.Refusal switch
        {
            WriteRefusal.None => (entry, target, null),
            WriteRefusal.NotYours => (entry, null, NotYours),
            WriteRefusal.Ambiguous => (entry, null, Ambiguous),
            _ => (entry, null, NotFound),
        };
    }

    private async Task<string> LogAsync(ChangeSender sender, string channel, string? fileName, string format, IReadOnlyList<UploadedBatteryReport> reports, CancellationToken cancellationToken)
    {
        var upload = new UploadRecord
        {
            Id = Guid.NewGuid().ToString("N"),
            AtUtc = clock.GetUtcNow().UtcDateTime,
            Who = sender.Who,
            SentAs = sender.Kind.ToString(),
            ClusterIds = sender.ClusterIds.Order(StringComparer.Ordinal).ToList(),
            Channel = channel,
            FileName = fileName,
            Format = format,
            Batteries = reports.Select(report => new UploadedBattery
            {
                Entry = report.Entry,
                NamedAs = report.NamedAs,
                PackId = report.PackId,
                ClusterId = report.ClusterId,
                Outcome = report.Outcome,
                Problem = report.Problem,
                Modules = report.Modules,
                Notes = report.Notes?.ToList() ?? [],
                Status = report.Status,
                Values = report.Values.Select(value => new UploadedValue
                {
                    Key = value.Key,
                    Value = report.Sent?.GetValueOrDefault(value.Key),
                    Kept = report.Kept?.Contains(value.Key) == true,
                    Outcome = value.Outcome.ToString(),
                    Message = string.IsNullOrEmpty(value.Message) ? null : value.Message,
                }).ToList(),
            }).ToList(),
        };
        await store.AddUploadAsync(upload, cancellationToken);

        int Count(string outcome) => reports.Count(report => report.Outcome == outcome);
        await audit.AppendApplicationAuditEventAsync(
            UploadedEvent,
            sender.Who,
            sender.Kind.ToString(),
            Source(sender),
            $"Upload of {reports.Count} {(reports.Count == 1 ? "battery" : "batteries")}: {Count(Saved)} saved, {Count(NothingSaved)} with nothing saved, {Count(Refused)} refused",
            new BsonDocument { ["upload"] = upload.Id, ["format"] = format, ["channel"] = channel },
            entityType: "dpp-upload",
            entityId: upload.Id,
            cancellationToken: cancellationToken);
        return upload.Id;
    }
}
