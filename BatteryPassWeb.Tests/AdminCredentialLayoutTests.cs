namespace BatteryPassWeb.Tests;

public sealed class AdminCredentialLayoutTests
{
    [Fact]
    public void AdminCredentialTabs_ShouldUseRoomyCredentialLayout()
    {
        var markup = File.ReadAllText(RepoFile("web", "Views", "Admin", "Clusters.cshtml"));

        Assert.Contains("bp-credential-stack", markup);
        Assert.Contains("bp-credential-form-grid bp-token-form-grid", markup);
        Assert.Contains("bp-credential-table", markup);
        Assert.Contains("bp-status-pill", markup);
        Assert.Contains("bp-action-row", markup);
    }

    [Fact]
    public void AdminCredentials_ShouldUseSingleManagementTabAndPasswordRevealControls()
    {
        var clusters = File.ReadAllText(RepoFile("web", "Views", "Admin", "Clusters.cshtml"));
        var clusterUsers = File.ReadAllText(RepoFile("web", "Views", "ClusterAdmin", "Users.cshtml"));
        var css = File.ReadAllText(RepoFile("web", "wwwroot", "css", "site.css"));

        Assert.Contains("API Token Management", clusters);
        Assert.Contains("data-credential-tab=\"api-tokens\"", clusters);
        Assert.Contains("data-credential-tab=\"battery-secrets\"", clusters);
        Assert.DoesNotContain(">API tokens</a>", clusters);
        Assert.DoesNotContain(">Battery secrets</a>", clusters);
        Assert.Contains("data-password-reveal", clusters);
        Assert.Contains("data-password-reveal", clusterUsers);
        Assert.Contains(".bp-tab-row", css);
        Assert.Contains("flex-wrap: nowrap", css);
    }

    [Fact]
    public void ClusterSecretsPage_ShouldUseRoomyCredentialLayout()
    {
        var markup = File.ReadAllText(RepoFile("web", "Views", "ClusterAdmin", "Secrets.cshtml"));

        Assert.Contains("bp-credential-stack", markup);
        Assert.Contains("bp-credential-form-grid bp-secret-form-grid", markup);
        Assert.Contains("bp-credential-table", markup);
        Assert.Contains("bp-status-pill", markup);
        Assert.Contains("bp-action-row", markup);
    }

    [Fact]
    public void SiteCss_ShouldDefineCredentialLayoutStyles()
    {
        var css = File.ReadAllText(RepoFile("web", "wwwroot", "css", "site.css"));

        Assert.Contains(".bp-credential-stack", css);
        Assert.Contains(".bp-credential-form-grid", css);
        Assert.Contains(".bp-checkbox-option", css);
        Assert.Contains(".bp-credential-table", css);
        Assert.Contains(".bp-action-row", css);
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
