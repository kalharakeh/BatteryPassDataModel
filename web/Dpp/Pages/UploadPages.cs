using System.Text.Json.Nodes;
using BatteryPassWeb.Dpp.Access;
using BatteryPassWeb.Dpp.Catalog;
using BatteryPassWeb.Dpp.Editing;
using BatteryPassWeb.Dpp.Forms;
using BatteryPassWeb.Dpp.Store;
using BatteryPassWeb.Dpp.Uploads;
using BatteryPassWeb.Dpp.Values;

namespace BatteryPassWeb.Dpp.Pages;

/// <summary>One value in the check or the history: the attribute, its value now and in the file, and what happens.</summary>
/// <param name="Quiet">An empty or unknown value, shown muted.</param>
public sealed record UploadValueRow(string Label, string Now, string InFile, string Result, bool Refused, bool Quiet);

/// <summary>The check before saving (mock-up B2): what the kept file would do to this battery.</summary>
/// <param name="Problem">Why the battery as a whole would be refused (e.g. a passport no longer active); null otherwise.</param>
public sealed record UploadCheckView(
    string CheckId,
    string FileName,
    string FormatWords,
    bool WillChange,
    string? Problem,
    IReadOnlyList<UploadValueRow> Values,
    string? Modules,
    IReadOnlyList<string> Notes,
    string? Status = null)
{
    /// <summary>
    /// Why nothing can be saved, as the card's main message (decision 69): what the file asks for, why it can't be done
    /// for this battery now, and what to do. Null when the file would change the battery.
    /// </summary>
    public string? Refusal { get; init; }
}

/// <summary>One earlier upload of the battery (mock-up B1, B3), cut to this battery's line.</summary>
public sealed record UploadHistoryRow(
    string Id,
    string When,
    string Channel,
    string WhoAndWhat,
    string Result,
    string? Problem,
    IReadOnlyList<UploadValueRow> Values,
    string? Modules,
    IReadOnlyList<string> Notes,
    string? Status = null);

/// <summary>The "Upload a file" card of a battery page (decision 35, step 5c, mock-up B1–B4).</summary>
/// <param name="Action">Where the card's forms post: the battery page plus "/upload".</param>
/// <param name="OpenId">The upload to show open in the history, just after it was saved.</param>
public sealed record UploadCardView(string Action, bool ForTeam, UploadCheckView? Check, IReadOnlyList<UploadHistoryRow> History, string? OpenId)
{
    /// <summary>"Start again": the battery page at the card, without the check.</summary>
    public string AgainHref => $"{Action[..^"/upload".Length]}?section=upload";

    /// <summary>The side menu's hint: when the last upload was, or what the section takes.</summary>
    public string Hint => History.Count > 0 ? $"Last {History[0].When}" : "A JSON file for this battery";
}

/// <summary>
/// The "Upload a file" card on both battery pages (decision 35, step 5c): the check of a kept file and the battery's
/// earlier uploads, in words. The checking and saving are <see cref="UploadService"/>'s.
/// </summary>
public sealed class UploadPages(UploadService uploads, AttributeForms forms, AttributeValueNormalizer normalizer)
{
    /// <summary>The kept check to show, across the redirect after "Check the file".</summary>
    public const string CheckKey = "UploadCheck";

    /// <summary>The upload just saved, shown open in the history.</summary>
    public const string OpenKey = "UploadOpen";

