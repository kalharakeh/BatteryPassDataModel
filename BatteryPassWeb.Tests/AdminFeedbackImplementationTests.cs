namespace BatteryPassWeb.Tests;

public sealed class AdminFeedbackImplementationTests
{
    [Fact]
    public void GlobalReportRoles_ShouldHaveAllClusterReadScopeAndDraftRules()
    {
        var accessControl = File.ReadAllText(RepoFile("web", "Services", "AccessControlService.cs"));
        var batteryTable = File.ReadAllText(RepoFile("web", "Services", "BatteryTableService.cs"));

        Assert.Contains("HasAllClusterReadScope", accessControl);
        Assert.Contains("CanSeeUnpublishedAcrossClusters", accessControl);
        Assert.Contains("RoleMarketSurveillanceAuthority", accessControl);
        Assert.Contains("RoleCommission", accessControl);
        Assert.Contains("HasAllClusterReadScope(user)", batteryTable);
        Assert.DoesNotContain("Cluster search requires global admin access.", batteryTable);
    }

    [Fact]
    public void RegisteredClusters_ShouldExposeUserOverviewDropdownAndCreateUserCopy()
    {
        var clustersView = File.ReadAllText(RepoFile("web", "Views", "Admin", "Clusters.cshtml"));

        Assert.Contains("data-cluster-user-toggle", clustersView);
        Assert.Contains("Users in cluster", clustersView);
        Assert.Contains("data-cluster-user-drawer-row", clustersView);
        Assert.Contains(">Create User<", clustersView);
        Assert.DoesNotContain(">Save user<", clustersView);
    }

    [Fact]
    public void CreateUserDropdown_ShouldIncludeGlobalReportRoles()
    {
        var clustersView = File.ReadAllText(RepoFile("web", "Views", "Admin", "Clusters.cshtml"));
        var createFormStart = clustersView.IndexOf("action=\"/admin/clusters/save-user\" class=\"bp-user-create-strip\"", StringComparison.Ordinal);
        Assert.True(createFormStart >= 0);
        var createFormEnd = clustersView.IndexOf("</form>", createFormStart, StringComparison.Ordinal);
        Assert.True(createFormEnd > createFormStart);
        var createForm = clustersView[createFormStart..createFormEnd];

        Assert.Contains("globalAccessOptions", createForm);
        Assert.Contains("[\"notifiedBody\"]", clustersView);
        Assert.Contains("[\"marketSurveillanceAuthority\"]", clustersView);
        Assert.Contains("[\"commission\"]", clustersView);
        Assert.Contains("[\"legitimateInterest\"]", clustersView);
    }

    [Fact]
    public void EditableFields_ShouldSeparateClusterAdminVisibilityFromEditability()
    {
        var policy = File.ReadAllText(RepoFile("web", "Services", "EditableFieldPolicyService.cs"));
        var adminController = File.ReadAllText(RepoFile("web", "Controllers", "AdminController.cs"));
        var clusterAdminController = File.ReadAllText(RepoFile("web", "Controllers", "ClusterAdminController.cs"));
        var adminView = File.ReadAllText(RepoFile("web", "Views", "Admin", "Clusters.cshtml"));
        var sharedEditView = File.ReadAllText(RepoFile("web", "Views", "Admin", "EditPassport.cshtml"));
        var editModel = File.ReadAllText(RepoFile("web", "Models", "ViewModels", "EditPassportViewModel.cs"));

        Assert.Contains("VisibleToClusterAdmin", policy);
        Assert.Contains("VisibleToClusterAdminCount", policy);
        Assert.Contains("visibleToClusterAdminFieldKeys", adminController);
        Assert.Contains("visibleToClusterAdminFieldKeys", adminView);
        Assert.Contains("Show for Cluster Admin", adminView);
        Assert.Contains("EditableFieldPolicyService", clusterAdminController);
        Assert.Contains("FieldVisibleByKey", editModel);
        Assert.Contains("data-visible-field-policy", sharedEditView);
        Assert.Contains("applyVisibleFieldPolicy", sharedEditView);
    }

