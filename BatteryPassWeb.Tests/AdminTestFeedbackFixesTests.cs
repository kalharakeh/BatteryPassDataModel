namespace BatteryPassWeb.Tests;

public sealed class AdminTestFeedbackFixesTests
{
    [Fact]
    public void BatteryPages_ShouldUseSharedBatteryTableServiceAndPartial()
    {
        var program = File.ReadAllText(RepoFile("web", "Program.cs"));
        var registry = File.ReadAllText(RepoFile("web", "Views", "Registry", "Index.cshtml"));
        var clusters = File.ReadAllText(RepoFile("web", "Views", "Admin", "Clusters.cshtml"));
        var adminPassports = File.ReadAllText(RepoFile("web", "Views", "Admin", "Passports.cshtml"));
        var clusterPassports = File.ReadAllText(RepoFile("web", "Views", "ClusterAdmin", "Passports.cshtml"));

        Assert.True(File.Exists(RepoFile("web", "Services", "BatteryTableService.cs")));
        Assert.True(File.Exists(RepoFile("web", "Views", "Shared", "_BatteryTable.cshtml")));
        Assert.Contains("AddSingleton<BatteryTableService>", program);
        Assert.Contains("BatteryTablePageViewModel", File.ReadAllText(RepoFile("web", "Models", "ViewModels", "BatteryViewModels.cs")));
        Assert.Contains("BatteryTableService", File.ReadAllText(RepoFile("web", "Controllers", "RegistryController.cs")));
        Assert.Contains("BatteryTableService", File.ReadAllText(RepoFile("web", "Controllers", "AdminController.cs")));
        Assert.Contains("BatteryTableService", File.ReadAllText(RepoFile("web", "Controllers", "ClusterAdminController.cs")));
        Assert.Contains("_BatteryTable", registry);
        Assert.Contains("_BatteryTable", clusters);
        Assert.Contains("_BatteryTable", adminPassports);
        Assert.Contains("_BatteryTable", clusterPassports);
    }

    [Fact]
    public void SharedBatteryTable_ShouldIncludeClusterNewPassportAndRoleAwareActions()
    {
        var table = File.ReadAllText(RepoFile("web", "Views", "Shared", "_BatteryTable.cshtml"));
        var history = File.ReadAllText(RepoFile("web", "Views", "Admin", "BatteryPassports.cshtml"));

        Assert.Contains("<th>Cluster</th>", table);
        Assert.Contains("New passport needed", table);
        Assert.Contains("row.CanEditBattery", table);
        Assert.Contains("row.CanCreatePassport", table);
        Assert.Contains("row.CanOpenConformance", table);
        Assert.Contains("row.HistoryUrl", table);
        Assert.Contains("row.ReturnUrl", table);
        Assert.Contains("returnUrl", history);
        Assert.Contains("Back to Registry", history);
    }

    [Fact]
    public void UsersAndMemberships_ShouldUseAccessDropdownAndApprovedClusterRoles()
    {
        var adminUsers = File.ReadAllText(RepoFile("web", "Views", "Admin", "Clusters.cshtml"));
        var clusterUsers = File.ReadAllText(RepoFile("web", "Views", "ClusterAdmin", "Users.cshtml"));
        var clusterRepository = File.ReadAllText(RepoFile("web", "Services", "ClusterRepository.cs"));
        var accessControl = File.ReadAllText(RepoFile("web", "Services", "AccessControlService.cs"));

        Assert.Contains(">Access", adminUsers);
        Assert.Contains("Global Admin", adminUsers);
        Assert.Contains("Cluster Member", adminUsers);
        Assert.Contains("Cluster Admin", adminUsers);
        Assert.Contains("Normal User", adminUsers);
        Assert.Contains("Notified Body", adminUsers);
        Assert.Contains("Market Surveillance Authorities", adminUsers);
        Assert.Contains("Commission", adminUsers);
        Assert.Contains("Person with Legitimate Interest", adminUsers);
        Assert.Contains("Notified Body", clusterUsers);
        Assert.Contains("NormalizeClusterMembershipRole", clusterRepository);
        Assert.Contains("CreateIndexOptions { Unique = true", clusterRepository);
        Assert.Contains("RoleNotifiedBody", accessControl);
        Assert.Contains("Cluster Member", accessControl);
    }

