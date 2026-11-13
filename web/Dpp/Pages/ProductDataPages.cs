using BatteryPassWeb.Configuration;
using BatteryPassWeb.Dpp.Catalog;
using BatteryPassWeb.Dpp.Documents;
using BatteryPassWeb.Dpp.Editing;
using BatteryPassWeb.Dpp.Publication;
using BatteryPassWeb.Dpp.Store;
using BatteryPassWeb.Dpp.Values;
using BatteryPassWeb.Services;
using Microsoft.Extensions.Options;
using MongoDB.Driver;

namespace BatteryPassWeb.Dpp.Pages;

/// <summary>The records the team pages create, rename and remove.</summary>
public enum RecordKind
{
    Family,
    Model,
    Batch,
    Site,
    Operator,
}

/// <summary>Where a record is removed, and why it can't be yet (null when it can).</summary>
public sealed record RemovalView(string Action, string? Blocked);

/// <summary>A model a new one can start from, with its family's name for grouping.</summary>
public sealed record CopySource(string FamilyName, BatteryModel Model);

/// <summary>One record's editor on a page: where it saves, what it shows, and the republish question when there is one.</summary>
public sealed record RecordEditorView(
    TargetKind Kind,
    string Id,
    string Name,
    string SaveAction,
    string CancelHref,
    IReadOnlyList<EditorSection> Sections,
    IReadOnlyList<AffectedPassport> Confirm,
    int PublishedPassports,
    IReadOnlyList<string> Languages,
    string Stamp,
    RemovalView Removal)
{
    /// <summary>Shown to the team (the default); false on the cluster admin's battery page (step 4a), which says "the team".</summary>
    public bool ForTeam { get; init; } = true;

    /// <summary>Why nothing can be saved (a battery whose passport is no longer active, redesign R1); null when it can.</summary>
    public string? FrozenNote { get; init; }

    public string UploadAction(string property) => $"{SaveAction}/documents/{Uri.EscapeDataString(property)}";

    public string KindLabel => Kind switch
    {
        TargetKind.Model => "Battery model",
        TargetKind.Batch => "Batch",
        TargetKind.Site => "Manufacturing site",
        TargetKind.Operator => "Economic operator",
        TargetKind.Pack => "Battery",
        _ => "Record",
    };

    public int Filled => Sections.Sum(section => section.Filled);

    public int Total => Sections.Sum(section => section.Fields.Count);

    /// <summary>Values the passport needs by February 2027 that are still empty.</summary>
    public int Missing => Sections.Sum(section => section.Missing);

    /// <summary>For a model's or a battery's editor: the model's three questions aren't all answered (R01), so it isn't complete.</summary>
    public bool QuestionsUnanswered { get; init; }

    /// <summary>Nothing the passport needs is missing and the model's questions are answered (round two, B): the one "complete".</summary>
    public bool IsComplete => Missing == 0 && !QuestionsUnanswered;
}

/// <summary>One field as its editor shows it.</summary>
public sealed record EditorFieldView(RecordEditorView Editor, EditorField Field);

/// <summary>An editor shown again after a post: what was typed and how the save went.</summary>
public sealed record EditorState(EditorPost Post, EditorSave Save);

/// <summary>What saving a model's answers did: <see cref="Saved"/>, or nothing yet because <see cref="Affected"/> published batteries would lack a value.</summary>
public sealed record AnswersSave(bool Saved, int Affected, bool NeedsConfirmation = false)
{
    /// <summary>What the published passports with waiting changes got once the answers were saved (round two, A3).</summary>
    public IReadOnlyList<(PassportOutcome Outcome, string SerialNumber)> Outcomes { get; init; } = [];
}

/// <param name="Unanswered">How many of its models haven't answered their three questions yet (R01).</param>
public sealed record FamilyRow(BatteryFamily Family, int Models, int Batches, int Packs, int Published, int Missing, int Unanswered = 0);

/// <summary>How complete a record is: values filled in, of how many, and how many the passport needs by Feb 2027 that are still empty.</summary>
public sealed record Completeness(int Filled, int Total, int Missing)
{
    /// <summary>
    /// A model's state in a line (round two, B): its missing count, else "questions to answer" while its three questions
    /// aren't answered, else complete. The family chip, the batch page and the editors say the same.
    /// </summary>
    public static (bool Done, string Words) OfModel(BatteryModel model)
    {
        ArgumentNullException.ThrowIfNull(model);
        var missing = RecordEditor.CountMissing(TargetKind.Model, model.Values, model.Answers());
        return missing > 0 ? (false, $"{missing} missing")
            : !model.Answers().AllAnswered ? (false, "Questions to answer")
            : (true, "Complete");
    }
}

public sealed record ModelChip(BatteryModel Model, int Filled, int Total, int Missing)
{
    /// <summary>The model has answered its three questions (R01); until then its batteries can't be published.</summary>
    public bool Answered => Model.Answers().AllAnswered;
}

public sealed record BatchRow(ProductionBatch Batch, string SiteName, int Filled, int Total, int Missing, int Packs, int Published);

public sealed record SiteRow(ManufacturingSite Site, int Filled, int Total, int Missing, int Batches);

