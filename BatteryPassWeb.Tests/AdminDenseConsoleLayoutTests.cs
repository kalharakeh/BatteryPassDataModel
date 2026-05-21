namespace BatteryPassWeb.Tests;

public sealed class AdminDenseConsoleLayoutTests
{
    [Fact]
    public void Layout_ShouldUseWideMainShellForRegistryAndAdminWorkspaces()
    {
        var layout = File.ReadAllText(RepoFile("web", "Views", "Shared", "_Layout.cshtml"));
        var css = File.ReadAllText(RepoFile("web", "wwwroot", "css", "site.css"));

        Assert.Contains("bp-main-wide", layout);
        Assert.Contains("StartsWithSegments(\"/registry\")", layout);
        Assert.Contains("StartsWithSegments(\"/admin\")", layout);
        Assert.Contains("StartsWithSegments(\"/cluster-admin\")", layout);
        Assert.Contains(".bp-main-wide", css);
        Assert.Contains("min(1760px, calc(100vw - 48px))", css);
    }

    [Fact]
    public void Registry_ShouldUseDenseConsoleTableAndToolbar()
    {
        var registry = File.ReadAllText(RepoFile("web", "Views", "Registry", "Index.cshtml"));
        var batteryTable = File.ReadAllText(RepoFile("web", "Views", "Shared", "_BatteryTable.cshtml"));
        var css = File.ReadAllText(RepoFile("web", "wwwroot", "css", "site.css"));

        Assert.Contains("bp-registry-page", registry);
        Assert.Contains("bp-console-toolbar", registry);
        Assert.Contains("_BatteryTable", registry);
        Assert.Contains("bp-table-card", batteryTable);
        Assert.Contains("bp-console-table", batteryTable);
        Assert.Contains("bp-battery-id-cell", batteryTable);
        Assert.Contains("bp-status-pill", batteryTable);
        Assert.Contains("bp-action-cell", batteryTable);
        Assert.Contains(".bp-console-table", css);
        Assert.Contains(".bp-battery-id-cell", css);
        Assert.Contains("white-space: nowrap", css);
    }

