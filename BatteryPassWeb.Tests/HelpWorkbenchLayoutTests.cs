namespace BatteryPassWeb.Tests;

public sealed class HelpWorkbenchLayoutTests
{
    [Fact]
    public void HelpPage_ShouldPlaceRequestWorkbenchAfterFullWidthDocumentation()
    {
        var markup = File.ReadAllText(RepoFile("web", "Views", "Help", "Index.cshtml"));

        var documentationIndex = markup.IndexOf("<h2 class=\"h5 mb-0\">Documentation</h2>", StringComparison.Ordinal);
        var workbenchIndex = markup.IndexOf("<h2 class=\"h5 mb-3\">Request Workbench</h2>", StringComparison.Ordinal);

        Assert.True(documentationIndex >= 0, "Documentation heading should exist.");
        Assert.True(workbenchIndex > documentationIndex, "Request Workbench should be rendered under Documentation.");
        Assert.Contains("bp-help-stack", markup);
    }

    [Fact]
    public void HelpWorkbench_ShouldExposeEditableConstructedHttpRequest()
    {
        var markup = File.ReadAllText(RepoFile("web", "Views", "Help", "Index.cshtml"));

        Assert.Contains("id=\"tester-raw-request\"", markup);
        Assert.Contains("buildRawRequest", markup);
        Assert.Contains("parseRawRequest", markup);
        Assert.Contains("requestSource: 'raw'", markup);
        Assert.Contains("templateSelect?.addEventListener('change'", markup);
    }

    [Fact]
    public void HelpWorkbenchValuesTemplate_ShouldTargetValuesEndpoint()
    {
        var markup = File.ReadAllText(RepoFile("web", "Views", "Help", "Index.cshtml"));

        Assert.Contains("/values?path=ratedEnergy&path=ratedCapacity&path=nickelMass", markup);
        Assert.Contains("Values read successfully", markup);
    }

    [Fact]
    public void HelpDocumentation_ShouldExplainExternalApiHttpCodes()
    {
        var markup = File.ReadAllText(RepoFile("web", "Views", "Help", "Index.cshtml"));

        Assert.Contains("200 OK", markup);
        Assert.Contains("201 Created", markup);
        Assert.Contains("400 Bad Request", markup);
        Assert.Contains("401 Unauthorized", markup);
        Assert.Contains("403 Forbidden", markup);
        Assert.Contains("404 Not Found", markup);
        Assert.Contains("500 Server Error", markup);
        Assert.Contains("503 Service Unavailable", markup);
    }

    [Fact]
    public void HelpDocumentation_ShouldExposeReadablePathsNewTabLink()
    {
        var markup = File.ReadAllText(RepoFile("web", "Views", "Help", "Index.cshtml"));

        Assert.Contains("id=\"supported-values-link\"", markup);
        Assert.Contains("target=\"_blank\"", markup);
        Assert.Contains("/paths?section=full&amp;includeContainers=true", markup);
        Assert.Contains("openSupportedValues", markup);
    }

    [Fact]
    public void HelpDocumentation_ShouldShowHowPathsAreReusedWithValues()
    {
        var markup = File.ReadAllText(RepoFile("web", "Views", "Help", "Index.cshtml"));

        Assert.Contains("Copy any item from", markup);
        Assert.Contains("/values?path=aspects.performanceAndDurability.payload.batteryTechicalProperties.ratedEnergy&amp;path=aspects.performanceAndDurability.payload.batteryTechicalProperties.ratedCapacity", markup);
        Assert.Contains("/values?path=app.operations.locationOfUse&amp;path=app.operations.contactPerson", markup);
    }

    [Fact]
    public void HelpController_ShouldResolveSamplePassportCompatibleWithSampleReadToken()
    {
        var source = File.ReadAllText(RepoFile("web", "Controllers", "HelpController.cs"));

        Assert.Contains("PassportRepository", source);
        Assert.Contains("ResolveSamplePassportIdAsync", source);
        Assert.Contains("string.IsNullOrWhiteSpace(passport.ClusterId)", source);
        Assert.DoesNotContain("SamplePassportId = ExternalApiInitializer.SamplePassportId,", source);
    }

    [Fact]
    public void HelpCss_ShouldKeepDocumentationAndWorkbenchFullWidth()
    {
        var css = File.ReadAllText(RepoFile("web", "wwwroot", "css", "site.css"));

        Assert.Contains(".bp-help-stack", css);
        Assert.DoesNotContain(".bp-help-layout {\r\n    grid-template-columns", css);
        Assert.DoesNotContain(".bp-help-tester-card {\r\n    position: sticky", css);
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
