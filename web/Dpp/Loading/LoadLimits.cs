namespace BatteryPassWeb.Dpp.Loading;

/// <summary>
/// How much one product data load may send: the workbook and its files together, and how many files. Each single file
/// still passes the forms' own limit (MAX_UPLOAD_BYTES). The app's request limit (Program.cs) takes the figure from
/// here for the page's two posts; a proxy in front of the app must allow the same for this path.
/// </summary>
public static class LoadLimits
{
    /// <summary>The workbook and its files together: 100 MB.</summary>
    public const long MaxRequestBytes = 100L * 1024 * 1024;

    /// <summary>The most files one load takes with its workbook.</summary>
    public const int MaxFiles = 40;

    /// <summary>The page's address; its posts are this address and the check under it.</summary>
    public const string Path = "/admin/product-data-load";

    /// <summary>What a load over the limit is told, by the page before it sends and by the app if it is sent all the same.</summary>
    public const string TooLarge = "The workbook and its files are more than 100 MB together, and one load takes up to 100 MB. Send fewer files this time: a document that isn't sent is listed as still to come, and a later load adds it.";

    public const string TooMany = "one load takes up to 40 files. Send fewer files this time: a document that isn't sent is listed as still to come, and a later load adds it";

    /// <summary>True for the page's two posts, which carry several files at once.</summary>
    public static bool AppliesTo(HttpRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        return HttpMethods.IsPost(request.Method) && request.Path.StartsWithSegments(Path, StringComparison.OrdinalIgnoreCase);
    }
}
