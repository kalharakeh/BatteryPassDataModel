using System.Globalization;

namespace BatteryPassWeb.Dpp.Documents;

/// <summary>The upload limit in the app's plain words: megabytes, never a long number of bytes (FU2).</summary>
public static class UploadLimitWords
{
    /// <summary>A limit as "10 MB" or "1.5 MB"; a limit under a megabyte is "512 KB" or "10 bytes".</summary>
    public static string Size(long bytes) => bytes < 1024
        ? $"{bytes} bytes"
        : bytes < 1_048_576
            ? $"{(bytes / 1024.0).ToString("0.#", CultureInfo.InvariantCulture)} KB"
            : $"{(bytes / 1_048_576.0).ToString("0.#", CultureInfo.InvariantCulture)} MB";

    /// <summary>"the file is larger than the 10 MB upload limit", for one file or picture (<paramref name="what"/>).</summary>
    public static string FileTooLarge(string what, long limit) => $"the {what} is larger than the {Size(limit)} upload limit";

    /// <summary>The answer to a request whose body is over the limit (the 413 answer).</summary>
    public static string RequestTooLarge(long limit) => $"Request body is too large. Maximum allowed size is {Size(limit)}.";
}
