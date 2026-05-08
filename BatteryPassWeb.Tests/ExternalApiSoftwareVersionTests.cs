namespace BatteryPassWeb.Tests;

public sealed class ExternalApiSoftwareVersionTests
{
    [Fact]
    public void ExternalApiController_ShouldExposeValidatedNonDirtySoftwareVersionEndpoint()
    {
        var source = File.ReadAllText(RepoFile("web", "Controllers", "ExternalApiController.cs"));

        Assert.Contains("ProductTemplateService", source);
        Assert.Contains("[HttpPatch(\"batteries/{passportId}/software\")]", source);
        Assert.Contains("UpdateSoftwareVersion", source);
        Assert.Contains("GetProductAsync(productId", source);
        Assert.Contains("Software version", source);
        Assert.Contains("Allowed versions", source);
        Assert.Contains("app.operations.softwareVersion", source);
        Assert.Contains("app.operations.softwareReleaseDate", source);
        Assert.Contains("app.operations.softwareLatestUpdate", source);
        Assert.DoesNotContain("MarkCanonicalDirtyAsync", source);
    }

    [Fact]
    public void ApiHelpPage_ShouldDocumentSoftwareVersionPatchAndWorkbenchTemplate()
    {
        var markup = File.ReadAllText(RepoFile("web", "Views", "Help", "Index.cshtml"));

        Assert.Contains("/software", markup);
        Assert.Contains("softwareVersion", markup);
        Assert.Contains("patchSoftwareVersion", markup);
        Assert.Contains("does not dirty the signed passport", markup);
    }

    [Fact]
    public void ProductTemplateEditor_ShouldAllowAddingAndRemovingSoftwareRows()
    {
        var markup = File.ReadAllText(RepoFile("web", "Views", "Admin", "Product.cshtml"));

        Assert.Contains("data-software-version-list", markup);
        Assert.Contains("data-add-software-version", markup);
        Assert.Contains("data-remove-software-version", markup);
        Assert.Contains("bp-template-version-action", markup);
        Assert.Contains("bp-button-compact", markup);
    }

    [Fact]
    public void ProductTemplateEditor_ShouldUseCleanImageLabelsWithoutRepeatedCategory()
    {
        var markup = File.ReadAllText(RepoFile("web", "Views", "Admin", "Product.cshtml"));

        Assert.Contains("@imageOption.Label</option>", markup);
        Assert.DoesNotContain("@imageOption.Label (@imageOption.Category)", markup);
    }

    [Fact]
    public void ProductTemplateEditor_ShouldLockExistingProductAndSoftwareIdentityFields()
    {
        var markup = File.ReadAllText(RepoFile("web", "Views", "Admin", "Product.cshtml"));

        Assert.Contains("name=\"productId\" value=\"@Model.ProductId\" readonly", markup);
        Assert.Contains("name=\"productName\" value=\"@Model.ProductName\" readonly", markup);
        Assert.Contains("name=\"softwareVersion\" value=\"@software.Version\" readonly", markup);
        Assert.Contains("Saved software version numbers cannot be changed", markup);
    }

    [Fact]
    public void ProductTemplateCss_ShouldKeepSoftwareActionsOnSeparateLine()
    {
        var css = File.ReadAllText(RepoFile("web", "wwwroot", "css", "site.css"));

        Assert.Contains(".bp-template-version-action", css);
        Assert.Contains("grid-column: 1 / -1", css);
        Assert.Contains(".bp-product-editor-section", css);
        Assert.Contains("align-items: start", css);
        Assert.Contains("align-content: start", css);
        Assert.Contains(".bp-template-version-row input", css);
        Assert.Contains("box-sizing: border-box", css);
    }

    [Fact]
    public void PassportViewModelFactory_ShouldPreferOperationalSoftwareVersionWhenApiUpdated()
    {
        var source = File.ReadAllText(RepoFile("web", "Services", "PassportViewModelFactory.cs"));

        Assert.Contains("appOperations", source);
        Assert.Contains("softwareVersion", source);
        Assert.Contains("FirstNonEmpty(", source);
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
