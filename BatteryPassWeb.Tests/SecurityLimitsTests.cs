using System.Reflection;
using System.Text.Json;
using BatteryPassWeb.Controllers;

namespace BatteryPassWeb.Tests;

public sealed class SecurityLimitsTests
{
    [Fact]
    public void SecurityLimits_ShouldExposeConfigAndRateLimiterPolicies()
    {
        var options = File.ReadAllText(RepoFile("web", "Configuration", "BatteryPassOptions.cs"));
        var policyNames = File.ReadAllText(RepoFile("web", "Configuration", "SecurityRateLimitPolicyNames.cs"));
        var program = File.ReadAllText(RepoFile("web", "Program.cs"));
        var appsettings = File.ReadAllText(RepoFile("web", "appsettings.json"));
        var developmentSettings = File.ReadAllText(RepoFile("web", "appsettings.Development.json"));
        var envExample = File.ReadAllText(RepoFile("web", ".env.example"));

        Assert.Contains("EnableRateLimiting", options);
        Assert.Contains("RateLimitWindowSeconds", options);
        Assert.Contains("LoginRateLimitPerWindow", options);
        Assert.Contains("ExternalApiReadRateLimitPerWindow", options);
        Assert.Contains("ExternalApiWriteRateLimitPerWindow", options);
        Assert.Contains("ExternalApiLifecycleRateLimitPerWindow", options);
        Assert.Contains("FileUploadRateLimitPerWindow", options);
        Assert.Contains("MaxAuthorizationHeaderBytes", options);
        Assert.Contains("MaxJsonBodyBytes", options);
        Assert.Contains("MaxTelemetryPoints", options);
        Assert.Contains("MaxUploadBytes", options);

        Assert.Contains("Login", policyNames);
        Assert.Contains("ExternalApiRead", policyNames);
        Assert.Contains("ExternalApiWrite", policyNames);
        Assert.Contains("ExternalApiLifecycle", policyNames);
        Assert.Contains("FileUpload", policyNames);

        Assert.Contains("AddRateLimiter", program);
        Assert.Contains("UseRateLimiter", program);
        Assert.Contains("Status429TooManyRequests", program);
        Assert.Contains("RateLimitPartition.GetFixedWindowLimiter", program);
        Assert.Contains("ExternalApiRateLimitPartitionKey", program);
        Assert.Contains("HashForRateLimitPartition", program);
        Assert.Contains("IHttpMaxRequestBodySizeFeature", program);

        Assert.Contains("\"EnableRateLimiting\": true", appsettings);
        Assert.Contains("\"EnableRateLimiting\": true", developmentSettings);
        Assert.Contains("ENABLE_RATE_LIMITING=true", envExample);
        Assert.Contains("MAX_JSON_BODY_BYTES=1048576", envExample);
        Assert.Contains("MAX_TELEMETRY_POINTS=250", envExample);
        Assert.Contains("MAX_UPLOAD_BYTES=10485760", envExample);
    }

    [Fact]
    public void Controllers_ShouldAttachRateLimitPoliciesToAbuseProneEndpoints()
    {
        var loginController = File.ReadAllText(RepoFile("web", "Controllers", "LoginController.cs"));
        var authApiController = File.ReadAllText(RepoFile("web", "Controllers", "AuthApiController.cs"));
        var externalApiController = File.ReadAllText(RepoFile("web", "Controllers", "ExternalApiController.cs"));
        var filesApiController = File.ReadAllText(RepoFile("web", "Controllers", "FilesApiController.cs"));

        Assert.Contains("EnableRateLimiting(SecurityRateLimitPolicyNames.Login)", loginController);
        Assert.Contains("EnableRateLimiting(SecurityRateLimitPolicyNames.Login)", authApiController);
        Assert.Contains("EnableRateLimiting(SecurityRateLimitPolicyNames.ExternalApiRead)", externalApiController);
        Assert.Contains("EnableRateLimiting(SecurityRateLimitPolicyNames.ExternalApiWrite)", externalApiController);
        Assert.Contains("EnableRateLimiting(SecurityRateLimitPolicyNames.ExternalApiLifecycle)", externalApiController);
        Assert.Contains("EnableRateLimiting(SecurityRateLimitPolicyNames.FileUpload)", filesApiController);
    }

    [Fact]
    public void TelemetryParser_ShouldRejectPayloadsAboveConfiguredPointLimit()
    {
        var method = typeof(ExternalApiController).GetMethod(
            "TryParseTelemetryPointsWithinLimit",
            BindingFlags.NonPublic | BindingFlags.Static);

        Assert.NotNull(method);

        using var document = JsonDocument.Parse("""
        [
          { "measuredAt": "2026-05-20T09:00:00Z", "currentVoltageV": 398.4 },
          { "measuredAt": "2026-05-20T09:01:00Z", "currentVoltageV": 399.1 },
          { "measuredAt": "2026-05-20T09:02:00Z", "currentVoltageV": 399.7 }
        ]
        """);

        object?[] args = [document.RootElement, 2, null!, ""];
        var success = (bool)method!.Invoke(null, args)!;

        Assert.False(success);
        Assert.Contains("maximum of 2", args[3]!.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void FileUpload_ShouldCheckConfiguredFileSizeBeforeBuffering()
    {
        var controller = File.ReadAllText(RepoFile("web", "Controllers", "FilesApiController.cs"));

        Assert.Contains("MaxUploadBytes", controller);
        Assert.Contains("StatusCodes.Status413PayloadTooLarge", controller);
        Assert.True(
            controller.IndexOf("file.Length > _options.MaxUploadBytes", StringComparison.Ordinal)
            < controller.IndexOf("new MemoryStream", StringComparison.Ordinal),
            "Upload size should be checked before buffering the uploaded file.");
    }

    private static string RepoFile(params string[] parts)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null)
        {
            var candidate = Path.Combine(new[] { directory.FullName }.Concat(parts).ToArray());
            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException($"Could not find repository file: {Path.Combine(parts)}");
    }
}
