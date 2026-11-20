using System.Text.Json.Nodes;
using BatteryPassWeb.Dpp.Catalog;
using BatteryPassWeb.Dpp.Pages;
using BatteryPassWeb.Dpp.Values;

namespace BatteryPassWeb.Dpp.Loading;

/// <summary>A file sent with the workbook: a document or a picture the workbook names.</summary>
public sealed record SentFile(string FileName, byte[] Content);

/// <summary>What the load does with a record or a document, as the check shows it.</summary>
public static class LoadStates
{
    public const string New = "New";
    public const string Changed = "Changed";
    public const string Same = "No change";
    public const string Removed = "Removed";
}

/// <summary>One value the load would change: the row's name, what the app has now and what the workbook gives.</summary>
public sealed record LoadChangeLine(string Name, string Old, string New)
{
    /// <summary>The column the value was taken from when the record's own cell was empty (the first model's); null for its own.</summary>
    public string? TakenFrom { get; init; }
}

/// <summary>
/// What a load does to published passports, said before Load (X1 round three; FU1): each published battery built from a
/// record that changes gets one new version, made after the whole load is saved, with all of its changes.
/// </summary>
/// <param name="Batteries">How many published batteries the load reaches with a change.</param>
/// <param name="Parts">Which records and parts reach them ("Battery model Core800: its values and 2 documents").</param>
/// <param name="Waiting">How many more have changes waiting from an earlier load that stopped, or from a hand save, and are reached by a record of the workbook.</param>
public sealed record LoadPublication(int Batteries, IReadOnlyList<string> Parts, int Waiting = 0)
{
    public string Text
    {
        get
        {
            var one = Batteries == 1;
            var text = new System.Text.StringBuilder();
            if (Batteries > 0)
            {
                text.Append($"{Pages.Words.Count(Batteries, "published battery", "published batteries")} {(one ? "is" : "are")} built from records this load changes. {(one ? "It gets" : "Each gets")} one new version, made after the whole load is saved, with all of the changes: {string.Join("; ", Parts)}. ");
            }

            if (Waiting > 0)
            {
                text.Append($"{Pages.Words.Count(Waiting, "more published battery", "more published batteries")} {(Waiting == 1 ? "has" : "have")} changes waiting from an earlier load or save and {(Waiting == 1 ? "is" : "are")} reached by this workbook: this load publishes {(Waiting == 1 ? "it" : "them")} too, one new version each. ");
            }

            var all = Batteries + Waiting == 1;
            text.Append($"If the load stops part way, {(all ? "it stays" : "they stay")} on {(all ? "its" : "their")} version, with the changes waiting. Load the workbook again, or press \"Publish the waiting changes\" on {(all ? "its" : "each")} page, and {(all ? "it gets" : "each gets")} one version.");
            return text.ToString();
        }
    }
}

/// <summary>One document or picture of a record, with what the load does with it.</summary>
public sealed record LoadDocumentView(string Name, string Detail, string State);

/// <summary>
/// A document or picture the workbook names that wasn't sent (or has no file or link yet): it doesn't stop the load.
/// Everything else is saved and this one stays as it is in the app, until a later load brings it or the team adds it
/// on the record's page.
/// </summary>
/// <param name="Record">The record it belongs to ("Battery model Core800s").</param>
/// <param name="Missing">What is missing ("the file safety-measures.pdf wasn't sent").</param>
/// <param name="Meaning">What that means for a passport.</param>
public sealed record LoadPending(string Record, string Document, string Missing, string Meaning);

/// <summary>One record as the check shows it: new, changed or left as it is, with every value that would change.</summary>
public sealed record LoadRecordView(
    LoadRecord Record,
    string Kind,
    string Name,
    string State,
    IReadOnlyList<LoadChangeLine> Changes,
    IReadOnlyList<LoadDocumentView> Documents,
    IReadOnlyList<string> Notes);

/// <summary>
/// What a load of this workbook and these files would do, and every problem in the way. Nothing is saved by a check.
/// </summary>
/// <param name="WorkbookProblem">Why the file couldn't be read at all; then nothing else is filled in.</param>
/// <param name="PublishedPassports">How many published passports the load reaches: each gets a new version.</param>
/// <param name="Stamp">A fingerprint of everything above: "Load" does what the check showed, or nothing.</param>
public sealed record LoadCheck(
    string WorkbookName,
    string? WorkbookProblem,
    IReadOnlyList<LoadRecordView> Records,
    IReadOnlyList<LoadProblem> Problems,
    IReadOnlyList<string> Notes,
    int PublishedPassports,
    string Stamp)
{
    /// <summary>What the load does to published passports; null when it reaches none.</summary>
    public LoadPublication? Publication { get; init; }

    /// <summary>The documents and pictures still to come: named in the workbook, not sent with it. They don't stop a load.</summary>
    public IReadOnlyList<LoadPending> Pending { get; init; } = [];

    public bool HasProblems => WorkbookProblem is not null || Problems.Count > 0;

    /// <summary>True when the load would make or change something.</summary>
    public bool HasChanges => Records.Any(record => record.State != LoadStates.Same);

    /// <summary>
    /// How many published batteries, apart from those the load changes, have changes waiting and are reached by a record
    /// of the workbook: the load publishes them too, so it is offered even when no record changes.
    /// </summary>
    public int WaitingPassports { get; init; }

    /// <summary>"Load" is offered only for a check without problems that has something to do.</summary>
    public bool CanLoad => !HasProblems && (HasChanges || WaitingPassports > 0);

    public int Count(LoadRecord record) => Records.Count(item => item.Record == record);

    /// <summary>"1 battery family, 3 battery models, 3 batches, 1 manufacturing site and 1 economic operator".</summary>
    public string Summary
    {
        get
        {
            var parts = new[]
            {
                (LoadRecord.Family, "battery family", "battery families"),
                (LoadRecord.Model, "battery model", "battery models"),
                (LoadRecord.Batch, "batch", "batches"),
                (LoadRecord.Site, "manufacturing site", "manufacturing sites"),
                (LoadRecord.Operator, "economic operator", "economic operators"),
            }
            .Where(part => Count(part.Item1) > 0)
            .Select(part => Words.Count(Count(part.Item1), part.Item2, part.Item3))
            .ToArray();
            return parts.Length switch
            {
                0 => "nothing",
                1 => parts[0],
                _ => $"{string.Join(", ", parts[..^1])} and {parts[^1]}",
            };
        }
    }
}

