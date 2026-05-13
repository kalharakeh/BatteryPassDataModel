namespace BatteryPassWeb.Tests;

public sealed class QaTesterPackDocumentationTests
{
    [Fact]
    public void QaTesterPack_ShouldGiveExternalTestersOneStartingPoint()
    {
        var docs = File.ReadAllText(RepoFile("docs", "qa-test-pack.md"));

        Assert.Contains("# QA Tester Pack", docs);
        Assert.Contains("Quick start", docs);
        Assert.Contains("Open the app link provided by the project owner", docs);
        Assert.Contains("Tester accounts", docs);
        Assert.Contains("Scenario matrix", docs);
        Assert.Contains("Acceptance checklist", docs);
        Assert.Contains("Known limitations", docs);
        Assert.Contains("Bug report template", docs);
        Assert.Contains("docs/end-user-testing-guide.md", docs);
        Assert.Contains("docs/sample-cluster-test-accounts.md", docs);
        Assert.Contains("/help", docs);
        Assert.Contains("/admin/help", docs);
        Assert.Contains("sample-customer-north-001", docs);
        Assert.Contains("Demo API battery", docs);
        Assert.Contains("sample Battery ID", docs, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("admin@example.test", docs);
        Assert.Contains("Password123!", docs);
        Assert.Contains("validate, sign, publish", docs, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("software parameters", docs, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("QR", docs);
        Assert.Contains("document evidence", docs, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Start the app", docs);
        Assert.DoesNotContain("MongoDB", docs);
    }

    [Fact]
    public void TestAccountDocs_ShouldMatchDenseAdminWorkflow()
    {
        var guide = File.ReadAllText(RepoFile("docs", "end-user-testing-guide.md"));
        var qa = File.ReadAllText(RepoFile("docs", "qa-test-pack.md"));
        var accounts = File.ReadAllText(RepoFile("docs", "sample-cluster-test-accounts.md"));
        var combined = string.Join(Environment.NewLine, guide, qa, accounts);

        Assert.DoesNotContain("/admin/clusters?tab=passports", combined);
        Assert.Contains("/admin/clusters?tab=batteries", combined);
        Assert.Contains("/admin/clusters?tab=battery", combined);
        Assert.Contains("Battery cluster assignments", combined);
        Assert.Contains("dense table", combined, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("membership count", combined, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("multiple cluster memberships", combined, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Token ID", combined);
        Assert.Contains("Token Value", combined);
        Assert.Contains("scope count", combined, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("inline row", combined, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("collapsed by default", combined, StringComparison.OrdinalIgnoreCase);
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
