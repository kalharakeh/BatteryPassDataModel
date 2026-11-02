using System.Text.Json;
using System.Text.Json.Nodes;
using BatteryPassWeb.Dpp.Api;
using BatteryPassWeb.Dpp.Editing;
using BatteryPassWeb.Dpp.Store;
using BatteryPassWeb.Services;
using MongoDB.Bson;

namespace BatteryPassWeb.Dpp.Uploads;

/// <summary>What happened to one battery of an upload.</summary>
/// <param name="Outcome">One of <see cref="UploadService.Saved"/>, <see cref="UploadService.NothingSaved"/> or <see cref="UploadService.Refused"/>.</param>
/// <param name="Problem">Why the battery was refused as a whole; null when it wasn't.</param>
public sealed record UploadedBatteryReport(
    int Entry,
    string NamedAs,
    string? BatteryId,
    string? PackId,
    string Outcome,
    string? Problem,
    IReadOnlyList<KeyResult> Values);

/// <summary>An upload's result: per battery what happened, or why the file couldn't be read at all.</summary>
/// <param name="UploadId">The log entry; null for a preview or an unreadable file, which aren't logged.</param>
public sealed record UploadReport(string? Format, bool Preview, IReadOnlyList<UploadedBatteryReport> Batteries, string? Problem, string? UploadId)
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
    IEnumerable<IUploadAdapter> adapters)
{
    public const int MaxBatteries = 500;
    public const int MaxBytes = 2 * 1024 * 1024;
    public const string UploadedEvent = "dpp.upload";

    public const string Saved = "saved";
    public const string NothingSaved = "nothing saved";
    public const string Refused = "refused";

    public const string NotAnUpload = "the file isn't an upload this app reads: give \"format\": \"dpp-v3-upload/1\" and the batteries";
    public const string NotFound = "no battery you may change has this ID";
    public const string NotYours = "this battery belongs to another cluster";
    public const string Ambiguous = "more than one battery has this serial number; use the battery ID";
    public const string NamedTwice = "the file names this battery more than once";
    public const string InTheFuture = "measuredAt lies in the future";

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
            if (problem is not null || pack is null || twice.Contains(pack.Id))
            {
                reports.Add(new UploadedBatteryReport(entry.Number, entry.NamedAs, target?.Identifier, pack?.Id, Refused, problem ?? NamedTwice, []));
                continue;
            }

            if (await store.PassportStatusAsync(pack.Id, cancellationToken) is { } status && DppStatuses.IsNoLongerActive(status))
            {
                reports.Add(new UploadedBatteryReport(entry.Number, entry.NamedAs, target!.Identifier, pack.Id, Refused, SavePipeline.NoLongerChanges(status), []));
                continue;
            }

            var source = sender.Kind == SenderKind.BatterySystemFeed ? BatteryApi.FeedSource : "upload";
            var result = await pipeline.SaveAsync(sender, new ChangeSet(TargetKind.Pack, pack.Id, entry.Values, source, entry.MeasuredAtUtc), preview, cancellationToken);
            var outcome = result.Keys.Any(key => key.Outcome == KeyOutcome.Saved) ? Saved : NothingSaved;
            reports.Add(new UploadedBatteryReport(entry.Number, entry.NamedAs, target!.Identifier, pack.Id, outcome, null, result.Keys));
        }

        string? uploadId = null;
        if (!preview)
        {
            uploadId = await LogAsync(sender, channel, fileName, document.Format!, reports, cancellationToken);
        }

        return new UploadReport(document.Format, preview, reports, null, uploadId);
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
                Outcome = report.Outcome,
                Problem = report.Problem,
                Values = report.Values.Select(value => new UploadedValue
                {
                    Key = value.Key,
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
            sender.Kind == SenderKind.BatterySystemFeed ? BatteryApi.FeedSource : "upload",
            $"Upload of {reports.Count} {(reports.Count == 1 ? "battery" : "batteries")}: {Count(Saved)} saved, {Count(NothingSaved)} with nothing saved, {Count(Refused)} refused",
            new BsonDocument { ["upload"] = upload.Id, ["format"] = format, ["channel"] = channel },
            entityType: "dpp-upload",
            entityId: upload.Id,
            cancellationToken: cancellationToken);
        return upload.Id;
    }
}
