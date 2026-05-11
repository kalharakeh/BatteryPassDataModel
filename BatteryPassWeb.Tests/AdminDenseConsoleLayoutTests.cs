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
        var css = File.ReadAllText(RepoFile("web", "wwwroot", "css", "site.css"));

        Assert.Contains("bp-registry-page", registry);
        Assert.Contains("bp-console-toolbar", registry);
        Assert.Contains("bp-table-card", registry);
        Assert.Contains("bp-console-table", registry);
        Assert.Contains("bp-battery-id-cell", registry);
        Assert.Contains("bp-status-pill", registry);
        Assert.Contains("bp-action-cell", registry);
        Assert.Contains(".bp-console-table", css);
        Assert.Contains(".bp-battery-id-cell", css);
        Assert.Contains("white-space: nowrap", css);
    }

    [Fact]
    public void LocalAdminPassports_ShouldUseBatteryIdentityColumnsAndIconActions()
    {
        var passports = File.ReadAllText(RepoFile("web", "Views", "ClusterAdmin", "Passports.cshtml"));
        var controller = File.ReadAllText(RepoFile("web", "Controllers", "ClusterAdminController.cs"));

        Assert.Contains("Battery ID", passports);
        Assert.Contains("Battery Family", passports);
        Assert.Contains("Battery version", passports);
        Assert.Contains("Battery serial number", passports);
        Assert.Contains("@row.BatteryFamily", passports);
        Assert.Contains("@row.BatteryVersion", passports);
        Assert.Contains("@row.BatterySerialNumber", passports);
        Assert.Contains("bp-console-table", passports);
        Assert.Contains("bp-battery-id-cell", passports);
        Assert.Contains("aria-label=\"Edit local fields\"", passports);
        Assert.DoesNotContain("<th class=\"px-4 py-3\">Model</th>", passports);
        Assert.DoesNotContain("@row.ModelNumber", passports);
        Assert.Contains("BatteryFamily = passport.BatteryFamily", controller);
        Assert.Contains("BatteryVersion = passport.BatteryVersion", controller);
        Assert.Contains("BatterySerialNumber = passport.BatterySerialNumber", controller);
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
        Assert.Contains("bp-console-split", users);
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

        Assert.Contains("bp-policy-console", clusters);
        Assert.Contains("bp-policy-section-grid", clusters);
        Assert.Contains("bp-policy-meta", clusters);
        Assert.Contains(".bp-policy-console", css);
        Assert.Contains(".bp-policy-section-grid", css);
    }

    [Fact]
    public void AdminHelp_ShouldUsePersistentAdminTabsAndLighterWorkflowIntro()
    {
        var help = File.ReadAllText(RepoFile("web", "Views", "Admin", "Help.cshtml"));
        var css = File.ReadAllText(RepoFile("web", "wwwroot", "css", "site.css"));

        Assert.Contains("bp-tab-row", help);
        Assert.Contains("Administration tabs", help);
        Assert.Contains("bp-tab-active", help);
        Assert.Contains("href=\"/admin/clusters?tab=passports\"", help);
        Assert.Contains("href=\"/admin/clusters?tab=api-token-management\"", help);
        Assert.Contains("bp-admin-help-start-panel", help);
        Assert.Contains("bp-admin-help-start-actions", help);
        Assert.Contains("bp-admin-help-navigation-panel", help);
        Assert.DoesNotContain("bp-admin-help-hero-panel", help);
        Assert.DoesNotContain("bp-admin-help-workflow-card", help);
        Assert.DoesNotContain("bp-admin-help-workflow-chip-row", help);
        Assert.Contains(".bp-admin-help-start-panel", css);
        Assert.Contains(".bp-admin-help-start-actions", css);
        Assert.Contains(".bp-admin-help-navigation-panel", css);
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