    [Fact]
    public void ClusterAdminPassportLifecycle_ShouldUseSharedHistoryAndConformanceUx()
    {
        var controller = File.ReadAllText(RepoFile("web", "Controllers", "ClusterAdminController.cs"));
        var table = File.ReadAllText(RepoFile("web", "Services", "BatteryTableService.cs"));
        var sharedEditView = File.ReadAllText(RepoFile("web", "Views", "Admin", "EditPassport.cshtml"));
        var conformanceView = File.ReadAllText(RepoFile("web", "Views", "Admin", "Conformance.cshtml"));
        var auditView = File.ReadAllText(RepoFile("web", "Views", "Admin", "Audit.cshtml"));
        var revisionsView = File.ReadAllText(RepoFile("web", "Views", "Admin", "Revisions.cshtml"));
        var clusterHistoryView = File.ReadAllText(RepoFile("web", "Views", "ClusterAdmin", "BatteryPassports.cshtml"));

        Assert.Contains("[HttpGet(\"batteries/{batteryId}/passports\")]", controller);
        Assert.Contains("[HttpGet(\"passports/{passportId}/conformance\")]", controller);
        Assert.Contains("View(\"~/Views/Admin/EditPassport.cshtml\", model)", controller);
        Assert.Contains("View(\"~/Views/Admin/Conformance.cshtml\"", controller);
        Assert.True(File.Exists(RepoPath("web", "Views", "ClusterAdmin", "BatteryPassports.cshtml")));
        Assert.Contains("/cluster-admin/batteries/{escapedBatteryId}/passports", table);
        Assert.Contains("$\"/cluster-admin/passports/{Uri.EscapeDataString(row.LatestPassportId)}/conformance\"", table);
        Assert.Contains("isClusterEdit", sharedEditView);
        Assert.Contains("clusterEditBasePath", sharedEditView);
        Assert.DoesNotContain("bp-local-admin-nav", sharedEditView);
        Assert.Contains("workflowBasePath", conformanceView);
        Assert.Contains("Validate passport", conformanceView);
        Assert.Contains("Sign passport", conformanceView);
        Assert.Contains("Publish passport", conformanceView);
        Assert.Contains("workflowBasePath", auditView);
        Assert.Contains("workflowBasePath", revisionsView);
        Assert.Contains("/cluster-admin/passports/@Uri.EscapeDataString(passport.PassportId)/conformance", clusterHistoryView);
    }

    [Fact]
    public void ClusterAdminBatteryEdits_ShouldUpdateBatteryAndReuseNewPassportLogic()
    {
        var controller = File.ReadAllText(RepoFile("web", "Controllers", "ClusterAdminController.cs"));
        var sharedEditView = File.ReadAllText(RepoFile("web", "Views", "Admin", "EditPassport.cshtml"));

        Assert.Contains("_batteryRepository.GetByBatteryIdAsync", controller);
        Assert.Contains("ApplyLocalBatteryForm", controller);
        Assert.Contains("_batteryPassportDeltaService.UpdateNewPassportRequired", controller);
        Assert.DoesNotContain("ApplyLocalPassportForm(document", controller);
        Assert.DoesNotContain("_passportRepository.ReplaceAsync(passportId, document", controller);
        Assert.Contains("Mode = \"cluster-edit\"", controller);
        Assert.Contains("FieldEditableByKey = BuildEditableFieldDictionary(editablePolicy)", controller);
        Assert.Contains("FieldVisibleByKey = BuildVisibleFieldDictionary(editablePolicy)", controller);
        Assert.Contains("return Redirect($\"/cluster-admin/passports?q={Uri.EscapeDataString(batteryId)}\")", controller);
        Assert.Contains("data-edit-passport-form", sharedEditView);
        Assert.Contains("name=\"productVersion\"", sharedEditView);
        Assert.Contains("name=\"softwareVersion\"", sharedEditView);
        Assert.Contains("Save battery", sharedEditView);
    }

    [Fact]
    public void ClusterAdminProductTemplateEnumFields_ShouldRenderAsDropdowns()
    {
        var controller = File.ReadAllText(RepoFile("web", "Controllers", "ClusterAdminController.cs"));
        var editView = File.ReadAllText(RepoFile("web", "Views", "Admin", "EditPassport.cshtml"));

        Assert.Contains("ProductTemplateService", controller);
        Assert.Contains("_productTemplateService.ListProductsAsync", controller);
        Assert.Contains("ProductTemplates = BuildProductTemplateSummaries(products)", controller);
        Assert.Contains("ProductTemplateCatalog = BuildProductTemplateFormCatalog(products)", controller);
        Assert.Contains("SelectedProductId = selectedProduct.ProductId", controller);
        Assert.Contains("SelectedProductVersion = selectedVersion.Version", controller);
        Assert.Contains("data-product-template-catalog", editView);
        Assert.Contains("name=\"productVersion\" data-product-version-select", editView);
        Assert.Contains("name=\"softwareVersion\" data-product-software-version-select", editView);
        Assert.Contains("function updateProductVersionOptions", editView);
        Assert.Contains("function updateSoftwareParameters", editView);
        Assert.DoesNotContain("<input type=\"text\" name=\"productVersion\"", editView);
        Assert.DoesNotContain("<input type=\"text\" name=\"softwareVersion\"", editView);
    }

