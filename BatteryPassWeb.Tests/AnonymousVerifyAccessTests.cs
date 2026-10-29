namespace BatteryPassWeb.Tests;

public sealed class AnonymousVerifyAccessTests
{

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
