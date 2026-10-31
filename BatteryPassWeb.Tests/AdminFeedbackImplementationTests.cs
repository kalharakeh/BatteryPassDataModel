namespace BatteryPassWeb.Tests;

public sealed class AdminFeedbackImplementationTests
{

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
        Assert.Contains("[\"liRepair\"]", clustersView);
        Assert.Contains("[\"liRecycling\"]", clustersView);
        Assert.Contains("[\"liTreatment\"]", clustersView);
    }

    [Fact]
    public void ExternalApi_ShouldListAccessibleClustersAndUseLifecycleTokens()
    {
        var api = File.ReadAllText(RepoFile("web", "Controllers", "DppApiController.cs"));
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
        Assert.DoesNotContain("value=\"readWriteSign\"", clusterTokenView); // a cluster token only reads or sends live values (decision 30)
        Assert.Contains("@Model.BasePath/clusters", helpView);
    }

    [Fact]
    public void PowerAutomateSetup_ShouldBeDocumentedWithEnvironmentKeys()
    {
        var env = File.ReadAllText(RepoFile("web", ".env.example"));
        var docsPath = RepoPath("docs", "power-automate-password-reset.md");

        Assert.Contains("POWER_AUTOMATE_RESET_WEBHOOK_URL", env);
        Assert.Contains("POWER_AUTOMATE_RESET_WEBHOOK_SECRET", env);
        Assert.DoesNotContain("EMAIL_SMTP", env);
        Assert.True(File.Exists(docsPath));
        var docs = File.ReadAllText(docsPath);
        Assert.Contains("When an HTTP request is received", docs);
        Assert.Contains("Send an email (V2)", docs);
        Assert.Contains("x-battery-pass-secret", docs);
        Assert.Contains("temporaryPassword", docs);
        Assert.DoesNotContain("resetUrl", docs);
        Assert.DoesNotContain("Brevo", docs);
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
