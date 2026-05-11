namespace BatteryPassWeb.Tests;

public sealed class ExternalApiBatteryVersionTests
{
    [Fact]
    public void ExternalApiController_ShouldExposeBatteryVersionEndpointRequiringNewTrustFlow()
    {
        var source = File.ReadAllText(RepoFile("web", "Controllers", "ExternalApiController.cs"));

        Assert.Contains("ProductTemplateService", source);
        Assert.Contains("[HttpPatch(\"batteries/{passportId}/battery-version\")]", source);
        Assert.Contains("UpdateBatteryVersion", source);
        Assert.Contains("batteryVersion", source);
        Assert.Contains("validationSigningRequired = true", source);
        Assert.Contains("MarkCanonicalDirtyAsync", source);
        Assert.DoesNotContain("[HttpPatch(\"batteries/{passportId}/software\")]", source);
        Assert.DoesNotContain("softwareVersion is required", source);
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

        Assert.DoesNotContain("/software", markup);
        Assert.DoesNotContain("patchSoftwareVersion", markup);
        Assert.Contains("Battery family and software parameters", markup);
    }

    [Fact]
    public void ProductTemplateEditor_ShouldShowScalarSoftwareParameters()
    {
        var markup = File.ReadAllText(RepoFile("web", "Views", "Admin", "Product.cshtml"));

        Assert.Contains("data-software-version-list", markup);
        Assert.Contains("name=\"softwareVersion\"", markup);
        Assert.Contains("name=\"softwareReleaseDate\"", markup);
        Assert.Contains("name=\"softwareLatestUpdate\"", markup);
        Assert.DoesNotContain("data-add-software-version", markup);
        Assert.DoesNotContain("data-remove-software-version", markup);
    }

    [Fact]
    public void ProductTemplateEditor_ShouldUseCleanImageLabelsWithoutRepeatedCategory()
    {
        var markup = File.ReadAllText(RepoFile("web", "Views", "Admin", "Product.cshtml"));

        Assert.Contains("@imageOption.Label</option>", markup);
        Assert.DoesNotContain("@imageOption.Label (@imageOption.Category)", markup);
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
