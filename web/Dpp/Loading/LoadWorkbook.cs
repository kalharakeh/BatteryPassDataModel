namespace BatteryPassWeb.Dpp.Loading;

/// <summary>
/// One problem the check found, named the way the owner finds it in Excel: the sheet, the row's name and the column
/// ("Models, Battery mass, Core800s: 4,7 kg is not a number; type the number only").
/// </summary>
public sealed record LoadProblem(string Sheet, string Row, string Column, string Message)
{
    public string Where => string.Join(", ", new[] { Sheet, Row, Column }.Where(part => part.Length > 0));

    public string Text => Where.Length == 0 ? Message : $"{Where}: {Message}";
}

/// <summary>Where a cell is, in the owner's words.</summary>
public sealed record CellPlace(string Sheet, string Row, string Column)
{
    public LoadProblem Problem(string message) => new(Sheet, Row, Column, message);
}

/// <summary>A filled cell of the workbook.</summary>
public sealed record FilledCell(string Text, bool IsNumber, CellPlace Place)
{
    /// <summary>The word that takes a value away.</summary>
    public const string None = "none";

    public bool IsNone => string.Equals(Text, None, StringComparison.OrdinalIgnoreCase);
}

/// <summary>One input column of a value sheet (a model, a batch, a site, an operator) with its filled cells by key.</summary>
public sealed class ValueColumn(string header, int number)
{
    /// <summary>The column's heading on the sheet ("Core800s", "Batch of Core800", "Site 1").</summary>
    public string Header { get; } = header;

    /// <summary>1 for the first input column of its sheet.</summary>
    public int Number { get; } = number;

    public Dictionary<string, FilledCell> Cells { get; } = new(StringComparer.Ordinal);

    public FilledCell? Cell(string key) => Cells.GetValueOrDefault(key);
}

/// <summary>A sheet with one row per field and one column per item.</summary>
public sealed class ValueSheet(string name, LoadRecord record)
{
    public string Name { get; } = name;

    public LoadRecord Record { get; } = record;

    public List<ValueColumn> Columns { get; } = [];

    /// <summary>Each key's row name, as the sheet shows it ("Battery mass").</summary>
    public Dictionary<string, string> RowNames { get; } = new(StringComparer.Ordinal);

    public string RowName(string key) => RowNames.GetValueOrDefault(key) ?? LoadFields.OwnField(key)?.Label ?? key;

    public CellPlace Place(string key, ValueColumn column) => new(Name, RowName(key), column.Header);
}

/// <summary>One line of the hazardous substances sheet: the model it is for (a name, or "all") and its cells by the form's input.</summary>
public sealed record HazardLine(string RowName, FilledCell? Model, IReadOnlyDictionary<string, FilledCell> Cells)
{
    public CellPlace Place(string column) => new(LoadFields.HazardSheet, RowName, column);
}

/// <summary>One line of the documents sheet: a document of one model (or of its batch).</summary>
public sealed record DocumentLine(string Model, DocumentField Field, string RowName, FilledCell? Link, FilledCell? Title, FilledCell? Language)
{
    public CellPlace Place(string column) => new(LoadFields.DocumentSheet, RowName, column);
}

/// <summary>What the workbook holds, cell by cell, before anything is compared with the app's data.</summary>
public sealed class LoadWorkbook
{
    public const string KeyHeader = "Key (do not change)";
    public const string NameHeader = "Value";
    public const string ModelHeader = "Model";
    public const string DocumentHeader = "Document";
    public const string LinkHeader = "File name or web link";
    public const string TitleHeader = "Title shown to readers";
    public const string LanguageHeader = "Language";

    public const string NotTheWorkbook = "this isn't the product data workbook as it was sent. Download the empty workbook from this page and fill that one";

    /// <summary>The headings of a value sheet that aren't input columns; every other headed column is one.</summary>
    private static readonly HashSet<string> FixedHeaders = new(StringComparer.OrdinalIgnoreCase)
    {
        KeyHeader, "No.", NameHeader, "What to enter", "Unit or choices", "Needed for February 2027?", "Who can see it", "Example (made up, from the demo)", "Your notes or source",
    };

