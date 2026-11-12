namespace BatteryPassWeb.Configuration;

public static class SecurityRateLimitPolicyNames
{
    public const string Login = "login";
    public const string ExternalApiRead = "external-api-read";
    public const string ExternalApiWrite = "external-api-write";
    public const string ExternalApiLifecycle = "external-api-lifecycle";
    public const string FileUpload = "file-upload";

    /// <summary>A passport's public data, without a token: counted by the caller's address only.</summary>
    public const string PublicDataRead = "public-data-read";
}
