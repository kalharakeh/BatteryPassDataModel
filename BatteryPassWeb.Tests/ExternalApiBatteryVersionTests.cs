namespace BatteryPassWeb.Tests;

public sealed class ExternalApiBatteryVersionTests
{

    [Fact]
    public void BatteryTemplateUpdateService_ShouldCopySoftwareReleaseMetadata()
    {
        var source = File.ReadAllText(RepoFile("web", "Services", "BatteryTemplateUpdateService.cs"));

        Assert.Contains("[\"app.product.softwareReleaseDate\"]", source);
        Assert.Contains("[\"app.product.softwareLatestUpdate\"]", source);
        Assert.Contains("Unknown Software Version", source);
    }

    [Fact]
    public void ProductTemplateService_ShouldChangeSinglePassportByBatteryVersion()
    {
        var source = File.ReadAllText(RepoFile("web", "Services", "ProductTemplateService.cs"));

        Assert.Contains("ChangePassportBatteryVersionAsync", source);
        Assert.Contains("requestedBatteryVersion", source);
        Assert.Contains("BuildSafeTemplatePushUpdate(", source);
        Assert.Contains("selectedVersion", source);
        Assert.Contains("app.product.productVersion", source);
        Assert.DoesNotContain("PushTemplateAsync", source);
    }

    [Fact]
    public void ExternalApiHelp_ShouldNotDocumentRemovedSoftwarePatch()
    {
        var markup = File.ReadAllText(RepoFile("web", "Views", "Help", "Index.cshtml"));

        Assert.DoesNotContain("/software-version", markup);
        Assert.DoesNotContain("software parameters", markup);
    }

    [Fact]
    public void PassportViewModelFactory_ShouldExposeSoftwareVersionAsBatteryVersionParameter()
    {
        var source = File.ReadAllText(RepoFile("web", "Services", "PassportViewModelFactory.cs"));

        Assert.Contains("softwareVersion", source);
        Assert.Contains("SoftwareReleaseDate", source);
        Assert.Contains("SoftwareLatestUpdate", source);
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
