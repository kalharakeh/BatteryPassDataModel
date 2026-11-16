using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Xml;

namespace BatteryPassWeb.Dpp.Loading;

/// <summary>What a cell's format in Excel says about the number it holds, beyond the number itself.</summary>
public enum CellNote
{
    None,

    /// <summary>Formatted as a per-cent: <see cref="XlsxCell.Text"/> is the number shown (16 for 16 %), not the one stored (0.16).</summary>
    Percent,

    /// <summary>Formatted as a date or a time: Excel stores a day count, which is not what the owner sees.</summary>
    Date,

    /// <summary>A formula that was saved without its value: there is nothing to read.</summary>
    FormulaWithoutValue,

    /// <summary>A number too large to be any value of the passport.</summary>
    TooLarge,
}

/// <summary>One filled cell: its text, and whether Excel stored it as a number.</summary>
/// <param name="Text">A text as typed; a number as the owner sees it, in its shortest form (4.7, not 4.7000000000000002; 16 for 16 %).</param>
/// <param name="IsError">The cell holds an Excel error such as #N/A.</param>
public sealed record XlsxCell(string Text, bool IsNumber, bool IsError = false)
{
    public CellNote Note { get; init; }
}

/// <summary>One sheet: its filled cells by row and column, both counted from 1.</summary>
public sealed class XlsxSheet(string name, IReadOnlyDictionary<(int Row, int Column), XlsxCell> cells)
{
    public string Name { get; } = name;

    public IReadOnlyDictionary<(int Row, int Column), XlsxCell> Cells { get; } = cells;

    public int LastRow { get; } = cells.Count == 0 ? 0 : cells.Keys.Max(key => key.Row);

    public int LastColumn { get; } = cells.Count == 0 ? 0 : cells.Keys.Max(key => key.Column);

    /// <summary>The rows that hold a filled cell, top to bottom.</summary>
    public IReadOnlyList<int> Rows { get; } = cells.Keys.Select(key => key.Row).Distinct().Order().ToArray();

    public XlsxCell? Cell(int row, int column) => Cells.GetValueOrDefault((row, column));

    /// <summary>The cell's text, trimmed; empty when the cell is empty.</summary>
    public string Text(int row, int column) => Cell(row, column)?.Text.Trim() ?? string.Empty;
}

public sealed class XlsxBook(IReadOnlyList<XlsxSheet> sheets)
{
    public IReadOnlyList<XlsxSheet> Sheets { get; } = sheets;

    public XlsxSheet? Sheet(string name) =>
        Sheets.FirstOrDefault(sheet => string.Equals(sheet.Name.Trim(), name, StringComparison.OrdinalIgnoreCase));
}

/// <summary>
/// Reads an Excel workbook (.xlsx, Office Open XML) with .NET's own zip and XML support, as <c>XlsxWriter</c> writes one:
/// no package is needed. It reads what a person sees: texts (shared or inline), numbers, and a formula's last value.
/// It is guarded against a file made to harm:
/// <list type="bullet">
/// <item>only a real .xlsx (a zip with a workbook part; not .xls, not a macro workbook, not a protected one);</item>
/// <item>a limit on the file's size, on the number of parts and on what they unpack to, counted while unpacking;</item>
/// <item>no document type definitions, and nothing outside the file is ever opened (external links are not followed);</item>
/// <item>a formula is never worked out: only the value Excel saved with it is read.</item>
/// </list>
/// </summary>
public static class XlsxReader
{
    /// <summary>The largest workbook file read.</summary>
    public const int MaxBytes = 5 * 1024 * 1024;

    /// <summary>The most parts a workbook's zip may hold.</summary>
    public const int MaxEntries = 200;

    /// <summary>The most the parts that are read may unpack to, all together.</summary>
    public const long MaxUnpackedBytes = 40L * 1024 * 1024;

    /// <summary>The most filled cells read from one workbook.</summary>
    public const int MaxCells = 200_000;

