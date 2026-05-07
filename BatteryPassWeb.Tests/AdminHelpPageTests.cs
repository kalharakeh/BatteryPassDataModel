namespace BatteryPassWeb.Tests;

public sealed class AdminHelpPageTests
{
    [Fact]
    public void AdminController_ShouldExposeAdminHelpRoute()
    {
        var source = File.ReadAllText(RepoFile("web", "Controllers", "AdminController.cs"));

        Assert.Contains("[HttpGet(\"help\")]", source);
        Assert.Contains("IActionResult Help()", source);
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
    public void AdminHelpView_ShouldRenderPageLevelBackToMainAdminLink()
    {
        var markup = File.ReadAllText(RepoFile("web", "Views", "Admin", "Help.cshtml"));
        var css = File.ReadAllText(RepoFile("web", "wwwroot", "css", "site.css"));

        Assert.Contains("Back to main admin page", markup);
        Assert.Contains("href=\"/admin\"", markup);
        Assert.Contains("bp-page-return-row", markup);
        Assert.Contains("bp-page-return-link", markup);
        Assert.True(
            markup.IndexOf("bp-page-return-row", StringComparison.Ordinal) <
            markup.IndexOf("bp-admin-help-hero", StringComparison.Ordinal),
            "The help return link should sit above the help hero instead of inside the content card.");

        Assert.Contains(".bp-page-return-row", css);
        Assert.Contains(".bp-page-return-link", css);
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
        Assert.Contains("Data requirements tab", markup);
        Assert.Contains("required/optional switches", markup);
        Assert.Contains("dataCompletionPolicies", markup);
        Assert.Contains("Trust &amp; conformance tab is visible only", markup);
        Assert.Contains("general admins or local cluster admins", markup);
    }

    [Fact]
    public void AdminHelpView_ShouldPointToDataRequirementsInsteadOfDuplicatingParameterList()
    {
        var markup = File.ReadAllText(RepoFile("web", "Views", "Admin", "Help.cshtml"));

        Assert.Contains("Open data requirements", markup);
        Assert.Contains("/admin/clusters?tab=data-requirements", markup);
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
        Assert.Contains("What public users can see", markup);
        Assert.Contains("What admins can see", markup);
        Assert.Contains("Open data requirements", markup);
        Assert.DoesNotContain("Parameter-by-parameter fill list", markup);

        Assert.Contains(".bp-admin-help-check-card", css);
        Assert.Contains(".bp-admin-help-split-checklists", css);
    }

    [Fact]
    public void AdminHelpView_ShouldStayVisuallyStructuredAndScannable()
    {
        var markup = File.ReadAllText(RepoFile("web", "Views", "Admin", "Help.cshtml"));
        var css = File.ReadAllText(RepoFile("web", "wwwroot", "css", "site.css"));

        Assert.Contains("bp-admin-help-overview", markup);
        Assert.Contains("bp-admin-help-quicknav", markup);
        Assert.Contains("bp-admin-help-lifecycle", markup);
        Assert.Contains("href=\"#workflow-steps\"", markup);
        Assert.Contains("id=\"target-state\"", markup);
        Assert.Contains("id=\"workflow-steps\"", markup);
        Assert.Contains("id=\"status-dictionary\"", markup);
        Assert.Contains("id=\"ready-checklist\"", markup);

        Assert.Contains(".bp-admin-help-overview", css);
        Assert.Contains(".bp-admin-help-quicknav", css);
        Assert.Contains(".bp-admin-help-lifecycle", css);
        Assert.Contains("position: sticky", css);
    }

    [Fact]
    public void AdminHelpView_ShouldRenderPolishedVisualHelpShell()
    {
        var markup = File.ReadAllText(RepoFile("web", "Views", "Admin", "Help.cshtml"));
        var css = File.ReadAllText(RepoFile("web", "wwwroot", "css", "site.css"));

        Assert.Contains("bp-admin-help-hero-panel", markup);
        Assert.Contains("bp-admin-help-trust-chip", markup);
        Assert.Contains("bp-admin-help-task-strip", markup);
        Assert.Contains("bp-admin-help-task-card", markup);
        Assert.Contains("bp-admin-help-step-card", markup);
        Assert.Contains("bp-admin-help-step-copy", markup);
        Assert.Contains("Trust gate", markup);
        Assert.Contains("Need to act now?", markup);

        Assert.Contains(".bp-admin-help-hero-panel", css);
        Assert.Contains(".bp-admin-help-trust-chip", css);
        Assert.Contains(".bp-admin-help-task-strip", css);
        Assert.Contains(".bp-admin-help-task-card", css);
        Assert.Contains(".bp-admin-help-step-card", css);
        Assert.Contains(".bp-admin-help-step-copy", css);
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
        Assert.Contains(".bp-admin-help-steps", css);
        Assert.Contains(".bp-admin-help-state-grid", css);
        Assert.Contains(".bp-admin-help-callout", css);
        Assert.Contains(".bp-admin-help-policy-link", css);
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
