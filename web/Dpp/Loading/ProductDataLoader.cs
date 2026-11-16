using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using BatteryPassWeb.Dpp.Catalog;
using BatteryPassWeb.Dpp.Documents;
using BatteryPassWeb.Dpp.Editing;
using BatteryPassWeb.Dpp.Forms;
using BatteryPassWeb.Dpp.Pages;
using BatteryPassWeb.Dpp.Store;
using BatteryPassWeb.Dpp.Values;
using BatteryPassWeb.Services;

namespace BatteryPassWeb.Dpp.Loading;

/// <summary>
/// Loads product data from the owner's Excel workbook (decision 83): a battery family with its models, their batches,
/// the manufacturing sites and the economic operators, with the documents and pictures sent along.
/// <list type="bullet">
/// <item><see cref="CheckAsync"/> saves nothing. It says what a load would do, value by value, and names every problem
/// by sheet, row and column. A value is judged by the forms' own reading (<see cref="FormValues"/>) and the save
/// pipeline's own checks, so the check refuses what the forms would refuse.</item>
/// <item><see cref="LoadAsync"/> saves all of it or none of it, and only what a check without problems showed. It
/// goes through the methods the product data pages call, so the audit log, the waiting marker and the new versions of
/// published passports are as when the team saves by hand.</item>
/// </list>
/// Records are found by name (the family; a model within it; a site; an operator) and a batch by model, site and year,
/// among real records only, so a second load updates and never duplicates. An empty cell changes nothing; the word
/// <c>none</c> takes a value away.
/// </summary>
public sealed partial class ProductDataLoader(
    DppStore store,
    ProductDataPages pages,
    RecordEditor editor,
    SavePipeline pipeline,
    DocumentService documentService,
    DocumentStore documents,
    DppLinks links,
    AttributeForms forms,
    ApplicationAuditService audit)
{
    /// <summary>The check judges values as the team's: who presses "Load" makes no difference to what is accepted.</summary>
    private static readonly ChangeSender Checker = ChangeSender.Team("product-data-load");

    private readonly LoadFields _fields = new(forms);

    /// <summary>What a load of this workbook with these files would do. Saves nothing.</summary>
    public async Task<LoadCheck> CheckAsync(string workbookName, byte[] workbook, IReadOnlyList<SentFile> files, CancellationToken cancellationToken = default) =>
        (await PlanAsync(workbookName, workbook, files, cancellationToken)).Check;

    private async Task<(LoadCheck Check, LoadPlan? Plan)> PlanAsync(string workbookName, byte[] workbook, IReadOnlyList<SentFile> files, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(workbook);
        ArgumentNullException.ThrowIfNull(files);
        var name = Path.GetFileName(workbookName ?? string.Empty);
        if (!name.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase))
        {
            return (Unread(name, workbook, files, XlsxReader.NotAWorkbook), null);
        }

        var (book, unreadable) = XlsxReader.Read(workbook);
        if (book is null)
        {
            return (Unread(name, workbook, files, unreadable!), null);
        }

        var content = LoadWorkbook.Read(book, _fields);
        var plan = new LoadPlan();
        plan.Problems.AddRange(content.Problems);
        var sent = new SentFiles(files, plan.Problems);

        await PlanNamedAsync(content.Sites, LoadRecord.Site, "site|Name", plan, plan.Sites, cancellationToken);
        await PlanNamedAsync(content.Operators, LoadRecord.Operator, "operator|Name", plan, plan.Operators, cancellationToken);
        await PlanFamilyAsync(content, plan, sent, cancellationToken);
        var models = await PlanModelsAsync(content, plan, sent, cancellationToken);
        await PlanBatchesAsync(content, plan, models, sent, cancellationToken);
        UnknownModels(content, plan, models);

        var named = content.Documents.Select(line => line.Link)
            .Concat(content.Models.Columns.Select(column => column.Cell("model|Picture")))
            .Concat(content.Family.Columns.Select(column => column.Cell("family|Picture")))
            .Where(cell => cell is { IsNone: false })
            .Select(cell => cell!.Text);
        foreach (var file in sent.NotAmong(named))
        {
            plan.Problems.Add(new LoadProblem(string.Empty, string.Empty, string.Empty, $"the file {file.FileName} was sent, but the workbook doesn't name it anywhere. Write its name in the cell it belongs to, or leave it out"));
        }

        foreach (var record in plan.All)
        {
            await ValidateAsync(record, plan, cancellationToken);
        }

        await PublishedAsync(plan, cancellationToken);
        return (Finish(name, workbook, files, plan), plan);
    }

    // ---------- sites and operators ----------

    private sealed record Existing(string Id, string Name, bool IsDemo, IReadOnlyDictionary<string, AttributeValue> Values);

    private async Task<IReadOnlyList<Existing>> ExistingAsync(LoadRecord record, CancellationToken cancellationToken) => record switch
    {
        LoadRecord.Site => (await store.ListAsync<ManufacturingSite>(cancellationToken)).Select(site => new Existing(site.Id, site.Name, site.IsDemo, site.Values)).ToArray(),
        LoadRecord.Operator => (await store.ListAsync<EconomicOperator>(cancellationToken)).Select(item => new Existing(item.Id, item.Name, item.IsDemo, item.Values)).ToArray(),
        LoadRecord.Family => (await store.ListAsync<BatteryFamily>(cancellationToken)).Select(family => new Existing(family.Id, family.Name, family.IsDemo, new Dictionary<string, AttributeValue>())).ToArray(),
        _ => throw new ArgumentOutOfRangeException(nameof(record), record, null),
    };

    /// <summary>The sites or the operators: each column with a name is one; a column without a name is not there.</summary>
    private async Task PlanNamedAsync(ValueSheet sheet, LoadRecord record, string nameKey, LoadPlan plan, List<RecordPlan> into, CancellationToken cancellationToken)
    {
        var existing = await ExistingAsync(record, cancellationToken);
        foreach (var column in sheet.Columns)
        {
            if (column.Cell(nameKey) is not { IsNone: false } nameCell)
            {
                if (HasTyped(sheet, column))
                {
                    plan.Problems.Add(sheet.Place(nameKey, column).Problem($"this column has values but no name. Give the {LoadFields.Words(record).ToLowerInvariant()} a name, or empty the column"));
                }

                continue;
            }

            if (into.Any(other => SameName(other.Name, nameCell.Text)))
            {
                plan.Problems.Add(nameCell.Place.Problem($"two columns are called {nameCell.Text}. Give each {LoadFields.Words(record).ToLowerInvariant()} its own name"));
                continue;
            }

            var item = new RecordPlan(record, nameCell.Text, column.Header);
            Match(item, existing, nameCell, plan);
            into.Add(item);
            PlanValues(item, sheet, column.Cells, plan);
        }
    }

    /// <summary>
    /// Finds the real record of this name. A demo record that holds the name is a problem: real product data and demo
    /// data don't share a name (decision 82), and a demo record is never updated or taken over.
    /// </summary>
    private static void Match(RecordPlan record, IReadOnlyList<Existing> existing, FilledCell nameCell, LoadPlan plan)
    {
        var named = existing.Where(item => SameName(item.Name, record.Name)).ToArray();
        if (named.FirstOrDefault(item => !item.IsDemo) is { } real)
        {
            record.ExistingId = real.Id;
            record.Stored = real.Values;
            if (!string.Equals(real.Name, record.Name, StringComparison.Ordinal))
            {
                record.RenameFrom = real.Name;
                record.Lines.Add(new LoadChangeLine("Name", real.Name, record.Name));
            }
        }
        else if (named.Length > 0)
        {
            plan.Problems.Add(nameCell.Place.Problem(DemoHoldsName(record.Record, record.Name)));
        }
    }

    /// <summary>The empty workbook as it was sent, to tell what a column came with from what was typed into it.</summary>
    private readonly Lazy<LoadWorkbook?> _asSent = new(() => XlsxReader.Read(LoadTemplate.Bytes()).Book is { } book ? LoadWorkbook.Read(book, new LoadFields(forms)) : null);

    /// <summary>
    /// True when a column holds something the owner typed. What the workbook came with (a model's serial number format,
    /// the one battery category, the manufacturer's identifier) doesn't make a column without a name a record.
    /// </summary>
    private bool HasTyped(ValueSheet sheet, ValueColumn column)
    {
        var sent = _asSent.Value;
        var came = (sheet.Record switch
        {
            LoadRecord.Family => sent?.Family,
            LoadRecord.Model => sent?.Models,
            LoadRecord.Batch => sent?.Batches,
            LoadRecord.Site => sent?.Sites,
            _ => sent?.Operators,
        })?.Columns.FirstOrDefault(other => other.Number == column.Number);
        return column.Cells.Any(cell => came?.Cell(cell.Key)?.Text != cell.Value.Text);
    }

    public static string DemoHoldsName(LoadRecord record, string name) =>
        $"a demo {LoadFields.Words(record).ToLowerInvariant()} is already called {name}, and real product data can't share a name with demo data. Clear the demo data on the Clusters tab, or choose another name";

    // ---------- the family ----------

    private async Task PlanFamilyAsync(LoadWorkbook content, LoadPlan plan, SentFiles sent, CancellationToken cancellationToken)
    {
        var sheet = content.Family;
        if (sheet.Columns.FirstOrDefault() is not { } column)
        {
            return;
        }

        if (column.Cell("family|Name") is not { IsNone: false } nameCell)
        {
            // Said once; the models are still checked, as those of a family that isn't there yet.
            plan.Problems.Add(sheet.Place("family|Name", column).Problem("give the family's name: the models belong to it"));
            plan.Family = new RecordPlan(LoadRecord.Family, "(no name)", column.Header);
        }
        else
        {
            plan.Family = new RecordPlan(LoadRecord.Family, nameCell.Text, column.Header);
            Match(plan.Family, await ExistingAsync(LoadRecord.Family, cancellationToken), nameCell, plan);
        }

        var family = plan.Family;
        if (column.Cell("family|Picture") is { } picture)
        {
            var current = family.ExistingId is null ? null : await store.GetAsync<BatteryFamily>(family.ExistingId, cancellationToken);
            var stored = current?.PictureFileId is null ? null : await pages.FamilyPictureAsync(current.Id, cancellationToken);
            await PlanPictureAsync(family, picture, sent, plan, current?.PictureFileId, stored?.Content, canRemove: false, cancellationToken);
        }
    }

    // ---------- the models ----------

    /// <summary>One model column of the workbook: its heading, its name when it has one, and its record when it is there.</summary>
    private sealed record ModelColumn(ValueColumn Column, string? Name, RecordPlan? Plan);

    private async Task<IReadOnlyList<ModelColumn>> PlanModelsAsync(LoadWorkbook content, LoadPlan plan, SentFiles sent, CancellationToken cancellationToken)
    {
        var sheet = content.Models;
        var first = sheet.Columns.FirstOrDefault();
        var existing = plan.Family?.ExistingId is { } familyId ? await store.ListModelsAsync(familyId, cancellationToken) : [];
        var columns = new List<ModelColumn>();
        foreach (var column in sheet.Columns)
        {
            if (column.Cell("model|Name") is not { IsNone: false } nameCell)
            {
                if (HasTyped(sheet, column))
                {
                    plan.Problems.Add(sheet.Place("model|Name", column).Problem("this column has values but no model name. Give the model its name, or empty the column"));
                }

                columns.Add(new ModelColumn(column, null, null));
                continue;
            }

            if (columns.Any(other => other.Name is not null && SameName(other.Name, nameCell.Text)))
            {
                plan.Problems.Add(nameCell.Place.Problem($"two models are called {nameCell.Text}. Give each model its own name"));
                columns.Add(new ModelColumn(column, null, null));
                continue;
            }

            var model = new RecordPlan(LoadRecord.Model, nameCell.Text, column.Header);
            columns.Add(new ModelColumn(column, nameCell.Text, model));
            if (plan.Family is null)
            {
                continue; // the workbook has no Family sheet: said once, for that sheet
            }

            plan.Models.Add(model);
            var current = existing.FirstOrDefault(item => SameName(item.Name, model.Name));
            if (current is { IsDemo: true })
            {
                plan.Problems.Add(nameCell.Place.Problem(DemoHoldsName(LoadRecord.Model, model.Name)));
                current = null;
            }

            if (current is not null)
            {
                model.ExistingId = current.Id;
                model.Stored = current.Values;
                if (!string.Equals(current.Name, model.Name, StringComparison.Ordinal))
                {
                    model.RenameFrom = current.Name;
                    model.Lines.Add(new LoadChangeLine("Name", current.Name, model.Name));
                }
            }

            var cells = Effective(first!, column);
            await PlanOwnModelFieldsAsync(model, sheet, cells, current, plan, sent, cancellationToken);
            PlanValues(model, sheet, cells, plan);
        }

        // The hazardous substances and the documents name their model: a line goes to the model it names.
        foreach (var column in columns.Where(column => column.Plan is not null && plan.Models.Contains(column.Plan)))
        {
            PlanHazards(column.Plan!, content.Hazards.Where(line => line.Model is { } cell && (IsAll(cell.Text) || ReferenceEquals(Resolve(columns, cell.Text), column))), plan);
            var current = column.Plan!.ExistingId is null ? null : existing.FirstOrDefault(item => item.Id == column.Plan.ExistingId);
            await PlanDocumentsAsync(column.Plan!, LoadRecord.Model, content, columns, column, plan, sent, current?.ExampleDocumentIds, cancellationToken);
        }

        return columns;
    }

    private async Task PlanOwnModelFieldsAsync(RecordPlan model, ValueSheet sheet, IReadOnlyDictionary<string, FilledCell> cells, BatteryModel? current, LoadPlan plan, SentFiles sent, CancellationToken cancellationToken)
    {
        var stored = current?.Answers() ?? ModelAnswers.NotAnswered;
        var posted = ModelAnswers.NotAnswered;
        foreach (var field in LoadFields.Own.Where(field => field.Record == LoadRecord.Model))
        {
            if (!cells.TryGetValue(field.Key, out var cell))
            {
                continue;
            }

            switch (field.Kind)
            {
                case OwnFieldKind.NotLoadedYet:
                    model.Notes.Add(field.Note!);
                    break;
                case OwnFieldKind.Answer:
                    // Yes or No as the cell's list offers them; an answer is changed, never taken back.
                    if (cell.Text is not ("Yes" or "No"))
                    {
                        plan.Problems.Add(cell.Place.Problem($"{cell.Text} isn't one of the two answers. Choose Yes or No from the cell's list"));
                        break;
                    }

                    var answer = cell.Text == "Yes";
                    if (stored.Of(field.Question!.Value) != answer)
                    {
                        posted = posted.With(field.Question.Value, answer);
                        model.Lines.Add(new LoadChangeLine(sheet.RowName(field.Key), current is null ? string.Empty : ProductDataPages.Answer(stored.Of(field.Question.Value)), cell.Text));
                    }

                    break;
                case OwnFieldKind.ModuleDescription:
                    var wanted = cell.IsNone ? null : cell.Text;
                    if (wanted?.Length > ModuleList.MaxDescriptionLength)
                    {
                        plan.Problems.Add(cell.Place.Problem($"this text has {wanted.Length} characters; keep it to {ModuleList.MaxDescriptionLength}"));
                    }
                    else if (wanted != current?.ModuleDescription)
                    {
                        model.SetDescription = true;
                        model.Description = wanted;
                        model.Lines.Add(new LoadChangeLine(sheet.RowName(field.Key), current?.ModuleDescription ?? string.Empty, wanted ?? Gone));
                    }

                    break;
                case OwnFieldKind.Picture:
                    var picture = current?.PictureFileId is null ? null : await pages.ModelPictureAsync(current.Id, cancellationToken);
                    await PlanPictureAsync(model, cell, sent, plan, current?.PictureFileId, picture?.Content, canRemove: true, cancellationToken);
                    break;
            }
        }

        if (posted != ModelAnswers.NotAnswered)
        {
            model.Answers = posted;
        }

        model.AnswersAfter = stored;
        foreach (var question in ModelQuestions.All.Where(question => posted.Of(question) is not null))
        {
            model.AnswersAfter = model.AnswersAfter.With(question, posted.Of(question));
        }
    }

    /// <summary>What a later model's or batch's column says: its own cells, and where one is empty, the first column's.</summary>
    private static Dictionary<string, FilledCell> Effective(ValueColumn first, ValueColumn own)
    {
        var cells = new Dictionary<string, FilledCell>(own.Cells, StringComparer.Ordinal);
        if (!ReferenceEquals(first, own))
        {
            foreach (var (key, cell) in first.Cells)
            {
                // A model's name and its serial number format are its own: they are never "the same as the first model's".
                if (!cells.ContainsKey(key) && LoadFields.OwnField(key) is not { Kind: OwnFieldKind.Name or OwnFieldKind.NotLoadedYet })
                {
                    cells[key] = cell;
                }
            }
        }

        return cells;
    }

    private static bool IsAll(string text) => string.Equals(text.Trim(), "all", StringComparison.OrdinalIgnoreCase);

    /// <summary>The model a line names: by the model's name, or by the heading of its column on the Models sheet.</summary>
    private static ModelColumn? Resolve(IReadOnlyList<ModelColumn> columns, string text) =>
        columns.FirstOrDefault(column => column.Name is not null && SameName(column.Name, text))
        ?? columns.FirstOrDefault(column => SameName(column.Column.Header, text));

    private static void UnknownModels(LoadWorkbook content, LoadPlan plan, IReadOnlyList<ModelColumn> columns)
    {
        void Judge(string model, CellPlace place, bool allAllowed)
        {
            if (allAllowed && IsAll(model))
            {
                return;
            }

            if (Resolve(columns, model) is not { } column)
            {
                plan.Problems.Add(place.Problem($"the sheet {LoadFields.ModelsSheet} has no model called {model}. Write a model's name as it is on that sheet{(allAllowed ? ", or all" : string.Empty)}"));
            }
            else if (column.Plan is null)
            {
                plan.Problems.Add(place.Problem($"the model {column.Column.Header} has no name on the sheet {LoadFields.ModelsSheet}, so this line has no model to go to"));
            }
        }

        foreach (var line in content.Hazards)
        {
            if (line.Model is { } model)
            {
                Judge(model.Text, model.Place, allAllowed: true);
            }
            else
            {
                plan.Problems.Add(line.Place(LoadWorkbook.ModelHeader).Problem("say which model this substance is in: a model's name, or all"));
            }
        }

        foreach (var line in content.Documents.Where(line => line.Link is not null || line.Title is not null || line.Language is not null))
        {
            Judge(line.Model, line.Place(LoadWorkbook.ModelHeader), allAllowed: false);
        }
    }

    // ---------- the batches ----------

    private async Task PlanBatchesAsync(LoadWorkbook content, LoadPlan plan, IReadOnlyList<ModelColumn> models, SentFiles sent, CancellationToken cancellationToken)
    {
        var sheet = content.Batches;
        var first = sheet.Columns.FirstOrDefault();
        var realSites = (await ExistingAsync(LoadRecord.Site, cancellationToken)).Where(site => !site.IsDemo).ToArray();
        foreach (var column in sheet.Columns)
        {
            var model = models.FirstOrDefault(item => item.Column.Number == column.Number);
            var documentsOfBatch = model is null ? [] : DocumentLinesOf(content, models, model, LoadRecord.Batch).Where(line => line.Link is not null || line.Title is not null || line.Language is not null).ToArray();
            if (model?.Plan is null || !plan.Models.Contains(model.Plan))
            {
                if (column.Cells.Count > 0 && model?.Plan is null)
                {
                    plan.Problems.Add(new LoadProblem(sheet.Name, string.Empty, column.Header, $"this batch has values, but its model has no name on the sheet {LoadFields.ModelsSheet}. Give the model its name, or empty the column"));
                }

                continue;
            }

            var cells = Effective(first!, column);
            if (cells.Count == 0)
            {
                foreach (var line in documentsOfBatch)
                {
                    plan.Problems.Add(line.Place(LoadWorkbook.LinkHeader).Problem($"this document belongs to the batch, and the sheet {LoadFields.BatchesSheet} gives no production year and site for it"));
                }

                continue; // no batch for this model in this workbook
            }

            var year = BatchYear(sheet, column, cells, plan);
            var (site, existingSiteId, siteName) = BatchSite(sheet, column, cells, plan, realSites);
            if (year is null || siteName is null)
            {
                continue;
            }

            var batch = new RecordPlan(LoadRecord.Batch, $"{model.Plan.Name}, batch {year.Value.ToString(CultureInfo.InvariantCulture)} · {siteName}", column.Header)
            {
                Model = model.Plan,
                Site = site,
                ExistingSiteId = existingSiteId,
                Year = year.Value,
            };
            var siteId = site?.ExistingId ?? existingSiteId;
            var current = model.Plan.ExistingId is { } modelId && siteId is not null
                ? (await store.ListBatchesAsync(modelId, cancellationToken)).FirstOrDefault(item => !item.IsDemo && item.SiteId == siteId && item.Year == year.Value)
                : null;
            if (current is not null)
            {
                batch.ExistingId = current.Id;
                batch.Stored = current.Values;
            }

            plan.Batches.Add(batch);
            PlanValues(batch, sheet, cells, plan);
            await PlanDocumentsAsync(batch, LoadRecord.Batch, content, models, model, plan, sent, current?.ExampleDocumentIds, cancellationToken);
        }
    }

    private static int? BatchYear(ValueSheet sheet, ValueColumn column, IReadOnlyDictionary<string, FilledCell> cells, LoadPlan plan)
    {
        if (!cells.TryGetValue("batch|Year", out var cell) || cell.IsNone)
        {
            plan.Problems.Add(sheet.Place("batch|Year", column).Problem("give the production year: a batch is one model, made at one site, in one year"));
            return null;
        }

        if (!int.TryParse(cell.Text, NumberStyles.None, CultureInfo.InvariantCulture, out var year))
        {
            plan.Problems.Add(cell.Place.Problem($"{cell.Text} is not a year; type the year only, for example 2026"));
            return null;
        }

        if (ProductDataPages.BatchYearProblem(year) is { } problem)
        {
            plan.Problems.Add(cell.Place.Problem($"{cell.Text}: {problem}"));
            return null;
        }

        return year;
    }

    private static (RecordPlan? Site, string? ExistingSiteId, string? Name) BatchSite(ValueSheet sheet, ValueColumn column, IReadOnlyDictionary<string, FilledCell> cells, LoadPlan plan, IReadOnlyList<Existing> realSites)
    {
        if (!cells.TryGetValue("batch|Site", out var cell) || cell.IsNone)
        {
            plan.Problems.Add(sheet.Place("batch|Site", column).Problem($"give the manufacturing site: its name from the sheet {LoadFields.SiteSheet}"));
            return (null, null, null);
        }

        if ((plan.Sites.FirstOrDefault(site => SameName(site.Name, cell.Text)) ?? plan.Sites.FirstOrDefault(site => SameName(site.Header, cell.Text))) is { } inWorkbook)
        {
            return (inWorkbook, null, inWorkbook.Name);
        }

        if (realSites.FirstOrDefault(site => SameName(site.Name, cell.Text)) is { } inApp)
        {
            return (null, inApp.Id, inApp.Name);
        }

        plan.Problems.Add(cell.Place.Problem($"there is no manufacturing site called {cell.Text}, on the sheet {LoadFields.SiteSheet} or in the app. Write the site's name as it is on the sheet {LoadFields.SiteSheet}"));
        return (null, null, null);
    }

    // ---------- passport values ----------

    private const string Gone = "(removed)";

    /// <summary>
    /// A record's passport values from its cells. Each value starts from what the app has, takes the filled cells over
    /// it and loses the parts marked <c>none</c>, exactly as if the team had typed them into the value's form: an empty
    /// cell changes nothing, in a value of several parts too.
    /// </summary>
    private void PlanValues(RecordPlan record, ValueSheet sheet, IReadOnlyDictionary<string, FilledCell> cells, LoadPlan plan)
    {
        foreach (var (key, cell) in cells)
        {
            if (_fields.Fixed.GetValueOrDefault(key) is { } only && !cell.IsNone && !string.Equals(cell.Text, only.Constant, StringComparison.OrdinalIgnoreCase))
            {
                plan.Problems.Add(cell.Place.Problem($"the app issues one kind only here: {only.Constant}. Leave the cell as it was"));
            }
        }

        var byValue = cells
            .Where(entry => _fields.Values.ContainsKey(entry.Key))
            .Select(entry => (Field: _fields.Values[entry.Key], Cell: entry.Value))
            .GroupBy(entry => entry.Field.Attribute.Property, StringComparer.Ordinal);
        foreach (var group in byValue.OrderBy(group => group.First().Field.Attribute.Number))
        {
            var form = group.First().Field.Form;
            var stored = record.Stored.GetValueOrDefault(group.Key)?.Value;
            var texts = new Dictionary<string, string>(FormValues.Show(form, stored), StringComparer.Ordinal);
            var refused = false;
            foreach (var (field, cell) in group)
            {
                var name = form.FieldName(field.Input);
                if (cell.IsNone)
                {
                    texts[name] = string.Empty;
                    continue;
                }

                if (Typed(field.Input, cell, plan) is not { } text)
                {
                    refused = true;
                    continue;
                }

                texts[name] = text;
            }

            if (refused)
            {
                continue;
            }

            record.Cells[group.Key] = group.Select(entry => (entry.Field.Input.Path, entry.Cell)).ToList();
            var value = FormValues.Read(form, name => texts.GetValueOrDefault(name));
            if (value is null)
            {
                if (stored is not null)
                {
                    record.Removals.Add(group.Key);
                    AddLines(record, sheet, form, group.Select(entry => entry.Field), FormValues.Show(form, stored), new Dictionary<string, string>());
                }

                continue;
            }

            if (FormValues.SameValue(stored, value))
            {
                continue;
            }

            record.Changed[group.Key] = value;
            AddLines(record, sheet, form, group.Select(entry => entry.Field), FormValues.Show(form, stored), FormValues.Show(form, value));
        }
    }

    private static void AddLines(RecordPlan record, ValueSheet sheet, AttributeForm form, IEnumerable<ValueField> given, IReadOnlyDictionary<string, string> before, IReadOnlyDictionary<string, string> after)
    {
        foreach (var field in given)
        {
            var name = form.FieldName(field.Input);
            var old = OneLine(before.GetValueOrDefault(name) ?? string.Empty);
            var now = OneLine(after.GetValueOrDefault(name) ?? string.Empty);
            if (old != now)
            {
                // As it will be stored, with the unit the app writes beside it.
                string WithUnit(string text) => text.Length > 0 && field.Input.Unit is { Length: > 0 } unit ? $"{text} {unit}" : text;
                record.Lines.Add(new LoadChangeLine(sheet.RowName(field.Key), WithUnit(old), now.Length == 0 ? Gone : WithUnit(now)));
            }
        }
    }

    /// <summary>A list shows one entry per line in a form; on the check's one line they are set apart as in the workbook.</summary>
    private static string OneLine(string text) => string.Join("; ", text.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));

    /// <summary>
    /// A cell as the form's input would get it typed; null, with the problem noted, when the cell can't be that.
    /// The loader's own reading rules (a call of the package, see its report): a number is a number cell or digits with
    /// a point, so that 4,7 or 4.700 is never guessed at; a list's entries are set apart with a semicolon.
    /// </summary>
    private static string? Typed(FormInput input, FilledCell cell, LoadPlan plan)
    {
        if (ExampleValues.IsExampleText(cell.Text))
        {
            plan.Problems.Add(cell.Place.Problem($"{Clip(cell.Text)} is still the demo's example. Type your own value"));
        }

        switch (input.Kind)
        {
            case InputKind.Integer or InputKind.Number when !cell.IsNumber && !PlainNumber().IsMatch(cell.Text):
                plan.Problems.Add(cell.Place.Problem($"{Clip(cell.Text)} is not a number; type the number only{(cell.Text.Contains(',', StringComparison.Ordinal) ? ", with a point for decimals" : string.Empty)}"));
                return null;
            case InputKind.Lines:
                return string.Join("\n", cell.Text.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
            default:
                return cell.Text;
        }
    }

    private static string Clip(string text) => text.Length <= 60 ? text : text[..57] + "...";

    // ---------- hazardous substances ----------

    private void PlanHazards(RecordPlan model, IEnumerable<HazardLine> lines, LoadPlan plan)
    {
        var form = _fields.Hazards;
        var rows = lines.ToArray();
        if (rows.Length == 0)
        {
            return; // no line names this model: its list stays as it is
        }

        var texts = new Dictionary<string, string>(StringComparer.Ordinal) { [form.RowCountField] = rows.Length.ToString(CultureInfo.InvariantCulture) };
        var refused = false;
        for (var row = 0; row < rows.Length; row++)
        {
            foreach (var input in form.Inputs)
            {
                if (!rows[row].Cells.TryGetValue(input.Path, out var cell) || cell.IsNone)
                {
                    continue;
                }

                if (Typed(input, cell, plan) is { } text)
                {
                    texts[form.FieldName(input, row)] = text;
                }
                else
                {
                    refused = true;
                }
            }
        }

        if (refused)
        {
            return;
        }

        model.HazardLines.AddRange(rows);
        var stored = model.Stored.GetValueOrDefault(LoadFields.HazardProperty)?.Value;
        var value = FormValues.Read(form, name => texts.GetValueOrDefault(name));
        if (value is null || FormValues.SameValue(stored, value))
        {
            return;
        }

        model.Changed[LoadFields.HazardProperty] = value;
        model.Lines.Add(new LoadChangeLine(form.Attribute.Name, Substances(stored), Substances(value)));
    }

    /// <summary>The substances as they will be stored: "Cobalt sulphate (CAS No 7646-79-9, 0.5)".</summary>
    private static string Substances(JsonNode? value) => value is JsonArray rows
        ? string.Join("; ", rows.Select(row =>
        {
            string Part(string name) => row?[name] is JsonValue part ? (part.GetValueKind() == JsonValueKind.String ? part.GetValue<string>() : part.ToJsonString()) : string.Empty;
            var name = Part("chemicalName") is { Length: > 0 } chemical ? chemical : "(no name)";
            var details = string.Join(", ", new[] { $"{Part("identifierType")} {Part("identifier")}".Trim(), Part("concentration") }.Where(text => text.Length > 0));
            return details.Length > 0 ? $"{name} ({details})" : name;
        }))
        : string.Empty;

    // ---------- documents and pictures ----------

    /// <summary>The lines of the documents sheet for one model (its own documents, or its batch's).</summary>
    private static IEnumerable<DocumentLine> DocumentLinesOf(LoadWorkbook content, IReadOnlyList<ModelColumn> columns, ModelColumn model, LoadRecord record) =>
        content.Documents.Where(line => line.Field.Record == record && ReferenceEquals(Resolve(columns, line.Model), model));

    private async Task PlanDocumentsAsync(RecordPlan record, LoadRecord of, LoadWorkbook content, IReadOnlyList<ModelColumn> columns, ModelColumn model, LoadPlan plan, SentFiles sent, IReadOnlyCollection<string>? exampleDocumentIds, CancellationToken cancellationToken)
    {
        var own = DocumentLinesOf(content, columns, model, of).ToDictionary(line => line.Field.Attribute.Property, StringComparer.Ordinal);
        var first = columns.Count > 0 && !ReferenceEquals(columns[0], model)
            ? DocumentLinesOf(content, columns, columns[0], of).ToDictionary(line => line.Field.Attribute.Property, StringComparer.Ordinal)
            : [];
        foreach (var field in _fields.Documents.Values.Where(field => field.Record == of).OrderBy(field => field.Attribute.Number))
        {
            // An empty cell of a later model's line means the same as the first model's.
            var mine = own.GetValueOrDefault(field.Attribute.Property);
            var theirs = first.GetValueOrDefault(field.Attribute.Property);
            var (link, title, language) = (mine?.Link ?? theirs?.Link, mine?.Title ?? theirs?.Title, mine?.Language ?? theirs?.Language);
            if (link is null && title is null && language is null)
            {
                continue;
            }

            // What a later model inherits is the first model's line: a problem there is said once, at that line.
            var line = mine is not null && (mine.Link ?? mine.Title ?? mine.Language) is not null ? mine : theirs ?? mine!;
            record.Cells[field.Attribute.Property] = new[] { ("url", link), ("resourceTitle", title), ("language", language) }.Where(cell => cell.Item2 is not null).Select(cell => (cell.Item1, cell.Item2!)).ToList();
            foreach (var cell in new[] { title, link }.Where(cell => cell is { IsNone: false } && ExampleValues.IsExampleText(cell.Text)))
            {
                plan.Problems.Add(cell!.Place.Problem($"{Clip(cell.Text)} is still the demo's example. Type your own"));
            }

            if (field.IsFile)
            {
                await PlanFileAsync(record, field, line, link, title, language, plan, sent, exampleDocumentIds, cancellationToken);
            }
            else
            {
                PlanLink(record, field, line, link, title, language, plan);
            }
        }
    }

    /// <summary>A public document: a web link with its title, language and a content type taken from the link's ending.</summary>
    private static void PlanLink(RecordPlan record, DocumentField field, DocumentLine line, FilledCell? link, FilledCell? title, FilledCell? language, LoadPlan plan)
    {
        var form = field.Form;
        var property = field.Attribute.Property;
        var stored = record.Stored.GetValueOrDefault(property)?.Value;
        var texts = new Dictionary<string, string>(FormValues.Show(form, stored), StringComparer.Ordinal);
        string Name(string path) => form.FieldName(form.Inputs.First(input => input.Path == path));
        if (link is { IsNone: true })
        {
            if (stored is not null)
            {
                record.Removals.Add(property);
                record.Documents.Add(new LoadDocumentView(line.DocumentName, texts.GetValueOrDefault(Name("url")) ?? string.Empty, LoadStates.Removed));
            }

            return;
        }

        if (link is not null)
        {
            if (!WebAddress.IsWebAddress(link.Text))
            {
                plan.Problems.Add(link.Place.Problem($"{Clip(link.Text)} is not a web link. The passport keeps only the address, so the document must be on a public page that starts with https://"));
                return;
            }

            if (!string.Equals(texts.GetValueOrDefault(Name("url")), link.Text, StringComparison.Ordinal) || string.IsNullOrWhiteSpace(texts.GetValueOrDefault(Name("contentType"))))
            {
                texts[Name("contentType")] = LinkContentType(link.Text);
            }

            texts[Name("url")] = link.Text;
        }
        else if (stored is null)
        {
            Pending(plan, record, field, line, "no web link is written yet", hasOne: false);
            return;
        }

        if (title is not null)
        {
            texts[Name("resourceTitle")] = title.IsNone ? string.Empty : title.Text;
        }

        if (language is not null)
        {
            texts[Name("language")] = language.IsNone ? string.Empty : language.Text;
        }

        var value = FormValues.Read(form, name => texts.GetValueOrDefault(name));
        if (value is null)
        {
            return;
        }

        var same = FormValues.SameValue(stored, value);
        if (!same)
        {
            record.Changed[property] = value;
        }

        record.Documents.Add(new LoadDocumentView(line.DocumentName, LinkWords(value), same ? LoadStates.Same : stored is null ? LoadStates.New : LoadStates.Changed));
    }

    private static string LinkWords(JsonNode value)
    {
        string? Text(string name) => value[name] is JsonValue text && text.TryGetValue<string>(out var words) ? words : null;
        return string.Join(" · ", new[]
        {
            $"Web link {Text("url")}",
            $"shown as {TypeWords(Text("contentType"))}",
            Text("resourceTitle") is { Length: > 0 } title ? $"title \"{title}\"" : "no title",
            Text("language") is { Length: > 0 } language ? LanguageNames.Of(language) : "no language",
        });
    }

    /// <summary>
    /// A link's content type: from its ending when that is a known one, else a web page. The check shows which.
    /// </summary>
    public static string LinkContentType(string link)
    {
        var path = Uri.TryCreate(link, UriKind.Absolute, out var address) ? address.AbsolutePath : link;
        return Path.GetExtension(path).ToLowerInvariant() switch
        {
            ".pdf" => FileTypes.Pdf,
            ".png" => FileTypes.Png,
            ".jpg" or ".jpeg" => FileTypes.Jpeg,
            ".svg" => "image/svg+xml",
            _ => "text/html",
        };
    }

    public static string TypeWords(string? contentType) => contentType switch
    {
        FileTypes.Pdf => "a PDF",
        FileTypes.Png => "a PNG picture",
        FileTypes.Jpeg => "a JPEG picture",
        "image/svg+xml" => "an SVG picture",
        "text/html" => "a web page",
        null or "" => "a document",
        _ => contentType,
    };

    /// <summary>
    /// A document uploaded to the app: the file is sent with the workbook and named in its cell. A file that is already
    /// the record's document, with the same title and language, is left alone, so a second load uploads nothing.
    /// </summary>
    private async Task PlanFileAsync(RecordPlan record, DocumentField field, DocumentLine line, FilledCell? link, FilledCell? title, FilledCell? language, LoadPlan plan, SentFiles sent, IReadOnlyCollection<string>? exampleDocumentIds, CancellationToken cancellationToken)
    {
        var property = field.Attribute.Property;
        var current = record.ExistingId is null ? null : await LinkedDocumentAsync(record, property, cancellationToken);
        if (link is null)
        {
            Pending(plan, record, field, line, "no file is named yet", current is not null);
            return;
        }

        if (link.IsNone)
        {
            if (current is not null)
            {
                plan.Problems.Add(link.Place.Problem("a document that is in the app can't be taken away with none. Send the file that replaces it, or leave the cell empty"));
            }

            return;
        }

        if (WebAddress.IsWebAddress(link.Text))
        {
            plan.Problems.Add(link.Place.Problem("this document is a file you send, not a web link: only the people it is for may open it. Write the file's name and send the file with the workbook"));
            return;
        }

        var wantedTitle = title is { IsNone: false } ? title.Text : null;
        var wantedLanguage = language is { IsNone: false } ? language.Text : null;
        var upload = new DocumentUpload(Path.GetFileName(link.Text), [], wantedTitle, wantedLanguage);
        var sameWords = current is not null
            && string.Equals(current.Title, DocumentService.TitleOf(upload), StringComparison.Ordinal)
            && string.Equals(current.Language ?? string.Empty, wantedLanguage ?? string.Empty, StringComparison.Ordinal);
        var isExample = current is not null && (current.IsExample || exampleDocumentIds?.Contains(current.Id) == true);
        if (sent.Take(link.Text) is not { } file)
        {
            // Not sent this time: fine when it is the record's document already.
            if (sameWords && !isExample && string.Equals(current!.FileName, upload.FileName, StringComparison.OrdinalIgnoreCase))
            {
                record.Documents.Add(new LoadDocumentView(line.DocumentName, $"File {current.FileName}, already in the app", LoadStates.Same));
            }
            else
            {
                // Not a problem: the owner won't have every document on the first day. Everything else is loaded.
                Pending(
                    plan,
                    record,
                    field,
                    line,
                    current is not null && string.Equals(current.FileName, upload.FileName, StringComparison.OrdinalIgnoreCase)
                        ? $"the file {upload.FileName} wasn't sent, so its new title or language isn't saved yet"
                        : $"the file {upload.FileName} wasn't sent",
                    current is not null);
            }

            return;
        }

        upload = upload with { Content = file.Content };
        var (contentType, problem) = documentService.Check(upload);
        if (problem is not null)
        {
            plan.Problems.Add(link.Place.Problem($"{upload.FileName}: {problem}"));
            return;
        }

        var detail = string.Join(" · ", new[]
        {
            $"File {upload.FileName} ({TypeWords(contentType)}, {Size(file.Content.LongLength)})",
            $"title \"{DocumentService.TitleOf(upload)}\"",
            wantedLanguage is null ? "no language" : LanguageNames.Of(wantedLanguage),
        });
        if (sameWords && !isExample && string.Equals(current!.Sha256, Sha256(file.Content), StringComparison.OrdinalIgnoreCase))
        {
            record.Documents.Add(new LoadDocumentView(line.DocumentName, detail, LoadStates.Same));
            return;
        }

        record.Uploads.Add(new UploadPlan(field, file, wantedTitle, wantedLanguage));
        record.Documents.Add(new LoadDocumentView(line.DocumentName, detail, current is null ? LoadStates.New : LoadStates.Changed));
    }

    /// <summary>Notes a document that is still to come, with what that means for a passport (<see cref="PassportNeeds"/>).</summary>
    private static void Pending(LoadPlan plan, RecordPlan record, DocumentField field, DocumentLine line, string missing, bool hasOne)
    {
        var meaning = hasOne ? "The document that is in the app stays until then."
            : PassportNeeds.NeedOf(field.Attribute, record.AnswersAfter) switch
            {
                Need.Required => "The passport of a real battery can't be published until it is there.",
                Need.Unanswered => "A passport may need it: that depends on the model's three answers.",
                _ => "No passport waits for it.",
            };
        plan.Pending.Add(new LoadPending(record.Words, line.DocumentName, missing, meaning));
    }

    /// <summary>The uploaded document a record's value links now; null when it links none.</summary>
    private async Task<DppDocument?> LinkedDocumentAsync(RecordPlan record, string property, CancellationToken cancellationToken)
    {
        var url = record.Stored.GetValueOrDefault(property)?.Value is JsonObject link && link["url"] is JsonValue address && address.TryGetValue<string>(out var text) ? text : null;
        const string marker = "/dpp/documents/";
        var at = url?.LastIndexOf(marker, StringComparison.Ordinal) ?? -1;
        return at < 0 ? null : await documents.GetAsync(Uri.UnescapeDataString(url![(at + marker.Length)..]), cancellationToken);
    }

    /// <summary>A family's or a model's picture, named in its cell and sent with the workbook.</summary>
    private async Task PlanPictureAsync(RecordPlan record, FilledCell cell, SentFiles sent, LoadPlan plan, string? storedFileId, byte[]? storedContent, bool canRemove, CancellationToken cancellationToken)
    {
        const string name = "Picture";
        if (cell.IsNone)
        {
            if (!canRemove)
            {
                plan.Problems.Add(cell.Place.Problem("a family's picture can't be taken away here. Leave the cell empty, or send another picture"));
            }
            else if (storedFileId is not null)
            {
                record.RemovePicture = true;
                record.Documents.Add(new LoadDocumentView(name, "The model shows its family's picture again", LoadStates.Removed));
            }

            return;
        }

        var fileName = Path.GetFileName(cell.Text);
        if (sent.Take(cell.Text) is not { } file)
        {
            if (storedFileId is not null && string.Equals(await pages.PictureFileNameAsync(storedFileId, cancellationToken), fileName, StringComparison.OrdinalIgnoreCase))
            {
                record.Documents.Add(new LoadDocumentView(name, $"File {fileName}, already in the app", LoadStates.Same));
            }
            else
            {
                plan.Pending.Add(new LoadPending(record.Words, name, $"the picture {fileName} wasn't sent", storedFileId is not null
                    ? "The picture that is in the app stays until then."
                    : "No passport waits for a picture: the public page shows none of its own until then."));
            }

            return;
        }

        if (pages.PictureProblem(file.Content) is { } problem)
        {
            plan.Problems.Add(cell.Place.Problem($"{fileName}: {problem}"));
            return;
        }

        var detail = $"File {fileName} ({TypeWords(FileTypes.Detect(file.Content))}, {Size(file.Content.LongLength)})";
        if (storedContent is not null && storedContent.AsSpan().SequenceEqual(file.Content))
        {
            record.Documents.Add(new LoadDocumentView(name, detail, LoadStates.Same));
            return;
        }

        record.Picture = file;
        record.Documents.Add(new LoadDocumentView(name, detail, storedFileId is null ? LoadStates.New : LoadStates.Changed));
    }

    private static string Size(long bytes) => bytes < 1024 * 1024
        ? $"{Math.Max(1, bytes / 1024).ToString(CultureInfo.InvariantCulture)} KB"
        : $"{(bytes / 1024d / 1024d).ToString("0.#", CultureInfo.InvariantCulture)} MB";

    private static string Sha256(byte[] content) => Convert.ToHexStringLower(SHA256.HashData(content));

    // ---------- what the forms would refuse ----------

    /// <summary>
    /// Judges a record's values with the save pipeline, nothing saved: a new record's with its evaluation, a record
    /// that is there with a preview of the save, which also knows what a published passport needs.
    /// </summary>
    private async Task ValidateAsync(RecordPlan record, LoadPlan plan, CancellationToken cancellationToken)
    {
        if (record.Record == LoadRecord.Family || !record.ChangesPassportData)
        {
            return;
        }

        var kind = LoadFields.KindOf(record.Record);
        var values = record.Changed.ToDictionary(entry => entry.Key, entry => (JsonNode?)entry.Value.DeepClone(), StringComparer.Ordinal);
        if (record.Uploads.Count > 0 && !links.IsConfigured)
        {
            plan.Problems.Add(new LoadProblem(string.Empty, string.Empty, string.Empty, "the app's own address (APP_BASE_URL) isn't set, so it can't keep uploaded documents. Ask whoever runs the app to set it"));
            return;
        }

        foreach (var upload in record.Uploads)
        {
            // The link an upload would save, to a document that isn't there yet: its title and language are judged with it.
            var link = new JsonObject
            {
                ["contentType"] = FileTypes.Detect(upload.File.Content),
                ["url"] = links.DocumentUrl("doc-check"),
                ["resourceTitle"] = DocumentService.TitleOf(new DocumentUpload(upload.File.FileName, [], upload.Title)),
            };
            if (!string.IsNullOrWhiteSpace(upload.Language))
            {
                link["language"] = upload.Language;
            }

            values[upload.Field.Attribute.Property] = link;
        }

        IReadOnlyList<KeyResult> results;
        if (record.ExistingId is null)
        {
            results = pipeline.Evaluate(Checker, kind, null, values).Results;
        }
        else
        {
            var preview = await pipeline.SaveAsync(Checker, new ChangeSet(kind, record.ExistingId, values, "form", null, record.Removals.ToArray()), preview: true, cancellationToken);
            results = preview.Keys;
        }

        foreach (var result in results.Where(result => result.Outcome is not (KeyOutcome.Saved or KeyOutcome.Removed)))
        {
            Refused(record, result, plan);
        }
    }

    /// <summary>A refused value as problem lines, each at the cell it is about.</summary>
    private void Refused(RecordPlan record, KeyResult result, LoadPlan plan)
    {
        if (AttributeCatalog.FindByProperty(result.Key) is not { } attribute)
        {
            return;
        }

        var form = forms.For(attribute);
        var cells = record.Cells.GetValueOrDefault(result.Key) ?? [];
        var fallback = cells.Count > 0 ? cells[0].Cell.Place : new CellPlace(LoadFields.SheetOf(record.Record), attribute.Name, record.Header);
        var any = false;
        foreach (var issue in result.Message.Split("; ", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var words = RefusalWording.Describe(form, issue);
            if (words.Length == 0)
            {
                continue;
            }

            any = true;
            var paths = PathsOf(form, issue);
            if (result.Key == LoadFields.HazardProperty && RowOf(issue) is { } row && row < record.HazardLines.Count)
            {
                var line = record.HazardLines[row];
                var input = form.Inputs.FirstOrDefault(input => paths.Contains(input.Path));
                var cell = input is null ? null : line.Cells.GetValueOrDefault(input.Path);
                plan.Problems.Add(line.Place(input?.Label ?? string.Empty).Problem(Said(RowPrefix().Replace(words, string.Empty), cell)));
                continue;
            }

            var at = cells.FirstOrDefault(cell => paths.Contains(cell.Path)).Cell;
            plan.Problems.Add((at?.Place ?? fallback).Problem(Said(words, at ?? (cells.Count == 1 ? cells[0].Cell : null))));
        }

        if (!any)
        {
            plan.Problems.Add(fallback.Problem(Said(result.Message.Length > 0 ? result.Message : "this value wasn't accepted", null)));
        }
    }

    /// <summary>The parts of a value a refusal names: in its location ("/voltValue type: …") and in a list of missing parts.</summary>
    private static HashSet<string> PathsOf(AttributeForm form, string issue)
    {
        var paths = new HashSet<string>(RefusalWording.Parts(form, issue), StringComparer.Ordinal);
        var location = issue.StartsWith('/') ? issue.Split(' ', 2)[0] : string.Empty;
        foreach (var segment in location.Split('/', StringSplitOptions.RemoveEmptyEntries))
        {
            paths.Add(segment);
        }

        if (issue.Contains("required:", StringComparison.Ordinal))
        {
            paths.UnionWith(Quoted().Matches(issue).Select(match => match.Groups[1].Value));
        }

        return paths;
    }

    /// <summary>The row of a list a refusal is about ("/2/concentration …" is the third row); null when it names none.</summary>
    private static int? RowOf(string issue) =>
        issue.StartsWith('/') && int.TryParse(issue.Split(' ', 2)[0].Split('/', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault(), NumberStyles.None, CultureInfo.InvariantCulture, out var row) ? row : null;

    /// <summary>A refusal in a problem line's own form: what was typed first, small first letter, no full stop.</summary>
    private static string Said(string words, FilledCell? cell)
    {
        var text = words.Trim().TrimEnd('.');
        if (text.Length > 1 && char.IsUpper(text[0]) && char.IsLower(text[1]) && (text.StartsWith("Must ", StringComparison.Ordinal) || text.StartsWith("Needs ", StringComparison.Ordinal) || text.StartsWith("Was ", StringComparison.Ordinal)))
        {
            text = char.ToLowerInvariant(text[0]) + text[1..];
            return cell is null ? $"the value {text}" : $"{Clip(cell.Text)} {text}";
        }

        return text;
    }

    // ---------- published passports ----------

    private async Task PublishedAsync(LoadPlan plan, CancellationToken cancellationToken)
    {
        var reached = new HashSet<string>(StringComparer.Ordinal);
        foreach (var record in plan.All.Where(record => record.ExistingId is not null && record.Record != LoadRecord.Family))
        {
            if (record.ChangesPassportData)
            {
                reached.UnionWith((await editor.AffectedAsync(LoadFields.KindOf(record.Record), record.ExistingId!, cancellationToken)).Select(passport => passport.PackId));
            }

            if (record.Answers is { } answers && await store.GetAsync<BatteryModel>(record.ExistingId!, cancellationToken) is { } model)
            {
                var (_, lacking) = await pages.PreviewModelAnswersAsync(model, answers, cancellationToken);
                if (lacking > 0)
                {
                    plan.Notes.Add($"{Words.Count(lacking, "published battery", "published batteries")} of {record.Name} {(lacking == 1 ? "lacks" : "lack")} a value the new answers ask for, so {(lacking == 1 ? "it" : "they")} can't get a new version until it is filled in. Loading says yes to that.");
                }
            }
        }

        plan.PublishedPassports = reached.Count;
        if (reached.Count > 0)
        {
            plan.Notes.Insert(0, $"Loading publishes new versions of {Words.Count(reached.Count, "passport")}: the published {(reached.Count == 1 ? "passport" : "passports")} built from the records that change.");
        }
    }

    // ---------- the check as it is shown ----------

    private static LoadCheck Unread(string name, byte[] workbook, IReadOnlyList<SentFile> files, string problem) =>
        new(name, problem, [], [], [], 0, Stamp(workbook, files, [], [], [problem], []));

    private static LoadCheck Finish(string name, byte[] workbook, IReadOnlyList<SentFile> files, LoadPlan plan)
    {
        var records = plan.All.Select(record => record.View()).ToArray();
        var problems = plan.Problems.Distinct().ToArray();
        var pending = plan.Pending.Distinct().ToArray();
        return new LoadCheck(name, null, records, problems, plan.Notes.ToArray(), plan.PublishedPassports, Stamp(workbook, files, records, problems, plan.Notes, pending)) { Pending = pending };
    }

    /// <summary>A fingerprint of the files and of everything the check says, so that a load can tell it is the same.</summary>
    private static string Stamp(byte[] workbook, IReadOnlyList<SentFile> files, IReadOnlyList<LoadRecordView> records, IReadOnlyList<LoadProblem> problems, IReadOnlyList<string> notes, IReadOnlyList<LoadPending> pending)
    {
        var text = JsonSerializer.Serialize(new
        {
            workbook = Sha256(workbook),
            files = files.Select(file => $"{Path.GetFileName(file.FileName).ToUpperInvariant()}:{Sha256(file.Content)}").Order(StringComparer.Ordinal).ToArray(),
            records,
            problems,
            notes,
            pending,
        });
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(text)))[..32];
    }

    private static bool SameName(string first, string second) =>
        string.Equals(first.Trim(), second.Trim(), StringComparison.OrdinalIgnoreCase);

    [GeneratedRegex(@"^[+-]?\d+(\.\d+)?$", RegexOptions.CultureInvariant)]
    private static partial Regex PlainNumber();

    [GeneratedRegex("\"(\\w+)\"")]
    private static partial Regex Quoted();

    [GeneratedRegex(@"^Row \d+[,:]?\s*", RegexOptions.CultureInvariant)]
    private static partial Regex RowPrefix();

    /// <summary>The files sent with a workbook, by name.</summary>
    private sealed class SentFiles
    {
        private readonly Dictionary<string, SentFile> _byName = new(StringComparer.OrdinalIgnoreCase);

        public SentFiles(IReadOnlyList<SentFile> files, List<LoadProblem> problems)
        {
            if (files.Count > LoadLimits.MaxFiles)
            {
                problems.Add(new LoadProblem(string.Empty, string.Empty, string.Empty, $"{files.Count} files were sent; {LoadLimits.TooMany}"));
            }

            foreach (var file in files)
            {
                var name = Path.GetFileName(file.FileName ?? string.Empty).Trim();
                if (name.Length > 0 && !_byName.TryAdd(name, file with { FileName = name }))
                {
                    problems.Add(new LoadProblem(string.Empty, string.Empty, string.Empty, $"two of the files you sent are called {name}. Send each file once, each under its own name"));
                }
            }
        }

        /// <summary>The sent file of this name; null when no such file was sent.</summary>
        public SentFile? Take(string name) => _byName.GetValueOrDefault(Key(name));

        /// <summary>The sent files that none of these cells names.</summary>
        public IEnumerable<SentFile> NotAmong(IEnumerable<string> names)
        {
            var named = names.Select(Key).ToHashSet(StringComparer.OrdinalIgnoreCase);
            return _byName.Where(entry => !named.Contains(entry.Key)).Select(entry => entry.Value);
        }

        private static string Key(string name) => Path.GetFileName(name).Trim();
    }
}
