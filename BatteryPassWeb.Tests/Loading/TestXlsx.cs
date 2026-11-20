using System.IO.Compression;
using System.Text;

namespace BatteryPassWeb.Tests.Loading;

/// <summary>Small Excel workbooks for the reader's tests, written part by part as Excel or a script would.</summary>
internal static class TestXlsx
{
    public const string Main = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";

    public const string ContentTypes = """
        <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
        <Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types"><Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/><Default Extension="xml" ContentType="application/xml"/><Override PartName="/xl/workbook.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml"/></Types>
        """;

    public const string Workbook = """
        <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
        <workbook xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships"><sheets><sheet name="Data" sheetId="1" r:id="rId1"/></sheets></workbook>
        """;

    /// <summary>The workbook's links as Excel writes them: beside the workbook part, with the shared texts.</summary>
    public const string Rels = """
        <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
        <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships"><Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet" Target="worksheets/sheet1.xml"/><Relationship Id="rId2" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/sharedStrings" Target="sharedStrings.xml"/></Relationships>
        """;

    /// <summary>A zip of these parts, in this order.</summary>
    public static byte[] Zip(params (string Path, string Content)[] parts)
    {
        using var buffer = new MemoryStream();
        using (var zip = new ZipArchive(buffer, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var (path, content) in parts)
            {
                using var writer = new StreamWriter(zip.CreateEntry(path, CompressionLevel.Optimal).Open(), new UTF8Encoding(false));
                writer.Write(content.Trim());
            }
        }

        return buffer.ToArray();
    }

    /// <summary>A one-sheet workbook ("Data") with this sheet data and, when given, these shared texts.</summary>
    public static byte[] OneSheet(string sheetData, string? sharedStrings = null, string? rels = null)
    {
        var parts = new List<(string, string)>
        {
            ("[Content_Types].xml", ContentTypes),
            ("xl/workbook.xml", Workbook),
            ("xl/_rels/workbook.xml.rels", rels ?? Rels),
            ("xl/worksheets/sheet1.xml", $"<worksheet xmlns=\"{Main}\"><sheetData>{sheetData}</sheetData></worksheet>"),
        };
        if (sharedStrings is not null)
        {
            parts.Add(("xl/sharedStrings.xml", $"<sst xmlns=\"{Main}\">{sharedStrings}</sst>"));
        }

        return Zip([.. parts]);
    }
}
