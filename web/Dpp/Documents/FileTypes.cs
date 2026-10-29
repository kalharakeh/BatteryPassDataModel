namespace BatteryPassWeb.Dpp.Documents;

/// <summary>Recognises the accepted file types by their first bytes, never by the file name.</summary>
public static class FileTypes
{
    public const string Pdf = "application/pdf";
    public const string Png = "image/png";
    public const string Jpeg = "image/jpeg";

    /// <summary>The content type of a PDF, PNG or JPEG file; null for anything else.</summary>
    public static string? Detect(ReadOnlySpan<byte> content) =>
        content.StartsWith("%PDF-"u8) ? Pdf
        : content.StartsWith(PngSignature) ? Png
        : content.StartsWith(JpegSignature) ? Jpeg
        : null;

    private static ReadOnlySpan<byte> PngSignature => [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    private static ReadOnlySpan<byte> JpegSignature => [0xFF, 0xD8, 0xFF];
}
