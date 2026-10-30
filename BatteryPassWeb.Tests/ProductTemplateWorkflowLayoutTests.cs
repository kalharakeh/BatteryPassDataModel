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

    [Fact]
    public void Documentation_ShouldExplainProductTemplateWorkflow()
    {
        var guide = File.ReadAllText(RepoFile("docs", "end-user-testing-guide.md"));
        var accounts = File.ReadAllText(RepoFile("docs", "sample-cluster-test-accounts.md"));

        Assert.Contains("product template", guide, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Compact 7M", guide);
        Assert.Contains("Compact 13M", guide);
        Assert.Contains("Core", guide);
        Assert.DoesNotContain("sample-end-user-storage-001", accounts);
        Assert.DoesNotContain("demo-draft-incomplete-001", accounts);
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
