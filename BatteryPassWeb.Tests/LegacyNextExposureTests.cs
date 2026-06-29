using System.Text.Json;

namespace BatteryPassWeb.Tests;

public sealed class LegacyNextExposureTests
{
    [Fact]
    public void ElasticBeanstalkBundle_ShouldExcludeLegacyNextApplication()
    {
        var publishScript = File.ReadAllText(RepoFile("scripts", "publish-elastic-beanstalk.ps1"));
        var ebIgnore = File.ReadAllText(RepoFile(".ebignore"));

        Assert.Contains("web/BatteryPassWeb.csproj", publishScript);
        Assert.DoesNotContain("web-next-legacy", publishScript);
        Assert.Contains("web-next-legacy/", ebIgnore);
        Assert.Contains("**/node_modules/", ebIgnore);
        Assert.Contains("**/.next/", ebIgnore);
    }

    [Fact]
    public void LegacyNextApplication_ShouldBeMarkedPrivateAndReferenceOnly()
    {
        var packageJson = File.ReadAllText(RepoFile("web-next-legacy", "package.json"));
        using var packageDocument = JsonDocument.Parse(packageJson);

        Assert.True(packageDocument.RootElement.GetProperty("private").GetBoolean());

        var readme = File.ReadAllText(RepoFile("web-next-legacy", "README.md"));
        Assert.Contains("Legacy reference only", readme);
        Assert.Contains("Do not deploy this app publicly", readme);
        Assert.Contains("npm audit --omit=dev", readme);
        Assert.Contains("npm run build", readme);
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
