namespace BatteryPassWeb.Tests;

public sealed class ProductTemplateWorkflowLayoutTests
{

    [Fact]
    public void ProductEditor_ShouldExposeProductVersionsAndBaseTemplateControls()
    {
        var adminController = File.ReadAllText(RepoFile("web", "Controllers", "AdminController.cs"));
        var css = File.ReadAllText(RepoFile("web", "wwwroot", "css", "site.css"));

        Assert.Contains(".bp-product-version-console", css);
        Assert.Contains(".bp-product-version-chip-rail", css);
        Assert.Contains(".bp-product-version-selected-panel", css);
        Assert.Contains(".bp-product-version-selected-field", css);
        Assert.Contains(".bp-product-version-selected-meta", css);
        Assert.Contains(".bp-product-version-selected-actions", css);
        Assert.DoesNotContain(".bp-product-version-tab-actions", css);
        Assert.DoesNotContain(".bp-product-version-sidebar", css);
        Assert.Contains(".bp-product-version-tab.is-saved-version .bp-product-version-tab-input", css);
        Assert.Contains("overflow: hidden;", css);
        Assert.DoesNotContain("products/{productId}/versions/{productVersion}/software/{softwareVersion}/push", adminController);
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
