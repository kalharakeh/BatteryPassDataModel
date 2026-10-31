namespace BatteryPassWeb.Tests;

public sealed class Phase6ADocumentationTests
{
    [Fact]
    public void SampleAccounts_ShouldDocumentEveryProductTemplatePassport()
    {
        var docs = File.ReadAllText(RepoFile("docs", "sample-cluster-test-accounts.md"));

        Assert.Contains("Battery Family Reset", docs);
        Assert.Contains("Compact 7M", docs);
        Assert.Contains("Compact 13M", docs);
        Assert.Contains("Core", docs);
        Assert.Contains("Demo API Cluster", docs);
        Assert.Contains("demo-cluster", docs);
        Assert.Contains("CP7M-DEMO-API-001", docs);
        Assert.Contains("SAMPLEBATTERYPASSPORTREADTOKN001", docs);
        Assert.Contains("sample-customer-north-001", docs);
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
        var help = File.ReadAllText(RepoFile("web", "Views", "Help", "Guide.cshtml"));

        Assert.Contains("Phase 6B", guide);
        Assert.Contains("Evidence readiness", guide);
        Assert.Contains("Upload or replace", guide);
        Assert.Contains("Changed since signing", guide);
        Assert.Contains("sign the passport again", guide, StringComparison.OrdinalIgnoreCase);

        // The team's help no longer describes the old evidence-readiness step: publishing checks and signs (decision 30).
        Assert.DoesNotContain("Evidence readiness", help);
    }

    [Fact]
    public void Guidance_ShouldMatchCurrentProductTemplateSoftwareAndUiWorkflow()
    {
        var guide = File.ReadAllText(RepoFile("docs", "end-user-testing-guide.md"));

        Assert.Contains("software parameters", guide);
        Assert.Contains("Detailed report tabs", guide);
        Assert.Contains("software version parameter", guide);
        Assert.Contains("General tab", guide);
        Assert.Contains("software-version endpoint", guide);
    }

    [Fact]
    public void Documentation_ShouldExplainVersionedTemplatesCredentialsAndResetAccounts()
    {
        var apiHelp = File.ReadAllText(RepoFile("web", "Views", "Help", "Index.cshtml"));
        var guide = File.ReadAllText(RepoFile("docs", "end-user-testing-guide.md"));
        var qa = File.ReadAllText(RepoFile("docs", "qa-test-pack.md"));
        var accounts = File.ReadAllText(RepoFile("docs", "sample-cluster-test-accounts.md"));

        Assert.Contains("Battery Model", guide, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("API Token Management", guide);
        Assert.Contains("eight batteries and nine passport snapshots", guide);
        Assert.Contains("API Token Management", qa);
        Assert.Contains("eight batteries and nine passport snapshots", qa);
        Assert.Contains("customer_001_001@customer.org", accounts);
        Assert.Contains("demo.user@example.test", accounts);
        Assert.Contains("12345", accounts);
        Assert.Contains("CP7M-NORTH-002", accounts);
        Assert.Contains("CP13M-SOUTH-002", accounts);
        Assert.Contains("CORE-FLEET-002", accounts);
        Assert.Contains("Token Value", apiHelp);
        Assert.DoesNotContain("create, validate, sign and publish passport token", apiHelp);
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
