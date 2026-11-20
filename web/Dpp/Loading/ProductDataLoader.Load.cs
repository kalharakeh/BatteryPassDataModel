using BatteryPassWeb.Dpp.Documents;
using BatteryPassWeb.Dpp.Editing;
using BatteryPassWeb.Dpp.Pages;
using BatteryPassWeb.Dpp.Publication;
using BatteryPassWeb.Dpp.Store;
using MongoDB.Bson;

namespace BatteryPassWeb.Dpp.Loading;

/// <summary>
/// The load itself: it carries out what a check without problems showed, through the methods the product data pages
/// call (new records, names, the three answers, the module description, pictures) and the record editor and the
/// document service (passport values and uploaded documents), in the order a person would: sites and operators, the
/// family, its models, their batches.
/// </summary>
public sealed partial class ProductDataLoader
{
    /// <summary>The load's own audit entry, besides the entries of the records it made and changed.</summary>
    public const string LoadedEvent = "dpp.product-data-loaded";

    /// <summary>A load that stopped part way and removed what it had made.</summary>
    public const string StoppedEvent = "dpp.product-data-load-stopped";

    public const string LoadRunning = "Another load is running. Wait for it, then check again.";
    public const string CheckFirst = "Nothing was loaded: check the workbook first, then press Load.";
    public const string HasProblems = "Nothing was loaded: the check found problems. Put them right in the workbook and check again.";
    public const string NothingToLoad = "Nothing to load: the app already holds everything this workbook says.";
    public const string ChangedSinceCheck = "Nothing was loaded: the workbook, the files or the app's data changed after the check. This is the check as it is now; press Load if it is right.";

    /// <summary>One load at a time in the app, so that a double click can't make two of everything.</summary>
    private static readonly SemaphoreSlim Gate = new(1, 1);

    /// <summary>Called after each record the load finishes ("site", "operator", "family", "model", "batch"); a test's way to stop a load part way.</summary>
    public Func<string, Task>? StepHook { get; set; }

    /// <summary>
    /// Loads the workbook: all of it or none of it. It checks again first and loads only when that check has no problems
    /// and is the one the page showed (<paramref name="stamp"/>). Once it starts to save it runs to its end, whatever
    /// happens to the request, as a copy of a demo family does.
    /// </summary>
    public async Task<LoadResult> LoadAsync(string who, string workbookName, byte[] workbook, IReadOnlyList<SentFile> files, string? stamp, CancellationToken cancellationToken = default)
    {
        if (!await Gate.WaitAsync(0, cancellationToken))
        {
            return new LoadResult(false, LoadRunning, await CheckAsync(workbookName, workbook, files, cancellationToken));
        }

        try
        {
            var (check, plan) = await PlanAsync(workbookName, workbook, files, cancellationToken);
            if (plan is null || check.HasProblems)
            {
                return new LoadResult(false, HasProblems, check);
            }

            if (string.IsNullOrWhiteSpace(stamp))
            {
                return new LoadResult(false, CheckFirst, check);
            }

            if (!string.Equals(stamp.Trim(), check.Stamp, StringComparison.Ordinal))
            {
                return new LoadResult(false, ChangedSinceCheck, check);
            }

            // A load with nothing new still publishes the changes that wait on batteries its records reach (FU round two, 1).
            return check.HasChanges || check.WaitingPassports > 0
                ? await CarryOutAsync(who, check, plan, workbook)
                : new LoadResult(false, NothingToLoad, check, plan.Family?.ExistingId);
        }
        finally
        {
            Gate.Release();
        }
    }

