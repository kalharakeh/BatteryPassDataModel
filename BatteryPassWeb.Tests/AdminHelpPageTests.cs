namespace BatteryPassWeb.Tests;

public sealed class AdminHelpPageTests
{
    [Fact]
    public void AdminController_ShouldExposeAdminHelpRoute()
    {
        var source = File.ReadAllText(RepoFile("web", "Controllers", "AdminController.cs"));

        Assert.Contains("[HttpGet(\"help\")]", source);
        Assert.Contains("IActionResult Help([FromQuery] string? status, [FromQuery] string? error)", source);
        Assert.Contains("ViewData[\"StatusMessage\"]", source);
        Assert.Contains("ViewData[\"ErrorMessage\"]", source);
        Assert.Contains("return View();", source);
    }

    [Fact]
    public void AdminHelpView_ShouldExplainCompletePassportLifecycle()
    {
        var markup = File.ReadAllText(RepoFile("web", "Views", "Admin", "Help.cshtml"));

        Assert.Contains("Admin workflow help", markup);
        Assert.Contains("Create battery ID", markup);
        Assert.Contains("Fill battery information", markup);
        Assert.Contains("Validate passport", markup);
        Assert.Contains("Sign passport", markup);
        Assert.Contains("Publish passport", markup);
        Assert.Contains("Edit after publishing", markup);
        Assert.Contains("Move from dirty back to clean", markup);
    }

    [Fact]
    public void AdminHelpView_ShouldRenderDenseNavigationWithoutBackToMainAdminLink()
    {
        var markup = File.ReadAllText(RepoFile("web", "Views", "Admin", "Help.cshtml"));
        var tabs = File.ReadAllText(RepoFile("web", "Views", "Shared", "_AdminTabs.cshtml"));

        Assert.Contains("bp-console-header", markup);
        Assert.Contains("_AdminTabs", markup);
        Assert.Contains("Administration tabs", tabs);
        Assert.Contains("bp-tab-active", tabs);
        Assert.Contains("href=\"@($\"/admin/clusters?tab=batteries{batteryQuerySuffix}\")\"", tabs);
        Assert.Contains("href=\"/admin/clusters?tab=local-editable-fields\"", tabs);
        Assert.Contains("href=\"/admin/help\"", tabs);
        Assert.DoesNotContain("Back to main admin page", markup);
        Assert.DoesNotContain("bp-page-return-row", markup);
        Assert.DoesNotContain("bp-page-return-link", markup);
        Assert.True(
            markup.IndexOf("_AdminTabs", StringComparison.Ordinal) <
            markup.IndexOf("bp-admin-help-reference-console", StringComparison.Ordinal),
            "The admin help tab navigation should stay above the dense reference console.");
    }

    [Fact]
    public void AdminHelpView_ShouldDescribeRequiredOptionalDataAndTrustStates()
    {
        var markup = File.ReadAllText(RepoFile("web", "Views", "Admin", "Help.cshtml"));

        Assert.Contains("Required data", markup);
        Assert.Contains("Optional data", markup);
        Assert.Contains("validated", markup);
        Assert.Contains("signed", markup);
        Assert.Contains("published", markup);
        Assert.Contains("Dirty", markup);
        Assert.Contains("Clean", markup);
        Assert.Contains("HTTP telemetry does not make the passport dirty", markup);
        Assert.Contains("zero blocking validation errors", markup);
        Assert.Contains("current valid signature proof", markup);
        Assert.Contains("Public discovery", markup);
        Assert.Contains("published, signed, clean, and verifiable", markup);
        Assert.Contains("Draft, dirty, unsigned, or unpublished passports stay visible to admins", markup);
        Assert.Contains("Battery families tab", markup);
        Assert.Contains("required/optional switches", markup);
        Assert.Contains("saved product policy", markup);
        Assert.Contains("Trust &amp; conformance tab is visible only", markup);
        Assert.Contains("general admins or local cluster admins", markup);
    }

    [Fact]
    public void AdminHelpView_ShouldPointToProductTemplatesInsteadOfDuplicatingParameterList()
    {
        var markup = File.ReadAllText(RepoFile("web", "Views", "Admin", "Help.cshtml"));

        Assert.Contains("Open battery families", markup);
        Assert.Contains("/admin/clusters?tab=products", markup);
        Assert.DoesNotContain("Parameter-by-parameter fill list", markup);
        Assert.DoesNotContain("bp-admin-help-parameter-grid", markup);
        Assert.DoesNotContain("bp-admin-help-parameter-list", markup);
    }

    [Fact]
    public void AdminHelpView_ShouldPrioritizeFirstTimeAndDirtyRecoveryChecklists()
    {
        var markup = File.ReadAllText(RepoFile("web", "Views", "Admin", "Help.cshtml"));
        var css = File.ReadAllText(RepoFile("web", "wwwroot", "css", "site.css"));

        Assert.Contains("First-time passport checklist", markup);
        Assert.Contains("Dirty recovery checklist", markup);
        Assert.Contains("Battery Model push", markup);
        Assert.Contains("What public users can see", markup);
        Assert.Contains("What admins can see", markup);
        Assert.Contains("Open battery families", markup);
        Assert.DoesNotContain("Parameter-by-parameter fill list", markup);

        Assert.Contains(".bp-admin-help-detail-card", css);
        Assert.Contains(".bp-admin-help-detail-grid", css);
    }

