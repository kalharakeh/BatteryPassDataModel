using System.IO.Compression;
using System.Text;
using BatteryPassWeb.Dpp.Identifiers;
using BatteryPassWeb.Dpp.Loading;

namespace BatteryPassWeb.Tests.Loading;

/// <summary>
/// Package X1, point 8: the workbook is read with .NET's own zip and XML support, both as Excel saves it (shared
/// texts, numbers as numbers) and as a script writes it (inline texts), and the reader is guarded.
/// </summary>
public sealed class XlsxReaderTests
{
    [Fact]
    public void Read_ShouldReadSharedTextsNumbersAndInlineTexts_ByRowAndColumn()
    {
        var workbook = TestXlsx.OneSheet(
            """
            <row r="1"><c r="A1" t="s"><v>0</v></c><c r="C1" t="inlineStr"><is><t>Inline text</t></is></c></row>
            <row r="3"><c r="B3"><v>470</v></c><c r="AB3" t="s"><v>1</v></c></row>
            """,
            "<si><t>Shared text</t></si><si><r><t>Two </t></r><r><rPr><b/></rPr><t>runs</t></r><rPh><t>phonetic</t></rPh></si>");

        var (book, problem) = XlsxReader.Read(workbook);

        Assert.Null(problem);
        var sheet = Assert.Single(book!.Sheets);
        Assert.Equal("Data", sheet.Name);
        Assert.Equal(new XlsxCell("Shared text", false), sheet.Cell(1, 1));
        Assert.Equal(new XlsxCell("Inline text", false), sheet.Cell(1, 3));
        Assert.Equal(new XlsxCell("470", true), sheet.Cell(3, 2));
        Assert.Equal("Two runs", sheet.Text(3, 28));
        Assert.Null(sheet.Cell(2, 1));
        Assert.Equal(3, sheet.LastRow);
        Assert.Equal(28, sheet.LastColumn);
    }

    [Theory]
    [InlineData("4.7000000000000002", "4.7")]
    [InlineData("0.5", "0.5")]
    [InlineData("-20", "-20")]
    [InlineData("280000", "280000")]
    [InlineData("2.8E5", "280000")]
    [InlineData("1E-3", "0.001")]
    public void ANumber_ShouldBeReadInItsShortestForm_AsAPersonWouldTypeIt(string stored, string expected)
    {
        var (book, _) = XlsxReader.Read(TestXlsx.OneSheet($"<row r=\"1\"><c r=\"A1\"><v>{stored}</v></c></row>"));

        Assert.Equal(new XlsxCell(expected, true), book!.Sheets[0].Cell(1, 1));
    }

    [Fact]
    public void AFormula_ShouldBeReadAsTheValueExcelSavedWithIt_AndNeverWorkedOut()
    {
        var workbook = TestXlsx.OneSheet(
            """
            <row r="1"><c r="A1"><f>1+1</f><v>2</v></c><c r="B1" t="str"><f>CONCAT("a","b")</f><v>ab</v></c><c r="C1"><f>HYPERLINK("https://outside.example/x")</f></c><c r="D1" t="b"><v>1</v></c><c r="E1" t="e"><f>1/0</f><v>#DIV/0!</v></c></row>
            """);

        var (book, problem) = XlsxReader.Read(workbook);

        Assert.Null(problem);
        var sheet = book!.Sheets[0];
        Assert.Equal(new XlsxCell("2", true), sheet.Cell(1, 1));
        Assert.Equal(new XlsxCell("ab", false), sheet.Cell(1, 2));
        Assert.Null(sheet.Cell(1, 3)); // a formula without a saved value is an empty cell
        Assert.Equal("TRUE", sheet.Text(1, 4));
        Assert.True(sheet.Cell(1, 5)!.IsError);
    }

