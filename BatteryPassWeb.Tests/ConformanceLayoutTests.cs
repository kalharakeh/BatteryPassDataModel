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

    [Fact]
    public void PassportViewModel_ShouldExposeTrustFields()
    {
        var source = File.ReadAllText(RepoFile("web", "Models", "ViewModels", "PassportViewModel.cs"));

        Assert.Contains("TrustState", source);
        Assert.Contains("TrustIsDirty", source);
        Assert.Contains("TrustLastValidatedAt", source);
        Assert.Contains("TrustBlockingErrorCount", source);
        Assert.Contains("TrustWarningCount", source);
    }

    [Fact]
    public void PassportPages_ShouldDisplayTrustState()
    {
        var summary = File.ReadAllText(RepoFile("web", "Views", "Passport", "Summary.cshtml"));
        var detail = File.ReadAllText(RepoFile("web", "Views", "Passport", "Detail.cshtml"));

        Assert.Contains("passport.TrustState", summary);
        Assert.Contains("passport.TrustState", detail);
    }

    [Fact]
    public void AdminPages_ShouldLinkToConformance()
    {
        var edit = File.ReadAllText(RepoFile("web", "Views", "Admin", "EditPassport.cshtml"));
        var clusters = File.ReadAllText(RepoFile("web", "Views", "Admin", "Clusters.cshtml"));

        Assert.Contains("/conformance", edit);
        Assert.Contains("/conformance", clusters);
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
