namespace BatteryPassWeb.Tests;

public sealed class ValidationPolicyLayoutTests
{

    [Fact]
    public void AdminController_ShouldSanitizeDraftSavesInsteadOfCreatingFakeVerifiedState()
    {
        var source = File.ReadAllText(RepoFile("web", "Controllers", "AdminController.cs"));

        Assert.DoesNotContain("validation[\"isValid\"] = true", source);
        Assert.DoesNotContain("verification[\"signedAt\"] = now", source);
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