    public const string NotAWorkbook = "this isn't an Excel workbook (.xlsx). Save it in Excel as \"Excel Workbook (*.xlsx)\" and choose it again";
    public const string TooLarge = "the workbook is larger than 5 MB. The product data workbook is far smaller; choose the right file";
    public const string TooMuchInside = "the workbook holds more than this page reads. Choose the product data workbook";
    public const string Damaged = "the workbook can't be read: it is damaged. Open it in Excel, save it again as .xlsx and choose it again";
    public const string OutsideTheSheet = "the workbook has a cell beyond Excel's last row or column, so Excel didn't save it this way. Open it in Excel, save it again as .xlsx and choose it again";

    /// <summary>Excel's last row and column (XFD1048576).</summary>
    public const int MaxRow = 1_048_576;

    public const int MaxColumn = 16_384;

    /// <summary>The most filled cells read from one sheet.</summary>
    public const int MaxCellsPerSheet = 100_000;

    private const string MainNamespace = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
    private const string RelationshipNamespace = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
    private const string PackageRelationshipNamespace = "http://schemas.openxmlformats.org/package/2006/relationships";
    private const string WorkbookContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml";
    private const string WorkbookPart = "xl/workbook.xml";

    /// <summary>The workbook, or why it can't be read (one of the sentences above, to finish "The workbook wasn't read: …").</summary>
    public static (XlsxBook? Book, string? Problem) Read(byte[] content)
    {
        ArgumentNullException.ThrowIfNull(content);
        if (content.Length > MaxBytes)
        {
            return (null, TooLarge);
        }

        // A zip starts with "PK"; an old .xls and a password-protected workbook start with D0 CF 11 E0.
        if (content.Length < 4 || content[0] != (byte)'P' || content[1] != (byte)'K')
        {
            return (null, NotAWorkbook);
        }

        try
        {
            using var zip = new ZipArchive(new MemoryStream(content, writable: false), ZipArchiveMode.Read);
            if (zip.Entries.Count > MaxEntries)
            {
                return (null, TooMuchInside);
            }

            var parts = new Parts(zip);
            if (!parts.Has("[Content_Types].xml") || !parts.Has(WorkbookPart)
                || !parts.Text("[Content_Types].xml").Contains(WorkbookContentType, StringComparison.OrdinalIgnoreCase))
            {
                return (null, NotAWorkbook);
            }

            var targets = SheetTargets(parts);
            var shared = SharedStrings(parts, targets.SharedStrings);
            var styles = Styles(parts, targets.Styles);
            var sheets = new List<XlsxSheet>();
            var cells = 0;
            foreach (var (name, path) in targets.Sheets)
            {
                var read = ReadSheet(parts, path, shared, styles);
                cells += read.Count;
                if (cells > MaxCells)
                {
                    return (null, TooMuchInside);
                }

                sheets.Add(new XlsxSheet(name, read));
            }

            return sheets.Count == 0 ? (null, NotAWorkbook) : (new XlsxBook(sheets), null);
        }
        catch (TooMuchException)
        {
            return (null, TooMuchInside);
        }
        catch (OutsideException)
        {
            return (null, OutsideTheSheet);
        }
        catch (Exception exception) when (exception is InvalidDataException or XmlException or FormatException or IOException or NotSupportedException or ArgumentException or OverflowException)
        {
            return (null, Damaged);
        }
    }

