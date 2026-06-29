namespace BatteryPassWeb.Configuration;

public static class SecurityRateLimitPolicyNames
{
    public const string Login = "login";
    public const string ExternalApiRead = "external-api-read";
    public const string ExternalApiWrite = "external-api-write";
    public const string ExternalApiLifecycle = "external-api-lifecycle";
    public const string FileUpload = "file-upload";
}
