namespace BatteryPassWeb.Tests;

public sealed class AdminTestFeedbackFixesTests
{

    [Fact]
    public void UsersAndMemberships_ShouldUseAccessDropdownAndApprovedClusterRoles()
    {
        var adminUsers = File.ReadAllText(RepoFile("web", "Views", "Admin", "Clusters.cshtml"));
        var clusterUsers = File.ReadAllText(RepoFile("web", "Views", "ClusterAdmin", "Users.cshtml"));
        var clusterRepository = File.ReadAllText(RepoFile("web", "Services", "ClusterRepository.cs"));
        var accessControl = File.ReadAllText(RepoFile("web", "Services", "AccessControlService.cs"));

        Assert.Contains(">Access", adminUsers);
        Assert.Contains("Global admin", adminUsers);
        Assert.Contains("Cluster member", adminUsers);
        Assert.Contains("Cluster admin", adminUsers);
        Assert.Contains("[\"member\"] = \"User\"", adminUsers);
        Assert.Contains("Notified body", adminUsers);
        Assert.Contains("Market surveillance authority", adminUsers);
        Assert.Contains("Commission", adminUsers);
        Assert.Contains("Legitimate interest: repair, repurposing, remanufacturing", adminUsers);
        Assert.Contains("Legitimate interest: recycling and battery treatment", adminUsers);
        Assert.Contains("Legitimate interest: authorised or WEEE treatment", adminUsers);
        Assert.Contains("Notified Body", clusterUsers);
        Assert.Contains("NormalizeClusterMembershipRole", clusterRepository);
        Assert.Contains("CreateIndexOptions { Unique = true", clusterRepository);
        Assert.Contains("RoleNotifiedBody", accessControl);
        Assert.Contains("Cluster Member", accessControl);
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
        var hero = File.ReadAllText(RepoFile("web", "Views", "Passport", "_PassportHero.cshtml"));
        var css = File.ReadAllText(RepoFile("web", "wwwroot", "css", "site.css"));

        Assert.Contains("bp-summary-identity-grid", hero);
        Assert.Contains("bp-report-identity-panel--qr", hero);
        Assert.Contains("height: clamp(220px, 28vw, 300px)", css);
        Assert.Contains("overflow: hidden", css);
        Assert.Contains("grid-template-columns: repeat(auto-fit", css);
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
