using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Xml.Linq;
using BatteryPassWeb.Dpp.Identifiers;
using BatteryPassWeb.Dpp.Loading;

namespace BatteryPassWeb.Tests.Loading;

/// <summary>
/// The empty product data workbook with cells filled in, for tests: a cell is found by its row's key and its
/// column's heading, as the loader finds it. <see cref="AsExcelSaves"/> writes it the way Excel does (shared texts,
/// links beside the workbook part); otherwise it stays as the script wrote it (inline texts).
/// </summary>
internal sealed class FilledWorkbook
{
    private static readonly XNamespace Main = TestXlsx.Main;
    private static readonly XNamespace Rel = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
    private static readonly XNamespace PackageRel = "http://schemas.openxmlformats.org/package/2006/relationships";

    private readonly Dictionary<string, byte[]> _parts = new(StringComparer.Ordinal);
    private readonly List<string> _order = [];
    private readonly Dictionary<string, string> _sheetPaths = new(StringComparer.Ordinal);
    private readonly Dictionary<string, XDocument> _sheets = new(StringComparer.Ordinal);
    private XDocument? _styles;

    private FilledWorkbook(byte[] template)
    {
        using var zip = new ZipArchive(new MemoryStream(template), ZipArchiveMode.Read);
        foreach (var entry in zip.Entries)
        {
            using var buffer = new MemoryStream();
            using (var stream = entry.Open())
            {
                stream.CopyTo(buffer);
            }

            _parts[entry.FullName] = buffer.ToArray();
            _order.Add(entry.FullName);
        }

        var targets = XDocument.Parse(Text("xl/_rels/workbook.xml.rels")).Root!.Elements(PackageRel + "Relationship")
            .ToDictionary(item => (string)item.Attribute("Id")!, item => ((string)item.Attribute("Target")!).TrimStart('/'), StringComparer.Ordinal);
        foreach (var sheet in XDocument.Parse(Text("xl/workbook.xml")).Descendants(Main + "sheet"))
        {
            _sheetPaths[(string)sheet.Attribute("name")!] = targets[(string)sheet.Attribute(Rel + "id")!];
        }
    }

    /// <summary>The workbook as it was sent: nothing filled but what the owner had already told us.</summary>
    public static FilledWorkbook Empty() => new(LoadTemplate.Bytes());

    /// <summary>Types a text into a cell of a value sheet.</summary>
    public FilledWorkbook Set(string sheet, string key, string column, string text) =>
        SetCell(sheet, RowOf(sheet, key), ColumnOf(sheet, column), text, number: false);

    /// <summary>Types a number into a cell: Excel keeps it as a number, here in the long form it writes (4.7000000000000002).</summary>
    public FilledWorkbook SetNumber(string sheet, string key, string column, double number) =>
        SetCell(sheet, RowOf(sheet, key), ColumnOf(sheet, column), number.ToString("G17", CultureInfo.InvariantCulture), number: true);

    /// <summary>
    /// Types a number into a cell formatted as Excel formats it: <paramref name="format"/> is a built-in format's number
    /// (9 is 0 %, 10 is 0.00 %, 14 a date) or, with <paramref name="code"/>, the workbook's own format. Excel keeps the
    /// number as stored, so 16 % is 0.16: <paramref name="stored"/> is that number.
    /// </summary>
    public FilledWorkbook SetFormatted(string sheet, string key, string column, double stored, int format, string? code = null)
    {
        SetNumber(sheet, key, column, stored);
        return Style(sheet, RowOf(sheet, key), ColumnOf(sheet, column), format, code);
    }

    /// <summary>A formula saved without its value, as a file written by a script or a program other than Excel may hold.</summary>
    public FilledWorkbook SetFormulaWithoutValue(string sheet, string key, string column)
    {
        SetCell(sheet, RowOf(sheet, key), ColumnOf(sheet, column), null, number: false);
        Cell(RowElement(sheet, RowOf(sheet, key)), ColumnOf(sheet, column), RowOf(sheet, key))!.Add(new XElement(Main + "f", "SUM(B2:B3)"));
        return this;
    }

