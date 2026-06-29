namespace BatteryPassWeb.Tests;

public sealed class AnonymousVerifyAccessTests
{
    [Fact]
    public void PublicVerifyEndpoint_ShouldGateTrustDataBehindPublicVisibilityOrInternalTrustAccess()
    {
        var controller = File.ReadAllText(RepoFile("web", "Controllers", "PassportsApiController.cs"));
        var verify = ExtractMethod(controller, "Verify");

        Assert.Contains("AccessControlService", controller);
        Assert.Contains("_accessControlService", controller);
        Assert.Contains("_passportPublishPolicyService.IsPubliclyVisible(passport)", verify);
        Assert.Contains("User.Identity?.IsAuthenticated != true", verify);
        Assert.Contains("_accessControlService.CanOpenPassportDetailAsync(User, passport, _passportPublishPolicyService, cancellationToken)", verify);
        Assert.Contains("_accessControlService.CanViewTrustConformanceAsync(User, BsonHelpers.GetString(passport, \"clusterId\"), cancellationToken)", verify);

        Assert.True(
            verify.IndexOf("_passportPublishPolicyService.IsPubliclyVisible(passport)", StringComparison.Ordinal)
            < verify.IndexOf("_passportTrustService.Verify(passport)", StringComparison.Ordinal),
            "Public visibility must be checked before trust verification is returned.");

        Assert.True(
            verify.IndexOf("_accessControlService.CanOpenPassportDetailAsync", StringComparison.Ordinal)
            < verify.IndexOf("_passportTrustService.Verify(passport)", StringComparison.Ordinal),
            "Internal access must be checked before trust verification is returned.");

        Assert.Contains("return NotFound", verify);
        Assert.Contains("trust = BsonHelpers.ToDotNet", verify);
    }

    [Fact]
    public void PublicVisibilityPolicy_ShouldRemainTheAnonymousVerificationBoundary()
    {
        var publishPolicy = File.ReadAllText(RepoFile("web", "Services", "PassportPublishPolicyService.cs"));

        Assert.Contains("IsPubliclyVisible", publishPolicy);
        Assert.Contains("registryInfo\", \"status\"", publishPolicy);
        Assert.Contains("published", publishPolicy);
        Assert.Contains("HasCurrentValidSignature(passport)", publishPolicy);
    }

    private static string ExtractMethod(string source, string methodName)
    {
        var marker = $"Task<IActionResult> {methodName}(";
        var index = source.IndexOf(marker, StringComparison.Ordinal);
        Assert.True(index >= 0, $"Could not find controller action {methodName}.");

        var methodStart = source.LastIndexOf('\n', index);
        var nextMethod = source.IndexOf("\n    [", index + marker.Length, StringComparison.Ordinal);

        return nextMethod < 0
            ? source[methodStart..]
            : source[methodStart..nextMethod];
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
