namespace BatteryPassWeb.Tests;

public sealed class ConformanceLayoutTests
{
    [Fact]
    public void PassportsApiController_ShouldExposeValidateEndpoint()
    {
        var source = File.ReadAllText(RepoFile("web", "Controllers", "PassportsApiController.cs"));

        Assert.Contains("[HttpPost(\"{passportId}/validate\")]", source);
        Assert.Contains("PassportValidationService", source);
        Assert.Contains("UpdateTrustValidationAsync", source);
    }

    [Fact]
    public void AdminController_ShouldExposeConformancePageAndValidateAction()
    {
        var source = File.ReadAllText(RepoFile("web", "Controllers", "AdminController.cs"));

        Assert.Contains("[HttpGet(\"passports/{passportId}/conformance\")]", source);
        Assert.Contains("[HttpPost(\"passports/{passportId}/validate\")]", source);
        Assert.Contains("ConformanceViewModel", source);
    }

    [Fact]
    public void ConformanceView_ShouldRenderTrustSummaryAndActions()
    {
        var markup = File.ReadAllText(RepoFile("web", "Views", "Admin", "Conformance.cshtml"));

        Assert.Contains("Conformance", markup);
        Assert.Contains("Blocking errors", markup);
        Assert.Contains("Warnings", markup);
        Assert.Contains("Validate passport", markup);
        Assert.Contains("Latest validation", markup);
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