public sealed record OperatorRow(EconomicOperator Operator, int Filled, int Total, int Missing, int Packs);

public sealed record FamiliesPage(IReadOnlyList<FamilyRow> Families);

public sealed record FamilyPage(
    BatteryFamily Family,
    IReadOnlyList<ModelChip> Models,
    BatteryModel? SelectedModel,
    IReadOnlyList<BatchRow> Batches,
    IReadOnlyList<ManufacturingSite> Sites,
    RecordEditorView? ModelEditor)
{
    /// <summary>Removing the family: possible once it has no models.</summary>
    public RemovalView Removal { get; init; } = new(string.Empty, null);

    /// <summary>The models a new model can start from: this family's first, then the others by family.</summary>
    public IReadOnlyList<CopySource> CopySources { get; init; } = [];

    /// <summary>What is still the demo's example in this real family's models and batches (decision 70).</summary>
    public IReadOnlyList<ExampleGroup> Examples { get; init; } = [];
}

public sealed record BatchPage(ProductionBatch Batch, BatteryModel Model, BatteryFamily Family, ManufacturingSite Site, RecordEditorView Editor)
{
    /// <summary>What is still the demo's example in this batch (decision 70).</summary>
    public IReadOnlyList<ExampleGroup> Examples { get; init; } = [];
}

public sealed record SitesAndOperatorsPage(IReadOnlyList<SiteRow> Sites, IReadOnlyList<OperatorRow> Operators, RecordEditorView? Editor)
{
    /// <summary>"Copy as real" for the chosen site or operator when it is demo data (decision 66); null otherwise.</summary>
    public RealCopyView? RealCopy { get; init; }

    /// <summary>What is still the demo's example in the chosen real site or operator (decision 70).</summary>
    public IReadOnlyList<ExampleGroup> Examples { get; init; } = [];
}