    [Fact]
    public void BatteryCreateAndEdit_ShouldRenderPolicyLockedFieldsAndClusterSelector()
    {
        var edit = File.ReadAllText(RepoFile("web", "Views", "Admin", "EditPassport.cshtml"));
        var adminController = File.ReadAllText(RepoFile("web", "Controllers", "AdminController.cs"));
        var policy = File.ReadAllText(RepoFile("web", "Services", "EditableFieldPolicyService.cs"));
        var delta = File.ReadAllText(RepoFile("web", "Services", "BatteryPassportDeltaService.cs"));

        Assert.Contains("name=\"clusterId\"", edit);
        Assert.Contains("data-editable-field-state", edit);
        Assert.Contains("is-locked-field", edit);
        Assert.Contains("Battery mass", edit);
        Assert.DoesNotContain(">Weight kg", edit);
        Assert.Contains("ApplyBatteryEditableFormValues", adminController);
        Assert.Contains("IsEditableAtCreation", adminController);
        Assert.Contains("IsEditableAfterCreation", adminController);
        Assert.Contains("Rejected locked field", adminController);
        Assert.Contains("\"general.batteryMass\"", policy);
        Assert.Contains("aspects.generalProductInformation.payload.batteryMass", policy);
        Assert.Contains("OperationalFieldKeys", delta);
    }

    [Fact]
    public void ProductEditor_ShouldManagePerModelSoftwareVersions()
    {
        var models = File.ReadAllText(RepoFile("web", "Services", "ProductTemplateModels.cs"));
        var productView = File.ReadAllText(RepoFile("web", "Views", "Admin", "Product.cshtml"));
        var edit = File.ReadAllText(RepoFile("web", "Views", "Admin", "EditPassport.cshtml"));
        var updateService = File.ReadAllText(RepoFile("web", "Services", "BatteryTemplateUpdateService.cs"));

        Assert.Contains("BatteryProductSoftwareVersion", models);
        Assert.Contains("IReadOnlyList<BatteryProductSoftwareVersion> SoftwareVersions", models);
        Assert.Contains("HighestSoftwareVersion", models);
        Assert.Contains("data-add-software-version", productView);
        Assert.Contains("data-remove-software-version", productView);
        Assert.Contains("softwareVersionRows", productView);
        Assert.Contains("data-product-software-version-select", edit);
        Assert.Contains("selectedVersion.SoftwareVersions", updateService);
    }

    [Fact]
    public void TokenPages_ShouldShowGeneratedCredentialModal()
    {
        var admin = File.ReadAllText(RepoFile("web", "Views", "Admin", "Clusters.cshtml"));
        var cluster = File.ReadAllText(RepoFile("web", "Views", "ClusterAdmin", "ApiTokens.cshtml"));

        Assert.Contains("data-generated-token-modal", admin);
        Assert.Contains("data-generated-token-modal", cluster);
        Assert.Contains("data-generated-token-copy", admin);
        Assert.Contains("data-generated-token-ok", admin);
        Assert.Contains("navigator.clipboard.writeText", admin + cluster);
        Assert.DoesNotContain("Generated credential (store now):", admin);
        Assert.DoesNotContain("Generated credential (store now):", cluster);
    }

    [Fact]
    public void Reports_ShouldUseResponsiveSharedHeaderColorsAndBoundedTelemetryCharts()
    {
        var summary = File.ReadAllText(RepoFile("web", "Views", "Passport", "Summary.cshtml"));
        var detail = File.ReadAllText(RepoFile("web", "Views", "Passport", "Detail.cshtml"));
        var css = File.ReadAllText(RepoFile("web", "wwwroot", "css", "site.css"));
        var factory = File.ReadAllText(RepoFile("web", "Services", "PassportViewModelFactory.cs"));

        Assert.Contains("bp-summary-identity-grid", summary);
        Assert.Contains("bp-summary-identity-grid", detail);
        Assert.Contains("bp-report-media-code-row", summary);
        Assert.Contains("bp-report-media-code-row", detail);
        Assert.Contains("CarbonStageColor", factory);
        Assert.Contains("bp-chart-frame", detail);
        Assert.Contains("height: 240px", css);
        Assert.Contains("overflow: hidden", css);
        Assert.Contains("grid-template-columns: repeat(auto-fit", css);
    }

    [Fact]
    public void FullReset_ShouldClearAndReseedCanonicalDemoState()
    {
        var productTemplateService = File.ReadAllText(RepoFile("web", "Services", "ProductTemplateService.cs"));

        Assert.Contains("ClearDemoCollectionsAsync", productTemplateService);
        Assert.Contains("\"users\"", productTemplateService);
        Assert.Contains("\"clusters\"", productTemplateService);
        Assert.Contains("\"clusterMemberships\"", productTemplateService);
        Assert.Contains("\"externalApiTokens\"", productTemplateService);
        Assert.Contains("\"editableFieldPolicies\"", productTemplateService);
        Assert.Contains("\"batteryTelemetry\"", productTemplateService);
        Assert.DoesNotContain("string.Empty, \"CP7M-DEMO-001\"", productTemplateService);
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
