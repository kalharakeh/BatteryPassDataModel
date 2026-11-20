using Microsoft.Extensions.Configuration;

namespace BatteryPassWeb.Configuration;

/// <summary>
/// The site's two addresses as the app reads them (C3 round three, point 1): the first of their settings that has a
/// value. A setting that is there but empty counts as not set, so <c>APP_BASE_URL=</c> falls back to <c>APP_URL</c>.
/// </summary>
public static class SiteAddressSettings
{
    /// <summary>APP_BASE_URL, else APP_URL, else BatteryPass:AppBaseUrl, else BatteryPass:AppUrl; null when none is set.</summary>
    public static string? AppBaseUrl(Func<string, string?> environment, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(environment);
        ArgumentNullException.ThrowIfNull(configuration);
        return FirstSet(environment("APP_BASE_URL"), environment("APP_URL"), configuration["BatteryPass:AppBaseUrl"], configuration["BatteryPass:AppUrl"]);
    }

    /// <summary>DPP_ID_BASE_URL, else BatteryPass:IdBaseUrl; null when neither is set.</summary>
    public static string? IdBaseUrl(Func<string, string?> environment, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(environment);
        ArgumentNullException.ThrowIfNull(configuration);
        return FirstSet(environment("DPP_ID_BASE_URL"), configuration["BatteryPass:IdBaseUrl"]);
    }

    /// <summary>The first value that isn't empty or only spaces.</summary>
    public static string? FirstSet(params string?[] values) => values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
}