/// <summary>
/// The team's product data pages (decision 23): battery families with their models and batches, and the shared
/// manufacturing sites and economic operators. Passport values are saved by <see cref="RecordEditor"/>; this class
/// loads the pages and makes the changes that aren't passport data (new records, names, the family picture).
/// </summary>
public sealed partial class ProductDataPages(
    DppStore store,
    RecordEditor editor,
    DocumentStore documents,
    ApplicationAuditService audit,
    IOptions<BatteryPassOptions> options,
    TimeProvider clock,
    PublicationService? publication = null)
{
    public const string CreatedEvent = "dpp.record-created";
    public const string RenamedEvent = "dpp.record-renamed";
    public const string PictureEvent = "dpp.family-picture-set";
    public const string ModuleDescriptionEvent = "dpp.model-module-description-set";
    public const string AnswersEvent = "dpp.model-answers-set";
    public const string RemovedEvent = "dpp.record-removed";

    public async Task<FamiliesPage> FamiliesAsync(CancellationToken cancellationToken = default)
    {
        var rows = new List<FamilyRow>();
        foreach (var family in (await store.ListAsync<BatteryFamily>(cancellationToken)).OrderBy(family => family.Name, StringComparer.OrdinalIgnoreCase))
        {
            var models = await store.ListModelsAsync(family.Id, cancellationToken);
            var batches = new List<ProductionBatch>();
            foreach (var model in models)
            {
                batches.AddRange(await store.ListBatchesAsync(model.Id, cancellationToken));
            }

            var counts = (await store.CountPacksByBatchAsync(batches.Select(batch => batch.Id), cancellationToken)).Values;
            var missing = models.Sum(model => RecordEditor.CountMissing(TargetKind.Model, model.Values, model.Answers()))
                + batches.Sum(batch => RecordEditor.CountMissing(TargetKind.Batch, batch.Values, ModelAnswers.NotAnswered));
            var unanswered = models.Count(model => !model.Answers().AllAnswered);
            rows.Add(new FamilyRow(family, models.Count, batches.Count, counts.Sum(count => count.Packs), counts.Sum(count => count.Published), missing, unanswered));
        }

        return new FamiliesPage(rows);
    }

    /// <summary>A family with its models; the selected model (the first one if none is asked for) with its batches and editor.</summary>
    public async Task<FamilyPage?> FamilyAsync(string familyId, string? modelId, EditorState? state = null, CancellationToken cancellationToken = default)
    {
        var family = await store.GetAsync<BatteryFamily>(familyId, cancellationToken);
        if (family is null)
        {
            return null;
        }

        var models = await store.ListModelsAsync(familyId, cancellationToken);
        var total = RecordEditor.AttributesOf(TargetKind.Model).Count;
        var chips = models.Select(model => new ModelChip(model, RecordEditor.CountFilled(TargetKind.Model, model.Values), total, RecordEditor.CountMissing(TargetKind.Model, model.Values, model.Answers()))).ToArray();
        var selected = models.FirstOrDefault(model => model.Id == modelId) ?? models.FirstOrDefault();
        var sites = (await store.ListAsync<ManufacturingSite>(cancellationToken)).OrderBy(site => site.Name, StringComparer.OrdinalIgnoreCase).ToArray();
        var removal = new RemovalView($"/admin/families/{Uri.EscapeDataString(familyId)}/remove", await WhyNotRemovableAsync(RecordKind.Family, familyId, cancellationToken));
        var copySources = await CopySourcesAsync(family, models, cancellationToken);
        if (selected is null)
        {
            return new FamilyPage(family, chips, null, [], sites, null) { Removal = removal, CopySources = copySources, Examples = await ExamplesOfAsync(family, models, cancellationToken) };
        }

        var batches = await store.ListBatchesAsync(selected.Id, cancellationToken);
        var counts = await store.CountPacksByBatchAsync(batches.Select(batch => batch.Id), cancellationToken);
        var siteNames = sites.ToDictionary(site => site.Id, site => site.Name, StringComparer.Ordinal);
        var batchTotal = RecordEditor.AttributesOf(TargetKind.Batch).Count;
        var rows = batches
            .Select(batch => new BatchRow(
                batch,
                siteNames.GetValueOrDefault(batch.SiteId, batch.SiteId),
                RecordEditor.CountFilled(TargetKind.Batch, batch.Values),
                batchTotal,
                RecordEditor.CountMissing(TargetKind.Batch, batch.Values, ModelAnswers.NotAnswered),
                counts[batch.Id].Packs,
                counts[batch.Id].Published))
            .ToArray();
        var view = await EditorAsync(TargetKind.Model, selected.Id, selected.Name, selected.Values, $"/admin/models/{Uri.EscapeDataString(selected.Id)}", FamilyHref(familyId, selected.Id), state, cancellationToken, selected.Answers());
        return new FamilyPage(family, chips, selected, rows, sites, view) { Removal = removal, CopySources = copySources, Examples = await ExamplesOfAsync(family, models, cancellationToken) };
    }

    private async Task<IReadOnlyList<CopySource>> CopySourcesAsync(BatteryFamily family, IReadOnlyList<BatteryModel> own, CancellationToken cancellationToken)
    {
        var families = (await store.ListAsync<BatteryFamily>(cancellationToken)).ToDictionary(item => item.Id, item => item.Name, StringComparer.Ordinal);
        var others = (await store.ListAsync<BatteryModel>(cancellationToken))
            .Where(model => model.FamilyId != family.Id && families.ContainsKey(model.FamilyId))
            .Select(model => new CopySource(families[model.FamilyId], model))
            .OrderBy(source => source.FamilyName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(source => source.Model.Name, StringComparer.OrdinalIgnoreCase);
        return own.Select(model => new CopySource(family.Name, model)).Concat(others).ToArray();
    }

    public async Task<BatchPage?> BatchAsync(string batchId, EditorState? state = null, CancellationToken cancellationToken = default)
    {
        var batch = await store.GetAsync<ProductionBatch>(batchId, cancellationToken);
        var model = batch is null ? null : await store.GetAsync<BatteryModel>(batch.ModelId, cancellationToken);
        var family = model is null ? null : await store.GetAsync<BatteryFamily>(model.FamilyId, cancellationToken);
        var site = batch is null ? null : await store.GetAsync<ManufacturingSite>(batch.SiteId, cancellationToken);
        if (batch is null || model is null || family is null || site is null)
        {
            return null;
        }

        var href = $"/admin/batches/{Uri.EscapeDataString(batch.Id)}";
        var view = await EditorAsync(TargetKind.Batch, batch.Id, $"Batch {batch.Year} · {site.Name}", batch.Values, href, href, state, cancellationToken);
        return new BatchPage(batch, model, family, site, view) { Examples = batch.IsDemo ? [] : ExampleGroup.Of($"Batch {batch.Year} · {site.Name}", Editing.ExampleValues.Of(batch.Values, batch.ExampleDocumentIds)) };
    }

    /// <summary>The site and operator lists, with the editor of the one asked for (a site wins over an operator).</summary>
    public async Task<SitesAndOperatorsPage> SitesAndOperatorsAsync(string? siteId, string? operatorId, EditorState? state = null, CancellationToken cancellationToken = default)
    {
        var sites = (await store.ListAsync<ManufacturingSite>(cancellationToken)).OrderBy(site => site.Name, StringComparer.OrdinalIgnoreCase).ToArray();
        var operators = (await store.ListAsync<EconomicOperator>(cancellationToken)).OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase).ToArray();
        var batchesPerSite = (await store.ListAsync<ProductionBatch>(cancellationToken)).GroupBy(batch => batch.SiteId).ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);
        var siteRows = sites.Select(site => new SiteRow(site, RecordEditor.CountFilled(TargetKind.Site, site.Values), RecordEditor.AttributesOf(TargetKind.Site).Count, RecordEditor.CountMissing(TargetKind.Site, site.Values, ModelAnswers.NotAnswered), batchesPerSite.GetValueOrDefault(site.Id))).ToArray();
        var operatorRows = new List<OperatorRow>();
        foreach (var item in operators)
        {
            var packs = (await store.FindPackIdsUsingAsync(operatorId: item.Id, cancellationToken: cancellationToken)).Count;
            operatorRows.Add(new OperatorRow(item, RecordEditor.CountFilled(TargetKind.Operator, item.Values), RecordEditor.AttributesOf(TargetKind.Operator).Count, RecordEditor.CountMissing(TargetKind.Operator, item.Values, ModelAnswers.NotAnswered), packs));
        }

        RecordEditorView? view = null;
        IDppRecord? chosen = null;
        if (sites.FirstOrDefault(site => site.Id == siteId) is { } selectedSite)
        {
            var href = $"/admin/sites/{Uri.EscapeDataString(selectedSite.Id)}";
            view = await EditorAsync(TargetKind.Site, selectedSite.Id, selectedSite.Name, selectedSite.Values, href, SitesHref("site", selectedSite.Id), state, cancellationToken);
            chosen = selectedSite;
        }
        else if (operators.FirstOrDefault(item => item.Id == operatorId) is { } selectedOperator)
        {
            var href = $"/admin/operators/{Uri.EscapeDataString(selectedOperator.Id)}";
            view = await EditorAsync(TargetKind.Operator, selectedOperator.Id, selectedOperator.Name, selectedOperator.Values, href, SitesHref("operator", selectedOperator.Id), state, cancellationToken);
            chosen = selectedOperator;
        }

        return new SitesAndOperatorsPage(siteRows, operatorRows, view) { RealCopy = RealCopyFor(chosen), Examples = ExamplesOf(chosen) };
    }

    public async Task<string> CreateFamilyAsync(string who, string name, CancellationToken cancellationToken = default)
    {
        var family = new BatteryFamily { Id = NewId("family"), Name = name.Trim() };
        await store.InsertAsync(family, cancellationToken);
        await AuditAsync(CreatedEvent, who, $"Battery family {family.Name} created", "dpp-family", family.Id, cancellationToken);
        return family.Id;
    }

    /// <summary>
    /// The new model's id; null when the family doesn't exist. With <paramref name="copyFromId"/>, the model starts with
    /// that model's typed values, as entered by <paramref name="who"/> now; its uploaded documents stay with it.
    /// </summary>
    public async Task<string?> CreateModelAsync(string who, string familyId, string name, string? copyFromId = null, CancellationToken cancellationToken = default)
    {
        if (await store.GetAsync<BatteryFamily>(familyId, cancellationToken) is null)
        {
            return null;
        }

        var values = OneWayValues(TargetKind.Model, who);
        var source = string.IsNullOrEmpty(copyFromId) ? null : await store.GetAsync<BatteryModel>(copyFromId, cancellationToken);
        if (source is not null)
        {
            var now = clock.GetUtcNow().UtcDateTime;
            foreach (var attribute in RecordEditor.AttributesOf(TargetKind.Model))
            {
                if (editor.Forms.For(attribute).Kind is not (Forms.FormKind.Upload or Forms.FormKind.Fixed)
                    && source.Values.TryGetValue(attribute.Property, out var copied))
                {
                    values[attribute.Property] = new AttributeValue { Value = copied.Value.DeepClone(), GeneratedAtUtc = now, Source = "form", Who = who };
                }
            }
        }

        var model = new BatteryModel { Id = NewId("model"), FamilyId = familyId, Name = name.Trim(), Values = values };
        await store.InsertAsync(model, cancellationToken);
        await AuditAsync(
            CreatedEvent,
            who,
            source is null ? $"Battery model {model.Name} created" : $"Battery model {model.Name} created as a copy of {source.Name} ({Words.Count(values.Count, "value")})",
            "dpp-model",
            model.Id,
            cancellationToken);
        return model.Id;
    }

    /// <summary>
    /// Why a new or renamed record can't take this name: another family, another model of the same family, another
    /// site or another operator already has it (ignoring case). Null when the name is free.
    /// </summary>
    public async Task<string?> NameTakenAsync(RecordKind kind, string name, string? familyId = null, string? exceptId = null, CancellationToken cancellationToken = default)
    {
        var wanted = name.Trim();

        // A name is taken only by a record of the same kind: real product data may carry a name the demo data uses
        // (the real family "Core" next to the demo's "Core"); the lists mark the demo ones. A new record is real, and
        // a record that is renamed is compared with its own kind.
        bool Taken<T>(IEnumerable<T> records, Func<T, string> id, Func<T, string> itemName, Func<T, bool> isDemo)
        {
            var items = records.ToList();
            var subjectIsDemo = exceptId is not null && items.Any(item => id(item) == exceptId && isDemo(item));
            return items.Any(item => id(item) != exceptId && isDemo(item) == subjectIsDemo && string.Equals(itemName(item).Trim(), wanted, StringComparison.OrdinalIgnoreCase));
        }

        return kind switch
        {
            RecordKind.Family when Taken(await store.ListAsync<BatteryFamily>(cancellationToken), item => item.Id, item => item.Name, item => item.IsDemo) => $"another battery family is already called {wanted}",
            RecordKind.Model when Taken(await store.ListModelsAsync(familyId ?? string.Empty, cancellationToken), item => item.Id, item => item.Name, item => item.IsDemo) => $"this family already has a model called {wanted}",
            RecordKind.Site when Taken(await store.ListAsync<ManufacturingSite>(cancellationToken), item => item.Id, item => item.Name, item => item.IsDemo) => $"another manufacturing site is already called {wanted}",
            RecordKind.Operator when Taken(await store.ListAsync<EconomicOperator>(cancellationToken), item => item.Id, item => item.Name, item => item.IsDemo) => $"another economic operator is already called {wanted}",
            _ => null,
        };
    }

    /// <summary>
    /// Why a record can't be removed yet: something still uses it. Only unused records are removed, so no battery or
    /// passport ever loses what it was built from. Null when it can be removed.
    /// </summary>
    public async Task<string?> WhyNotRemovableAsync(RecordKind kind, string id, CancellationToken cancellationToken = default)
    {
        switch (kind)
        {
            case RecordKind.Family:
                var models = (await store.ListModelsAsync(id, cancellationToken)).Count;
                return models > 0 ? $"It still has {Words.Count(models, "battery model")}. Remove them first." : null;
            case RecordKind.Model:
                var modelPacks = (await store.FindPackIdsUsingAsync(modelId: id, cancellationToken: cancellationToken)).Count;
                var batches = (await store.ListBatchesAsync(id, cancellationToken)).Count;
                return modelPacks > 0 ? $"{Words.Count(modelPacks, "battery", "batteries")} {(modelPacks == 1 ? "uses" : "use")} it."
                    : batches > 0 ? $"It still has {Words.Count(batches, "batch", "batches")}. Remove them first." : null;
            case RecordKind.Batch:
                var batchPacks = (await store.FindPackIdsUsingAsync(batchId: id, cancellationToken: cancellationToken)).Count;
                return batchPacks > 0 ? $"{Words.Count(batchPacks, "battery", "batteries")} {(batchPacks == 1 ? "belongs" : "belong")} to it." : null;
            case RecordKind.Site:
                var siteBatches = (await store.ListAsync<ProductionBatch>(cancellationToken)).Count(batch => batch.SiteId == id);
                var sitePacks = (await store.FindPackIdsUsingAsync(siteId: id, cancellationToken: cancellationToken)).Count;
                return siteBatches > 0 ? $"{Words.Count(siteBatches, "batch", "batches")} {(siteBatches == 1 ? "was" : "were")} made there."
                    : sitePacks > 0 ? $"{Words.Count(sitePacks, "battery", "batteries")} {(sitePacks == 1 ? "was" : "were")} made there." : null;
            case RecordKind.Operator:
                var operatorPacks = (await store.FindPackIdsUsingAsync(operatorId: id, cancellationToken: cancellationToken)).Count;
                return operatorPacks > 0 ? $"{Words.Count(operatorPacks, "battery", "batteries")} {(operatorPacks == 1 ? "names" : "name")} it as economic operator." : null;
            default:
                throw new ArgumentOutOfRangeException(nameof(kind), kind, null);
        }
    }

    /// <summary>Removes an unused record with its uploaded files; the reason when it can't be removed.</summary>
    public async Task<(bool Removed, string? Problem, string Name)> RemoveAsync(string who, RecordKind kind, string id, CancellationToken cancellationToken = default)
    {
        var name = kind switch
        {
            RecordKind.Family => (await store.GetAsync<BatteryFamily>(id, cancellationToken))?.Name,
            RecordKind.Model => (await store.GetAsync<BatteryModel>(id, cancellationToken))?.Name,
            RecordKind.Batch => await store.GetAsync<ProductionBatch>(id, cancellationToken) is { } batch ? $"Batch {batch.Year}" : null,
            RecordKind.Site => (await store.GetAsync<ManufacturingSite>(id, cancellationToken))?.Name,
            RecordKind.Operator => (await store.GetAsync<EconomicOperator>(id, cancellationToken))?.Name,
            _ => null,
        };
        if (name is null)
        {
            return (false, "it was not found", string.Empty);
        }

        if (await WhyNotRemovableAsync(kind, id, cancellationToken) is { } blocked)
        {
            return (false, blocked, name);
        }

        switch (kind)
        {
            case RecordKind.Family:
                var family = await store.GetAsync<BatteryFamily>(id, cancellationToken);
                await store.DeleteAsync<BatteryFamily>(id, cancellationToken);
                if (family?.PictureFileId is { } pictureId)
                {
                    await documents.DeleteFileAsync(pictureId, cancellationToken);
                }

                break;
            case RecordKind.Model:
                await store.DeleteAsync<BatteryModel>(id, cancellationToken);
                await documents.DeleteOwnedAsync(TargetKind.Model, id, cancellationToken);
                break;
            case RecordKind.Batch:
                await store.DeleteAsync<ProductionBatch>(id, cancellationToken);
                await documents.DeleteOwnedAsync(TargetKind.Batch, id, cancellationToken);
                break;
            case RecordKind.Site:
                await store.DeleteAsync<ManufacturingSite>(id, cancellationToken);
                break;
            case RecordKind.Operator:
                await store.DeleteAsync<EconomicOperator>(id, cancellationToken);
                break;
        }

        await AuditAsync(RemovedEvent, who, $"{KindName(kind)} {name} removed", $"dpp-{kind.ToString().ToLowerInvariant()}", id, cancellationToken);
        return (true, null, name);
    }

    public static string KindName(RecordKind kind) => kind switch
    {
        RecordKind.Family => "Battery family",
        RecordKind.Model => "Battery model",
        RecordKind.Batch => "Batch",
        RecordKind.Site => "Manufacturing site",
        _ => "Economic operator",
    };

    /// <summary>A new batch of a model: one per calendar year and manufacturing site (decision 3).</summary>
    public async Task<(string? BatchId, string? Problem)> CreateBatchAsync(string who, string modelId, int year, string siteId, CancellationToken cancellationToken = default)
    {
        if (await store.GetAsync<BatteryModel>(modelId, cancellationToken) is null)
        {
            return (null, "the battery model was not found");
        }

        if (await store.GetAsync<ManufacturingSite>(siteId, cancellationToken) is null)
        {
            return (null, "choose a manufacturing site; add it under Sites and operators first if it's missing");
        }

        if (year is < 2000 or > 2100)
        {
            return (null, "the year must be a calendar year such as 2026");
        }

        var batch = new ProductionBatch { Id = NewId("batch"), ModelId = modelId, SiteId = siteId, Year = year, Values = OneWayValues(TargetKind.Batch, who) };
        try
        {
            await store.InsertAsync(batch, cancellationToken);
        }
        catch (MongoWriteException exception) when (exception.WriteError.Category == ServerErrorCategory.DuplicateKey)
        {
            return (null, $"this model already has a {year} batch from that site");
        }

        await AuditAsync(CreatedEvent, who, $"Batch {year} of model {modelId} created", "dpp-batch", batch.Id, cancellationToken);
        return (batch.Id, null);
    }

    public async Task<string> CreateSiteAsync(string who, string name, CancellationToken cancellationToken = default)
    {
        var site = new ManufacturingSite { Id = NewId("site"), Name = name.Trim(), Values = OneWayValues(TargetKind.Site, who) };
        await store.InsertAsync(site, cancellationToken);
        await AuditAsync(CreatedEvent, who, $"Manufacturing site {site.Name} created", "dpp-site", site.Id, cancellationToken);
        return site.Id;
    }

    public async Task<string> CreateOperatorAsync(string who, string name, CancellationToken cancellationToken = default)
    {
        var item = new EconomicOperator { Id = NewId("operator"), Name = name.Trim(), Values = OneWayValues(TargetKind.Operator, who) };
        await store.InsertAsync(item, cancellationToken);
        await AuditAsync(CreatedEvent, who, $"Economic operator {item.Name} created", "dpp-operator", item.Id, cancellationToken);
        return item.Id;
    }

    /// <summary>Renames a family, model, site or operator when the name changed; true when it did.</summary>
    public async Task<bool> RenameAsync<T>(string who, string id, string currentName, string? name, CancellationToken cancellationToken = default)
        where T : class, IDppRecord
    {
        var trimmed = name?.Trim();
        if (string.IsNullOrEmpty(trimmed) || trimmed == currentName || !await store.RenameAsync<T>(id, trimmed, cancellationToken))
        {
            return false;
        }

        var entityType = typeof(T).Name switch
        {
            nameof(BatteryFamily) => "dpp-family",
            nameof(BatteryModel) => "dpp-model",
            nameof(ManufacturingSite) => "dpp-site",
            _ => "dpp-operator",
        };
        await AuditAsync(RenamedEvent, who, $"{currentName} renamed to {trimmed}", entityType, id, cancellationToken);
        return true;
    }

    /// <summary>Stores a PNG or JPEG as the family's picture (decision 20); the problem when it isn't accepted.</summary>
    public async Task<string?> SetFamilyPictureAsync(string who, string familyId, string fileName, byte[] content, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(content);
        if (await store.GetAsync<BatteryFamily>(familyId, cancellationToken) is null)
        {
            return "the battery family was not found";
        }

        if (PictureProblem(content) is { } problem)
        {
            return problem;
        }

        var fileId = await documents.UploadFileAsync(Path.GetFileName(fileName), content, cancellationToken);
        await store.SetFamilyPictureAsync(familyId, fileId, FileTypes.Detect(content)!, cancellationToken);
        await AuditAsync(PictureEvent, who, $"Picture of battery family {familyId} set", "dpp-family", familyId, cancellationToken);
        return null;
    }

    /// <summary>
    /// Sets or clears a model's module description (decision 31). App data, not passport data: no new version. Returns
    /// the problem, or null when it was saved or nothing changed.
    /// </summary>
    public async Task<string?> SetModuleDescriptionAsync(string who, BatteryModel model, string? description, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(model);
        var wanted = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        if (wanted?.Length > ModuleList.MaxDescriptionLength)
        {
            return $"keep it to {ModuleList.MaxDescriptionLength} characters";
        }

        if (wanted == model.ModuleDescription)
        {
            return null;
        }

        await store.SetModelModuleDescriptionAsync(model.Id, wanted, cancellationToken);
        await AuditAsync(ModuleDescriptionEvent, who, $"Module description of battery model {model.Name} {(wanted is null ? "cleared" : "set")}", "dpp-model", model.Id, cancellationToken);
        return null;
    }

    /// <summary>
    /// Saves a model's answers to its three questions (R01). A question left without an answer keeps the one it has: an
    /// answer is changed, never taken back. When a new "yes" asks for values that published batteries of this model lack,
    /// nothing is saved until <paramref name="confirmed"/>: those batteries can't get a new version until the values are
    /// filled in. The answers aren't passport data, so no passport changes.
    /// </summary>
    public async Task<AnswersSave> SetModelAnswersAsync(string who, BatteryModel model, ModelAnswers posted, bool confirmed, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(posted);
        var before = model.Answers();
        var after = before;
        foreach (var question in ModelQuestions.All)
        {
            if (posted.Of(question) is { } answer)
            {
                after = after.With(question, answer);
            }
        }

        if (after == before)
        {
            return new AnswersSave(false, 0);
        }

        var affected = await BatteriesLackingAsync(model, before, after, cancellationToken);
        if (affected > 0 && !confirmed)
        {
            return new AnswersSave(false, affected, NeedsConfirmation: true);
        }

        await store.SetModelAnswersAsync(model.Id, after.CommercialWarranty, after.CyclingUse, after.CadmiumOrLeadAboveLimit, cancellationToken);
        var words = string.Join("; ", ModelQuestions.All.Select(question => $"{ModelQuestions.Text(question)} {Answer(after.Of(question))}"));
        await AuditAsync(AnswersEvent, who, $"Answers of battery model {model.Name} saved: {words}", "dpp-model", model.Id, cancellationToken);

        // A change saved while the model couldn't publish is in its published passports once they can (decision 9). The
        // answers say, per passport that had a change waiting, what it got (round two, A3).
        IReadOnlyList<(PassportOutcome, string)> outcomes = [];
        if (publication is not null && after.AllAnswered)
        {
            var reached = await editor.AffectedAsync(TargetKind.Model, model.Id, cancellationToken);
            var marked = (await store.GetManyAsync<BatteryPack>(reached.Select(passport => passport.PackId), cancellationToken))
                .Where(pack => pack.ChangesWaitingSinceUtc is not null)
                .Select(pack => pack.Id)
                .ToHashSet(StringComparer.Ordinal);
            var waiting = reached.Where(passport => marked.Contains(passport.PackId)).ToList(); // the stored markers (round three, 1)

            await publication.RepublishAsync(ChangeSender.Team(who), waiting.Select(passport => passport.PackId), cancellationToken);
            outcomes = await editor.OutcomesAsync(waiting, cancellationToken);
        }

        return new AnswersSave(true, affected) { Outcomes = outcomes };
    }

    /// <summary>An answer in words, as the audit log and the page say it.</summary>
    public static string Answer(bool? answer) => answer switch
    {
        true => "Yes",
        false => "No",
        null => "Not answered yet",
    };

    /// <summary>
    /// How many published batteries of the model lack a value that the <paramref name="after"/> answers ask for and the
    /// <paramref name="before"/> answers didn't (the model's own value, or the battery's own for #19 and #83).
    /// </summary>
    private async Task<int> BatteriesLackingAsync(BatteryModel model, ModelAnswers before, ModelAnswers after, CancellationToken cancellationToken)
    {
        var newlyNeeded = AttributeCatalog.All
            .Where(attribute => PassportNeeds.NeedOf(attribute, after) == Need.Required && PassportNeeds.NeedOf(attribute, before) != Need.Required)
            .ToArray();
        if (newlyNeeded.Length == 0)
        {
            return 0;
        }

        var packIds = await store.FindPackIdsUsingAsync(modelId: model.Id, cancellationToken: cancellationToken);
        var published = (await store.ListActiveVersionsAsync(packIds, cancellationToken)).Select(version => version.PackId).ToHashSet(StringComparer.Ordinal);
        var packs = await store.GetManyAsync<BatteryPack>(published, cancellationToken);
        return packs.Count(pack => newlyNeeded.Any(attribute => !pack.Values.ContainsKey(attribute.Property) && !model.Values.ContainsKey(attribute.Property)));
    }

    /// <summary>The family's picture; null when the family or its picture doesn't exist.</summary>
    /// <summary>Stores a PNG or JPEG as a model's own picture, shown instead of its family's (redesign R2); the problem when it isn't accepted.</summary>
    public async Task<string?> SetModelPictureAsync(string who, string modelId, string fileName, byte[] content, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(content);
        if (await store.GetAsync<BatteryModel>(modelId, cancellationToken) is not { } model)
        {
            return "the battery model was not found";
        }

        if (PictureProblem(content) is { } problem)
        {
            return problem;
        }

        var fileId = await documents.UploadFileAsync(Path.GetFileName(fileName), content, cancellationToken);
        await store.SetModelPictureAsync(modelId, fileId, FileTypes.Detect(content)!, cancellationToken);
        if (model.PictureFileId is { } replaced)
        {
            await documents.DeleteFileAsync(replaced, cancellationToken);
        }

        await AuditAsync(PictureEvent, who, $"Picture of battery model {model.Name} set", "dpp-model", modelId, cancellationToken);
        return null;
    }

    /// <summary>Removes a model's own picture, so its batteries show the family's again (redesign R2); false when it had none.</summary>
    public async Task<bool> RemoveModelPictureAsync(string who, string modelId, CancellationToken cancellationToken = default)
    {
        if (await store.GetAsync<BatteryModel>(modelId, cancellationToken) is not { PictureFileId: { } fileId } model)
        {
            return false;
        }

        await store.SetModelPictureAsync(modelId, null, null, cancellationToken);
        await documents.DeleteFileAsync(fileId, cancellationToken);
        await AuditAsync(PictureEvent, who, $"Picture of battery model {model.Name} removed", "dpp-model", modelId, cancellationToken);
        return true;
    }

    public async Task<(byte[] Content, string ContentType)?> ModelPictureAsync(string modelId, CancellationToken cancellationToken = default)
    {
        var model = await store.GetAsync<BatteryModel>(modelId, cancellationToken);
        if (model?.PictureFileId is null || model.PictureContentType is null)
        {
            return null;
        }

        return (await documents.DownloadFileAsync(model.PictureFileId, cancellationToken), model.PictureContentType);
    }

    /// <summary>Why a picture isn't accepted (not a PNG or JPEG, or over the upload limit); null when it is.</summary>
    private string? PictureProblem(byte[] content)
    {
        var limit = options.Value.MaxUploadBytes;
        if ((content.Length == 0 ? null : FileTypes.Detect(content)) is not (FileTypes.Png or FileTypes.Jpeg))
        {
            return "the picture must be a PNG or JPEG file";
        }

        return limit > 0 && content.LongLength > limit ? $"the picture is larger than the {limit}-byte upload limit" : null;
    }

    public async Task<(byte[] Content, string ContentType)?> FamilyPictureAsync(string familyId, CancellationToken cancellationToken = default)
    {
        var family = await store.GetAsync<BatteryFamily>(familyId, cancellationToken);
        if (family?.PictureFileId is null || family.PictureContentType is null)
        {
            return null;
        }

        return (await documents.DownloadFileAsync(family.PictureFileId, cancellationToken), family.PictureContentType);
    }

    public static string FamilyHref(string familyId, string? modelId = null) =>
        modelId is null
            ? $"/admin/families/{Uri.EscapeDataString(familyId)}"
            : $"/admin/families/{Uri.EscapeDataString(familyId)}?model={Uri.EscapeDataString(modelId)}";

    public static string SitesHref(string kind, string id) => $"/admin/sites-and-operators?{kind}={Uri.EscapeDataString(id)}";

    /// <summary>A link back to a family page, passed along when the team goes to add a site; anything else is dropped.</summary>
    public static string? SafeReturn(string? href) =>
        href is { Length: > 0 } && href.StartsWith("/admin/families/", StringComparison.Ordinal) && !href.Contains("//", StringComparison.Ordinal) && !href.Contains('\\')
            ? href
            : null;

    private async Task<RecordEditorView> EditorAsync(
        TargetKind kind,
        string id,
        string name,
        IReadOnlyDictionary<string, Values.AttributeValue> stored,
        string saveAction,
        string cancelHref,
        EditorState? state,
        CancellationToken cancellationToken,
        ModelAnswers? answers = null)
    {
        var refusals = state?.Save.Refusals(editor.Forms);
        var sections = await editor.SectionsAsync(kind, id, stored, answers ?? ModelAnswers.NotAnswered, state?.Post, refusals, cancellationToken);
        var published = await editor.AffectedAsync(kind, id, cancellationToken);
        var recordKind = kind switch
        {
            TargetKind.Model => RecordKind.Model,
            TargetKind.Batch => RecordKind.Batch,
            TargetKind.Site => RecordKind.Site,
            _ => RecordKind.Operator,
        };
        return new RecordEditorView(
            kind,
            id,
            name,
            saveAction,
            cancelHref,
            sections,
            state?.Save.Outcome == EditorOutcome.NeedsConfirmation ? state.Save.Affected : [],
            published.Count,
            editor.Forms.Languages,
            editor.Stamp(kind, stored, kind == TargetKind.Batch ? null : name),
            new RemovalView($"{saveAction}/remove", await WhyNotRemovableAsync(recordKind, id, cancellationToken)))
        {
            QuestionsUnanswered = kind == TargetKind.Model && answers is { AllAnswered: false },
        };
    }

    private Task AuditAsync(string eventType, string who, string summary, string entityType, string entityId, CancellationToken cancellationToken) =>
        audit.AppendApplicationAuditEventAsync(eventType, who, "Team", "form", summary, entityType: entityType, entityId: entityId, cancellationToken: cancellationToken);

    private static string NewId(string prefix) => $"{prefix}-{Guid.NewGuid():N}";

    // A new record starts with the values the schema allows only one way (see RecordEditor.OneWayValues).
    private Dictionary<string, AttributeValue> OneWayValues(TargetKind kind, string who)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        return editor.OneWayValues(kind).ToDictionary(
            entry => entry.Key,
            entry => new AttributeValue { Value = entry.Value.DeepClone(), GeneratedAtUtc = now, Source = "form", Who = who },
            StringComparer.Ordinal);
    }
}
