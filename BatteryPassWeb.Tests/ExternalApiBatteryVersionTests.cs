namespace BatteryPassWeb.Tests;

public sealed class ExternalApiBatteryVersionTests
{
    [Fact]
    public void ExternalApiController_ShouldExposeBatteryVersionEndpointRequiringNewTrustFlow()
    {
        var source = File.ReadAllText(RepoFile("web", "Controllers", "ExternalApiController.cs"));

        Assert.Contains("BatteryRepository", source);
        Assert.Contains("[HttpPatch(\"batteries/{batteryId}/battery-model\")]", source);
        Assert.Contains("UpdateBatteryModel", source);
        Assert.Contains("batteryModel", source);
        Assert.Contains("newPassportRequired =", source);
        Assert.Contains("BatteryTemplateUpdateService", source);
        Assert.Contains("[HttpPost(\"batteries/{batteryId}/passports\")]", source);
        Assert.Contains("[HttpPost(\"passports/{passportId}/validate\")]", source);
        Assert.Contains("[HttpPost(\"passports/{passportId}/sign\")]", source);
        Assert.Contains("[HttpPost(\"passports/{passportId}/publish\")]", source);
        Assert.DoesNotContain("[HttpPatch(\"batteries/{passportId}/battery-version\")]", source);
        Assert.Contains("[HttpPatch(\"batteries/{batteryId}/software-version\")]", source);
        Assert.Contains("softwareVersion is required", source);
    }

    [Fact]
    public void ExternalApi_ShouldRejectUndefinedBatteryModelAndExposeSoftwareVersionEndpoint()
    {
        var controller = File.ReadAllText(RepoFile("web", "Controllers", "ExternalApiController.cs"));
        var updateService = File.ReadAllText(RepoFile("web", "Services", "BatteryTemplateUpdateService.cs"));

        Assert.Contains("UpdateBatteryModel", controller);
        Assert.Contains("Unknown Battery Model", controller);
        Assert.Contains("[HttpPatch(\"batteries/{batteryId}/software-version\")]", controller);
        Assert.Contains("ApplyBatteryModelAsync", updateService);
        Assert.Contains("ApplySoftwareVersionAsync", updateService);
    }

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

        Assert.Contains("/software-version", markup);
        Assert.Contains("Battery ID, family, and software parameters", markup);
    }

    [Fact]
    public void ProductTemplateEditor_ShouldShowPerModelSoftwareVersionRows()
    {
        var markup = File.ReadAllText(RepoFile("web", "Views", "Admin", "Product.cshtml"));

        Assert.Contains("data-software-version-list", markup);
        Assert.Contains("name=\"softwareVersion\"", markup);
        Assert.Contains("name=\"softwareReleaseDate\"", markup);
        Assert.Contains("name=\"softwareLatestUpdate\"", markup);
        Assert.Contains("data-add-software-version", markup);
        Assert.Contains("data-remove-software-version", markup);
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
