namespace BatteryPassWeb.Tests;

public sealed class DataRequirementsAdminLayoutTests
{

    [Fact]
    public void AdminController_ShouldUseProductScopedRequirementsInsteadOfGlobalAdminSave()
    {
        var source = File.ReadAllText(RepoFile("web", "Controllers", "AdminController.cs"));

        Assert.DoesNotContain("[HttpPost(\"data-requirements/save\")]", source);
        Assert.DoesNotContain("SaveDataRequirements", source);
    }

    [Fact]
    public void ProductTemplateEditor_ShouldRemainRequirementSourceOfTruth()
    {
        Assert.Contains("Battery families tab", File.ReadAllText(RepoFile("web", "Views", "Admin", "Help.cshtml")));
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