    [Fact]
    public void LocalAdminPassports_ShouldUseBatteryIdentityColumnsAndIconActions()
    {
        var passports = File.ReadAllText(RepoFile("web", "Views", "ClusterAdmin", "Passports.cshtml"));
        var batteryTable = File.ReadAllText(RepoFile("web", "Views", "Shared", "_BatteryTable.cshtml"));
        var service = File.ReadAllText(RepoFile("web", "Services", "BatteryTableService.cs"));

        Assert.Contains("_BatteryTable", passports);
        Assert.Contains("Battery ID", batteryTable);
        Assert.Contains("<th>Passports</th>", batteryTable);
        Assert.Contains("<th>Latest passport status</th>", batteryTable);
        Assert.Contains("Battery Family", batteryTable);
        Assert.Contains("Battery Model", batteryTable);
        Assert.Contains("Battery serial number", batteryTable);
        Assert.Contains("LatestPassportStatus", batteryTable);
        Assert.Contains("@row.BatteryFamily", batteryTable);
        Assert.Contains("@row.BatteryModel", batteryTable);
        Assert.Contains("@row.BatterySerialNumber", batteryTable);
        Assert.Contains("bp-console-table", batteryTable);
        Assert.Contains("bp-battery-id-cell", batteryTable);
        Assert.Contains("aria-label=\"Edit battery\"", batteryTable);
        Assert.DoesNotContain("<th class=\"px-4 py-3\">Model</th>", passports);
        Assert.DoesNotContain("@row.ModelNumber", passports);
        Assert.Contains("_batteryRepository.ToSummary", service);
        Assert.Contains("BatterySearchRedirectPath", service);
        Assert.Contains("serial", service, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AdminBatteryClusterAssignments_ShouldUseDenseBatteryIdentityRowsAndAssignmentOnlyControls()
    {
        var clusters = File.ReadAllText(RepoFile("web", "Views", "Admin", "Clusters.cshtml"));
        var css = File.ReadAllText(RepoFile("web", "wwwroot", "css", "site.css"));
        var batteryTabStart = clusters.IndexOf("selectedTab == \"battery\"", StringComparison.Ordinal);
        var clusterTabStart = clusters.IndexOf("selectedTab == \"clusters\"", StringComparison.Ordinal);
        var batteryTab = clusters.Substring(batteryTabStart, clusterTabStart - batteryTabStart);

        Assert.True(batteryTabStart >= 0);
        Assert.True(clusterTabStart > batteryTabStart);
        Assert.Contains("bp-admin-cluster-assignment-table", batteryTab);
        Assert.Contains("bp-console-table", batteryTab);
        Assert.Contains("Battery ID", batteryTab);
        Assert.Contains("Battery Family", batteryTab);
        Assert.Contains("Battery Model", batteryTab);
        Assert.Contains("Battery serial number", batteryTab);
        Assert.Contains("@passport.BatteryId", batteryTab);
        Assert.Contains("@passport.BatteryFamily", batteryTab);
        Assert.Contains("bp-battery-id-cell", batteryTab);
        Assert.Contains("bp-cluster-assignment-form", batteryTab);
        Assert.Contains("aria-label=\"Save cluster assignment\"", batteryTab);
        Assert.Contains("title=\"Save cluster assignment\"", batteryTab);
        Assert.DoesNotContain("<th class=\"bp-action-cell\">Actions</th>", batteryTab);
        Assert.DoesNotContain("aria-label=\"Summary report\"", batteryTab);
        Assert.DoesNotContain("aria-label=\"Detailed report\"", batteryTab);
        Assert.DoesNotContain("aria-label=\"Edit passport snapshot\"", batteryTab);
        Assert.DoesNotContain("<th class=\"px-3 py-3\">Battery</th>", batteryTab);
        Assert.DoesNotContain("class=\"bp-secondary-button\">Save</button>", batteryTab);
        Assert.Contains(".bp-cluster-assignment-form", css);
        Assert.Contains(".bp-admin-cluster-assignment-table", css);
    }

    [Fact]
    public void AdminUxPolish_ShouldUseExplicitRolesDuplicateClusterValidationAndStandardSelects()
    {
        var admin = File.ReadAllText(RepoFile("web", "Controllers", "AdminController.cs"));
        var clusters = File.ReadAllText(RepoFile("web", "Views", "Admin", "Clusters.cshtml"));
        var clusterUsers = File.ReadAllText(RepoFile("web", "Views", "ClusterAdmin", "Users.cshtml"));
        var css = File.ReadAllText(RepoFile("web", "wwwroot", "css", "site.css"));

        Assert.Contains("Create Cluster", clusters);
        Assert.Contains("Cluster ID already exists.", admin);
        Assert.Contains("Access", clusters);
        Assert.Contains("Global Admin", clusters);
        Assert.Contains("Cluster Member", clusters);
        Assert.DoesNotContain("No global admin", clusters);
        Assert.Contains("bp-select-shell", clusters);
        Assert.Contains("bp-select-shell", clusterUsers);
        Assert.Contains(".bp-select-shell::after", css);
        Assert.Contains("bp-user-management-table", clusterUsers);
        Assert.DoesNotContain("bp-console-split", clusterUsers);
    }

    [Fact]
    public void AdminRegisteredClusters_ShouldUseDenseProductionTableWithoutMockupTabStyles()
    {
        var clusters = File.ReadAllText(RepoFile("web", "Views", "Admin", "Clusters.cshtml"));
        var css = File.ReadAllText(RepoFile("web", "wwwroot", "css", "site.css"));
        var clusterTabStart = clusters.IndexOf("selectedTab == \"clusters\"", StringComparison.Ordinal);
        var usersTabStart = clusters.IndexOf("selectedTab == \"users\"", StringComparison.Ordinal);
        var clusterTab = clusters.Substring(clusterTabStart, usersTabStart - clusterTabStart);

        Assert.True(clusterTabStart >= 0);
        Assert.True(usersTabStart > clusterTabStart);
        Assert.Contains("bp-cluster-create-strip", clusterTab);
        Assert.Contains("bp-cluster-management-table", clusterTab);
        Assert.Contains("bp-console-table", clusterTab);
        Assert.Contains("<th>Cluster</th>", clusterTab);
        Assert.Contains("<th>Cluster ID</th>", clusterTab);
        Assert.Contains("<th>Rename</th>", clusterTab);
        Assert.Contains("<th>Delete</th>", clusterTab);
        Assert.Contains("<th>Force delete</th>", clusterTab);
        Assert.Contains("aria-label=\"Save cluster name\"", clusterTab);
        Assert.Contains("aria-label=\"Delete cluster\"", clusterTab);
        Assert.Contains("aria-label=\"Force delete cluster\"", clusterTab);
        Assert.DoesNotContain("bp-management-card", clusterTab);
        Assert.DoesNotContain("bp-management-danger-zone", clusterTab);
        Assert.DoesNotContain("option-button", clusterTab);
        Assert.DoesNotContain("mockup-shell", clusterTab);
        Assert.Contains(".bp-cluster-create-strip", css);
        Assert.Contains(".bp-cluster-management-table", css);
        Assert.Contains(".bp-cluster-danger-form", css);
    }

    [Fact]
    public void AdminUsers_ShouldUseDenseMoreMenuTableForMultiClusterAssignments()
    {
        var clusters = File.ReadAllText(RepoFile("web", "Views", "Admin", "Clusters.cshtml"));
        var css = File.ReadAllText(RepoFile("web", "wwwroot", "css", "site.css"));
        var usersTabStart = clusters.IndexOf("selectedTab == \"users\"", StringComparison.Ordinal);
        var nextTabStart = clusters.IndexOf("selectedTab == \"local-editable-fields\"", StringComparison.Ordinal);
        var usersTab = clusters.Substring(usersTabStart, nextTabStart - usersTabStart);

        Assert.True(usersTabStart >= 0);
        Assert.True(nextTabStart > usersTabStart);
        Assert.Contains("bp-user-create-strip", usersTab);
        Assert.Contains("bp-user-management-table", usersTab);
        Assert.Contains("bp-console-table", usersTab);
        Assert.Contains("<th>Username</th>", usersTab);
        Assert.Contains("<th>Email</th>", usersTab);
        Assert.Contains("<th>Cluster memberships</th>", usersTab);
        Assert.Contains("<th class=\"bp-action-cell\">Actions</th>", usersTab);
        Assert.Contains("memberships.Count", usersTab);
        Assert.Contains("data-user-drawer-toggle", usersTab);
        Assert.Contains("bp-user-drawer-row", usersTab);
        Assert.Contains("bp-user-drawer-grid", usersTab);
        Assert.Contains("Account email (read-only)", usersTab);
        Assert.Contains("readonly", usersTab);
        Assert.Contains("bp-user-membership-add-row", usersTab);
        Assert.Contains("Access is app-wide", usersTab);
        Assert.DoesNotContain("<th>System role</th>", usersTab);
        Assert.DoesNotContain("Quick add membership", usersTab);
        Assert.DoesNotContain("bp-user-management-list", usersTab);
        Assert.DoesNotContain("bp-user-card", usersTab);
        Assert.Contains(".bp-user-create-strip", css);
        Assert.Contains(".bp-user-management-table", css);
        Assert.Contains(".bp-user-drawer-grid", css);
        Assert.Contains(".bp-user-membership-add-row", css);
    }

    [Fact]
    public void UserPasswordChanges_ShouldRequireConfirmationAndUseIconRevealButtons()
    {
        var clusters = File.ReadAllText(RepoFile("web", "Views", "Admin", "Clusters.cshtml"));
        var account = File.ReadAllText(RepoFile("web", "Views", "Account", "Index.cshtml"));
        var adminController = File.ReadAllText(RepoFile("web", "Controllers", "AdminController.cs"));
        var accountController = File.ReadAllText(RepoFile("web", "Controllers", "AccountController.cs"));

        Assert.Contains("name=\"passwordConfirmation\"", clusters);
        Assert.Contains("Confirm password", clusters);
        Assert.Contains("data-password-reveal", clusters);
        Assert.Contains("<svg class=\"bp-action-icon\"", clusters);
        Assert.Contains("name=\"passwordConfirmation\"", account);
        Assert.Contains("Confirm password", account);
        Assert.Contains("data-password-reveal", account);
        Assert.Contains("<svg class=\"bp-action-icon\"", account);
        Assert.Contains("passwordConfirmation", adminController);
        Assert.Contains("Passwords do not match.", adminController);
        Assert.Contains("passwordConfirmation", accountController);
        Assert.Contains("Passwords do not match.", accountController);
    }

    [Fact]
    public void LocalAdminEditAndUsers_ShouldShareDenseConsoleStructure()
    {
        var edit = File.ReadAllText(RepoFile("web", "Views", "ClusterAdmin", "EditPassport.cshtml"));
        var users = File.ReadAllText(RepoFile("web", "Views", "ClusterAdmin", "Users.cshtml"));
        var tokens = File.ReadAllText(RepoFile("web", "Views", "ClusterAdmin", "ApiTokens.cshtml"));
        var css = File.ReadAllText(RepoFile("web", "wwwroot", "css", "site.css"));

        Assert.Contains("bp-local-admin-nav", users);
        Assert.Contains("bp-local-admin-nav", edit);
        Assert.Contains("bp-local-admin-nav", tokens);
        Assert.Contains("bp-user-management-table", users);
        Assert.DoesNotContain("bp-console-split", users);
        Assert.Contains("bp-local-edit-grid", edit);
        Assert.Contains("bp-field-shell", edit);
        Assert.Contains("bp-console-table", tokens);
        Assert.Contains(".bp-local-admin-nav", css);
        Assert.Contains(".bp-console-split", css);
        Assert.Contains(".bp-local-edit-grid", css);
    }

    [Fact]
    public void AdminLocalEditableFields_ShouldUseConsolePolicyLayout()
    {
        var clusters = File.ReadAllText(RepoFile("web", "Views", "Admin", "Clusters.cshtml"));
        var css = File.ReadAllText(RepoFile("web", "wwwroot", "css", "site.css"));
        var localEditableStart = clusters.IndexOf("selectedTab == \"local-editable-fields\"", StringComparison.Ordinal);
        var apiTokenStart = clusters.IndexOf("selectedTab == \"api-token-management\"", StringComparison.Ordinal);
        var localEditableTab = clusters.Substring(localEditableStart, apiTokenStart - localEditableStart);

        Assert.True(localEditableStart >= 0);
        Assert.True(apiTokenStart > localEditableStart);
        Assert.Contains("Editable fields", localEditableTab);
        Assert.Contains("name=\"editableAtCreationFieldKeys\"", localEditableTab);
        Assert.Contains("name=\"editableAfterCreationFieldKeys\"", localEditableTab);
        Assert.Contains("name=\"editableByLocalAdminFieldKeys\"", localEditableTab);
        Assert.DoesNotContain("Local editable fields", localEditableTab);
        Assert.Contains("bp-policy-console", clusters);
        Assert.Contains("bp-local-editable-policy-table", localEditableTab);
        Assert.Contains("bp-console-table", localEditableTab);
        Assert.Contains("<th>Section</th>", localEditableTab);
        Assert.Contains("<th>Total fields</th>", localEditableTab);
        Assert.Contains("<th>Editable</th>", localEditableTab);
        Assert.Contains("<th>After creation</th>", localEditableTab);
        Assert.Contains("<th>Cluster admin</th>", localEditableTab);
        Assert.Contains("<th>Examples</th>", localEditableTab);
        Assert.Contains("bp-local-editable-drawer-row", localEditableTab);
        Assert.Contains("data-local-editable-drawer-toggle", localEditableTab);
        Assert.Contains("aria-expanded=\"false\"", localEditableTab);
        Assert.Contains("class=\"bp-local-editable-drawer-row\" hidden", localEditableTab);
        Assert.DoesNotContain("sectionIndex == 0 ? \"true\" : \"false\"", localEditableTab);
        Assert.DoesNotContain("hidden=\"@(sectionIndex != 0)\"", localEditableTab);
        Assert.Contains("policySection.Fields.Count", localEditableTab);
        Assert.Contains("sectionCreationCount", localEditableTab);
        Assert.Contains("sectionAfterCreationCount", localEditableTab);
        Assert.Contains("sectionLocalAdminCount", localEditableTab);
        Assert.Contains("bp-local-editable-field-toggle", localEditableTab);
        Assert.DoesNotContain("name=\"editableFieldKeys\"", localEditableTab);
        Assert.Contains("/admin/local-editable-fields/save", localEditableTab);
        Assert.DoesNotContain("bp-policy-section-grid", localEditableTab);
        Assert.DoesNotContain("bp-requirement-grid", localEditableTab);
        Assert.Contains("bp-policy-meta", clusters);
        Assert.Contains(".bp-policy-console", css);
        Assert.Contains(".bp-local-editable-policy-table", css);
        Assert.Contains(".bp-local-editable-drawer-row", css);
        Assert.Contains(".bp-local-editable-field-grid", css);
        Assert.Contains(".bp-local-editable-field-toggle", css);
    }

    [Fact]
    public void AdminHelp_ShouldUsePersistentAdminTabsAndLighterWorkflowIntro()
    {
        var help = File.ReadAllText(RepoFile("web", "Views", "Admin", "Help.cshtml"));
        var css = File.ReadAllText(RepoFile("web", "wwwroot", "css", "site.css"));

        Assert.Contains("bp-tab-row", help);
        Assert.Contains("Administration tabs", help);
        Assert.Contains("bp-tab-active", help);
        Assert.Contains("href=\"/admin/clusters?tab=batteries\"", help);
        Assert.Contains("href=\"/admin/clusters?tab=api-token-management\"", help);
        Assert.Contains("bp-admin-help-reference-console", help);
        Assert.Contains("bp-admin-help-reference-toolbar", help);
        Assert.Contains("bp-admin-help-workflow-table", help);
        Assert.Contains("bp-admin-help-detail-row", help);
        Assert.Contains("bp-admin-help-detail-panel", help);
        Assert.Contains("data-admin-help-detail-target", help);
        Assert.Contains("parentNode.insertBefore", help);
        Assert.DoesNotContain("scrollIntoView", help);
        Assert.DoesNotContain("bp-admin-help-hero-panel", help);
        Assert.DoesNotContain("bp-admin-help-workflow-card", help);
        Assert.DoesNotContain("bp-admin-help-workflow-chip-row", help);
        Assert.Contains(".bp-admin-help-reference-console", css);
        Assert.Contains(".bp-admin-help-reference-toolbar", css);
        Assert.Contains(".bp-admin-help-workflow-table", css);
        Assert.Contains(".bp-admin-help-detail-row", css);
        Assert.Contains(".bp-admin-help-detail-panel", css);
    }

    [Fact]
    public void AdminForms_ShouldUseModernConsoleFieldControls()
    {
        var clusters = File.ReadAllText(RepoFile("web", "Views", "Admin", "Clusters.cshtml"));
        var clusterUsers = File.ReadAllText(RepoFile("web", "Views", "ClusterAdmin", "Users.cshtml"));
        var clusterTokens = File.ReadAllText(RepoFile("web", "Views", "ClusterAdmin", "ApiTokens.cshtml"));
        var css = File.ReadAllText(RepoFile("web", "wwwroot", "css", "site.css"));

        Assert.Contains("bp-console-form-card", clusters);
        Assert.Contains("bp-console-form-card", clusterUsers);
        Assert.Contains("bp-console-form-card", clusterTokens);
        Assert.Contains(".bp-console-form-card", css);
        Assert.Contains(".bp-console-form-card input:not([type=\"checkbox\"])", css);
        Assert.Contains(".bp-console-form-card select", css);
        Assert.Contains(".bp-console-form-card input:not([type=\"checkbox\"]):focus", css);
        Assert.Contains("box-shadow: 0 0 0 3px rgba(42, 110, 207, 0.14)", css);
        Assert.Contains(".bp-console-form-card .form-control", css);
        Assert.Contains(".bp-console-form-card .form-select", css);
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