    private const int HeaderSearchRows = 10;

    private LoadWorkbook()
    {
    }

    public ValueSheet Family { get; private set; } = new(LoadFields.FamilySheet, LoadRecord.Family);

    public ValueSheet Models { get; private set; } = new(LoadFields.ModelsSheet, LoadRecord.Model);

    public ValueSheet Batches { get; private set; } = new(LoadFields.BatchesSheet, LoadRecord.Batch);

    public ValueSheet Sites { get; private set; } = new(LoadFields.SiteSheet, LoadRecord.Site);

    public ValueSheet Operators { get; private set; } = new(LoadFields.OperatorSheet, LoadRecord.Operator);

    public List<HazardLine> Hazards { get; } = [];

    public List<DocumentLine> Documents { get; } = [];

    public List<LoadProblem> Problems { get; } = [];

    /// <summary>
    /// Reads the sheets by their keys (column "Key (do not change)", wherever it is) and by the words of their header
    /// row, never by a fixed position: the owner may widen, reorder or hide columns.
    /// </summary>
    public static LoadWorkbook Read(XlsxBook book, LoadFields fields)
    {
        ArgumentNullException.ThrowIfNull(book);
        ArgumentNullException.ThrowIfNull(fields);
        var workbook = new LoadWorkbook();
        workbook.Family = workbook.ReadValues(book, fields, LoadRecord.Family);
        workbook.Models = workbook.ReadValues(book, fields, LoadRecord.Model);
        workbook.Batches = workbook.ReadValues(book, fields, LoadRecord.Batch);
        workbook.Sites = workbook.ReadValues(book, fields, LoadRecord.Site);
        workbook.Operators = workbook.ReadValues(book, fields, LoadRecord.Operator);
        workbook.ReadHazards(book, fields);
        workbook.ReadDocuments(book, fields);
        return workbook;
    }

    private ValueSheet ReadValues(XlsxBook book, LoadFields fields, LoadRecord record)
    {
        var name = LoadFields.SheetOf(record);
        var result = new ValueSheet(name, record);
        if (Header(book, name) is not var (sheet, headerRow, headers))
        {
            return result;
        }

        var keyColumn = headers.First(header => Same(header.Value, KeyHeader)).Key;
        var nameColumn = headers.FirstOrDefault(header => Same(header.Value, NameHeader)).Key;
        var inputs = headers.Where(header => !FixedHeaders.Contains(header.Value)).OrderBy(header => header.Key).ToArray();
        var columns = inputs.Select((header, index) => (Sheet: header.Key, Column: new ValueColumn(header.Value, index + 1))).ToArray();
        result.Columns.AddRange(columns.Select(column => column.Column));
        for (var row = headerRow + 1; row <= sheet.LastRow; row++)
        {
            var key = sheet.Text(row, keyColumn);
            var rowName = nameColumn > 0 ? FirstLine(sheet.Text(row, nameColumn)) : string.Empty;
            if (!key.Contains('|', StringComparison.Ordinal))
            {
                // A heading. Something typed beside it belongs to no field.
                if (columns.Any(column => sheet.Cell(row, column.Sheet) is not null) && rowName.Length == 0)
                {
                    Problems.Add(new LoadProblem(name, $"row {row}", string.Empty, "this row has something typed in it but it isn't one of the workbook's rows, so the app can't tell what it is. Type it in the row it belongs to"));
                }

                continue;
            }

            if (LoadFields.RecordOf(key) != record || !fields.Knows(key))
            {
                Problems.Add(new LoadProblem(name, rowName.Length > 0 ? rowName : $"row {row}", string.Empty, $"the app doesn't know this row (its key is {key}). Use the workbook as it was sent; don't add rows or change column A"));
                continue;
            }

            if (result.RowNames.ContainsKey(key))
            {
                Problems.Add(new LoadProblem(name, rowName.Length > 0 ? rowName : $"row {row}", string.Empty, "this row is in the sheet twice. Keep one of them"));
                continue;
            }

            result.RowNames[key] = rowName.Length > 0 ? rowName : LoadFields.OwnField(key)?.Label ?? key;
            if (key == LoadFields.HazardRowKey)
            {
                continue; // its cells only point to the sheet Hazardous substances
            }

            foreach (var (sheetColumn, column) in columns)
            {
                if (Filled(sheet, row, sheetColumn, result.Place(key, column)) is { } cell)
                {
                    column.Cells[key] = cell;
                }
            }
        }

        return result;
    }

