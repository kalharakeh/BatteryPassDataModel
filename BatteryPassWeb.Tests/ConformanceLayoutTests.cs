namespace BatteryPassWeb.Tests;

public sealed class ConformanceLayoutTests
{

    [Fact]
    public void AdminController_ShouldExposeConformancePageAndValidateAction()
    {
        var source = File.ReadAllText(RepoFile("web", "Controllers", "AdminController.cs"));

        Assert.DoesNotContain("[HttpPost(\"passports/{passportId}/complete-required-data\")]", source);
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