    [Fact]
    public void Read_ShouldFollowLinksInsideTheFileOnly_WhetherFromItsRootOrBesideTheWorkbook()
    {
        // As the script wrote the owner's workbook: targets from the file's root. An external link is never opened.
        var rels = """
            <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships"><Relationship Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet" Target="/xl/worksheets/sheet1.xml" Id="rId1"/><Relationship Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/externalLink" Target="file:///C:/secrets/other.xlsx" TargetMode="External" Id="rId9"/></Relationships>
            """;
        var workbook = TestXlsx.OneSheet("<row r=\"1\"><c r=\"A1\" t=\"inlineStr\"><is><t>here</t></is></c></row>", rels: rels);

        var (book, problem) = XlsxReader.Read(workbook);

        Assert.Null(problem);
        Assert.Equal("here", book!.Sheets[0].Text(1, 1));
    }

    [Fact]
    public void ASheetLinkedOutsideTheFile_ShouldNotBeRead()
    {
        var rels = """
            <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships"><Relationship Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet" Target="../../outside.xml" Id="rId1"/></Relationships>
            """;

        var (book, problem) = XlsxReader.Read(TestXlsx.OneSheet("<row r=\"1\"><c r=\"A1\"><v>1</v></c></row>", rels: rels));

        Assert.Null(book);
        Assert.Equal(XlsxReader.NotAWorkbook, problem);
    }

    [Fact]
    public void TheAppsOwnExcelFiles_ShouldReadBack()
    {
        var written = XlsxWriter.Write("IDs", ["Serial number", "Released"], [["000123", new DateTime(2026, 11, 16, 9, 30, 0, DateTimeKind.Utc)], ["=1+1", null]]);

        var (book, problem) = XlsxReader.Read(written);

        Assert.Null(problem);
        var sheet = book!.Sheet("ids")!;
        Assert.Equal("Serial number", sheet.Text(1, 1));
        Assert.Equal(new XlsxCell("000123", false), sheet.Cell(2, 1));
        Assert.True(sheet.Cell(2, 2)!.IsNumber);
        Assert.Equal(new XlsxCell("=1+1", false), sheet.Cell(3, 1));
    }

    [Theory]
    [InlineData(new byte[] { 0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1 })] // an old .xls, or a workbook with a password
    [InlineData(new byte[] { 0x25, 0x50, 0x44, 0x46, 0x2D })] // a PDF
    [InlineData(new byte[] { })]
    public void AFileThatIsNoXlsx_ShouldBeRefusedInPlainWords(byte[] content)
    {
        var (book, problem) = XlsxReader.Read(content);

        Assert.Null(book);
        Assert.Equal(XlsxReader.NotAWorkbook, problem);
    }

    [Fact]
    public void AZipThatIsNoWorkbook_AndAMacroWorkbook_ShouldBeRefused()
    {
        var anyZip = TestXlsx.Zip(("readme.txt", "hello"));
        var macro = TestXlsx.Zip(
            ("[Content_Types].xml", TestXlsx.ContentTypes.Replace("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml", "application/vnd.ms-excel.sheet.macroEnabled.main+xml", StringComparison.Ordinal)),
            ("xl/workbook.xml", TestXlsx.Workbook),
            ("xl/_rels/workbook.xml.rels", TestXlsx.Rels),
            ("xl/worksheets/sheet1.xml", $"<worksheet xmlns=\"{TestXlsx.Main}\"><sheetData/></worksheet>"));

        Assert.Equal(XlsxReader.NotAWorkbook, XlsxReader.Read(anyZip).Problem);
        Assert.Equal(XlsxReader.NotAWorkbook, XlsxReader.Read(macro).Problem);
    }

    [Fact]
    public void AFileOverTheSizeLimit_ShouldBeRefusedBeforeItIsOpened()
    {
        var (book, problem) = XlsxReader.Read(new byte[XlsxReader.MaxBytes + 1]);

        Assert.Null(book);
        Assert.Equal(XlsxReader.TooLarge, problem);
    }

