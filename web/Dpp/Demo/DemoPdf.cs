using System.Text;

namespace BatteryPassWeb.Dpp.Demo;

/// <summary>A one-page PDF with a title and a line of text, made by hand so the demo documents open in any viewer.</summary>
public static class DemoPdf
{
    public static byte[] Make(string title, string line)
    {
        var content = $"BT /F1 18 Tf 72 770 Td ({Escape(title)}) Tj 0 -30 Td /F1 11 Tf ({Escape(line)}) Tj 0 -18 Td (Demo data: this document is made up.) Tj ET";
        string[] objects =
        [
            "<< /Type /Catalog /Pages 2 0 R >>",
            "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 595 842] /Contents 4 0 R /Resources << /Font << /F1 5 0 R >> >> >>",
            $"<< /Length {content.Length} >>\nstream\n{content}\nendstream",
            "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>",
        ];
        var pdf = new StringBuilder("%PDF-1.7\n");
        var offsets = new List<int>();
        for (var i = 0; i < objects.Length; i++)
        {
            offsets.Add(pdf.Length);
            pdf.Append($"{i + 1} 0 obj\n{objects[i]}\nendobj\n");
        }

        var xref = pdf.Length;
        pdf.Append($"xref\n0 {objects.Length + 1}\n0000000000 65535 f \n");
        foreach (var offset in offsets)
        {
            pdf.Append($"{offset:D10} 00000 n \n");
        }

        pdf.Append($"trailer\n<< /Size {objects.Length + 1} /Root 1 0 R >>\nstartxref\n{xref}\n%%EOF\n");
        return Encoding.ASCII.GetBytes(pdf.ToString());
    }

    /// <summary>PDF text: ASCII only, with the three characters that would end the string escaped.</summary>
    private static string Escape(string text) =>
        new string(text.Select(c => c is >= ' ' and <= '~' ? c : '?').ToArray()).Replace("\\", "\\\\").Replace("(", "\\(").Replace(")", "\\)");
}
