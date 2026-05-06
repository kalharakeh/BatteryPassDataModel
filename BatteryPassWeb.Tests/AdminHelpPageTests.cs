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
