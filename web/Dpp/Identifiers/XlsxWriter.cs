using System.Globalization;
using System.IO.Compression;
using System.Security;
using System.Text;

namespace BatteryPassWeb.Dpp.Identifiers;

/// <summary>
/// A one-sheet Excel workbook (.xlsx, Office Open XML) written with .NET's own zip support, so no package is needed (step
/// 6d). It opens the same on every PC, whatever its list separator, unlike a CSV:
/// <list type="bullet">
/// <item>a bold header row and columns wide enough for what they hold;</item>
/// <item>every text as a text cell, never a formula or a number: the serial number 000123 and 2026-11 stay as written,
/// and "=…" stays text;</item>
/// <item>a <see cref="DateTime"/> as a real date cell, shown as yyyy-mm-dd hh:mm.</item>
/// </list>
/// </summary>
public static class XlsxWriter
{
    public const string ContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    /// <summary>The date cells' format and the style index that uses it.</summary>
    public const string DateFormat = "yyyy-mm-dd hh:mm";

    private const int HeaderStyle = 1;
    private const int DateStyle = 2;
    private const int DateWidth = 18;

    /// <param name="rows">Each cell a <see cref="string"/> (a text cell) or a <see cref="DateTime"/> (a date cell); null is empty.</param>
    public static byte[] Write(string sheetName, IReadOnlyList<string> headers, IReadOnlyList<IReadOnlyList<object?>> rows)
    {
        ArgumentNullException.ThrowIfNull(headers);
        ArgumentNullException.ThrowIfNull(rows);
        using var buffer = new MemoryStream();
        using (var zip = new ZipArchive(buffer, ZipArchiveMode.Create, leaveOpen: true))
        {
            Add(zip, "[Content_Types].xml", """
                <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
                <Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types"><Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/><Default Extension="xml" ContentType="application/xml"/><Override PartName="/xl/workbook.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml"/><Override PartName="/xl/worksheets/sheet1.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml"/><Override PartName="/xl/styles.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml"/></Types>
                """);
            Add(zip, "_rels/.rels", """
                <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
                <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships"><Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="xl/workbook.xml"/></Relationships>
                """);
            Add(zip, "xl/workbook.xml", $"""
                <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
                <workbook xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships"><sheets><sheet name="{Escape(sheetName)}" sheetId="1" r:id="rId1"/></sheets></workbook>
                """);
            Add(zip, "xl/_rels/workbook.xml.rels", """
                <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
                <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships"><Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet" Target="worksheets/sheet1.xml"/><Relationship Id="rId2" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles" Target="styles.xml"/></Relationships>
                """);
            Add(zip, "xl/styles.xml", $"""
                <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
                <styleSheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main"><numFmts count="1"><numFmt numFmtId="164" formatCode="{DateFormat}"/></numFmts><fonts count="2"><font><sz val="11"/><name val="Calibri"/></font><font><b/><sz val="11"/><name val="Calibri"/></font></fonts><fills count="2"><fill><patternFill patternType="none"/></fill><fill><patternFill patternType="gray125"/></fill></fills><borders count="1"><border/></borders><cellStyleXfs count="1"><xf/></cellStyleXfs><cellXfs count="3"><xf fontId="0"/><xf fontId="1" applyFont="1"/><xf fontId="0" numFmtId="164" applyNumberFormat="1"/></cellXfs></styleSheet>
                """);
            Add(zip, "xl/worksheets/sheet1.xml", Sheet(headers, rows));
        }

        return buffer.ToArray();
    }

    private static string Sheet(IReadOnlyList<string> headers, IReadOnlyList<IReadOnlyList<object?>> rows)
    {
        var sheet = new StringBuilder("""<?xml version="1.0" encoding="UTF-8" standalone="yes"?><worksheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main"><cols>""");
        for (var column = 0; column < headers.Count; column++)
        {
            sheet.Append(CultureInfo.InvariantCulture, $"<col min=\"{column + 1}\" max=\"{column + 1}\" width=\"{Width(column, headers, rows)}\" customWidth=\"1\"/>");
        }

        sheet.Append("</cols><sheetData>");
        Row(sheet, 1, headers, header: true);
        for (var index = 0; index < rows.Count; index++)
        {
            Row(sheet, index + 2, rows[index], header: false);
        }

        return sheet.Append("</sheetData></worksheet>").ToString();
    }

    /// <summary>A column wide enough for its longest text (within 10 and 60 characters), or for a date.</summary>
    private static int Width(int column, IReadOnlyList<string> headers, IReadOnlyList<IReadOnlyList<object?>> rows)
    {
        var longest = Math.Max(headers[column].Length, rows.Select(row => column < row.Count ? row[column] switch
        {
            DateTime => DateWidth,
            string text => text.Length,
            _ => 0,
        } : 0).DefaultIfEmpty(0).Max());
        return Math.Clamp(longest + 2, 10, 60);
    }

    private static void Row(StringBuilder sheet, int number, IEnumerable<object?> cells, bool header)
    {
        sheet.Append(CultureInfo.InvariantCulture, $"<row r=\"{number}\">");
        var column = 0;
        foreach (var cell in cells)
        {
            var reference = $"{Column(column++)}{number.ToString(CultureInfo.InvariantCulture)}";
            switch (cell)
            {
                case DateTime date:
                    sheet.Append(CultureInfo.InvariantCulture, $"<c r=\"{reference}\" s=\"{DateStyle}\"><v>{date.ToOADate().ToString("R", CultureInfo.InvariantCulture)}</v></c>");
                    break;
                case string text:
                    sheet.Append(CultureInfo.InvariantCulture, $"<c r=\"{reference}\" t=\"inlineStr\"{(header ? $" s=\"{HeaderStyle}\"" : string.Empty)}><is><t xml:space=\"preserve\">{Escape(text)}</t></is></c>");
                    break;
            }
        }

        sheet.Append("</row>");
    }

    /// <summary>A, B, …, Z, AA, AB, … for a zero-based column.</summary>
    public static string Column(int index)
    {
        var name = string.Empty;
        for (var number = index + 1; number > 0; number = (number - 1) / 26)
        {
            name = (char)('A' + ((number - 1) % 26)) + name;
        }

        return name;
    }

    private static void Add(ZipArchive zip, string path, string text)
    {
        using var writer = new StreamWriter(zip.CreateEntry(path, CompressionLevel.Optimal).Open(), new UTF8Encoding(false));
        writer.Write(text.Trim());
    }

    private static string Escape(string? text) => SecurityElement.Escape(text ?? string.Empty);
}
