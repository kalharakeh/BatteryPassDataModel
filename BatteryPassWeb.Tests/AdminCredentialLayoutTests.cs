namespace BatteryPassWeb.Tests;

public sealed class AdminCredentialLayoutTests
{
    [Fact]
    public void AdminApiTokenManagement_ShouldUseDenseTokenConsoleLayout()
    {
        var markup = File.ReadAllText(RepoFile("web", "Views", "Admin", "Clusters.cshtml"));
        var tokenTabStart = markup.IndexOf("selectedTab == \"api-token-management\"", StringComparison.Ordinal);
        var productsTabStart = markup.IndexOf("selectedTab == \"products\"", StringComparison.Ordinal);

        Assert.True(tokenTabStart >= 0);
        Assert.True(productsTabStart > tokenTabStart);
        var tokenTab = markup.Substring(tokenTabStart, productsTabStart - tokenTabStart);

        Assert.Contains("bp-token-console", tokenTab);
        Assert.Contains("bp-token-console-summary", tokenTab);
        Assert.Contains("bp-token-create-strip", tokenTab);
        Assert.Contains("bp-token-create-topline", tokenTab);
        Assert.Contains("bp-token-cluster-section-header", tokenTab);
        Assert.Contains("bp-token-cluster-chip-picker", tokenTab);
        Assert.Contains("bp-token-scope-select", tokenTab);
        Assert.Contains("bp-token-filter-toolbar", tokenTab);
        Assert.Contains("bp-token-management-table", tokenTab);
        Assert.Contains("bp-token-name-column", tokenTab);
        Assert.Contains("bp-token-id-column", tokenTab);
        Assert.Contains("bp-token-access-column", tokenTab);
        Assert.Contains("bp-token-scope-column", tokenTab);
        Assert.Contains("bp-token-status-column", tokenTab);
        Assert.Contains("bp-token-last-used-column", tokenTab);
        Assert.Contains("bp-token-actions-column", tokenTab);
        Assert.Contains("<th>Token ID</th>", tokenTab);
        Assert.Contains("bp-token-id-cell", tokenTab);
        Assert.Contains("Html.PartialAsync(\"_CopyIdButton\", token.TokenId)", tokenTab);
        Assert.Contains("bp-token-scope-cell", tokenTab);
        Assert.Contains("bp-token-scope-details", tokenTab);
        Assert.Contains("bp-token-scope-summary", tokenTab);
        Assert.Contains("bp-token-scope-list", tokenTab);
        Assert.Contains("ScopeSummary(token)", tokenTab);
        Assert.Contains("token.ClusterScopeNames", tokenTab);
        Assert.Contains("bp-console-table", tokenTab);
        Assert.Contains("bp-status-pill", tokenTab);
        Assert.Contains("bp-icon-action-row", tokenTab);
        Assert.Contains("/admin/api/tokens/cleanup-generated", tokenTab);
        Assert.Contains("Clean up unused generated tokens", tokenTab);
        Assert.Contains("aria-label=\"Regenerate token", tokenTab);
        Assert.Contains("aria-label=\"Deactivate token", tokenTab);
        Assert.Contains("aria-label=\"Activate token", tokenTab);
        Assert.Contains("aria-label=\"Delete token", tokenTab);
        Assert.Contains("@TokenClusterPickerName(cluster.Name)", tokenTab);
        Assert.Contains("var tokenDisplayName = TokenDisplayName(token);", tokenTab);
        Assert.Contains("<p class=\"bp-credential-name\">@tokenDisplayName</p>", tokenTab);
        Assert.Contains("token.ClusterIdsLabel", tokenTab);
        Assert.DoesNotContain("<p class=\"mb-0\">@token.ClusterIdsLabel</p>", tokenTab);
        Assert.DoesNotContain("<p class=\"mb-0 small text-secondary\"><code class=\"bp-code-wrap\">@token.TokenId</code>", tokenTab);
        Assert.DoesNotContain("<p class=\"bp-credential-name\">@token.Name</p>", tokenTab);
        Assert.DoesNotContain("<strong>@cluster.Name</strong>", tokenTab);
        Assert.DoesNotContain("<small>@cluster.ClusterId</small>", tokenTab);
        Assert.DoesNotContain("bp-token-create-grid", tokenTab);
        Assert.DoesNotContain("bp-action-row", tokenTab);
    }

    [Fact]
    public void AdminCredentials_ShouldUseSingleManagementTabAndPasswordRevealControls()
    {
        var clusters = File.ReadAllText(RepoFile("web", "Views", "Admin", "Clusters.cshtml"));
        var clusterUsers = File.ReadAllText(RepoFile("web", "Views", "ClusterAdmin", "Users.cshtml"));
        var css = File.ReadAllText(RepoFile("web", "wwwroot", "css", "site.css"));

        Assert.Contains("API Token Management", clusters);
        Assert.DoesNotContain(">API tokens</a>", clusters);
        Assert.DoesNotContain(">Battery secrets</a>", clusters);
        Assert.DoesNotContain("Battery secrets", clusters);
        Assert.Contains("data-password-reveal", clusters);
        Assert.Contains("data-password-reveal", clusterUsers);
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