    [Fact]
    public void AdminHelpView_ShouldStayVisuallyStructuredAndScannable()
    {
        var markup = File.ReadAllText(RepoFile("web", "Views", "Admin", "Help.cshtml"));
        var css = File.ReadAllText(RepoFile("web", "wwwroot", "css", "site.css"));

        Assert.Contains("bp-admin-help-reference-console", markup);
        Assert.Contains("bp-admin-help-reference-toolbar", markup);
        Assert.Contains("bp-admin-help-workflow-table", markup);
        Assert.Contains("bp-admin-help-detail-row", markup);
        Assert.Contains("bp-admin-help-detail-panel", markup);
        Assert.Contains("data-admin-help-detail-row", markup);
        Assert.Contains("data-admin-help-detail-target", markup);
        Assert.Contains("tabindex=\"-1\"", markup);
        Assert.Contains("parentNode.insertBefore", markup);
        Assert.DoesNotContain("scrollIntoView", markup);
        Assert.Contains("bp-admin-help-anchor-tabs", markup);
        Assert.Contains("<th>Workflow</th>", markup);
        Assert.Contains("<th>Admin surface</th>", markup);
        Assert.Contains("<th>Success signal</th>", markup);
        Assert.Contains("<th class=\"bp-action-cell\">Details</th>", markup);
        Assert.Contains("href=\"#workflow-steps\"", markup);
        Assert.Contains("id=\"target-state\"", markup);
        Assert.Contains("id=\"workflow-steps\"", markup);
        Assert.Contains("id=\"status-dictionary\"", markup);
        Assert.Contains("id=\"ready-checklist\"", markup);
        Assert.DoesNotContain("bp-admin-help-overview", markup);
        Assert.DoesNotContain("bp-admin-help-navigation-panel", markup);
        Assert.DoesNotContain("bp-admin-help-quicknav", markup);

        Assert.Contains(".bp-admin-help-reference-console", css);
        Assert.Contains(".bp-admin-help-reference-toolbar", css);
        Assert.Contains(".bp-admin-help-workflow-table", css);
        Assert.Contains(".bp-admin-help-detail-row", css);
        Assert.Contains(".bp-admin-help-detail-panel", css);
        Assert.Contains(".bp-admin-help-anchor-tabs", css);
    }

    [Fact]
    public void AdminHelpView_ShouldRenderPolishedVisualHelpShell()
    {
        var markup = File.ReadAllText(RepoFile("web", "Views", "Admin", "Help.cshtml"));
        var css = File.ReadAllText(RepoFile("web", "wwwroot", "css", "site.css"));

        Assert.Contains("bp-admin-help-reference-console", markup);
        Assert.Contains("bp-admin-help-reference-toolbar", markup);
        Assert.Contains("bp-admin-help-workflow-table", markup);
        Assert.Contains("bp-admin-help-detail-panel", markup);
        Assert.Contains("bp-admin-help-detail-row", markup);
        Assert.Contains("bp-admin-help-status-table", markup);
        Assert.DoesNotContain("bp-admin-help-start-panel", markup);
        Assert.DoesNotContain("bp-admin-help-hero-panel", markup);
        Assert.DoesNotContain("bp-admin-help-workflow-card", markup);
        Assert.DoesNotContain("bp-admin-help-task-card", markup);
        Assert.DoesNotContain("bp-admin-help-workflow-chip-row", markup);
        Assert.DoesNotContain("bp-admin-help-action-console", markup);
        Assert.DoesNotContain("bp-admin-help-action-pills", markup);
        Assert.DoesNotContain("Need to act now?", markup);

        Assert.Contains(".bp-admin-help-reference-console", css);
        Assert.Contains(".bp-admin-help-reference-toolbar", css);
        Assert.Contains(".bp-admin-help-workflow-table", css);
        Assert.Contains(".bp-admin-help-detail-row", css);
        Assert.Contains(".bp-admin-help-detail-panel", css);
        Assert.Contains(".bp-admin-help-status-table", css);
    }

    [Fact]
    public void AdminHelpView_ShouldKeepResetInMaintenanceDisclosureAfterPrimaryGuide()
    {
        var markup = File.ReadAllText(RepoFile("web", "Views", "Admin", "Help.cshtml"));
        var css = File.ReadAllText(RepoFile("web", "wwwroot", "css", "site.css"));

        Assert.Contains("bp-admin-help-maintenance-reset", markup);
        Assert.Contains("<summary>", markup);
        Assert.Contains("Maintenance", markup);
        Assert.True(
            markup.IndexOf("bp-admin-help-maintenance-reset", StringComparison.Ordinal) >
            markup.IndexOf("id=\"ready-checklist\"", StringComparison.Ordinal),
            "The reset action should be available, but it should not dominate the first help screen.");

        Assert.Contains(".bp-admin-help-maintenance-reset", css);
    }

    [Fact]
    public void AdminPages_ShouldLinkToAdminHelp()
    {
        var index = File.ReadAllText(RepoFile("web", "Views", "Admin", "Index.cshtml"));
        var clusters = File.ReadAllText(RepoFile("web", "Views", "Admin", "Clusters.cshtml"));
        var edit = File.ReadAllText(RepoFile("web", "Views", "Admin", "EditPassport.cshtml"));
        var conformance = File.ReadAllText(RepoFile("web", "Views", "Admin", "Conformance.cshtml"));

        Assert.Contains("/admin/help", index);
        Assert.Contains("/admin/help", clusters);
        Assert.Contains("/admin/help", edit);
        Assert.Contains("/admin/help", conformance);
    }

    [Fact]
    public void AdminHelpCss_ShouldDefineWorkflowLayout()
    {
        var css = File.ReadAllText(RepoFile("web", "wwwroot", "css", "site.css"));

        Assert.Contains(".bp-admin-help-page", css);
        Assert.Contains(".bp-admin-help-workflow-table", css);
        Assert.Contains(".bp-admin-help-detail-grid", css);
        Assert.Contains(".bp-admin-help-status-table", css);
        Assert.Contains(".bp-admin-help-policy-note", css);
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
