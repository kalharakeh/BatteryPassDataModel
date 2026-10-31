namespace BatteryPassWeb.Tests;

public sealed class AdminCredentialLayoutTests
{
    [Fact]
    public void AdminCredentials_ShouldUseSingleManagementTabAndPasswordRevealControls()
    {
        var clusters = File.ReadAllText(RepoFile("web", "Views", "Admin", "Clusters.cshtml"));
        var clusterUsers = File.ReadAllText(RepoFile("web", "Views", "ClusterAdmin", "Users.cshtml"));
        var css = File.ReadAllText(RepoFile("web", "wwwroot", "css", "site.css"));

        Assert.Contains("<h2>API tokens</h2>", clusters);
        Assert.DoesNotContain(">Battery secrets</a>", clusters);
        Assert.DoesNotContain("Battery secrets", clusters);
        Assert.Contains("_PasswordReveal", clusters);
        Assert.Contains("_PasswordReveal", clusterUsers);
        Assert.Contains(".bp-tab-row", css);
        Assert.Contains("flex-wrap: nowrap", css);
    }

    [Fact]
    public void ClusterSecretsPage_ShouldBeRemoved()
    {
        var path = RepoPath("web", "Views", "ClusterAdmin", "Secrets.cshtml");

        Assert.False(File.Exists(path));
    }

    [Fact]
    public void SiteCss_ShouldDefineCredentialLayoutStyles()
    {
        var css = File.ReadAllText(RepoFile("web", "wwwroot", "css", "site.css"));

        Assert.Contains(".bp-token-create-strip", css);
        Assert.Contains(".bp-token-create-topline", css);
        Assert.Contains(".bp-token-cluster-section-header", css);
        Assert.Contains(".bp-token-cluster-chip-picker", css);
        Assert.Contains(".bp-token-scope-select", css);
        Assert.Contains(".bp-token-filter-toolbar", css);
        Assert.Contains(".bp-token-management-table", css);
        Assert.Contains(".bp-token-name-column", css);
        Assert.Contains(".bp-token-id-column", css);
        Assert.Contains(".bp-token-scope-column", css);
        Assert.Contains(".bp-token-id-cell", css);
        Assert.Contains(".bp-token-scope-details", css);
        Assert.Contains(".bp-token-scope-summary", css);
        Assert.Contains(".bp-token-scope-list", css);
        Assert.Contains(".bp-token-scope-cell", css);
        Assert.Contains("min-width: 1120px", css);
        Assert.Contains("grid-template-columns: minmax(0, 1fr) auto", css);
        Assert.Contains("gap: 4px", css);
        Assert.Contains("table-layout: fixed", css);
        Assert.Contains(".bp-checkbox-option", css);
        Assert.Contains(".bp-icon-action-row", css);
    }

    [Fact]
    public void AdminApiTokenScopeLabels_ShouldUseClusterNamesAndCleanupGeneratedDuplicates()
    {
        var adminController = File.ReadAllText(RepoFile("web", "Controllers", "AdminController.cs"));
        var clusterAdminController = File.ReadAllText(RepoFile("web", "Controllers", "ClusterAdminController.cs"));
        var repository = File.ReadAllText(RepoFile("web", "Services", "ExternalApiRepository.cs"));

        Assert.Contains("ClusterTokenScopeLabel", adminController);
        Assert.Contains("ClusterTokenScopeLabel", clusterAdminController);
        Assert.DoesNotContain("? $\"{clusterName} ({clusterId})\" : clusterId", adminController);
        Assert.DoesNotContain("? $\"{clusterName} ({clusterId})\" : clusterId", clusterAdminController);
        Assert.Contains("[HttpPost(\"api/tokens/delete\")]", adminController);
        Assert.Contains("[HttpPost(\"api/tokens/cleanup-generated\")]", adminController);
        Assert.Contains("GeneratedClusterDuplicateTokenIds", adminController);
        Assert.Contains("ExistingGeneratedClusterTokenExists", adminController);
        Assert.Contains("autoClusterId: clusterId", adminController);
        Assert.Contains("DeleteTokenAsync", repository);
        Assert.Contains("DeleteTokensAsync", repository);
        Assert.Contains("ClusterScopeNames", File.ReadAllText(RepoFile("web", "Models", "ViewModels", "AdminClusterViewModel.cs")));
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

    private static string RepoPath(params string[] parts)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null)
        {
            var candidate = Path.Combine(new[] { directory.FullName }.Concat(parts).ToArray());
            if (Directory.Exists(Path.GetDirectoryName(candidate)))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException($"Could not find repository path: {Path.Combine(parts)}");
    }
}
