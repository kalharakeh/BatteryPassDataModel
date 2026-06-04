namespace BatteryPassWeb.Tests;

public sealed class ApplicationAuditCoverageTests
{
    [Fact]
    public void Program_ShouldRegisterApplicationAuditService()
    {
        var program = File.ReadAllText(RepoFile("web", "Program.cs"));

        Assert.Contains("AddSingleton<ApplicationAuditService>", program);
    }

    [Fact]
    public void ApplicationAuditService_ShouldProvideUnifiedAuditQueries()
    {
        var servicePath = RepoFileOrEmpty("web", "Services", "ApplicationAuditService.cs");

        Assert.True(File.Exists(servicePath), "ApplicationAuditService.cs should exist.");
        var source = File.ReadAllText(servicePath);
        Assert.Contains("applicationAuditEvents", source);
        Assert.Contains("ListApplicationAuditEventsAsync", source);
        Assert.Contains("ListPassportTimelineEventsAsync", source);
        Assert.Contains("AppendApplicationAuditEventAsync", source);
        Assert.Contains("NormalizePassportAuditEvent", source);
        Assert.Contains("NormalizeBatteryAuditEvent", source);
    }

    [Fact]
    public void AdminController_ShouldExposeFilteredGlobalAuditLog()
    {
        var source = File.ReadAllText(RepoFile("web", "Controllers", "AdminController.cs"));
        var viewPath = RepoFileOrEmpty("web", "Views", "Admin", "AuditLog.cshtml");

        Assert.Contains("ApplicationAuditService", source);
        Assert.Contains("[HttpGet(\"audit\")]", source);
        Assert.Contains("ListApplicationAuditEventsAsync", source);
        Assert.True(File.Exists(viewPath), "AuditLog.cshtml should exist.");

        var view = File.ReadAllText(viewPath);
        Assert.Contains("name=\"q\"", view);
        Assert.Contains("name=\"entityType\"", view);
        Assert.Contains("name=\"category\"", view);
        Assert.Contains("name=\"actor\"", view);
        Assert.Contains("name=\"source\"", view);
        Assert.Contains("name=\"from\"", view);
        Assert.Contains("name=\"to\"", view);
        Assert.Contains("Model.Events", view);
    }

    [Fact]
    public void AdminWorkspace_ShouldLinkToApplicationAuditLog()
    {
        var clusters = File.ReadAllText(RepoFile("web", "Views", "Admin", "Clusters.cshtml"));
        var help = File.ReadAllText(RepoFile("web", "Views", "Admin", "Help.cshtml"));
        var layout = File.ReadAllText(RepoFile("web", "Views", "Shared", "_Layout.cshtml"));
        var adminTabsPath = RepoFileOrEmpty("web", "Views", "Shared", "_AdminTabs.cshtml");

        Assert.DoesNotContain("href=\"/admin/audit\"", layout);
        Assert.True(File.Exists(adminTabsPath), "_AdminTabs.cshtml should provide the shared Admin workspace tab row.");

        var tabs = File.ReadAllText(adminTabsPath);
        Assert.Contains("Administration tabs", tabs);
        Assert.Contains("href=\"/admin/audit\"", tabs);
        Assert.Contains(">Audit log<", tabs);
        Assert.Contains("bp-tab-active", tabs);
        Assert.Contains("_AdminTabs", clusters);
        Assert.Contains("_AdminTabs", help);
    }

    [Fact]
    public void AuditPages_ShouldUseAdminShellAndSharedCompactRows()
    {
        var globalAuditView = File.ReadAllText(RepoFile("web", "Views", "Admin", "AuditLog.cshtml"));
        var passportAuditView = File.ReadAllText(RepoFile("web", "Views", "Admin", "Audit.cshtml"));
        var partialPath = RepoFileOrEmpty("web", "Views", "Shared", "_AuditEventRow.cshtml");
        var css = File.ReadAllText(RepoFile("web", "wwwroot", "css", "site.css"));

        Assert.DoesNotContain("Back to batteries", globalAuditView);
        Assert.Contains("<h1 class=\"mb-1\">Administration</h1>", globalAuditView);
        Assert.Contains("_AdminTabs", globalAuditView);
        Assert.True(File.Exists(partialPath), "_AuditEventRow.cshtml should render the shared compact audit row.");

        var partial = File.ReadAllText(partialPath);
        Assert.Contains("bp-audit-event-row", partial);
        Assert.Contains("bp-audit-event-main", partial);
        Assert.Contains("bp-audit-event-facts", partial);
        Assert.Contains("bp-audit-event-chip", partial);
        Assert.Contains("_AuditEventRow", globalAuditView);
        Assert.Contains("_AuditEventRow", passportAuditView);
        Assert.DoesNotContain("bp-ledger-event ", globalAuditView);
        Assert.DoesNotContain("bp-ledger-event ", passportAuditView);
        Assert.Contains(".bp-audit-event-row", css);
        Assert.Contains("grid-template-columns: minmax(128px, 150px) minmax(300px, 0.46fr) minmax(360px, 0.54fr)", css);
        Assert.Contains("border: 1px solid #c5d4e6", css);
        Assert.Contains("box-shadow: 0 10px 22px rgba(14, 38, 70, 0.07), inset 5px 0 #8ea6c2", css);
        Assert.Contains(".bp-audit-log .bp-ledger-timeline {\n  gap: 16px;", css);
        Assert.Contains("grid-template-columns: 1fr;\n    gap: 0;", css);
        Assert.Contains("overflow-wrap: anywhere", css);
    }