    private void ReadHazards(XlsxBook book, LoadFields fields)
    {
        const string name = LoadFields.HazardSheet;
        if (Header(book, name) is not var (sheet, headerRow, headers))
        {
            return;
        }

        var keyColumn = headers.First(header => Same(header.Value, KeyHeader)).Key;
        var modelColumn = headers.FirstOrDefault(header => Same(header.Value, ModelHeader)).Key;
        var inputColumns = fields.Hazards.Inputs
            .Select(input => (Input: input, Column: headers.FirstOrDefault(header => Same(header.Value, input.Label)).Key))
            .ToArray();
        if (modelColumn == 0 || inputColumns.Any(input => input.Column == 0))
        {
            var missing = inputColumns.Where(input => input.Column == 0).Select(input => input.Input.Label).Prepend(modelColumn == 0 ? ModelHeader : string.Empty).Where(text => text.Length > 0);
            Problems.Add(new LoadProblem(name, string.Empty, string.Empty, $"the sheet has no column called {string.Join(" or ", missing)}; {NotTheWorkbook}"));
            return;
        }

        var keyed = false; // the rows above the first row with a key are the heading and its help line
        for (var row = headerRow + 1; row <= sheet.LastRow; row++)
        {
            var key = sheet.Text(row, keyColumn);
            if (key.Length > 0)
            {
                keyed = true;
            }

            if (!keyed || key == LoadFields.HazardExampleKey)
            {
                continue;
            }

            var chemical = sheet.Text(row, inputColumns.First(input => input.Input.Path == "chemicalName").Column);
            var rowName = chemical.Length > 0 ? $"row {row} ({chemical})" : $"row {row}";
            if (key.Length > 0 && key != LoadFields.HazardLineKey)
            {
                Problems.Add(new LoadProblem(name, rowName, string.Empty, $"the app doesn't know this row (its key is {key}). Use the workbook as it was sent; don't change column A"));
                continue;
            }

            var model = Filled(sheet, row, modelColumn, new CellPlace(name, rowName, ModelHeader));
            var cells = new Dictionary<string, FilledCell>(StringComparer.Ordinal);
            foreach (var (input, column) in inputColumns)
            {
                if (Filled(sheet, row, column, new CellPlace(name, rowName, input.Label)) is { } cell)
                {
                    cells[input.Path] = cell;
                }
            }

            if (model is not null || cells.Count > 0)
            {
                Hazards.Add(new HazardLine(rowName, model, cells));
            }
        }
    }

