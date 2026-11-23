using System.Security.Cryptography;
using BatteryPassWeb.Dpp.Editing;
using BatteryPassWeb.Dpp.Forms;
using BatteryPassWeb.Dpp.Loading;
using BatteryPassWeb.Dpp.Pages;
using BatteryPassWeb.Tests.Dpp;

namespace BatteryPassWeb.Tests.Loading;

/// <summary>
/// Package X1, point 7: the empty workbook is kept with the app as it was sent, and it stays in step with the
/// catalogue. Every key in it is a field the loader knows, and every catalogue value stored on a model, batch, site or
/// operator (with every part the schema gives it) has a row, a document line or a hazardous-substances column. A
/// value added to the catalogue, or a row added to the workbook, fails here until the other follows.
/// </summary>
public sealed class WorkbookInStepTests
{
    /// <summary>
    /// SHA-256 of "Core family - data to fill.xlsx": as it was sent to the owner on 13 November 2026, with the rows of the
    /// EU's values added by package R1 (decision 94). The file as sent is kept with the tests (OldWorkbookTests).
    /// </summary>
    private const string SentSha256 = "8064d8dcb83622dfc41682e94362e324e1d26a55e087672cd22ba21e97276ef4";

    private static readonly LoadFields Fields = new(AttributeFormsTests.Forms);

    private static XlsxBook Book()
    {
        var (book, problem) = XlsxReader.Read(LoadTemplate.Bytes());
        Assert.Null(problem);
        return book!;
    }

    [Fact]
    public void TheWorkbookInTheApp_ShouldBeTheFileThatWasSent_ByteForByte()
    {
        Assert.Equal(SentSha256, Convert.ToHexStringLower(SHA256.HashData(LoadTemplate.Bytes())));
        Assert.Equal("Core family - data to fill.xlsx", LoadTemplate.FileName);
    }

    [Fact]
    public void EveryKeyInTheWorkbook_ShouldBeAFieldTheLoaderKnows()
    {
        var workbook = LoadWorkbook.Read(Book(), Fields);

        // An unknown key, a key on the wrong sheet, a missing sheet or column: each would be a problem here.
        Assert.Empty(workbook.Problems.Select(problem => problem.Text));
        var keys = AllKeys(Book()).ToArray();
        Assert.True(keys.Length > 100, $"only {keys.Length} keys were found: the key column wasn't read");
        Assert.All(keys, key => Assert.True(Known(key), $"the loader doesn't know the workbook's key {key}"));
    }

    [Fact]
    public void EveryOwnFieldOfTheLoader_ShouldHaveARowInTheWorkbook()
    {
        var workbook = LoadWorkbook.Read(Book(), Fields);

        Assert.All(LoadFields.Own, field => Assert.True(SheetOf(workbook, field.Record).RowNames.ContainsKey(field.Key), $"the workbook has no row for {field.Key}"));
    }

    [Theory]
    [InlineData(TargetKind.Model, LoadRecord.Model)]
    [InlineData(TargetKind.Batch, LoadRecord.Batch)]
    [InlineData(TargetKind.Site, LoadRecord.Site)]
    [InlineData(TargetKind.Operator, LoadRecord.Operator)]
    public void EveryCatalogueValueOfARecord_ShouldHaveARowADocumentLineOrAHazardColumn(TargetKind kind, LoadRecord record)
    {
        var book = Book();
        var workbook = LoadWorkbook.Read(book, Fields);
        var sheet = SheetOf(workbook, record);
        var models = workbook.Models.Columns.Select(column => column.Header).ToArray();
        Assert.Equal(["Core800", "Core800s", "Core700"], models);
        var hazardHeaders = Headers(book.Sheet(LoadFields.HazardSheet)!);
        var missing = new List<string>();
        foreach (var attribute in RecordEditor.AttributesOf(kind))
        {
            var form = AttributeFormsTests.Forms.For(attribute);
            var owner = $"{LoadFields.Prefix(record)}|{attribute.Property}";
            switch (form.Kind)
            {
                case FormKind.Upload:
                case FormKind.Link:
                    // one line per model, for the model itself or for its batch
                    missing.AddRange(models
                        .Where(model => !workbook.Documents.Any(line => line.Model == model && line.Field.Record == record && line.Field.Attribute.Property == attribute.Property))
                        .Select(model => $"{attribute.Label} {attribute.Property}: no document line for {model}"));
                    break;
                case FormKind.Rows:
                    missing.AddRange(form.Inputs.Where(input => !hazardHeaders.Contains(input.Label, StringComparer.OrdinalIgnoreCase)).Select(input => $"{attribute.Label} {attribute.Property}: no column \"{input.Label}\" on the hazardous substances sheet"));
                    if (!sheet.RowNames.ContainsKey(owner))
                    {
                        missing.Add($"{attribute.Label} {attribute.Property}: no row that points to its sheet");
                    }

                    break;
                case FormKind.Fixed:
                    missing.AddRange(form.Constants.Keys.Where(name => !sheet.RowNames.ContainsKey($"{owner}|{name}")).Select(name => $"{attribute.Label} {attribute.Property}.{name}: no row"));
                    break;
                default:
                    // every input of the form; the units the app writes itself (the form's constants) need none
                    missing.AddRange(form.Inputs
                        .Select(input => input.Path.Length == 0 ? owner : $"{owner}|{input.Path}")
                        .Where(key => !sheet.RowNames.ContainsKey(key))
                        .Select(key => $"{attribute.Label}: no row with the key {key}"));
                    break;
            }
        }

        Assert.Empty(missing);
    }

