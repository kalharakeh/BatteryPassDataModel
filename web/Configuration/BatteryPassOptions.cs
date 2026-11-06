namespace BatteryPassWeb.Configuration;

public sealed class BatteryPassOptions
{
    public const int DefaultSessionTimeoutMinutes = 180;
    public const int MinimumSessionTimeoutMinutes = 15;
    public const int MaximumSessionTimeoutMinutes = 1440;
    public const bool DefaultEnableRateLimiting = true;
    public const int DefaultRateLimitWindowSeconds = 60;
    public const int DefaultLoginRateLimitPerWindow = 10;
    public const int DefaultExternalApiReadRateLimitPerWindow = 120;
    public const int DefaultExternalApiWriteRateLimitPerWindow = 30;
    public const int DefaultExternalApiLifecycleRateLimitPerWindow = 10;
    public const int DefaultFileUploadRateLimitPerWindow = 10;
    public const int DefaultMaxAuthorizationHeaderBytes = 4096;
    public const long DefaultMaxJsonBodyBytes = 1_048_576;
    public const long DefaultMaxUploadBytes = 10_485_760;

    public string MongoDbUri { get; set; } = string.Empty;
    public string MongoDbName { get; set; } = "battery_pass_demo";
    public string SessionSecret { get; set; } = string.Empty;
    public int SessionTimeoutMinutes { get; set; } = DefaultSessionTimeoutMinutes;
    public string DemoAdminEmail { get; set; } = "admin@example.test";
    public string DemoAdminPassword { get; set; } = "Password123!";

    /// <summary>The demo accounts' password (decision 32, DEMO_USERS_PASSWORD); without it the demo data has no accounts.</summary>
    public string? DemoUsersPassword { get; set; }
    public string ExternalApiEncryptionKey { get; set; } = string.Empty;
    public string PowerAutomateResetWebhookUrl { get; set; } = string.Empty;
    public string PowerAutomateResetWebhookSecret { get; set; } = string.Empty;
    public string PasswordResetAppName { get; set; } = "Battery Pass";
    public string AppBaseUrl { get; set; } = string.Empty;

    /// <summary>The identifier scheme (O-1, step 6d): the ISO/IEC 15459 issuing agency code, e.g. UN (DUNS).</summary>
    public string IdAgency { get; set; } = string.Empty;

    /// <summary>The company's code at that agency: Scania Industrial Batteries AB's DUNS, 778474481.</summary>
    public string IdCompany { get; set; } = string.Empty;

    /// <summary>The domain the IDs are written under for good, e.g. https://www.sib-scania-eubp.eu; not where the app runs.</summary>
    public string IdBaseUrl { get; set; } = string.Empty;
    public bool RequireHttpsRedirection { get; set; } = true;
    public bool EnableDemoAdminFallback { get; set; }
    public bool EnableRateLimiting { get; set; } = DefaultEnableRateLimiting;
    public int RateLimitWindowSeconds { get; set; } = DefaultRateLimitWindowSeconds;
    public int LoginRateLimitPerWindow { get; set; } = DefaultLoginRateLimitPerWindow;
    public int ExternalApiReadRateLimitPerWindow { get; set; } = DefaultExternalApiReadRateLimitPerWindow;
    public int ExternalApiWriteRateLimitPerWindow { get; set; } = DefaultExternalApiWriteRateLimitPerWindow;
    public int ExternalApiLifecycleRateLimitPerWindow { get; set; } = DefaultExternalApiLifecycleRateLimitPerWindow;
    public int FileUploadRateLimitPerWindow { get; set; } = DefaultFileUploadRateLimitPerWindow;
    public int MaxAuthorizationHeaderBytes { get; set; } = DefaultMaxAuthorizationHeaderBytes;
    public long MaxJsonBodyBytes { get; set; } = DefaultMaxJsonBodyBytes;
    public long MaxUploadBytes { get; set; } = DefaultMaxUploadBytes;

    public static int NormalizeSessionTimeoutMinutes(int minutes)
    {
        if (minutes < MinimumSessionTimeoutMinutes)
        {
            return MinimumSessionTimeoutMinutes;
        }

        if (minutes > MaximumSessionTimeoutMinutes)
        {
            return MaximumSessionTimeoutMinutes;
        }

        return minutes;
    }

    public static int NormalizePositiveInt(int value, int fallback) =>
        value > 0 ? value : fallback;

    public static long NormalizePositiveLong(long value, long fallback) =>
        value > 0 ? value : fallback;
}
