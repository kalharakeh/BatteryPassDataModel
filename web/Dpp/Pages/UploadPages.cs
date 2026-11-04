using System.Text.Json.Nodes;
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
    IReadOnlyList<string> Notes);

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
    IReadOnlyList<string> Notes);

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

    public async Task<UploadCardView> CardAsync(BatteryPack pack, string href, bool forTeam, string? checkId, string? openId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(pack);
        UploadCheckView? check = null;
        if (await uploads.ShowCheckAsync(pack, forTeam, checkId, cancellationToken) is { } shown && shown.Report.Batteries.Count == 1)
        {
            var battery = shown.Report.Batteries[0];
            check = new UploadCheckView(
                shown.Check.Id,
                shown.Check.FileName,
                FormatWords(shown.Report.Format),
                battery.Outcome == UploadService.Saved,
                battery.Problem is null ? null : Capitalise(battery.Problem),
                Rows(pack, battery.Values.Select(value => (value.Key, value.Outcome, value.Message, battery.Sent?.GetValueOrDefault(value.Key))), preview: true),
                battery.Modules is null ? null : Capitalise(battery.Modules),
                (battery.Notes ?? []).Select(Capitalise).ToArray());
        }

        var history = (await uploads.HistoryAsync(pack, forTeam, cancellationToken))
            .Where(upload => upload.Batteries.Count == 1)
            .Select(upload =>
            {
                var battery = upload.Batteries[0];
                var values = Rows(pack, battery.Values.Select(value => (value.Key, Enum.Parse<KeyOutcome>(value.Outcome), value.Message ?? string.Empty, value.Value)), preview: false);
                var saved = battery.Values.Count(value => value.Outcome == nameof(KeyOutcome.Saved));
                var result = battery.Outcome == UploadService.Refused
                    ? "Refused"
                    : saved == 0 && battery.Modules?.Contains("was set", StringComparison.Ordinal) != true ? "Nothing saved" : $"{Words.Count(saved, "value")} saved";
                return new UploadHistoryRow(
                    upload.Id,
                    ValueWords.Time(upload.AtUtc),
                    upload.Channel == "api" ? "API" : "Page",
                    $"{upload.Who} · {upload.FileName ?? FormatWords(upload.Format)}",
                    result,
                    battery.Problem is null ? null : Capitalise(battery.Problem),
                    values,
                    battery.Modules is null ? null : Capitalise(battery.Modules),
                    battery.Notes.Select(Capitalise).ToArray());
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
        return $"Saved: {Words.Count(saved, "value")}{modules}.";
    }

    private UploadValueRow[] Rows(BatteryPack pack, IEnumerable<(string Key, KeyOutcome Outcome, string Message, string? Sent)> values, bool preview) =>
        values.Select(value =>
        {
            var attribute = AttributeCatalog.FindByProperty(value.Key);
            var label = attribute is null ? value.Key : $"{attribute.Label} {attribute.Name}";
            var sent = value.Sent is null ? null : JsonNode.Parse(value.Sent);
            var result = value.Outcome switch
            {
                KeyOutcome.Saved => preview ? "Will be saved" : "Saved",
                KeyOutcome.KeptEmpty => "Kept: empty in the file",
                KeyOutcome.Ignored => "Ignored: not in the attribute list",
                _ => $"Not saved: {value.Message}",
            };
            return new UploadValueRow(
                label,
                attribute is null ? string.Empty : Text(attribute, pack.Values.GetValueOrDefault(value.Key)?.Value),
                attribute is null ? sent?.ToJsonString() ?? string.Empty : Text(attribute, sent is null ? null : normalizer.Normalize(attribute, sent)),
                result,
                value.Outcome == KeyOutcome.Rejected,
                value.Outcome is KeyOutcome.Ignored or KeyOutcome.KeptEmpty);
        }).ToArray();

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