    [Fact]
    public void TheHazardousSubstancesSheet_ShouldHaveItsExampleLineAndLinesToFill()
    {
        var keys = Keys(Book().Sheet(LoadFields.HazardSheet)!).ToArray();

        Assert.Equal(LoadFields.HazardExampleKey, keys[0]);
        Assert.Equal(30, keys.Count(key => key == LoadFields.HazardLineKey));
        Assert.Empty(LoadWorkbook.Read(Book(), Fields).Hazards); // the example line is never read, and no line is filled
    }

    [Fact]
    public void TheEmptyWorkbook_ShouldHoldOnlyWhatTheOwnerAlreadyToldUs()
    {
        var workbook = LoadWorkbook.Read(Book(), Fields);

        Assert.Equal("Core", workbook.Family.Columns.Single().Cell("family|Name")?.Text);
        Assert.Equal(["Core800", "Core800s", "Core700"], workbook.Models.Columns.Select(column => column.Cell("model|Name")?.Text));
        Assert.Equal(["Batch of Core800", "Batch of Core800s", "Batch of Core700"], workbook.Batches.Columns.Select(column => column.Header));
        Assert.Equal(["Site 1", "Site 2 (only if needed)"], workbook.Sites.Columns.Select(column => column.Header));
        Assert.Equal(["Operator 1", "Operator 2 (only if needed)"], workbook.Operators.Columns.Select(column => column.Header));
        Assert.All(workbook.Documents, line => Assert.True(line.Link is null && line.Title is null && line.Language is null));
        Assert.Equal(39, workbook.Documents.Count); // 13 documents for each of the 3 models
    }

    private static ValueSheet SheetOf(LoadWorkbook workbook, LoadRecord record) => record switch
    {
        LoadRecord.Family => workbook.Family,
        LoadRecord.Model => workbook.Models,
        LoadRecord.Batch => workbook.Batches,
        LoadRecord.Site => workbook.Sites,
        _ => workbook.Operators,
    };

    private static bool Known(string key)
    {
        var parts = key.Split('|');
        return parts[0] switch
        {
            "doc" => parts.Length == 4 && LoadFields.RecordOf(parts[2]) is { } record && Fields.Document(record, parts[3]) is not null,
            "hazard" => key is LoadFields.HazardExampleKey or LoadFields.HazardLineKey,
            _ => Fields.Knows(key),
        };
    }

    private static IEnumerable<string> AllKeys(XlsxBook book) =>
        book.Sheets.SelectMany(Keys);

    /// <summary>The keys of a sheet: the cells under its "Key (do not change)" heading that hold a key.</summary>
    private static IEnumerable<string> Keys(XlsxSheet sheet)
    {
        var header = sheet.Cells.Where(cell => cell.Value.Text.Trim() == LoadWorkbook.KeyHeader).Select(cell => cell.Key).FirstOrDefault();
        return header == default
            ? []
            : Enumerable.Range(header.Row + 1, Math.Max(0, sheet.LastRow - header.Row)).Select(row => sheet.Text(row, header.Column)).Where(key => key.Contains('|', StringComparison.Ordinal));
    }

    private static IReadOnlyList<string> Headers(XlsxSheet sheet) =>
        Enumerable.Range(1, sheet.LastColumn).Select(column => sheet.Text(1, column)).Where(text => text.Length > 0).ToArray();
}