    [Fact]
    public void ApplicationAuditFilters_ShouldAvoidDateFieldOverlap()
    {
        var view = File.ReadAllText(RepoFile("web", "Views", "Admin", "AuditLog.cshtml"));
        var css = File.ReadAllText(RepoFile("web", "wwwroot", "css", "site.css"));

        Assert.Contains("name=\"from\"", view);
        Assert.Contains("name=\"to\"", view);
        Assert.DoesNotContain("minmax(120px, 0.8fr) minmax(120px, 0.8fr)", css);
        Assert.Contains("grid-template-columns: repeat(auto-fit, minmax(150px, 1fr));", css);
        Assert.Contains(".bp-audit-filter-grid input[type=\"date\"]", css);
        Assert.Contains("flex-wrap: wrap;", css);
        Assert.Contains(".bp-audit-filter-grid {\n    grid-template-columns: 1fr;", css);
    }

    [Fact]
    public void PassportAuditPages_ShouldUseExpandedBatteryTimeline()
    {
        var admin = File.ReadAllText(RepoFile("web", "Controllers", "AdminController.cs"));
        var clusterAdmin = File.ReadAllText(RepoFile("web", "Controllers", "ClusterAdminController.cs"));
        var auditView = File.ReadAllText(RepoFile("web", "Views", "Admin", "Audit.cshtml"));

        Assert.Contains("ListPassportTimelineEventsAsync", admin);
        Assert.Contains("ListPassportTimelineEventsAsync", clusterAdmin);
        Assert.Contains("_AuditEventRow", auditView);
    }

    [Fact]
    public void BatteryAndPassportWritePaths_ShouldAppendMeaningfulAuditEvents()
    {
        var admin = File.ReadAllText(RepoFile("web", "Controllers", "AdminController.cs"));
        var clusterAdmin = File.ReadAllText(RepoFile("web", "Controllers", "ClusterAdminController.cs"));
        var externalApi = File.ReadAllText(RepoFile("web", "Controllers", "ExternalApiController.cs"));
        var passportsApi = File.ReadAllText(RepoFile("web", "Controllers", "PassportsApiController.cs"));
        var snapshot = File.ReadAllText(RepoFile("web", "Services", "BatteryPassportSnapshotService.cs"));

        Assert.Contains("\"battery.updated\"", admin);
        Assert.Contains("\"battery.updated\"", clusterAdmin);
        Assert.Contains("\"battery.telemetry.written\"", externalApi);
        Assert.Contains("\"battery.operations.updated\"", externalApi);
        Assert.Contains("\"battery.model.updated\"", externalApi);
        Assert.Contains("\"battery.software.updated\"", externalApi);
        Assert.Contains("\"passport.created\"", passportsApi);
        Assert.Contains("\"passport.updated\"", passportsApi);
        Assert.Contains("\"passport.archived\"", passportsApi);
        Assert.Contains("\"passport.validated\"", passportsApi);
        Assert.Contains("\"passport.created\"", snapshot);
        Assert.Contains("\"battery.passport.created\"", snapshot);
    }

    [Fact]
    public void AdminSecurityAndPolicyActions_ShouldAppendApplicationAuditEvents()
    {
        var admin = File.ReadAllText(RepoFile("web", "Controllers", "AdminController.cs"));
        var clusterAdmin = File.ReadAllText(RepoFile("web", "Controllers", "ClusterAdminController.cs"));
        var login = File.ReadAllText(RepoFile("web", "Controllers", "LoginController.cs"));
        var account = File.ReadAllText(RepoFile("web", "Controllers", "AccountController.cs"));

        Assert.Contains("\"cluster.created\"", admin);
        Assert.Contains("\"cluster.updated\"", admin);
        Assert.Contains("\"cluster.deleted\"", admin);
        Assert.Contains("\"cluster.user.saved\"", admin);
        Assert.Contains("\"apiToken.created\"", admin);
        Assert.Contains("\"apiToken.regenerated\"", admin);
        Assert.Contains("\"editablePolicy.updated\"", admin);
        Assert.Contains("\"apiToken.created\"", clusterAdmin);
        Assert.Contains("\"cluster.user.saved\"", clusterAdmin);
        Assert.Contains("\"security.passwordReset.requested\"", login);
        Assert.Contains("\"account.profile.updated\"", account);
    }

    private static string RepoFile(params string[] parts)
    {
        var path = RepoFileOrEmpty(parts);
        if (File.Exists(path))
        {
            return path;
        }

        throw new FileNotFoundException($"Could not find repository file: {Path.Combine(parts)}");
    }

    private static string RepoFileOrEmpty(params string[] parts)
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

        var fallbackRoot = AppContext.BaseDirectory;
        return Path.Combine(new[] { fallbackRoot }.Concat(parts).ToArray());
    }
}