    [Fact]
    public void PassportCreation_ShouldBeGuardedWhenNoNewPassportIsNeeded()
    {
        var adminController = File.ReadAllText(RepoFile("web", "Controllers", "AdminController.cs"));
        var clusterAdminController = File.ReadAllText(RepoFile("web", "Controllers", "ClusterAdminController.cs"));
        var historyModel = File.ReadAllText(RepoFile("web", "Models", "ViewModels", "BatteryViewModels.cs"));
        var adminHistoryView = File.ReadAllText(RepoFile("web", "Views", "Admin", "BatteryPassports.cshtml"));
        var externalApi = File.ReadAllText(RepoFile("web", "Controllers", "ExternalApiController.cs"));

        Assert.Contains("CanCreateBatteryPassport", adminController);
        Assert.Contains("No new passport is needed", adminController);
        Assert.Contains("CanCreateBatteryPassport", clusterAdminController);
        Assert.Contains("CanCreatePassport", historyModel);
        Assert.Contains("Model.CanCreatePassport", adminHistoryView);
        Assert.Contains("ClearNewPassportRequiredAsync", externalApi);
    }

    [Fact]
    public void ExternalApi_ShouldListAccessibleClustersAndUseLifecycleTokens()
    {
        var api = File.ReadAllText(RepoFile("web", "Controllers", "ExternalApiController.cs"));
        var repository = File.ReadAllText(RepoFile("web", "Services", "ExternalApiRepository.cs"));
        var adminController = File.ReadAllText(RepoFile("web", "Controllers", "AdminController.cs"));
        var clusterAdminController = File.ReadAllText(RepoFile("web", "Controllers", "ClusterAdminController.cs"));
        var adminView = File.ReadAllText(RepoFile("web", "Views", "Admin", "Clusters.cshtml"));
        var clusterTokenView = File.ReadAllText(RepoFile("web", "Views", "ClusterAdmin", "ApiTokens.cshtml"));
        var helpView = File.ReadAllText(RepoFile("web", "Views", "Help", "Index.cshtml"));

        Assert.Contains("[HttpGet(\"clusters\")]", api);
        Assert.Contains("[HttpGet(\"clusters/{clusterId}/batteries\")]", api);
        Assert.Contains("ExternalTokenAccessMode.Lifecycle", repository);
        Assert.Contains("readWriteSign", repository);
        Assert.Contains("\"readwritesign\"", adminController.ToLowerInvariant());
        Assert.Contains("\"readwritesign\"", clusterAdminController.ToLowerInvariant());
        Assert.Contains("value=\"readWriteSign\"", adminView);
        Assert.Contains("value=\"readWriteSign\"", clusterTokenView);
        Assert.Contains("@Model.BasePath/clusters", helpView);
    }

    [Fact]
    public void ResetSeed_ShouldIncludeGlobalReadRoleUsersAndLifecycleToken()
    {
        var service = File.ReadAllText(RepoFile("web", "Services", "ProductTemplateService.cs"));
        var initializer = File.ReadAllText(RepoFile("web", "Services", "ExternalApiInitializer.cs"));

        Assert.Contains("notified.body@example.test", service);
        Assert.Contains("msa@example.test", service);
        Assert.Contains("commission@example.test", service);
        Assert.Contains("legitimate.interest@example.test", service);
        Assert.Contains("SampleLifecycleTokenId", initializer);
        Assert.Contains("SampleLifecycleTokenValue", initializer);
        Assert.Contains("ExternalTokenAccessMode.Lifecycle", service);
    }

    [Fact]
    public void BrevoSetup_ShouldBeDocumentedWithEnvironmentKeys()
    {
        var env = File.ReadAllText(RepoFile("web", ".env.example"));
        var docsPath = RepoPath("docs", "brevo-password-reset.md");

        Assert.Contains("EMAIL_SMTP_HOST", env);
        Assert.Contains("EMAIL_SMTP_PORT", env);
        Assert.Contains("EMAIL_SMTP_USERNAME", env);
        Assert.Contains("EMAIL_SMTP_PASSWORD", env);
        Assert.True(File.Exists(docsPath));
        var docs = File.ReadAllText(docsPath);
        Assert.Contains("smtp-relay.brevo.com", docs);
        Assert.Contains("Brevo", docs);
    }

    private static string RepoPath(params string[] parts)
    {
        var root = Path.GetDirectoryName(RepoFile("web", "Program.cs"))!;
        return Path.Combine(new[] { Directory.GetParent(root)!.FullName }.Concat(parts).ToArray());
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