    /// <summary>Deletes a whole column of a sheet as Excel does: the columns to its right move one to the left.</summary>
    public FilledWorkbook DeleteColumn(string sheet, string heading)
    {
        var gone = ColumnOf(sheet, heading);
        foreach (var row in Sheet(sheet).Descendants(Main + "row"))
        {
            var number = (int)row.Attribute("r")!;
            foreach (var cell in row.Elements(Main + "c").ToArray())
            {
                var column = ColumnNumber((string)cell.Attribute("r")!);
                if (column == gone)
                {
                    cell.Remove();
                }
                else if (column > gone)
                {
                    cell.SetAttributeValue("r", $"{XlsxWriter.Column(column - 2)}{number.ToString(CultureInfo.InvariantCulture)}");
                }
            }
        }

        return this;
    }

    /// <summary>
    /// Copies a whole column of a sheet into the column right after the sheet's input columns, heading included, as an
    /// owner does to add a model with Copy and Paste. The notes column moves one to the right.
    /// </summary>
    public FilledWorkbook CopyColumn(string sheet, string heading)
    {
        var from = ColumnOf(sheet, heading);
        var notes = ColumnOf(sheet, "Your notes or source");
        foreach (var row in Sheet(sheet).Descendants(Main + "row"))
        {
            var number = (int)row.Attribute("r")!;
            foreach (var cell in row.Elements(Main + "c").Where(cell => ColumnNumber((string)cell.Attribute("r")!) >= notes).Reverse().ToArray())
            {
                cell.SetAttributeValue("r", $"{XlsxWriter.Column(ColumnNumber((string)cell.Attribute("r")!))}{number.ToString(CultureInfo.InvariantCulture)}");
            }

            if (Cell(row, from, number) is { } source)
            {
                var copy = new XElement(source);
                copy.SetAttributeValue("r", $"{XlsxWriter.Column(notes - 1)}{number.ToString(CultureInfo.InvariantCulture)}");
                var after = row.Elements(Main + "c").LastOrDefault(other => ColumnNumber((string)other.Attribute("r")!) < notes);
                if (after is null)
                {
                    row.AddFirst(copy);
                }
                else
                {
                    after.AddAfterSelf(copy);
                }
            }
        }

        return this;
    }

    /// <summary>Types a text into the n-th column after the example column (1 is the first input column).</summary>
    public FilledWorkbook SetAt(string sheet, string key, int input, string text) =>
        SetCell(sheet, RowOf(sheet, key), ColumnOf(sheet, "Example (made up, from the demo)") + input, text, number: false);

    /// <summary>Types a number into the n-th column after the example column.</summary>
    public FilledWorkbook SetNumberAt(string sheet, string key, int input, double number) =>
        SetCell(sheet, RowOf(sheet, key), ColumnOf(sheet, "Example (made up, from the demo)") + input, number.ToString("G17", CultureInfo.InvariantCulture), number: true);

    /// <summary>A formula whose saved result is an empty text, as Excel saves =IF(C20="","",C20).</summary>
    public FilledWorkbook SetFormulaWithEmptyResult(string sheet, string key, string column)
    {
        SetCell(sheet, RowOf(sheet, key), ColumnOf(sheet, column), null, number: false);
        var cell = Cell(RowElement(sheet, RowOf(sheet, key)), ColumnOf(sheet, column), RowOf(sheet, key))!;
        cell.SetAttributeValue("t", "str");
        cell.Add(new XElement(Main + "f", "IF(C20=\"\",\"\",C20)"), new XElement(Main + "v", string.Empty));
        return this;
    }

    /// <summary>Puts a cell with this reference into the first row with a key, as a crafted file might.</summary>
    public FilledWorkbook CellAt(string sheet, string reference, string text)
    {
        var row = Sheet(sheet).Descendants(Main + "row").First();
        row.Add(new XElement(Main + "c", new XAttribute("r", reference), new XAttribute("t", "inlineStr"), new XElement(Main + "is", new XElement(Main + "t", text))));
        return this;
    }

    private FilledWorkbook Style(string sheet, int row, int column, int format, string? code)
    {
        _styles ??= XDocument.Parse(Text("xl/styles.xml"), LoadOptions.PreserveWhitespace);
        var root = _styles.Root!;
        if (code is not null)
        {
            var formats = root.Element(Main + "numFmts") ?? new XElement(Main + "numFmts");
            if (formats.Parent is null)
            {
                root.AddFirst(formats);
            }

            formats.Add(new XElement(Main + "numFmt", new XAttribute("numFmtId", format), new XAttribute("formatCode", code)));
            formats.SetAttributeValue("count", formats.Elements(Main + "numFmt").Count());
        }

        var styles = root.Element(Main + "cellXfs")!;
        styles.Add(new XElement(Main + "xf", new XAttribute("numFmtId", format), new XAttribute("fontId", 0), new XAttribute("fillId", 0), new XAttribute("borderId", 0), new XAttribute("applyNumberFormat", 1)));
        var index = styles.Elements(Main + "xf").Count() - 1;
        styles.SetAttributeValue("count", index + 1);
        Cell(RowElement(sheet, row), column, row)!.SetAttributeValue("s", index);
        return this;
    }