    /// <param name="viewer">Who is looking; the kept check shows only to the person who checked the file.</param>
    public async Task<UploadCardView> CardAsync(BatteryPack pack, string href, bool forTeam, string? checkId, string? openId, CancellationToken cancellationToken = default, string? viewer = null)
    {
        ArgumentNullException.ThrowIfNull(pack);
        UploadCheckView? check = null;
        if (await uploads.ShowCheckAsync(pack, forTeam, checkId, cancellationToken, viewer) is { } shown && shown.Report.Batteries.Count == 1)
        {
            var battery = shown.Report.Batteries[0];
            check = new UploadCheckView(
                shown.Check.Id,
                shown.Check.FileName,
                FormatWords(shown.Report.Format),
                battery.Outcome == UploadService.Saved,
                battery.Problem is null ? null : Capitalise(battery.Problem),
                Rows(pack, battery.Values.Select(value => (value.Key, value.Outcome, value.Message, battery.Sent?.GetValueOrDefault(value.Key), battery.Kept?.Contains(value.Key) == true)), preview: true, forTeam),
                battery.Modules is null ? null : Capitalise(battery.Modules),
                (battery.Notes ?? []).Select(Capitalise).ToArray(),
                battery.Status)
            {
                Refusal = battery.Outcome == UploadService.Saved ? null : RefusalWords(battery.Problem, AskedStatus(shown.Check.Json)),
            };
        }

        var history = (await uploads.HistoryAsync(pack, forTeam, cancellationToken))
            .Where(upload => upload.Batteries.Count == 1)
            .Select(upload =>
            {
                var battery = upload.Batteries[0];
                var values = Rows(pack, battery.Values.Select(value => (value.Key, Enum.Parse<KeyOutcome>(value.Outcome), value.Message ?? string.Empty, value.Value, value.Kept)), preview: false, forTeam);
                var saved = battery.Values.Count(value => value.Outcome == nameof(KeyOutcome.Saved));
                var result = battery.Outcome == UploadService.Refused
                    ? "Refused"
                    : battery.Status is not null ? "Status changed"
                    : saved == 0 && battery.Modules?.Contains("was set", StringComparison.Ordinal) != true ? "Nothing saved" : $"{Words.Count(saved, "value")} saved";
                return new UploadHistoryRow(
                    upload.Id,
                    ValueWords.Time(upload.AtUtc),
                    upload.Channel == "api" ? "API" : "Page",
                    $"{Sender(upload)} · {upload.FileName ?? FormatWords(upload.Format)}",
                    result,
                    battery.Problem is null ? null : Capitalise(battery.Problem),
                    values,
                    battery.Modules is null ? null : Capitalise(battery.Modules),
                    battery.Notes.Select(Capitalise).ToArray(),
                    battery.Status);
            })
            .ToArray();
        return new UploadCardView($"{href}/upload", forTeam, check, history, openId);
    }

    /// <summary>"Check the file" (mock-up B1): the kept check's ID, or why the file wasn't checked.</summary>
    public async Task<(string? CheckId, string? Problem)> CheckAsync(ChangeSender sender, string packId, string? fileName, Stream? content, long length, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(sender);
        if (content is null || length == 0)
        {
            return (null, "choose the file first");
        }

        if (length > UploadService.MaxBytes)
        {
            return (null, UploadService.TooLarge);
        }

        using var buffer = new MemoryStream();
        await content.CopyToAsync(buffer, cancellationToken);
        var report = await uploads.CheckBatteryFileAsync(sender, packId, Path.GetFileName(fileName ?? "file.json"), buffer.ToArray(), cancellationToken);
        return report.Problem is { } problem ? (null, problem) : (report.CheckId, null);
    }

    /// <summary>"Save" (mock-up B3): what was saved and the upload's ID, or why nothing was.</summary>
    public async Task<(string? Done, string? UploadId, string? Problem)> SaveAsync(ChangeSender sender, string packId, string? checkId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(sender);
        return await uploads.SaveCheckedAsync(sender, packId, checkId, cancellationToken) is { } report
            ? (SavedWords(report), report.UploadId, null)
            : (null, null, UploadService.CheckExpired);
    }