    /// <summary>The sheets in the workbook's own order, each with the part that holds it, and the shared texts' part.</summary>
    private static (IReadOnlyList<(string Name, string Path)> Sheets, string? SharedStrings, string? Styles) SheetTargets(Parts parts)
    {
        // The workbook's own links: only those inside the file count; one that points outside it is never opened.
        var inside = new Dictionary<string, string>(StringComparer.Ordinal);
        string? sharedStrings = null;
        string? styles = null;
        if (parts.Has("xl/_rels/workbook.xml.rels"))
        {
            using var reader = parts.Xml("xl/_rels/workbook.xml.rels");
            while (reader.Read())
            {
                if (reader.NodeType != XmlNodeType.Element || reader.LocalName != "Relationship" || reader.NamespaceURI != PackageRelationshipNamespace)
                {
                    continue;
                }

                var id = reader.GetAttribute("Id");
                var target = reader.GetAttribute("Target");
                if (id is null || target is null || string.Equals(reader.GetAttribute("TargetMode"), "External", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (PartPath(target) is not { } path)
                {
                    continue;
                }

                inside[id] = path;
                if ((reader.GetAttribute("Type") ?? string.Empty).EndsWith("/sharedStrings", StringComparison.Ordinal))
                {
                    sharedStrings = path;
                }
                else if ((reader.GetAttribute("Type") ?? string.Empty).EndsWith("/styles", StringComparison.Ordinal))
                {
                    styles = path;
                }
            }
        }

        var sheets = new List<(string, string)>();
        using (var reader = parts.Xml(WorkbookPart))
        {
            while (reader.Read())
            {
                if (reader.NodeType == XmlNodeType.Element && reader.LocalName == "sheet" && reader.NamespaceURI == MainNamespace
                    && reader.GetAttribute("name") is { } name
                    && reader.GetAttribute("id", RelationshipNamespace) is { } id
                    && inside.TryGetValue(id, out var path)
                    && parts.Has(path))
                {
                    sheets.Add((name, path));
                }
            }
        }

        return (sheets, sharedStrings is not null && parts.Has(sharedStrings) ? sharedStrings : null, styles is not null && parts.Has(styles) ? styles : null);
    }

    /// <summary>
    /// A link's target as a part of this file: "/xl/worksheets/sheet1.xml" (from the file's root) or
    /// "worksheets/sheet1.xml" (beside the workbook part). Null for a target that would leave the file.
    /// </summary>
    private static string? PartPath(string target)
    {
        var path = target.Replace('\\', '/');
        var segments = new List<string>();
        foreach (var segment in (path.StartsWith('/') ? path : "xl/" + path).Split('/', StringSplitOptions.RemoveEmptyEntries))
        {
            if (segment == ".")
            {
                continue;
            }

            if (segment == "..")
            {
                if (segments.Count == 0)
                {
                    return null;
                }

                segments.RemoveAt(segments.Count - 1);
                continue;
            }

            segments.Add(segment);
        }

        return segments.Count == 0 || path.Contains(':', StringComparison.Ordinal) ? null : string.Join('/', segments);
    }

    private static IReadOnlyList<string> SharedStrings(Parts parts, string? path)
    {
        var texts = new List<string>();
        if (path is null)
        {
            return texts;
        }

        using var reader = parts.Xml(path);
        while (reader.Read())
        {
            if (reader.NodeType == XmlNodeType.Element && reader.LocalName == "si" && reader.NamespaceURI == MainNamespace)
            {
                texts.Add(RichText(reader));
            }
        }

        return texts;
    }

    /// <summary>
    /// The text inside a shared or inline string: its plain text, or its formatted runs put together. The phonetic
    /// reading some languages add (rPh) is not part of what the cell shows.
    /// </summary>
    private static string RichText(XmlReader reader)
    {
        var text = new StringBuilder();
        if (reader.IsEmptyElement)
        {
            return string.Empty;
        }

        var depth = reader.Depth;
        var phonetic = -1;
        while (reader.Read() && !(reader.NodeType == XmlNodeType.EndElement && reader.Depth == depth))
        {
            if (reader.NodeType == XmlNodeType.Element && reader.LocalName == "rPh" && !reader.IsEmptyElement)
            {
                phonetic = reader.Depth;
            }
            else if (reader.NodeType == XmlNodeType.EndElement && reader.Depth == phonetic)
            {
                phonetic = -1;
            }
            else if (phonetic < 0 && reader.NodeType == XmlNodeType.Element && reader.LocalName == "t" && reader.NamespaceURI == MainNamespace && !reader.IsEmptyElement)
            {
                text.Append(ElementText(reader));
            }
        }

        return text.ToString();
    }

    /// <summary>The text of the element the reader is on; the reader ends on its end tag.</summary>
    private static string ElementText(XmlReader reader)
    {
        var text = new StringBuilder();
        var depth = reader.Depth;
        while (reader.Read() && !(reader.NodeType == XmlNodeType.EndElement && reader.Depth == depth))
        {
            if (reader.NodeType is XmlNodeType.Text or XmlNodeType.CDATA or XmlNodeType.Whitespace or XmlNodeType.SignificantWhitespace)
            {
                text.Append(reader.Value);
            }
        }

        return text.ToString();
    }

    /// <summary>
    /// What each cell style's number format says (by the style's index, as a cell names it): a per-cent, a date or
    /// time, or nothing to change. Excel's built-in formats by number; the workbook's own by their code.
    /// </summary>
    private static IReadOnlyList<CellNote> Styles(Parts parts, string? path)
    {
        var notes = new List<CellNote>();
        if (path is null)
        {
            return notes;
        }

        var codes = new Dictionary<int, string>();
        var inCellStyles = false;
        using var reader = parts.Xml(path);
        while (reader.Read())
        {
            if (reader.NamespaceURI != MainNamespace)
            {
                continue;
            }

            if (reader.NodeType == XmlNodeType.Element && reader.LocalName == "numFmt"
                && int.TryParse(reader.GetAttribute("numFmtId"), NumberStyles.None, CultureInfo.InvariantCulture, out var id))
            {
                codes[id] = reader.GetAttribute("formatCode") ?? string.Empty;
            }
            else if (reader.LocalName == "cellXfs")
            {
                inCellStyles = reader.NodeType == XmlNodeType.Element && !reader.IsEmptyElement;
            }
            else if (inCellStyles && reader.NodeType == XmlNodeType.Element && reader.LocalName == "xf")
            {
                var format = int.TryParse(reader.GetAttribute("numFmtId"), NumberStyles.None, CultureInfo.InvariantCulture, out var number) ? number : 0;
                notes.Add(NoteOf(format, codes.GetValueOrDefault(format)));
            }
        }

        return notes;
    }

    /// <summary>A number format as a per-cent, a date or time, or neither.</summary>
    public static CellNote NoteOf(int format, string? code)
    {
        if (code is null)
        {
            return format switch
            {
                9 or 10 => CellNote.Percent,
                (>= 14 and <= 22) or (>= 27 and <= 36) or (>= 45 and <= 47) or (>= 50 and <= 58) => CellNote.Date,
                _ => CellNote.None,
            };
        }

        // What the code shows, without its quoted texts, escaped characters and [colour] or [condition] parts.
        var shown = new StringBuilder();
        var elapsed = false;
        for (var index = 0; index < code.Length; index++)
        {
            var character = code[index];
            if (character == '"')
            {
                index = code.IndexOf('"', index + 1) is var end and >= 0 ? end : code.Length;
            }
            else if (character is '\\' or '_' or '*')
            {
                index++;
            }
            else if (character == '[')
            {
                var end = code.IndexOf(']', index + 1);
                var inside = end < 0 ? string.Empty : code[(index + 1)..end].ToLowerInvariant();
                elapsed |= inside.Length > 0 && inside.All(letter => letter is 'h' or 'm' or 's');
                index = end < 0 ? code.Length : end;
            }
            else
            {
                shown.Append(char.ToLowerInvariant(character));
            }
        }

        var text = shown.ToString();
        return text.Contains('%', StringComparison.Ordinal) ? CellNote.Percent
            : elapsed || (text != "general" && text.IndexOfAny(['d', 'm', 'y', 'h', 's']) >= 0) ? CellNote.Date
            : CellNote.None;
    }

    private static Dictionary<(int Row, int Column), XlsxCell> ReadSheet(Parts parts, string path, IReadOnlyList<string> shared, IReadOnlyList<CellNote> styles)
    {
        var cells = new Dictionary<(int, int), XlsxCell>();
        using var reader = parts.Xml(path);
        var row = 0;
        var column = 0;
        while (reader.Read())
        {
            if (reader.NodeType != XmlNodeType.Element || reader.NamespaceURI != MainNamespace)
            {
                continue;
            }

            if (reader.LocalName == "row")
            {
                // A row or cell without its reference follows the one before it.
                row = int.TryParse(reader.GetAttribute("r"), NumberStyles.None, CultureInfo.InvariantCulture, out var number) ? number : row + 1;
                column = 0;
                if (row is < 1 or > MaxRow)
                {
                    throw new OutsideException();
                }
            }
            else if (reader.LocalName == "c")
            {
                (row, column) = Position(reader.GetAttribute("r"), row, column + 1);
                if (row is < 1 or > MaxRow || column is < 1 or > MaxColumn)
                {
                    throw new OutsideException();
                }

                var type = reader.GetAttribute("t") ?? "n";
                var style = int.TryParse(reader.GetAttribute("s"), NumberStyles.None, CultureInfo.InvariantCulture, out var index) && index < styles.Count ? styles[index] : CellNote.None;
                if (ReadCell(reader, type, shared, style) is { } cell)
                {
                    if (cells.Count >= MaxCellsPerSheet)
                    {
                        throw new TooMuchException();
                    }

                    cells[(row, column)] = cell;
                }
            }
        }

        return cells;
    }

    /// <summary>"AB12" as row 12, column 28; without a reference, the next cell of the current row.</summary>
    private static (int Row, int Column) Position(string? reference, int row, int nextColumn)
    {
        if (string.IsNullOrEmpty(reference))
        {
            return (row, nextColumn);
        }

        var column = 0;
        var index = 0;
        while (index < reference.Length && char.IsAsciiLetter(reference[index]))
        {
            column = (column * 26) + (char.ToUpperInvariant(reference[index]) - 'A' + 1);
            if (column > MaxColumn)
            {
                throw new OutsideException();
            }

            index++;
        }

        return column > 0 && int.TryParse(reference.AsSpan(index), NumberStyles.None, CultureInfo.InvariantCulture, out var number)
            ? (number, column)
            : column > 0 ? throw new OutsideException() : (row, nextColumn);
    }

    /// <summary>The cell the reader is on; null when it is empty. A formula's text is skipped: only its saved value counts.</summary>
    private static XlsxCell? ReadCell(XmlReader reader, string type, IReadOnlyList<string> shared, CellNote style)
    {
        if (reader.IsEmptyElement)
        {
            return null;
        }

        string? value = null;
        string? inline = null;
        var formula = false;
        var depth = reader.Depth;
        while (reader.Read() && !(reader.NodeType == XmlNodeType.EndElement && reader.Depth == depth))
        {
            if (reader.NodeType != XmlNodeType.Element || reader.NamespaceURI != MainNamespace)
            {
                continue;
            }

            if (reader.LocalName == "v" && !reader.IsEmptyElement)
            {
                value = ElementText(reader);
            }
            else if (reader.LocalName == "is")
            {
                inline = RichText(reader);
            }
            else if (reader.LocalName == "f")
            {
                formula = true; // its text is never read, let alone worked out: only the value saved with it counts
            }
        }

        if (formula && string.IsNullOrEmpty(value) && string.IsNullOrEmpty(inline))
        {
            return new XlsxCell(string.Empty, IsNumber: false) { Note = CellNote.FormulaWithoutValue };
        }

        switch (type)
        {
            case "inlineStr":
                return Filled(inline ?? value, isNumber: false);
            case "s":
                return value is not null && int.TryParse(value.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var index) && index < shared.Count
                    ? Filled(shared[index], isNumber: false)
                    : null;
            case "str":
                return Filled(value, isNumber: false);
            case "b":
                return value is null ? null : new XlsxCell(value.Trim() == "1" ? "TRUE" : "FALSE", IsNumber: false);
            case "e":
                return value is null ? null : new XlsxCell(value.Trim(), IsNumber: false, IsError: true);
            default:
                return string.IsNullOrWhiteSpace(value)
                    ? null
                    : double.TryParse(value.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var number) && double.IsFinite(number)
                        ? Number(number, style)
                        : new XlsxCell(value.Trim(), IsNumber: false);
        }
    }

    /// <summary>The largest number read as a value; above it a cell is <see cref="CellNote.TooLarge"/>.</summary>
    private const double Largest = 1e15;

    /// <summary>
    /// A stored number as the owner sees it. Excel writes 4.7 as 4.7000000000000002, so the number is written back in its
    /// shortest form; a per-cent cell shows 16 where 0.16 is stored, so it is read as 16, exactly in decimal.
    /// </summary>
    private static XlsxCell Number(double number, CellNote style)
    {
        if (Math.Abs(number) >= Largest)
        {
            return new XlsxCell(number.ToString("R", CultureInfo.InvariantCulture), IsNumber: true) { Note = CellNote.TooLarge };
        }

        var text = NumberText(number);
        if (style == CellNote.Percent)
        {
            var shown = decimal.Parse(text, NumberStyles.Float, CultureInfo.InvariantCulture) * 100m;
            return new XlsxCell(Plain(shown), IsNumber: true) { Note = CellNote.Percent };
        }

        return new XlsxCell(text, IsNumber: true) { Note = style };
    }

    /// <summary>A number as a person would type it: 470, 0.5, -20; never 4.7E+2. For numbers below 1e15.</summary>
    public static string NumberText(double number)
    {
        var text = number.ToString("R", CultureInfo.InvariantCulture);
        return text.Contains('E', StringComparison.Ordinal) ? Plain((decimal)number) : text;
    }

    /// <summary>A decimal without trailing zeros: 16.00 is 16, 0.50 is 0.5.</summary>
    private static string Plain(decimal number) => (number / 1.0000000000000000000000000000m).ToString(CultureInfo.InvariantCulture);

    private static XlsxCell? Filled(string? text, bool isNumber) =>
        string.IsNullOrEmpty(text) ? null : new XlsxCell(text, isNumber);

    private sealed class TooMuchException : Exception;

    private sealed class OutsideException : Exception;

    /// <summary>The file's parts, read with a running count of what was unpacked.</summary>
    private sealed class Parts(ZipArchive zip)
    {
        private static readonly XmlReaderSettings Settings = new()
        {
            DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null,
            IgnoreComments = true,
            IgnoreProcessingInstructions = true,
            CloseInput = true,
        };

        private readonly Dictionary<string, ZipArchiveEntry> _entries = zip.Entries
            .GroupBy(entry => entry.FullName.Replace('\\', '/').TrimStart('/'), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);

        private long _unpacked;

        public bool Has(string path) => _entries.ContainsKey(path);

        public XmlReader Xml(string path) => XmlReader.Create(new Counted(_entries[path].Open(), this), Settings);

        public string Text(string path)
        {
            using var reader = new StreamReader(new Counted(_entries[path].Open(), this), Encoding.UTF8);
            return reader.ReadToEnd();
        }

        private void Add(int bytes)
        {
            _unpacked += bytes;
            if (_unpacked > MaxUnpackedBytes)
            {
                throw new TooMuchException();
            }
        }

        /// <summary>Counts the bytes as they are unpacked: a part's stated size is never trusted.</summary>
        private sealed class Counted(Stream inner, Parts parts) : Stream
        {
            public override bool CanRead => true;

            public override bool CanSeek => false;

            public override bool CanWrite => false;

            public override long Length => throw new NotSupportedException();

            public override long Position
            {
                get => throw new NotSupportedException();
                set => throw new NotSupportedException();
            }

            public override int Read(byte[] buffer, int offset, int count)
            {
                var read = inner.Read(buffer, offset, count);
                parts.Add(read);
                return read;
            }

            public override void Flush()
            {
            }

            public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

            public override void SetLength(long value) => throw new NotSupportedException();

            public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

            protected override void Dispose(bool disposing)
            {
                if (disposing)
                {
                    inner.Dispose();
                }

                base.Dispose(disposing);
            }
        }
    }
}
