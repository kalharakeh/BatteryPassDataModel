namespace BatteryPassWeb.Dpp.Loading;

/// <summary>
/// The empty product data workbook, as it was sent to the owner on 13 November 2026, kept with the app so that the
/// page can hand it out and a test can keep it in step with the catalogue.
/// </summary>
public static class LoadTemplate
{
    /// <summary>The name the file was sent under, and is downloaded as.</summary>
    public const string FileName = "Core family - data to fill.xlsx";

    private const string ResourceName = "Dpp.Loading.core-family-data-to-fill.xlsx";

    /// <summary>The file's bytes, exactly as sent.</summary>
    public static byte[] Bytes()
    {
        using var stream = typeof(LoadTemplate).Assembly.GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException($"The empty product data workbook ({ResourceName}) isn't in the app.");
        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);
        return buffer.ToArray();
    }
}