    [Fact]
    public void AZipWithTooManyParts_ShouldBeRefused()
    {
        var parts = Enumerable.Range(0, XlsxReader.MaxEntries + 1).Select(index => ($"xl/extra{index}.xml", "<x/>")).ToArray();

        Assert.Equal(XlsxReader.TooMuchInside, XlsxReader.Read(TestXlsx.Zip(parts)).Problem);
    }

    [Fact]
    public void ASheetThatUnpacksToFarMoreThanItsFileSize_ShouldBeRefusedWhileUnpacking()
    {
        // A zip bomb: a small file whose one sheet unpacks to more than the limit.
        using var buffer = new MemoryStream();
        using (var zip = new ZipArchive(buffer, ZipArchiveMode.Create, leaveOpen: true))
        {
            void Add(string path, string text)
            {
                using var writer = new StreamWriter(zip.CreateEntry(path, CompressionLevel.Optimal).Open(), new UTF8Encoding(false));
                writer.Write(text.Trim());
            }

            Add("[Content_Types].xml", TestXlsx.ContentTypes);
            Add("xl/workbook.xml", TestXlsx.Workbook);
            Add("xl/_rels/workbook.xml.rels", TestXlsx.Rels);
            using var sheet = zip.CreateEntry("xl/worksheets/sheet1.xml", CompressionLevel.Optimal).Open();
            sheet.Write(Encoding.UTF8.GetBytes($"<worksheet xmlns=\"{TestXlsx.Main}\"><sheetData><row r=\"1\"><c r=\"A1\" t=\"inlineStr\"><is><t>"));
            var block = Encoding.UTF8.GetBytes(new string('a', 1024 * 1024));
            for (long written = 0; written <= XlsxReader.MaxUnpackedBytes; written += block.Length)
            {
                sheet.Write(block);
            }

            sheet.Write(Encoding.UTF8.GetBytes("</t></is></c></row></sheetData></worksheet>"));
        }

        var bomb = buffer.ToArray();
        Assert.True(bomb.Length < XlsxReader.MaxBytes, "the test's file must pass the size limit to reach the unpacking limit");

        var (book, problem) = XlsxReader.Read(bomb);

        Assert.Null(book);
        Assert.Equal(XlsxReader.TooMuchInside, problem);
    }

    [Fact]
    public void ADocumentTypeDefinition_ShouldBeRefused_SoNoEntityIsExpandedOrFetched()
    {
        var sheet = $"""
            <?xml version="1.0"?>
            <!DOCTYPE worksheet [<!ENTITY secret SYSTEM "file:///C:/Windows/win.ini">]>
            <worksheet xmlns="{TestXlsx.Main}"><sheetData><row r="1"><c r="A1" t="inlineStr"><is><t>&secret;</t></is></c></row></sheetData></worksheet>
            """;
        var workbook = TestXlsx.Zip(
            ("[Content_Types].xml", TestXlsx.ContentTypes),
            ("xl/workbook.xml", TestXlsx.Workbook),
            ("xl/_rels/workbook.xml.rels", TestXlsx.Rels),
            ("xl/worksheets/sheet1.xml", sheet));

        var (book, problem) = XlsxReader.Read(workbook);

        Assert.Null(book);
        Assert.Equal(XlsxReader.Damaged, problem);
    }

    [Fact]
    public void ADamagedFile_ShouldBeRefusedInPlainWords_NotWithAnError()
    {
        var cut = TestXlsx.OneSheet("<row r=\"1\"><c r=\"A1\"><v>1</v></c></row>")[..120];
        var brokenXml = TestXlsx.Zip(
            ("[Content_Types].xml", TestXlsx.ContentTypes),
            ("xl/workbook.xml", TestXlsx.Workbook),
            ("xl/_rels/workbook.xml.rels", TestXlsx.Rels),
            ("xl/worksheets/sheet1.xml", $"<worksheet xmlns=\"{TestXlsx.Main}\"><sheetData><row"));

        Assert.Equal(XlsxReader.Damaged, XlsxReader.Read(cut).Problem);
        Assert.Equal(XlsxReader.Damaged, XlsxReader.Read(brokenXml).Problem);
    }
}
