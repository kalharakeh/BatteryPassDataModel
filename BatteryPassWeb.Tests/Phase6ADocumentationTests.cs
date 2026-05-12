namespace BatteryPassWeb.Tests;

public sealed class Phase6ADocumentationTests
{
    [Fact]
    public void AdminController_ShouldExposeAdminOnlyProductTemplateReset()
    {
        var source = File.ReadAllText(RepoFile("web", "Controllers", "AdminController.cs"));

        Assert.Contains("ProductTemplateService", source);
        Assert.Contains("[HttpPost(\"product-templates/reset\")]", source);
        Assert.Contains("ResetProductTemplateDemo", source);
        Assert.Contains("_productTemplateService.ResetTemplateDemoAsync", source);
        Assert.Contains("Product template demo reset completed", source);
    }

    [Fact]
    public void AdminHelp_ShouldShowCompactProductTemplateResetAction()
    {
        var markup = File.ReadAllText(RepoFile("web", "Views", "Admin", "Help.cshtml"));
        var css = File.ReadAllText(RepoFile("web", "wwwroot", "css", "site.css"));

        Assert.Contains("Battery family reset", markup);
        Assert.Contains("/admin/product-templates/reset", markup);
        Assert.Contains("bp-admin-help-demo-reset", markup);
        Assert.Contains(".bp-admin-help-demo-reset", css);
    }

    [Fact]
    public void SampleAccounts_ShouldDocumentEveryProductTemplatePassport()
    {
        var docs = File.ReadAllText(RepoFile("docs", "sample-cluster-test-accounts.md"));

        Assert.Contains("Battery Family Reset", docs);
        Assert.Contains("Compact 7M", docs);
        Assert.Contains("Compact 13M", docs);
        Assert.Contains("Core", docs);
        Assert.Contains("did:web:acme.battery.pass:0226151e-949c-d067-8ef3-162431e28976", docs);
        Assert.Contains("did:web:acme.battery.pass:sample-customer-north-001", docs);
        Assert.Contains("did:web:acme.battery.pass:sample-customer-south-001", docs);
        Assert.Contains("did:web:acme.battery.pass:sample-end-user-fleet-001", docs);
        Assert.DoesNotContain("sample-end-user-storage-001", docs);
    }

    [Fact]
    public void TestingGuide_ShouldDocumentPhase6AAcceptanceScript()
    {
        var guide = File.ReadAllText(RepoFile("docs", "end-user-testing-guide.md"));

        Assert.Contains("Phase 6A end-to-end demo hardening checklist", guide);
        Assert.Contains("Reset battery-family passports", guide);
        Assert.Contains("Battery Family baseline checklist", guide);
        Assert.Contains("Expected state: Published, signed, clean, public, QR-ready", guide);
        Assert.Contains("Confirm three Battery families are listed", guide);
        Assert.Contains("Confirm matching batteries preserve manual overrides", guide);
        Assert.Contains("External HTTP telemetry update does not dirty the passport", guide);
        Assert.Contains("Restricted document download returns 403 for unauthorized users", guide);
    }

    [Fact]
    public void Documentation_ShouldExplainPhase6BEvidenceWorkflow()
    {
        var guide = File.ReadAllText(RepoFile("docs", "end-user-testing-guide.md"));
        var help = File.ReadAllText(RepoFile("web", "Views", "Admin", "Help.cshtml"));

        Assert.Contains("Phase 6B", guide);
        Assert.Contains("Evidence readiness", guide);
        Assert.Contains("Upload or replace", guide);
        Assert.Contains("Changed since signing", guide);
        Assert.Contains("sign the passport again", guide, StringComparison.OrdinalIgnoreCase);

        Assert.Contains("Document evidence", help);
        Assert.Contains("Evidence readiness", help);
        Assert.Contains("document hash", help, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("sign again", help, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Guidance_ShouldMatchCurrentProductTemplateSoftwareAndUiWorkflow()
    {
        var apiHelp = File.ReadAllText(RepoFile("web", "Views", "Help", "Index.cshtml"));
        var adminHelp = File.ReadAllText(RepoFile("web", "Views", "Admin", "Help.cshtml"));
        var guide = File.ReadAllText(RepoFile("docs", "end-user-testing-guide.md"));

        Assert.Contains("software parameters", apiHelp);
        Assert.Contains("Battery Family baseline", apiHelp);
        Assert.Contains("no software update endpoint", apiHelp);
        Assert.Contains("Battery Model changes require validation and signing", apiHelp);

        Assert.Contains("Software parameters", adminHelp);
        Assert.Contains("Detailed report General tab", adminHelp);
        Assert.Contains("Battery Model API changes require a new passport", adminHelp);
        Assert.Contains("release date and latest update values", adminHelp);

        Assert.Contains("software parameters", guide);
        Assert.Contains("Detailed report tabs", guide);
        Assert.Contains("software version parameter", guide);
        Assert.Contains("General tab", guide);
        Assert.Contains("no software update endpoint", guide);
    }

    [Fact]
    public void Documentation_ShouldExplainVersionedTemplatesCredentialsAndResetAccounts()
    {
        var apiHelp = File.ReadAllText(RepoFile("web", "Views", "Help", "Index.cshtml"));
        var adminHelp = File.ReadAllText(RepoFile("web", "Views", "Admin", "Help.cshtml"));
        var guide = File.ReadAllText(RepoFile("docs", "end-user-testing-guide.md"));
        var qa = File.ReadAllText(RepoFile("docs", "qa-test-pack.md"));
        var accounts = File.ReadAllText(RepoFile("docs", "sample-cluster-test-accounts.md"));

        Assert.Contains("Battery Model", guide, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("API Token Management", guide);
        Assert.Contains("one unassigned demonstrator plus six clustered customer batteries", guide);
        Assert.Contains("API Token Management", qa);
        Assert.Contains("one unassigned demonstrator plus six clustered customer batteries", qa);
        Assert.Contains("customer_001_001@customer.org", accounts);
        Assert.Contains("12345", accounts);
        Assert.Contains("CP7M-NORTH-002", accounts);
        Assert.Contains("CP13M-SOUTH-002", accounts);
        Assert.Contains("CORE-FLEET-002", accounts);
        Assert.Contains("Local editable fields", adminHelp);
        Assert.Contains("Token Value", apiHelp);
        Assert.Contains("Sign tokens", apiHelp);
        Assert.DoesNotContain("Battery Secret", apiHelp);
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
