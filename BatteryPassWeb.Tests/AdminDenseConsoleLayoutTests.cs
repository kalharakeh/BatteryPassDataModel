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
        var list = File.ReadAllText(RepoFile("web", "Views", "Shared", "_BatteryList.cshtml")); // the shared list since R7
        var css = File.ReadAllText(RepoFile("web", "wwwroot", "css", "site.css"));

        Assert.Contains("bp-registry-page", registry);
        Assert.Contains("bp-console-toolbar", list);
        Assert.Contains("bp-console-table", list); // the Registry's own table on the new model (decision 30)
        Assert.Contains(".bp-console-table", css);
        Assert.Contains(".bp-battery-id-cell", css);
        Assert.Contains("white-space: nowrap", css);
    }

    [Fact]
    public void AdminUxPolish_ShouldUseExplicitRolesDuplicateClusterValidationAndStandardSelects()
    {
        var admin = File.ReadAllText(RepoFile("web", "Controllers", "AdminController.cs"));
        var clusters = File.ReadAllText(RepoFile("web", "Views", "Admin", "Clusters.cshtml"));
        var clusterUsers = File.ReadAllText(RepoFile("web", "Views", "ClusterAdmin", "Users.cshtml"));
        var css = File.ReadAllText(RepoFile("web", "wwwroot", "css", "site.css"));

        Assert.Contains("Add cluster", clusters);
        Assert.Contains("Cluster ID already exists.", admin);
        Assert.Contains("Access", clusters);
        Assert.Contains("Global admin (the team)", clusters);
        Assert.Contains("Cluster member (sees only", clusters);
        Assert.DoesNotContain("No global admin", clusters);
        Assert.Contains("bp-select-shell", clusters);
        Assert.Contains("bp-select-shell", clusterUsers);
        Assert.Contains(".bp-select-shell::after", css);
        Assert.Contains("<details class=\"bp-row\" data-open-user", clusterUsers);
        Assert.DoesNotContain("bp-console-split", clusterUsers);
    }

    [Fact]
    public void UserPasswordChanges_ShouldRequireConfirmationAndUseIconRevealButtons()
    {
        var clusters = File.ReadAllText(RepoFile("web", "Views", "Admin", "Clusters.cshtml"));
        var account = File.ReadAllText(RepoFile("web", "Views", "Account", "Index.cshtml"));
        var adminController = File.ReadAllText(RepoFile("web", "Controllers", "AdminController.cs"));
        var accountController = File.ReadAllText(RepoFile("web", "Controllers", "AccountController.cs"));

        Assert.Contains("name=\"passwordConfirmation\"", clusters);
        Assert.Contains("Type it again", clusters);
        Assert.Contains("_PasswordReveal", clusters);
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
        var users = File.ReadAllText(RepoFile("web", "Views", "ClusterAdmin", "Users.cshtml"));
        var tokens = File.ReadAllText(RepoFile("web", "Views", "ClusterAdmin", "ApiTokens.cshtml"));
        var localTabs = File.ReadAllText(RepoFile("web", "Views", "Shared", "_ClusterAdminTabs.cshtml"));
        var css = File.ReadAllText(RepoFile("web", "wwwroot", "css", "site.css"));

        Assert.Contains("_ClusterAdminTabs", users);
        Assert.Contains("_ClusterAdminTabs", tokens);
        Assert.Contains("bp-local-admin-nav", localTabs);
        Assert.Contains("<details class=\"bp-row\" data-open-user", users);
        Assert.DoesNotContain("bp-console-split", users);
        Assert.Contains("_ApiTokenList", tokens);
        Assert.Contains(".bp-local-admin-nav", css);
        Assert.Contains(".bp-console-split", css);
        Assert.Contains(".bp-edit-grid", css);
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
