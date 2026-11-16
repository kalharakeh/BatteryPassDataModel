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

            return check.HasChanges
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
        var touched = new List<string>(); // records that were already there and that the load began to change
        var before = new Dictionary<string, AffectedPassport>(StringComparer.Ordinal);
        try
        {
            foreach (var site in plan.Sites)
            {
                site.Id = site.ExistingId ?? await pages.CreateSiteAsync(who, site.Name, none);
                Made(site, RecordKind.Site);
                await ApplyAsync(site);
                await StepAsync("site");
            }

            foreach (var item in plan.Operators)
            {
                item.Id = item.ExistingId ?? await pages.CreateOperatorAsync(who, item.Name, none);
                Made(item, RecordKind.Operator);
                await ApplyAsync(item);
                await StepAsync("operator");
            }

            if (plan.Family is { } family)
            {
                family.Id = family.ExistingId ?? await pages.CreateFamilyAsync(who, family.Name, none);
                Made(family, RecordKind.Family);
                await ApplyAsync(family);
                if (family.Picture is { } picture)
                {
                    Unless(await pages.SetFamilyPictureAsync(who, family.Id, picture.FileName, picture.Content, none), family, "its picture wasn't saved");
                }

                await StepAsync("family");
            }

            foreach (var model in plan.Models)
            {
                model.Id = model.ExistingId
                    ?? await pages.CreateModelAsync(who, plan.Family!.Id!, model.Name, null, none)
                    ?? throw new LoadStopped($"the battery model {model.Name} couldn't be made: its family is gone");
                Made(model, RecordKind.Model);
                await OwnModelFieldsAsync(model);
                await ApplyAsync(model);
                await StepAsync("model");
            }

            foreach (var batch in plan.Batches)
            {
                if (batch.ExistingId is null)
                {
                    var (batchId, problem) = await pages.CreateBatchAsync(who, batch.Model!.Id!, batch.Year, batch.Site?.Id ?? batch.ExistingSiteId!, none);
                    Unless(problem, batch, "it couldn't be made");
                    batch.Id = batchId;
                }
                else
                {
                    batch.Id = batch.ExistingId;
                }

                Made(batch, RecordKind.Batch);
                await ApplyAsync(batch);
                await StepAsync("batch");
            }
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            return await StoppedAsync(who, check, exception, made, touched);
        }

        var outcomes = await editor.OutcomesAsync(before.Values.ToArray(), none);
        var newRecords = plan.All.Count(record => record.IsNew);
        var changed = plan.All.Count(record => !record.IsNew && record.HasWork);
        var did = (newRecords, changed) switch
        {
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
        return new LoadResult(true, $"Product data loaded: {did}, of {check.Summary}.{published}", check, plan.Family?.Id);

        // A record the load made is removed again if the load stops; one that was there is noted as begun.
        void Made(RecordPlan record, RecordKind kind)
        {
            var id = record.Id!;
            if (record.ExistingId is not null)
            {
                if (record.HasWork)
                {
                    touched.Add($"{LoadFields.Words(record.Record).ToLowerInvariant()} {record.Name}");
                }

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

    /// <summary>
    /// A load that stopped part way: the records it had made are removed again, newest first, so a first load leaves
    /// nothing behind. Changes it had already saved to records that were there before stay, and the message names
    /// those records; loading the same workbook again finishes the rest.
    /// </summary>
    private async Task<LoadResult> StoppedAsync(string who, LoadCheck check, Exception exception, List<Func<Task<bool>>> made, List<string> touched)
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
        var stays = touched.Count == 0 ? " Nothing was saved."
            : $" Changes it had already saved stay, in: {string.Join(", ", touched)}. Check the workbook again to see what is left, and load it again.";
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

    /// <summary>A step of the load that was refused although the check had passed it.</summary>
    private sealed class LoadStopped(string message) : Exception(message);
}