    /// <summary>
    /// Why a checked file can't be saved for this battery, said plainly (decision 69): what the file asks for, why it can't
    /// be done now, and what to do. <paramref name="problem"/> is the check's reason (null when nothing would change);
    /// <paramref name="asked"/> the status change or repair the file asks for, if any.
    /// </summary>
    public static string RefusalWords(string? problem, UploadStatus? asked)
    {
        var what = asked?.What switch
        {
            StatusEvent.Repaired => "a repair",
            StatusEvent.Waste => "the status waste",
            StatusEvent.PlacedOnMarketAgain => "the status re-used",
            StatusEvent.Remanufactured => "the status remanufactured",
            StatusEvent.Repurposed => "the status repurposed",
            _ => "a status change",
        };
        var takeOut = $"take the {(asked?.What == StatusEvent.Repaired ? "repair" : "status change")} out of the file to save its readings.";
        var asks = $"This file asks for {what}, and a file is saved whole or not at all. ";
        if (problem is null)
        {
            return "Nothing in this file would change the battery: its values are the same as now, empty, or not saved (the table says which). Check that it is the right file for this battery, or correct the values and check it again.";
        }

        foreach (var status in new[] { DppStatuses.Invalid, DppStatuses.MarkedForDeletion, DppStatuses.Inactive })
        {
            if (problem == SavePipeline.NoLongerChanges(status))
            {
                return status == DppStatuses.Inactive
                    ? "This battery's passport is inactive: the battery has a new passport. Upload the file on the new passport's battery page instead."
                    : $"This battery's passport is {(status == DppStatuses.Invalid ? "marked invalid" : "marked for deletion")}, so nothing on it changes any more. Nothing from this file can be saved for it; check that the file is for the right battery.";
            }
        }

        return problem switch
        {
            StatusChangeService.OnlyPublished => $"{asks}A status change needs a published passport, and this battery isn't published yet. Publish the battery first, or {takeOut}",
            StatusChangeService.WaitingForSuccessor => $"{asks}This battery is waiting for its new passport, so its status doesn't change now. Publish or cancel the new passport on the battery's page first, or {takeOut}",
            StatusChangeService.WasteIsFinal => $"{asks}This battery is waste, which is final. {Upper(takeOut)}",
            StatusChangeService.ValuesRefused => $"{asks}A value the status change states again was refused or is missing; the table marks it. Correct it in the file and check it again, or {takeOut}",
            StatusChangeService.RepairNeedsDescription => $"{asks}The repair doesn't say what was repaired. Add repairDescription to the file and check it again, or {takeOut}",
            StatusChangeService.RepairNeedsDate => $"{asks}The repair has no day. Add repairDate to the file and check it again, or {takeOut}",
            StatusChangeService.RepairNotInTheFuture => $"{asks}The repair's day lies in the future. Correct repairDate in the file and check it again, or {takeOut}",
            StatusChangeService.NotYours => $"{asks}Only the team and the battery's cluster admins change its status. Ask the team, or {takeOut}",
            UploadService.NamedTwice => "The file names this battery more than once, so none of its entries is used. Keep one entry for this battery and check the file again.",
            _ when problem.StartsWith("a battery that is ", StringComparison.Ordinal) => $"{asks}{Upper(problem)}. {Upper(takeOut)}",
            _ when problem.StartsWith("the passport can't get a new version now", StringComparison.Ordinal) =>
                $"{asks}The passport can't get a new version now; the team sees why on the battery's page. Put that right first, or {takeOut}",
            _ when asked is not null => $"{asks}It can't be done now: {problem}. {Upper(takeOut)}",
            _ => $"The file's entry for this battery can't be used: {problem}. Correct the file and check it again.",
        };
    }

    private static string Upper(string text) => text.Length == 0 ? text : char.ToUpperInvariant(text[0]) + text[1..];

    /// <summary>The status change or repair a kept file asks for (the app's upload format only); null when none or unreadable.</summary>
    private static UploadStatus? AskedStatus(string json)
    {
        try
        {
            return JsonNode.Parse(json) is JsonObject file && new NativeUploadFormat() is var format && format.Reads(file)
                ? format.Read(file).Entries.FirstOrDefault()?.Status
                : null;
        }
        catch (System.Text.Json.JsonException)
        {
            return null;
        }
    }

    /// <summary>Who sent an upload, in words: a battery system or an API token by what it is, never by its token's ID.</summary>
    private static string Sender(UploadRecord upload) =>
        upload.SentAs == nameof(SenderKind.BatterySystemFeed) ? "Battery system"
        : upload.Who.StartsWith("api-token:", StringComparison.Ordinal) ? "API token"
        : upload.Who;

