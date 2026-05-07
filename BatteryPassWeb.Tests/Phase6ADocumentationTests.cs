namespace BatteryPassWeb.Tests;

public sealed class Phase6ADocumentationTests
{
    [Fact]
    public void AdminController_ShouldExposeAdminOnlyDemoScenarioReset()
    {
        var source = File.ReadAllText(RepoFile("web", "Controllers", "AdminController.cs"));

        Assert.Contains("DemoScenarioResetService", source);
        Assert.Contains("[HttpPost(\"demo-scenarios/reset\")]", source);
        Assert.Contains("ResetDemoScenarios", source);
        Assert.Contains("_demoScenarioResetService.ResetAllAsync", source);
        Assert.Contains("Demo scenarios reset", source);
    }

    [Fact]
    public void AdminHelp_ShouldShowCompactPhase6ADemoResetAction()
    {
        var markup = File.ReadAllText(RepoFile("web", "Views", "Admin", "Help.cshtml"));
        var css = File.ReadAllText(RepoFile("web", "wwwroot", "css", "site.css"));

        Assert.Contains("Phase 6A demo reset", markup);
        Assert.Contains("/admin/demo-scenarios/reset", markup);
        Assert.Contains("bp-admin-help-demo-reset", markup);
        Assert.Contains(".bp-admin-help-demo-reset", css);
    }

    [Fact]
    public void SampleAccounts_ShouldDocumentEveryPhase6AScenarioPassport()
    {
        var docs = File.ReadAllText(RepoFile("docs", "sample-cluster-test-accounts.md"));

        Assert.Contains("Phase 6A demo scenarios", docs);
        Assert.Contains("did:web:acme.battery.pass:demo-published-trusted-001", docs);
        Assert.Contains("did:web:acme.battery.pass:demo-draft-incomplete-001", docs);
        Assert.Contains("did:web:acme.battery.pass:demo-ready-to-sign-001", docs);
        Assert.Contains("did:web:acme.battery.pass:demo-signed-unpublished-001", docs);
        Assert.Contains("did:web:acme.battery.pass:demo-dirty-after-edit-001", docs);
        Assert.Contains("did:web:acme.battery.pass:demo-invalid-signature-001", docs);
        Assert.Contains("did:web:acme.battery.pass:demo-restricted-document-001", docs);
    }

    [Fact]
    public void TestingGuide_ShouldDocumentPhase6AAcceptanceScript()
    {
        var guide = File.ReadAllText(RepoFile("docs", "end-user-testing-guide.md"));

        Assert.Contains("Phase 6A end-to-end demo hardening checklist", guide);
        Assert.Contains("Reset demo scenarios", guide);
        Assert.Contains("Expected state: Published, signed, clean, public, QR-ready", guide);
        Assert.Contains("Expected state: Missing required data, blocked from signing", guide);
        Assert.Contains("Expected state: Signed core changed after proof", guide);
        Assert.Contains("External HTTP telemetry update does not dirty the passport", guide);
        Assert.Contains("Restricted document download returns 403 for unauthorized users", guide);
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
