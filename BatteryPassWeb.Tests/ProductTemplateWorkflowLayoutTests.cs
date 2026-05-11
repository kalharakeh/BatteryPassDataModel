namespace BatteryPassWeb.Tests;

public sealed class ProductTemplateWorkflowLayoutTests
{
    [Fact]
    public void Program_ShouldRegisterProductTemplateServices()
    {
        var source = File.ReadAllText(RepoFile("web", "Program.cs"));

        Assert.Contains("AddSingleton<ProductTemplateService>", source);
        Assert.DoesNotContain("DemoScenarioResetService", source);
    }

    [Fact]
    public void AdminController_ShouldExposeProductTemplateRoutesAndUseProductPolicies()
    {
        var source = File.ReadAllText(RepoFile("web", "Controllers", "AdminController.cs"));
        var service = File.ReadAllText(RepoFile("web", "Services", "ProductTemplateService.cs"));
        var dataCompletionSource = File.ReadAllText(RepoFile("web", "Services", "DataCompletionPolicyService.cs"));

        Assert.Contains("ProductTemplateService", source);
        Assert.Contains("[HttpGet(\"products/{productId}\")]", source);
        Assert.Contains("[HttpPost(\"products/save\")]", source);
        Assert.Contains("[HttpPost(\"products/{productId}/software/{softwareVersion}/push\")]", source);
        Assert.Contains("GetPolicyForPassportAsync", source);
        Assert.Contains("batteryProductTemplateVersions", service);
        Assert.Contains("batteryProductTemplateSoftwareVersions", service);
        Assert.Contains("productVersion", service);
        Assert.Contains("PushTemplateAsync(string productId, string productVersion, string softwareVersion", service);
        Assert.Contains("GetProductVersionPolicyAsync", dataCompletionSource);
        Assert.DoesNotContain("[HttpPost(\"passports/{passportId}/complete-required-data\")]", source);
    }

    [Fact]
    public void AdminPages_ShouldExposeProductsTabAndTemplateBasedNewPassportFlow()
    {
        var clusters = File.ReadAllText(RepoFile("web", "Views", "Admin", "Clusters.cshtml"));
        var edit = File.ReadAllText(RepoFile("web", "Views", "Admin", "EditPassport.cshtml"));
        var help = File.ReadAllText(RepoFile("web", "Views", "Admin", "Help.cshtml"));

        Assert.Contains("tab=products", clusters);
        Assert.Contains("Product templates", clusters);
        Assert.Contains("name=\"productId\"", edit);
        Assert.Contains("name=\"softwareVersion\"", edit);
        Assert.Contains("data-product-template-select", edit);
        Assert.DoesNotContain("Complete required demo data", help);
        Assert.Contains("Open product templates", help);
    }

    [Fact]
    public void AdminProductTemplateUi_ShouldUseCalmerMinimalSurfaceStyling()
    {
        var clusters = File.ReadAllText(RepoFile("web", "Views", "Admin", "Clusters.cshtml"));
        var edit = File.ReadAllText(RepoFile("web", "Views", "Admin", "EditPassport.cshtml"));
        var css = File.ReadAllText(RepoFile("web", "wwwroot", "css", "site.css"));

        Assert.Contains("bp-product-template-actions", clusters);
        Assert.Contains("bp-template-sync-note", edit);
        Assert.DoesNotContain("Template sync", edit);
        Assert.DoesNotContain("Overrides:", edit);
        Assert.DoesNotContain("bp-template-sync-meta", edit);
        Assert.Contains(".bp-summary-title", css);
        Assert.DoesNotContain("background: #1d4ed8", css);
        Assert.DoesNotContain("radial-gradient(circle at 92% 8%, rgba(28, 118, 103", css);
        Assert.DoesNotContain("bp-template-sync-card", edit);
    }

    [Fact]
    public void ProductEditor_ShouldExposeProductVersionsAndBaseTemplateControls()
    {
        var productView = File.ReadAllText(RepoFile("web", "Views", "Admin", "Product.cshtml"));
        var adminController = File.ReadAllText(RepoFile("web", "Controllers", "AdminController.cs"));

        Assert.Contains("name=\"productVersion\"", productView);
        Assert.Contains("data-product-version-list", productView);
        Assert.Contains("data-base-product-select", productView);
        Assert.Contains("data-base-product-version-select", productView);
        Assert.Contains("data-base-software-select", productView);
        Assert.Contains("products/{productId}/versions/{productVersion}/software/{softwareVersion}/push", adminController);
        Assert.DoesNotContain("does not create three default software versions", productView);
    }

    [Fact]
    public void AdminAndClusterAdmin_ShouldExposeLocalEditableFieldPolicy()
    {
        var clusters = File.ReadAllText(RepoFile("web", "Views", "Admin", "Clusters.cshtml"));
        var clusterEdit = File.ReadAllText(RepoFile("web", "Views", "ClusterAdmin", "EditPassport.cshtml"));
        var adminController = File.ReadAllText(RepoFile("web", "Controllers", "AdminController.cs"));
        var clusterAdminController = File.ReadAllText(RepoFile("web", "Controllers", "ClusterAdminController.cs"));
        var program = File.ReadAllText(RepoFile("web", "Program.cs"));

        Assert.Contains("tab=local-editable-fields", clusters);
        Assert.Contains("Local editable fields", clusters);
        Assert.Contains("/admin/local-editable-fields/save", clusters);
        Assert.Contains("FieldEditableByKey", clusterEdit);
        Assert.Contains("LocalAdminEditableFieldPolicyService", adminController);
        Assert.Contains("LocalAdminEditableFieldPolicyService", clusterAdminController);
        Assert.Contains("AddSingleton<LocalAdminEditableFieldPolicyService>", program);
    }

    [Fact]
    public void EditPassport_ShouldUpdateProductTemplateFieldsLiveFromCatalogData()
    {
        var edit = File.ReadAllText(RepoFile("web", "Views", "Admin", "EditPassport.cshtml"));
        var editModelSource = File.ReadAllText(RepoFile("web", "Models", "ViewModels", "EditPassportViewModel.cs"));

        Assert.Contains("data-product-template-catalog", edit);
        Assert.Contains("data-product-template-select", edit);
        Assert.Contains("name=\"productVersion\"", edit);
        Assert.Contains("data-product-version-select", edit);
        Assert.Contains("data-product-software-select", edit);
        Assert.Contains("SelectedProductVersion", editModelSource);
        Assert.Contains("applyProductTemplateSelection", edit);
        Assert.Contains("updateProductVersionOptions", edit);
        Assert.Contains("updateSoftwareOptions(productVersion", edit);
        Assert.Contains("updateSoftwareOptions", edit);
        Assert.Contains("materialNickel", edit);
        Assert.Contains("ratedEnergy", edit);
        Assert.Contains("recycledNickelPre", edit);
        Assert.Contains("carbonRawMaterial", edit);
    }

    [Fact]
    public void ConformanceWorkflow_ShouldNotOfferDemoCompletion()
    {
        var conformance = File.ReadAllText(RepoFile("web", "Views", "Admin", "Conformance.cshtml"));

        Assert.DoesNotContain("Complete required demo data", conformance);
        Assert.DoesNotContain("showCompleteRequiredData", conformance);
        Assert.Contains("Product template", conformance);
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
