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
        Assert.Contains("[HttpPost(\"products/{productId}/versions/{productVersion}/push\")]", source);
        Assert.Contains("GetPolicyForPassportAsync", source);
        Assert.Contains("batteryProductTemplateVersions", service);
        Assert.DoesNotContain("batteryProductTemplateSoftwareVersions", service);
        Assert.Contains("productVersion", service);
        Assert.Contains("PushProductVersionTemplate", source);
        Assert.DoesNotContain("PushTemplateAsync(string productId, string productVersion, string softwareVersion", service);
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
        Assert.Contains("Battery families", clusters);
        Assert.Contains("name=\"productId\"", edit);
        Assert.Contains("name=\"softwareVersion\"", edit);
        Assert.Contains("data-product-template-select", edit);
        Assert.DoesNotContain("Complete required demo data", help);
        Assert.Contains("Open battery families", help);
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
        var css = File.ReadAllText(RepoFile("web", "wwwroot", "css", "site.css"));

        Assert.Contains("name=\"productVersion\"", productView);
        Assert.Contains("data-product-version-list", productView);
        Assert.Contains("data-product-version-row", productView);
        Assert.Contains("data-product-version-tab-rail", productView);
        Assert.Contains("bp-product-editor-workspace", productView);
        Assert.Contains("bp-product-editor-content", productView);
        Assert.Contains("data-product-version-tab", productView);
        Assert.Contains("data-selected-product-version-label", productView);
        Assert.Contains("data-version-scoped-section", productView);
        Assert.Contains("data-template-document-list", productView);
        Assert.Contains("TemplateDocuments", productView);
        Assert.Contains("RequiredFieldKeys", productView);
        Assert.Contains("collectRequiredFieldKeys", productView);
        Assert.Contains("applyRequiredFieldKeys", productView);
        Assert.Contains("data-add-product-version", productView);
        Assert.Contains("data-edit-product-version", productView);
        Assert.Contains("data-remove-product-version", productView);
        Assert.Contains("row.addEventListener('click'", productView);
        Assert.Contains("const versionInput = event.target.closest('[data-product-version-input]');", productView);
        Assert.Contains("if (versionInput && !versionInput.readOnly)", productView);
        Assert.Contains("selectProductVersion(version)", productView);
        Assert.Contains("const productId = @Html.Raw(JsonSerializer.Serialize(Model.ProductId));", productView);
        Assert.Contains("const isNewProduct = @Html.Raw(JsonSerializer.Serialize(isNew));", productView);
        Assert.DoesNotContain("const productId = @JsonSerializer.Serialize(Model.ProductId);", productView);
        Assert.Contains("name=\"productVersionsJson\"", productView);
        Assert.Contains("data-base-product-select", productView);
        Assert.Contains("data-base-product-version-select", productView);
        Assert.Contains("data-software-version-list", productView);
        Assert.Contains("name=\"softwareVersion\"", productView);
        Assert.Contains("name=\"softwareReleaseDate\"", productView);
        Assert.Contains("name=\"softwareLatestUpdate\"", productView);
        Assert.DoesNotContain("data-base-software-select", productView);
        Assert.Contains("bp-product-version-console", productView);
        Assert.Contains("bp-product-version-chip-rail", productView);
        Assert.Contains("bp-product-version-selected-panel", productView);
        Assert.Contains("data-selected-product-version-input", productView);
        Assert.Contains("bp-product-version-selected-field", productView);
        Assert.Contains("bp-product-version-selected-meta", productView);
        Assert.Contains("bp-product-version-selected-actions", productView);
        Assert.Contains("data-push-selected-product-version", productView);
        Assert.Contains("data-remove-selected-product-version", productView);
        Assert.Contains("updateSelectedProductVersionControls", productView);
        Assert.DoesNotContain("bp-product-version-current-pill", productView);
        Assert.DoesNotContain("bp-product-version-tab-actions", productView);
        Assert.DoesNotContain("bp-product-version-sidebar", productView);
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
        Assert.DoesNotContain("Material composition for <span", productView);
        Assert.DoesNotContain("Carbon lifecycle stages for <span", productView);
        Assert.DoesNotContain("Circularity for <span", productView);
        Assert.Contains("products/{productId}/versions/{productVersion}/push", adminController);
        Assert.DoesNotContain("products/{productId}/versions/{productVersion}/software/{softwareVersion}/push", adminController);
        Assert.Contains("ProductTemplateDocumentFormPayload", adminController);
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
        Assert.DoesNotContain("id=\"admin-software\"", edit);
        Assert.Contains("name=\"productVersion\"", edit);
        Assert.Contains("data-product-version-select", edit);
        Assert.Contains("data-product-software-version", edit);
        Assert.Contains("data-product-software-release", edit);
        Assert.Contains("data-product-software-update", edit);
        Assert.Contains("SelectedProductVersion", editModelSource);
        Assert.Contains("data-required-passport-validation", edit);
        Assert.Contains("data-field-key", edit);
        Assert.Contains("has-missing-fields", edit);
        Assert.Contains("markMissingRequiredFields", edit);
        Assert.Contains("puttingIntoService", File.ReadAllText(RepoFile("web", "Controllers", "AdminController.cs")));
        Assert.Contains("return Redirect(\"/admin/clusters?tab=passports", File.ReadAllText(RepoFile("web", "Controllers", "AdminController.cs")));
        Assert.Contains("applyProductTemplateSelection", edit);
        Assert.Contains("updateProductVersionOptions", edit);
        Assert.DoesNotContain("updateSoftwareOptions(productVersion", edit);
        Assert.DoesNotContain("updateSoftwareOptions", edit);
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