    private async Task<LoadResult> CarryOutAsync(string who, LoadCheck check, LoadPlan plan, byte[] workbook)
    {
        var none = CancellationToken.None;
        var sender = ChangeSender.Team(who);
        var made = new List<Func<Task<bool>>>(); // how to remove each record the load made, in the order they were made
        var saved = new List<string>(); // records that were already there and that the load has saved changes to
        var before = new Dictionary<string, AffectedPassport>(StringComparer.Ordinal);

        // One new version for each published battery the load reaches, made after the whole load is saved (FU1). A load that
        // stops part way leaves each reached battery on its version with its marker set; the next load, or "Publish the
        // waiting changes" on its page, publishes it once.
        using var hold = editor.HoldPublications();

        // The order of saving: first every record the load makes, made and filled in full. No reader sees any of them
        // yet (no battery uses a new record), and a stop there removes them again. Then the records that were already
        // there, which readers may see. Every reason a step could be refused was judged by the check; a stop now can
        // only be a fault of the database, and loading the same workbook again finishes what is left.
        foreach (var record in plan.All.Where(record => !record.IsNew))
        {
            record.Id = record.ExistingId;
        }

        try
        {
            foreach (var record in Ordered(plan).Where(record => record.IsNew))
            {
                await SaveRecordAsync(record);
            }
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            return await StoppedAsync(who, check, exception, made, saved);
        }

        try
        {
            foreach (var record in Ordered(plan).Where(record => !record.IsNew && record.HasWork))
            {
                saved.Add($"{LoadFields.Words(record.Record).ToLowerInvariant()} {record.Name}");
                await SaveRecordAsync(record);
            }
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            return await StoppedAsync(who, check, exception, [], saved, madeKept: made.Count, reachedBatteries: before.Count);
        }

        // Batteries that an earlier load, or a load that stopped, left with changes waiting, and that a record named in the
        // workbook reaches, are published now too: once each, with everything (FU round two, 1).
        foreach (var passport in await WaitingReachedAsync(plan, none))
        {
            before.TryAdd(passport.PackId, passport);
            hold?.Note(passport.PackId);
        }

        await editor.PublishHeldAsync(hold, sender);
        var outcomes = await editor.OutcomesAsync(before.Values.ToArray(), none);
        var newRecords = plan.All.Count(record => record.IsNew);
        var changed = plan.All.Count(record => !record.IsNew && record.HasWork);
        var did = (newRecords, changed) switch
        {
            (0, 0) => "no record needed a change",
            (> 0, > 0) => $"{Words.Count(newRecords, "record")} made and {changed} changed",
            (> 0, _) => $"{Words.Count(newRecords, "record")} made",
            _ => $"{Words.Count(changed, "record")} changed",
        };
        await audit.AppendApplicationAuditEventAsync(
            LoadedEvent,
            who,
            "Team",
            "form",
            $"Product data loaded from {check.WorkbookName}: {did}, of {check.Summary}",
            new BsonDocument
            {
                ["workbook"] = check.WorkbookName,
                ["sha256"] = Sha256(workbook),
                ["made"] = new BsonArray(plan.All.Where(record => record.IsNew).Select(record => $"{LoadFields.Words(record.Record)} {record.Name}")),
                ["changed"] = new BsonArray(plan.All.Where(record => !record.IsNew && record.HasWork).Select(record => $"{LoadFields.Words(record.Record)} {record.Name}")),
                ["values"] = plan.All.Sum(record => record.Changed.Count),
                ["removed"] = plan.All.Sum(record => record.Removals.Count),
                ["documents"] = plan.All.Sum(record => record.Uploads.Count),
                ["newVersions"] = outcomes.Count(outcome => outcome.Outcome.Outcome == PublishOutcome.NewVersion),
            },
            entityType: "dpp-family",
            entityId: plan.Family?.Id ?? string.Empty,
            cancellationToken: none);
        var published = outcomes.Count > 0 ? " " + PublishWords.ForMany(outcomes) : string.Empty;
        return new LoadResult(true, newRecords + changed == 0 ? $"Nothing in the workbook was new.{published}" : $"Product data loaded: {did}, of {check.Summary}.{published}", check, plan.Family?.Id);

        // One record: made when it is new, then its name, own fields, values and documents.
        async Task SaveRecordAsync(RecordPlan record)
        {
            switch (record.Record)
            {
                case LoadRecord.Site:
                    record.Id ??= await pages.CreateSiteAsync(who, record.Name, none);
                    Made(record, RecordKind.Site);
                    await ApplyAsync(record);
                    await StepAsync("site");
                    break;
                case LoadRecord.Operator:
                    record.Id ??= await pages.CreateOperatorAsync(who, record.Name, none);
                    Made(record, RecordKind.Operator);
                    await ApplyAsync(record);
                    await StepAsync("operator");
                    break;
                case LoadRecord.Family:
                    record.Id ??= await pages.CreateFamilyAsync(who, record.Name, none);
                    Made(record, RecordKind.Family);
                    await ApplyAsync(record);
                    if (record.Picture is { } picture)
                    {
                        Unless(await pages.SetFamilyPictureAsync(who, record.Id, picture.FileName, picture.Content, none), record, "its picture wasn't saved");
                    }

                    await StepAsync("family");
                    break;
                case LoadRecord.Model:
                    record.Id ??= await pages.CreateModelAsync(who, plan.Family!.Id!, record.Name, null, none)
                        ?? throw new LoadStopped($"the battery model {record.Name} couldn't be made: its family is gone");
                    Made(record, RecordKind.Model);
                    await OwnModelFieldsAsync(record);
                    await ApplyAsync(record);
                    await StepAsync("model");
                    break;
                default:
                    if (record.Id is null)
                    {
                        var (batchId, problem) = await pages.CreateBatchAsync(who, record.Model!.Id!, record.Year, record.Site?.Id ?? record.ExistingSiteId!, none);
                        Unless(problem, record, "it couldn't be made");
                        record.Id = batchId;
                    }

                    Made(record, RecordKind.Batch);
                    await ApplyAsync(record);
                    await StepAsync("batch");
                    break;
            }
        }

        // A record the load made is removed again if the load stops while it makes the new records.
        void Made(RecordPlan record, RecordKind kind)
        {
            var id = record.Id!;
            if (record.ExistingId is not null)
            {
                return;
            }

            made.Add(async () =>
            {
                if (kind == RecordKind.Model)
                {
                    await pages.RemoveModelPictureAsync(who, id, none);
                }

                return (await pages.RemoveAsync(who, kind, id, none)).Removed;
            });
        }

        async Task OwnModelFieldsAsync(RecordPlan model)
        {
            if (model.Answers is { } answers && await store.GetAsync<BatteryModel>(model.Id!, none) is { } unanswered)
            {
                await Reached(model);
                await pages.SetModelAnswersAsync(who, unanswered, answers, confirmed: true, none);
            }

            if (model.SetSerialFormat && await store.GetAsync<BatteryModel>(model.Id!, none) is { } formatted)
            {
                // Pressing Load is the yes the model's page asks for when batteries don't fit the new format.
                Unless((await pages.SetSerialFormatAsync(who, formatted, model.SerialFormat, confirmed: true, identifiers, none)).Problem, model, "its serial number format wasn't saved");
            }

            if (model.SetDescription && await store.GetAsync<BatteryModel>(model.Id!, none) is { } described)
            {
                Unless(await pages.SetModuleDescriptionAsync(who, described, model.Description, none), model, "its module description wasn't saved");
            }

            if (model.Picture is { } picture)
            {
                Unless(await pages.SetModelPictureAsync(who, model.Id!, picture.FileName, picture.Content, none), model, "its picture wasn't saved");
            }
            else if (model.RemovePicture)
            {
                await pages.RemoveModelPictureAsync(who, model.Id!, none);
            }
        }

        // The published passports a record's change reaches, with the version each is on before the load touches it.
        async Task Reached(RecordPlan record)
        {
            if (record.ExistingId is null || record.Record == LoadRecord.Family)
            {
                return;
            }

            foreach (var passport in await editor.AffectedAsync(LoadFields.KindOf(record.Record), record.Id!, none))
            {
                before.TryAdd(passport.PackId, passport);
            }
        }

        // A record's name (when its capitals changed), its passport values and its uploaded documents.
        async Task ApplyAsync(RecordPlan record)
        {
            var id = record.Id!;
            if (record.RenameFrom is { } old)
            {
                _ = record.Record switch
                {
                    LoadRecord.Family => await pages.RenameAsync<BatteryFamily>(who, id, old, record.Name, none),
                    LoadRecord.Model => await pages.RenameAsync<BatteryModel>(who, id, old, record.Name, none),
                    LoadRecord.Site => await pages.RenameAsync<ManufacturingSite>(who, id, old, record.Name, none),
                    _ => await pages.RenameAsync<EconomicOperator>(who, id, old, record.Name, none),
                };
            }

            if (!record.ChangesPassportData)
            {
                return;
            }

            var kind = LoadFields.KindOf(record.Record);
            await Reached(record);
            if (record.Changed.Count > 0 || record.Removals.Count > 0)
            {
                // Pressing "Load" is the team's yes to the new versions the check announced.
                var post = new EditorPost(record.Changed, new Dictionary<string, string>(), new Dictionary<string, int>()) { Removals = record.Removals };
                var save = await editor.SaveAsync(sender, kind, id, post, confirmed: true, none);
                if (save.Outcome != EditorOutcome.Saved)
                {
                    var refused = string.Join("; ", save.Keys.Where(key => key.Outcome == KeyOutcome.Rejected).Select(key => $"{key.Key}: {key.Message}"));
                    throw new LoadStopped($"the values of {LoadFields.Words(record.Record).ToLowerInvariant()} {record.Name} weren't all saved{(refused.Length > 0 ? $" ({refused})" : string.Empty)}");
                }
            }

            foreach (var upload in record.Uploads)
            {
                var result = await documentService.UploadAsync(sender, kind, id, upload.Field.Attribute.Property, new DocumentUpload(upload.File.FileName, upload.File.Content, upload.Title, upload.Language), none);
                Unless(result.Problem, record, $"the file {upload.File.FileName} wasn't uploaded");
            }
        }
    }