    /// <summary>Empties a cell.</summary>
    public FilledWorkbook Clear(string sheet, string key, string column) =>
        SetCell(sheet, RowOf(sheet, key), ColumnOf(sheet, column), null, number: false);

    /// <summary>Changes a key in column A, as someone who edits the hidden column would.</summary>
    public FilledWorkbook Rekey(string sheet, string key, string newKey) =>
        SetCell(sheet, RowOf(sheet, key), ColumnOf(sheet, LoadWorkbook.KeyHeader), newKey, number: false);

    /// <summary>
    /// Copies the example column into the empty cells of an input column, as the manager's check does: what the workbook
    /// was sent with filled (the record's name, a serial number format) stays.
    /// </summary>
    public FilledWorkbook ExampleInto(string sheet, string column)
    {
        var document = Sheet(sheet);
        var (example, target, keys) = (ColumnOf(sheet, "Example (made up, from the demo)"), ColumnOf(sheet, column), ColumnOf(sheet, LoadWorkbook.KeyHeader));
        foreach (var row in document.Descendants(Main + "row").ToArray())
        {
            var number = (int)row.Attribute("r")!;
            var key = CellText(row, keys, number);
            if (key is null || !key.Contains('|', StringComparison.Ordinal) || key.EndsWith("|Name", StringComparison.Ordinal) || key == LoadFields.HazardRowKey)
            {
                continue;
            }

            var cell = Cell(row, example, number);
            if (cell is null || CellText(row, example, number) is not { Length: > 0 } text || CellText(row, target, number) is { Length: > 0 })
            {
                continue; // nothing to copy, or a cell the workbook was sent with filled (a serial number format, the DUNS)
            }

            SetCell(sheet, number, target, text, number: (string?)cell.Attribute("t") is null or "n");
        }

        return this;
    }

    /// <summary>Fills the line of one document for one model on the documents sheet.</summary>
    public FilledWorkbook Document(string model, string record, string property, string? link, string? title = null, string? language = null)
    {
        var row = RowOf(LoadFields.DocumentSheet, $"doc|{model}|{record}|{property}");
        SetCell(LoadFields.DocumentSheet, row, ColumnOf(LoadFields.DocumentSheet, LoadWorkbook.LinkHeader), link, number: false);
        SetCell(LoadFields.DocumentSheet, row, ColumnOf(LoadFields.DocumentSheet, LoadWorkbook.TitleHeader), title, number: false);
        return SetCell(LoadFields.DocumentSheet, row, ColumnOf(LoadFields.DocumentSheet, LoadWorkbook.LanguageHeader), language, number: false);
    }

    /// <summary>Types a text into one cell of a document's line (the Model cell, for one).</summary>
    public FilledWorkbook DocumentCell(string model, string record, string property, string column, string? text) =>
        SetCell(LoadFields.DocumentSheet, RowOf(LoadFields.DocumentSheet, $"doc|{model}|{record}|{property}"), ColumnOf(LoadFields.DocumentSheet, column), text, number: false);

    /// <summary>Fills the n-th line to fill (from 1) of the hazardous substances sheet.</summary>
    public FilledWorkbook Hazard(int line, string? model, string? chemical, string? identifierType = "CAS No", string? identifier = "21324-40-3", object? concentration = null, string? hazards = "H301; H314", string? precautions = "P260; P280", string? where = null)
    {
        const string sheet = LoadFields.HazardSheet;
        var row = HazardRow(line);
        void Put(string column, object? value)
        {
            if (value is double or int or decimal)
            {
                SetCell(sheet, row, ColumnOf(sheet, column), Convert.ToDouble(value, CultureInfo.InvariantCulture).ToString("G17", CultureInfo.InvariantCulture), number: true);
            }
            else
            {
                SetCell(sheet, row, ColumnOf(sheet, column), value as string, number: false);
            }
        }

        Put("Model", model);
        Put("Chemical name", chemical);
        Put("Identifier type", identifierType);
        Put("Identifier", identifier);
        Put("Concentration", concentration ?? 1.2);
        Put("Hazard phrases", hazards);
        Put("Precautionary phrases", precautions);
        Put("Where in the battery", where);
        return this;
    }

