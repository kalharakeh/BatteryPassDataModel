using System.Security.Cryptography;
using System.Text;

namespace BatteryPassWeb.Services;

public static class BatteryImageCatalog
{
    public const string Compact7ImageUrl = "/images/compact7.png";
    public const string Compact13ImageUrl = "/images/compact13.png";
    public const string CoreImageUrl = "/images/core.png";

    public const string Compact7Category = "Compact 7M";
    public const string Compact13Category = "Compact 13M";
    public const string CoreCategory = "Core";

    private static readonly BatteryImageOption[] OptionsInternal =
    [
        new("compact7", "Compact 7", Compact7ImageUrl, Compact7Category),
        new("compact13", "Compact 13", Compact13ImageUrl, Compact13Category),
        new("core", "Core", CoreImageUrl, CoreCategory)
    ];

    public static IReadOnlyList<BatteryImageOption> Options => OptionsInternal;

    public static string DefaultImageUrl => Compact7ImageUrl;

    public static string NormalizeAssetUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return string.Empty;
        }

        var trimmed = url.Trim();
        if (Uri.TryCreate(trimmed, UriKind.Absolute, out var absoluteUri) && absoluteUri.AbsolutePath.StartsWith("/", StringComparison.Ordinal))
        {
            trimmed = absoluteUri.AbsolutePath;
        }

        if (trimmed.Equals("/images/compact7.webp", StringComparison.OrdinalIgnoreCase)
            || trimmed.Equals("/images/compact7.png", StringComparison.OrdinalIgnoreCase))
        {
            return Compact7ImageUrl;
        }

        if (trimmed.Equals("/images/compact13.webp", StringComparison.OrdinalIgnoreCase)
            || trimmed.Equals("/images/compact13.png", StringComparison.OrdinalIgnoreCase))
        {
            return Compact13ImageUrl;
        }

        if (trimmed.Equals("/images/core.webp", StringComparison.OrdinalIgnoreCase)
            || trimmed.Equals("/images/core.png", StringComparison.OrdinalIgnoreCase))
        {
            return CoreImageUrl;
        }

        return trimmed;
    }

    public static bool TryGetOption(string? url, out BatteryImageOption option)
    {
        var normalized = NormalizeAssetUrl(url);
        var match = OptionsInternal.FirstOrDefault(item => item.Url.Equals(normalized, StringComparison.OrdinalIgnoreCase));
        if (match == null)
        {
            option = OptionsInternal[0];
            return false;
        }

        option = match;
        return true;
    }

    public static string NormalizeKnownImageUrl(string? url, string passportId = "")
    {
        if (TryGetOption(url, out var option))
        {
            return option.Url;
        }

        return PickImageForPassport(passportId);
    }

    public static string CategoryForImageUrl(string? url)
    {
        if (TryGetOption(url, out var option))
        {
            return option.Category;
        }

        return Compact7Category;
    }

    public static string PickImageForPassport(string? passportId)
    {
        if (string.IsNullOrWhiteSpace(passportId))
        {
            return DefaultImageUrl;
        }

        var normalized = passportId.Trim().ToLowerInvariant();
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(normalized));
        var index = hash[0] % OptionsInternal.Length;
        return OptionsInternal[index].Url;
    }
}

public sealed class BatteryImageOption
{
    public BatteryImageOption(string key, string label, string url, string category)
    {
        Key = key;
        Label = label;
        Url = url;
        Category = category;
    }

    public string Key { get; }
    public string Label { get; }
    public string Url { get; }
    public string Category { get; }
}