    private void ReadDocuments(XlsxBook book, LoadFields fields)
    {
        const string name = LoadFields.DocumentSheet;
        if (Header(book, name) is not var (sheet, headerRow, headers))
        {
            return;
        }

        int Column(string header) => headers.FirstOrDefault(entry => Same(entry.Value, header)).Key;
        var keyColumn = Column(KeyHeader);
        var (modelColumn, documentColumn, linkColumn, titleColumn, languageColumn) = (Column(ModelHeader), Column(DocumentHeader), Column(LinkHeader), Column(TitleHeader), Column(LanguageHeader));
        if (linkColumn == 0 || titleColumn == 0 || languageColumn == 0)
        {
            var missing = new[] { (linkColumn, LinkHeader), (titleColumn, TitleHeader), (languageColumn, LanguageHeader) }.Where(column => column.Item1 == 0).Select(column => column.Item2);
            Problems.Add(new LoadProblem(name, string.Empty, string.Empty, $"the sheet has no column called {string.Join(" or ", missing)}; {NotTheWorkbook}"));
            return;
        }

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (var row = headerRow + 1; row <= sheet.LastRow; row++)
        {
            var key = sheet.Text(row, keyColumn);
            var document = documentColumn > 0 ? FirstLine(sheet.Text(row, documentColumn)) : string.Empty;
            var typed = new[] { linkColumn, titleColumn, languageColumn }.Any(column => sheet.Cell(row, column) is not null);
            if (!key.Contains('|', StringComparison.Ordinal))
            {
                if (typed)
                {
                    Problems.Add(new LoadProblem(name, document.Length > 0 ? document : $"row {row}", string.Empty, "this line has no key in column A (it is hidden), so the app can't tell which document it is. Copy a whole line of that document, change its Model cell and fill it"));
                }

                continue;
            }

            var parts = key.Split('|');
            var record = parts.Length == 4 && parts[0] == "doc" ? LoadFields.RecordOf(parts[2]) : null;
            if (record is not (LoadRecord.Model or LoadRecord.Batch) || fields.Document(record.Value, parts[3]) is not { } field)
            {
                Problems.Add(new LoadProblem(name, document.Length > 0 ? document : $"row {row}", string.Empty, $"the app doesn't know this line (its key is {key}). Use the workbook as it was sent; don't change column A"));
                continue;
            }

            // The Model cell says which model the line is for; a copied line keeps its key and gets another model there.
            var model = modelColumn > 0 && sheet.Text(row, modelColumn) is { Length: > 0 } typedModel ? typedModel : parts[1];
            var rowName = $"{(document.Length > 0 ? document : field.Attribute.Name)} ({model})";
            if (!seen.Add($"{model}|{parts[2]}|{parts[3]}"))
            {
                if (typed)
                {
                    Problems.Add(new LoadProblem(name, rowName, string.Empty, "there are two lines for this document and model. Fill one of them and empty the other"));
                }

                continue;
            }

            Documents.Add(new DocumentLine(
                model,
                field,
                rowName,
                Filled(sheet, row, linkColumn, new CellPlace(name, rowName, LinkHeader)),
                Filled(sheet, row, titleColumn, new CellPlace(name, rowName, TitleHeader)),
                Filled(sheet, row, languageColumn, new CellPlace(name, rowName, LanguageHeader))));
        }
    }

    /// <summary>
    /// A sheet with its header row: the first row that has the key column's heading, and that row's headings by column.
    /// Null, with a problem noted, when the workbook has no such sheet.
    /// </summary>
    private (XlsxSheet Sheet, int Row, IReadOnlyDictionary<int, string> Headers)? Header(XlsxBook book, string name)
    {
        if (book.Sheet(name) is not { } sheet)
        {
            Problems.Add(new LoadProblem(name, string.Empty, string.Empty, $"the workbook has no sheet called {name}; {NotTheWorkbook}"));
            return null;
        }

        for (var row = 1; row <= Math.Min(sheet.LastRow, HeaderSearchRows); row++)
        {
            var headers = new Dictionary<int, string>();
            for (var column = 1; column <= sheet.LastColumn; column++)
            {
                if (sheet.Text(row, column) is { Length: > 0 } text)
                {
                    headers[column] = OneLine(text);
                }
            }

            if (headers.Values.Any(text => Same(text, KeyHeader)))
            {
                return (sheet, row, headers);
            }
        }

        Problems.Add(new LoadProblem(name, string.Empty, string.Empty, $"the sheet has no column called {KeyHeader}; {NotTheWorkbook}"));
        return null;
    }

    private FilledCell? Filled(XlsxSheet sheet, int row, int column, CellPlace place)
    {
        if (sheet.Cell(row, column) is not { } cell || cell.Text.Trim().Length == 0)
        {
            return null;
        }

        if (cell.IsError)
        {
            Problems.Add(place.Problem($"the cell shows an Excel error ({cell.Text.Trim()}). Type the value itself"));
            return null;
        }

        return new FilledCell(cell.Text.Trim(), cell.IsNumber, place);
    }

    private static bool Same(string? text, string other) => string.Equals(text?.Trim(), other, StringComparison.OrdinalIgnoreCase);

    private static string OneLine(string text) => string.Join(' ', text.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));

    private static string FirstLine(string text) => text.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).FirstOrDefault() ?? string.Empty;
}