    private static void Unless(string? problem, RecordPlan record, string what)
    {
        if (problem is not null)
        {
            throw new LoadStopped($"{LoadFields.Words(record.Record).ToLowerInvariant()} {record.Name}: {what} ({problem})");
        }
    }

    private Task StepAsync(string step) => StepHook is { } hook ? hook(step) : Task.CompletedTask;

    /// <summary>The records in the order they are saved: sites and operators, the family, its models, their batches.</summary>
    private static IEnumerable<RecordPlan> Ordered(LoadPlan plan) =>
        plan.Sites.Concat(plan.Operators).Concat(plan.Family is null ? [] : [plan.Family]).Concat(plan.Models).Concat(plan.Batches);

    /// <summary>
    /// A load that stopped part way. While it made the new records, they are removed again, newest first, so nothing is
    /// left. Once it changes records that were already there, everything it saved stays: the new records are whole, and
    /// each change saved is one the check showed. Loading the same workbook again finishes the rest; nothing has to be
    /// undone by hand.
    /// </summary>
    /// <param name="madeKept">In the second part: how many new records were made, and stay.</param>
    private async Task<LoadResult> StoppedAsync(string who, LoadCheck check, Exception exception, List<Func<Task<bool>>> made, List<string> touched, int madeKept = 0, int reachedBatteries = 0)
    {
        var left = 0;
        for (var index = made.Count - 1; index >= 0; index--)
        {
            try
            {
                if (!await made[index]())
                {
                    left++;
                }
            }
            catch (Exception)
            {
                left++;
            }
        }

        var reason = exception is LoadStopped ? exception.Message : $"an error stopped it ({exception.GetType().Name})";
        var removed = made.Count == 0 ? string.Empty
            : left == 0 ? " The records it had made were removed again."
            : $" {left} of the {Words.Count(made.Count, "record")} it had made couldn't be removed again; look at the product data pages.";
        var kept = madeKept > 0 ? $" The {Words.Count(madeKept, "new record")} it made stay{(madeKept == 1 ? "s" : string.Empty)}, complete." : string.Empty;
        var waiting = reachedBatteries > 0 && touched.Count > 0
            ? $" {Words.Count(reachedBatteries, "published battery", "published batteries")} built from these records stay{(reachedBatteries == 1 ? "s" : string.Empty)} on {(reachedBatteries == 1 ? "its" : "their")} version, with the changes waiting. Loading the workbook again, or \"Publish the waiting changes\" on {(reachedBatteries == 1 ? "its" : "each")} page, publishes {(reachedBatteries == 1 ? "it" : "them")}."
            : string.Empty;
        var stays = touched.Count == 0 && madeKept == 0 ? " Nothing was saved. Load again when the fault is gone."
            : $"{kept}{(touched.Count > 0 ? $" It had started to save changes to: {string.Join(", ", touched)}." : string.Empty)} Load again to finish; nothing has to be undone by hand.{waiting}";
        try
        {
            await audit.AppendApplicationAuditEventAsync(
                StoppedEvent,
                who,
                "Team",
                "form",
                $"Product data load from {check.WorkbookName} stopped part way: {reason}.{removed}{stays}",
                entityType: "dpp-family",
                cancellationToken: CancellationToken.None);
        }
        catch (Exception)
        {
            // the database is gone too: nothing more can be done here
        }

        return new LoadResult(false, $"The load stopped part way: {reason}.{removed}{stays}", check);
    }

    /// <summary>
    /// The published batteries that have changes waiting (their marker is set) and that any record named in the workbook
    /// reaches, changed or not (FU round two, 1). A battery is listed once.
    /// </summary>
    private async Task<IReadOnlyList<AffectedPassport>> WaitingReachedAsync(LoadPlan plan, CancellationToken cancellationToken)
    {
        var reached = new Dictionary<string, AffectedPassport>(StringComparer.Ordinal);
        foreach (var record in plan.All.Where(record => record.ExistingId is not null && record.Record != LoadRecord.Family))
        {
            foreach (var passport in await editor.AffectedAsync(LoadFields.KindOf(record.Record), record.ExistingId!, cancellationToken))
            {
                reached.TryAdd(passport.PackId, passport);
            }
        }

        var marked = (await store.GetManyAsync<BatteryPack>(reached.Keys, cancellationToken))
            .Where(pack => pack.ChangesWaitingSinceUtc is not null)
            .Select(pack => pack.Id)
            .ToHashSet(StringComparer.Ordinal);
        return reached.Values.Where(passport => marked.Contains(passport.PackId)).ToArray();
    }

    /// <summary>A step of the load that was refused although the check had passed it.</summary>
    private sealed class LoadStopped(string message) : Exception(message);
}