/// <summary>How a load went.</summary>
/// <param name="Loaded">True when everything the check showed was saved.</param>
/// <param name="Check">The check the load ran on; when it didn't load, the reason is in it or in <paramref name="Message"/>.</param>
public sealed record LoadResult(bool Loaded, string Message, LoadCheck Check, string? FamilyId = null);

/// <summary>A document file the load uploads to a record.</summary>
internal sealed record UploadPlan(DocumentField Field, SentFile File, string? Title, string? Language);

/// <summary>One record of the workbook with what the load would do to it. The check builds it; the load carries it out.</summary>
internal sealed class RecordPlan(LoadRecord record, string name, string header)
{
    public LoadRecord Record { get; } = record;

    public string Name { get; set; } = name;

    /// <summary>The heading of its column on its sheet, for messages.</summary>
    public string Header { get; } = header;

    /// <summary>The record in the app this one is; null for a record the load makes.</summary>
    public string? ExistingId { get; set; }

    /// <summary>Set by the load: the existing record's id, or the new one's.</summary>
    public string? Id { get; set; }

    public bool IsNew => ExistingId is null;

    /// <summary>The name as the app has it, when the workbook writes it with other capitals.</summary>
    public string? RenameFrom { get; set; }

    public IReadOnlyDictionary<string, AttributeValue> Stored { get; set; } = new Dictionary<string, AttributeValue>(StringComparer.Ordinal);

    public Dictionary<string, JsonNode> Changed { get; } = new(StringComparer.Ordinal);

    public HashSet<string> Removals { get; } = new(StringComparer.Ordinal);

    /// <summary>The workbook cells behind each value, by the part of the value they fill: a refusal is shown at its cell.</summary>
    public Dictionary<string, List<(string Path, FilledCell Cell)>> Cells { get; } = new(StringComparer.Ordinal);

    /// <summary>The lines of the hazardous substances sheet that make this model's list, in order.</summary>
    public List<HazardLine> HazardLines { get; } = [];

    public List<UploadPlan> Uploads { get; } = [];

    public SentFile? Picture { get; set; }

    public bool RemovePicture { get; set; }

    /// <summary>The model's answers the workbook gives that differ from the stored ones; null when none does.</summary>
    public ModelAnswers? Answers { get; set; }

    /// <summary>A model's answers as they will be after the load: they decide which of its documents a passport needs.</summary>
    public ModelAnswers AnswersAfter { get; set; } = ModelAnswers.NotAnswered;

    public string Words => $"{LoadFields.Words(Record)} {Name}";

    public bool SetDescription { get; set; }

    /// <summary>The model's serial number format changes to <see cref="SerialFormat"/> (null: removed).</summary>
    public bool SetSerialFormat { get; set; }

    public string? SerialFormat { get; set; }

    public string? Description { get; set; }

    /// <summary>A batch's model and site: a record of this workbook, or a site that is already in the app.</summary>
    public RecordPlan? Model { get; set; }

    public RecordPlan? Site { get; set; }

    public string? ExistingSiteId { get; set; }

    public int Year { get; set; }

    public List<LoadChangeLine> Lines { get; } = [];

    public List<LoadDocumentView> Documents { get; } = [];

    public List<string> Notes { get; } = [];

    /// <summary>True when the load writes passport values or documents of this record: its published passports get a new version.</summary>
    public bool ChangesPassportData => Changed.Count > 0 || Removals.Count > 0 || Uploads.Count > 0;

    public bool HasWork => IsNew || RenameFrom is not null || ChangesPassportData || Picture is not null || RemovePicture || Answers is not null || SetDescription || SetSerialFormat;

    public string State => IsNew ? LoadStates.New : HasWork ? LoadStates.Changed : LoadStates.Same;

    public LoadRecordView View() => new(Record, LoadFields.Words(Record), Name, State, Lines.ToArray(), Documents.ToArray(), Notes.ToArray());
}

/// <summary>Everything a load would do, in the order it does it, and what stands in its way.</summary>
internal sealed class LoadPlan
{
    public RecordPlan? Family { get; set; }

    public List<RecordPlan> Models { get; } = [];

    public List<RecordPlan> Batches { get; } = [];

    public List<RecordPlan> Sites { get; } = [];

    public List<RecordPlan> Operators { get; } = [];

    public List<LoadProblem> Problems { get; } = [];

    public List<string> Notes { get; } = [];

    public List<LoadPending> Pending { get; } = [];

    public int PublishedPassports { get; set; }

    public int WaitingPassports { get; set; }

    public LoadPublication? Publication { get; set; }

    /// <summary>The records as the check lists them: the family, its models, their batches, the sites, the operators.</summary>
    public IEnumerable<RecordPlan> All =>
        (Family is null ? [] : new[] { Family }).Concat(Models).Concat(Batches).Concat(Sites).Concat(Operators);
}