    /// <summary>Formats one cell of the n-th line to fill of the hazardous substances sheet, as Excel formats it.</summary>
    public FilledWorkbook FormatHazard(int line, string column, int format, string? code = null) =>
        Style(LoadFields.HazardSheet, HazardRow(line), ColumnOf(LoadFields.HazardSheet, column), format, code);

    /// <summary>Empties one cell of the n-th line to fill of the hazardous substances sheet.</summary>
    public FilledWorkbook ClearHazard(int line, string column) =>
        SetCell(LoadFields.HazardSheet, HazardRow(line), ColumnOf(LoadFields.HazardSheet, column), null, number: false);

    private int HazardRow(int line)
    {
        const string sheet = LoadFields.HazardSheet;
        var keys = ColumnOf(sheet, LoadWorkbook.KeyHeader);
        return Sheet(sheet).Descendants(Main + "row").Where(item => CellText(item, keys, (int)item.Attribute("r")!) == LoadFields.HazardLineKey).Select(item => (int)item.Attribute("r")!).ElementAt(line - 1);
    }

    /// <summary>The workbook's bytes, as the script wrote it: inline texts, links from the file's root.</summary>
    public byte[] Bytes()
    {
        foreach (var (name, document) in _sheets)
        {
            _parts[_sheetPaths[name]] = Encoding.UTF8.GetBytes(document.ToString(SaveOptions.DisableFormatting));
        }

        if (_styles is not null)
        {
            _parts["xl/styles.xml"] = Encoding.UTF8.GetBytes(_styles.ToString(SaveOptions.DisableFormatting));
        }

        return Zip(_order.Select(path => (path, _parts[path])));
    }

    /// <summary>
    /// The same workbook as Excel writes it after the owner saves it: every text in the shared texts part, numbers as
    /// numbers, and the workbook's links beside the workbook part ("worksheets/sheet3.xml").
    /// </summary>
    public byte[] AsExcelSaves()
    {
        Bytes();
        var shared = new List<string>();
        var parts = new Dictionary<string, byte[]>(_parts, StringComparer.Ordinal);
        foreach (var path in _sheetPaths.Values)
        {
            var document = XDocument.Parse(Encoding.UTF8.GetString(parts[path]), LoadOptions.PreserveWhitespace);
            foreach (var cell in document.Descendants(Main + "c").Where(cell => (string?)cell.Attribute("t") == "inlineStr").ToArray())
            {
                var text = string.Concat(cell.Descendants(Main + "t").Select(item => item.Value));
                cell.RemoveNodes();
                if (text.Length == 0)
                {
                    cell.Attribute("t")!.Remove();
                    continue;
                }

                var index = shared.IndexOf(text);
                if (index < 0)
                {
                    shared.Add(text);
                    index = shared.Count - 1;
                }

                cell.SetAttributeValue("t", "s");
                cell.Add(new XElement(Main + "v", index.ToString(CultureInfo.InvariantCulture)));
            }

            parts[path] = Encoding.UTF8.GetBytes(document.ToString(SaveOptions.DisableFormatting));
        }

        var strings = new XElement(Main + "sst", shared.Select(text => new XElement(Main + "si", new XElement(Main + "t", new XAttribute(XNamespace.Xml + "space", "preserve"), text))));
        parts["xl/sharedStrings.xml"] = Encoding.UTF8.GetBytes(strings.ToString(SaveOptions.DisableFormatting));
        var rels = XDocument.Parse(Encoding.UTF8.GetString(parts["xl/_rels/workbook.xml.rels"]));
        foreach (var relationship in rels.Root!.Elements(PackageRel + "Relationship"))
        {
            relationship.SetAttributeValue("Target", ((string)relationship.Attribute("Target")!).Replace("/xl/", string.Empty, StringComparison.Ordinal));
        }

        rels.Root.Add(new XElement(PackageRel + "Relationship", new XAttribute("Id", "rIdShared"), new XAttribute("Type", "http://schemas.openxmlformats.org/officeDocument/2006/relationships/sharedStrings"), new XAttribute("Target", "sharedStrings.xml")));
        parts["xl/_rels/workbook.xml.rels"] = Encoding.UTF8.GetBytes(rels.ToString(SaveOptions.DisableFormatting));
        return Zip(_order.Append("xl/sharedStrings.xml").Select(path => (path, parts[path])));
    }

