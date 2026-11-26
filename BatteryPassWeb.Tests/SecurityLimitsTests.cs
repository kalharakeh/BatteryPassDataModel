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
        Assert.Contains("MAX_UPLOAD_BYTES=10485760", envExample);
    }

    [Fact]
    public void Controllers_ShouldAttachRateLimitPoliciesToAbuseProneEndpoints()
    {
        var loginController = File.ReadAllText(RepoFile("web", "Controllers", "LoginController.cs"));
        var authApiController = File.ReadAllText(RepoFile("web", "Controllers", "AuthApiController.cs"));
        var externalApiController = File.ReadAllText(RepoFile("web", "Controllers", "DppApiController.cs"));

        Assert.Contains("EnableRateLimiting(SecurityRateLimitPolicyNames.Login)", loginController);
        Assert.Contains("EnableRateLimiting(SecurityRateLimitPolicyNames.Login)", authApiController);
        Assert.Contains("EnableRateLimiting(SecurityRateLimitPolicyNames.ExternalApiRead)", externalApiController);
        Assert.Contains("EnableRateLimiting(SecurityRateLimitPolicyNames.ExternalApiWrite)", externalApiController);
        Assert.Contains("EnableRateLimiting(SecurityRateLimitPolicyNames.ExternalApiLifecycle)", externalApiController);
    }

    [Fact]
    public void FileUpload_ShouldCheckConfiguredFileSizeBeforeBuffering()
    {
        // The old files API went with step 3d-1; uploads go through the documents service, behind the request size limit.
        var program = File.ReadAllText(RepoFile("web", "Program.cs"));
        var documents = File.ReadAllText(RepoFile("web", "Dpp", "Documents", "DocumentService.cs"));

        Assert.Contains("return options.MaxUploadBytes + 65_536;", program);
        Assert.Contains("options.Value.MaxUploadBytes", documents);
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