    /// <summary>"battery system export (BMS2_ACO 01.32.07)" or "upload file", for the format a file was read as.</summary>
    public static string FormatWords(string? format) => format switch
    {
        null or "" => "file",
        NativeUploadFormat.Format => "upload file",
        _ when format.StartsWith(BmsExportFormat.Mapping, StringComparison.Ordinal) => $"battery system export ({format})",
        _ => format,
    };

    /// <summary>The line after "Save": what was saved, in words.</summary>
    public static string SavedWords(UploadReport report)
    {
        ArgumentNullException.ThrowIfNull(report);
        var battery = report.Batteries.FirstOrDefault();
        if (battery is null || battery.Outcome != UploadService.Saved)
        {
            return $"Nothing was saved{(battery?.Problem is { } problem ? $": {problem}" : string.Empty)}.";
        }

        var saved = battery.Values.Count(value => value.Outcome == KeyOutcome.Saved);
        var modules = battery.Modules?.Contains("was set", StringComparison.Ordinal) == true ? $", and {battery.Modules}" : string.Empty;
        return battery.Status is { } status ? $"Saved. {status}" : $"Saved: {Words.Count(saved, "value")}{modules}.";
    }

    private UploadValueRow[] Rows(BatteryPack pack, IEnumerable<(string Key, KeyOutcome Outcome, string Message, string? Sent, bool Kept)> values, bool preview, bool forTeam) =>
        values.Select(value =>
        {
            var attribute = AttributeCatalog.FindByProperty(value.Key);
            var label = attribute is null ? value.Key : $"{attribute.Label} {attribute.Name}";
            var sent = value.Sent is null ? null : JsonNode.Parse(value.Sent);
            var result = value.Kept && value.Outcome == KeyOutcome.Saved ? "Kept: the battery's current value" : value.Outcome switch
            {
                KeyOutcome.Saved => preview ? "Will be saved" : "Saved",
                KeyOutcome.KeptEmpty => "Kept: empty in the file",
                KeyOutcome.Ignored => "Ignored: not in the attribute list",
                _ => $"Not saved: {value.Message}",
            };
            return new UploadValueRow(
                label,
                attribute is null || !MaySeeNow(attribute, pack, forTeam) ? string.Empty : Text(attribute, pack.Values.GetValueOrDefault(value.Key)?.Value),
                attribute is null ? sent?.ToJsonString() ?? string.Empty : Text(attribute, sent is null ? null : normalizer.Normalize(attribute, sent)),
                result,
                value.Outcome == KeyOutcome.Rejected,
                value.Outcome is KeyOutcome.Ignored or KeyOutcome.KeptEmpty || value.Kept);
        }).ToArray();

    /// <summary>
    /// The battery's current value ("Now") is shown only to someone who may read it: the team always, a cluster admin the
    /// public, purchaser and shared values of their own battery, not the authorities' (#13 and the like) (S05).
    /// </summary>
    private static bool MaySeeNow(AttributeDefinition attribute, BatteryPack pack, bool forTeam) =>
        forTeam
        || AudienceRules.MaySee(
            Reader.With("cluster-admin", ReaderRole.ClusterAdmin, pack.ClusterId is { } cluster ? [cluster] : []),
            attribute,
            pack.ClusterId is { } own ? [own] : []);

    private string Text(AttributeDefinition attribute, JsonNode? value)
    {
        if (value is null)
        {
            return string.Empty;
        }

        var lines = ValueWords.Lines(forms.For(attribute), value);
        return lines.Count == 0 ? value.ToJsonString() : string.Join("; ", lines.Select(line => line.Label is null ? line.Text : $"{line.Label}: {line.Text}"));
    }

    private static string Capitalise(string text) => text.Length == 0 ? text : char.ToUpperInvariant(text[0]) + text[1..] + (text.EndsWith('.') ? string.Empty : ".");
}