    private static byte[] Zip(IEnumerable<(string Path, byte[] Content)> parts)
    {
        using var buffer = new MemoryStream();
        using (var zip = new ZipArchive(buffer, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var (path, content) in parts)
            {
                var entry = zip.CreateEntry(path, CompressionLevel.Optimal);
                entry.LastWriteTime = WrittenAt; // the same bytes every time: a check and its Load see one workbook
                using var stream = entry.Open();
                stream.Write(content);
            }
        }

        return buffer.ToArray();
    }

    /// <summary>The time every part is written with, as a file saved once has one.</summary>
    private static readonly DateTimeOffset WrittenAt = new(2026, 11, 16, 9, 0, 0, TimeSpan.Zero);

    private string Text(string path) => Encoding.UTF8.GetString(_parts[path]);

    private XDocument Sheet(string name)
    {
        if (!_sheets.TryGetValue(name, out var document))
        {
            document = XDocument.Parse(Text(_sheetPaths[name]), LoadOptions.PreserveWhitespace);
            _sheets[name] = document;
        }

        return document;
    }

    private int HeaderRow(string sheet) =>
        Sheet(sheet).Descendants(Main + "row").Select(row => (int)row.Attribute("r")!).First(number => Enumerable.Range(0, 30).Any(column => CellText(RowElement(sheet, number), column + 1, number) == LoadWorkbook.KeyHeader));

    private XElement RowElement(string sheet, int number) =>
        Sheet(sheet).Descendants(Main + "row").First(row => (int)row.Attribute("r")! == number);

    private int ColumnOf(string sheet, string heading)
    {
        var header = HeaderRow(sheet);
        var row = RowElement(sheet, header);
        for (var column = 1; column <= 40; column++)
        {
            if (string.Equals(CellText(row, column, header)?.Trim(), heading, StringComparison.OrdinalIgnoreCase))
            {
                return column;
            }
        }

        throw new InvalidOperationException($"The sheet {sheet} has no column headed \"{heading}\".");
    }

    private int RowOf(string sheet, string key)
    {
        var keys = ColumnOf(sheet, LoadWorkbook.KeyHeader);
        return Sheet(sheet).Descendants(Main + "row").Select(row => (int)row.Attribute("r")!).FirstOrDefault(number => CellText(RowElement(sheet, number), keys, number) == key) is > 0 and var found
            ? found
            : throw new InvalidOperationException($"The sheet {sheet} has no row with the key {key}.");
    }

    private static XElement? Cell(XElement row, int column, int rowNumber) =>
        row.Elements(Main + "c").FirstOrDefault(cell => (string?)cell.Attribute("r") == $"{XlsxWriter.Column(column - 1)}{rowNumber.ToString(CultureInfo.InvariantCulture)}");

    private static string? CellText(XElement row, int column, int rowNumber)
    {
        var cell = Cell(row, column, rowNumber);
        return cell is null ? null
            : (string?)cell.Attribute("t") == "inlineStr" ? string.Concat(cell.Descendants(Main + "t").Select(item => item.Value))
            : cell.Element(Main + "v")?.Value;
    }

    private FilledWorkbook SetCell(string sheet, int rowNumber, int column, string? text, bool number)
    {
        var row = RowElement(sheet, rowNumber);
        var reference = $"{XlsxWriter.Column(column - 1)}{rowNumber.ToString(CultureInfo.InvariantCulture)}";
        var cell = Cell(row, column, rowNumber);
        if (cell is null)
        {
            cell = new XElement(Main + "c", new XAttribute("r", reference));
            var after = row.Elements(Main + "c").LastOrDefault(other => ColumnNumber((string)other.Attribute("r")!) < column);
            if (after is null)
            {
                row.AddFirst(cell);
            }
            else
            {
                after.AddAfterSelf(cell);
            }
        }

        cell.RemoveNodes();
        cell.Attribute("t")?.Remove();
        if (text is null)
        {
            return this;
        }

        if (number)
        {
            cell.Add(new XElement(Main + "v", text));
        }
        else
        {
            cell.SetAttributeValue("t", "inlineStr");
            cell.Add(new XElement(Main + "is", new XElement(Main + "t", new XAttribute(XNamespace.Xml + "space", "preserve"), text)));
        }

        return this;
    }

    private static int ColumnNumber(string reference)
    {
        var column = 0;
        foreach (var letter in reference.TakeWhile(char.IsAsciiLetter))
        {
            column = (column * 26) + (char.ToUpperInvariant(letter) - 'A' + 1);
        }

        return column;
    }
}
