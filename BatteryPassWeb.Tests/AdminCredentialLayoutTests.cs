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
