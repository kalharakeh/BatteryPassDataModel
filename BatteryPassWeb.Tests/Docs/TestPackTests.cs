using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using BatteryPassWeb.Tests.Dpp;

namespace BatteryPassWeb.Tests.Docs;

/// <summary>
/// Step 6b-2, the test pack rewritten for the new app: qa-test-pack.md is the one starting point, end-user-testing-guide.md
/// the script per reader and sample-cluster-test-accounts.md the demo data. They follow the demo catalogue, so a new demo
/// account or battery shows here. They never print a password (decision 32: DEMO_USERS_PASSWORD only).
/// </summary>
public sealed partial class TestPackTests
{
    private static string Read(params string[] path) => File.ReadAllText(DppTestFiles.RepoFile(path)).Replace("\r\n", "\n");

    private static string Pack() => Read("docs", "qa-test-pack.md");

    private static string Guide() => Read("docs", "end-user-testing-guide.md");

    private static string Accounts() => Read("docs", "sample-cluster-test-accounts.md");

    private static JsonNode Catalogue() => JsonNode.Parse(Read("web", "Dpp", "Demo", "demo-catalogue.json"))!;

    [Fact]
    public void ThePack_ShouldBeTheOneStartingPoint()
    {
        var pack = Pack();

        foreach (var heading in new[] { "# QA Tester Pack", "## Quick start", "## Tester accounts", "## Demo batteries", "## Scenario matrix", "## Acceptance checklist", "## Known limitations", "## Bug report template" })
        {
            Assert.Contains(heading, pack, StringComparison.Ordinal);
        }

        Assert.Contains("docs/end-user-testing-guide.md", pack, StringComparison.Ordinal);
        Assert.Contains("docs/sample-cluster-test-accounts.md", pack, StringComparison.Ordinal);
        Assert.Contains("`/help`", pack, StringComparison.Ordinal);
        Assert.Contains("`/help/api`", pack, StringComparison.Ordinal);
        Assert.Contains("**Reset demo data**", pack, StringComparison.Ordinal);
        Assert.Contains("`DEMO_USERS_PASSWORD`", pack, StringComparison.Ordinal);
    }

    [Fact]
    public void EveryDemoAccount_ShouldBeInThePackAndTheAccounts()
    {
        var emails = Catalogue()["accounts"]!.AsArray().Select(account => account!["email"]!.GetValue<string>()).ToArray();

        Assert.Equal(12, emails.Length);
        foreach (var email in emails)
        {
            Assert.Contains($"`{email}`", Accounts(), StringComparison.Ordinal);
            Assert.Contains($"`{email}`", Pack(), StringComparison.Ordinal);
        }
    }

    [Fact]
    public void EveryDemoBatteryAndCluster_ShouldBeInThePackAndTheAccounts()
    {
        var catalogue = Catalogue();
        foreach (var serial in catalogue["packs"]!.AsArray().Select(pack => pack!["serial"]!.GetValue<string>()))
        {
            Assert.Contains($"| {serial} |", Accounts(), StringComparison.Ordinal);
            Assert.Contains($"| {serial} |", Pack(), StringComparison.Ordinal);
        }

        foreach (var cluster in catalogue["clusters"]!.AsArray())
        {
            Assert.Contains($"| `{cluster!["id"]!.GetValue<string>()}` | {cluster["name"]!.GetValue<string>()} |", Accounts(), StringComparison.Ordinal);
        }
    }

    [Fact]
    public void TheGuide_ShouldScriptEveryKindOfReader()
    {
        var guide = Guide();

        foreach (var heading in new[] { "## 1. Anyone, not signed in", "## 2. Cluster user", "## 3. Cluster admin", "## 4. The team", "## 5. Market surveillance", "## 6. Notified body, Commission and the legitimate-interest roles", "## 7. The API" })
        {
            Assert.Contains(heading, guide, StringComparison.Ordinal);
        }

        foreach (var account in new[] { "demo.north.user", "demo.north.admin", "demo.msa", "demo.notified-body", "demo.commission", "demo.repair", "demo.recycling", "demo.treatment" })
        {
            Assert.Contains($"`{account}@example.test`", guide, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void NoTestDoc_ShouldPrintAPassword()
    {
        foreach (var doc in new[] { Pack(), Guide(), Accounts() })
        {
            Assert.DoesNotContain("Password123", doc, StringComparison.Ordinal);
            Assert.DoesNotMatch(PasswordValue(), doc);
        }
    }

    [Fact]
    public void TheHelpPages_ShouldKeepTheNewWorkflowWords()
    {
        // Kept from the old documentation tests: the help no longer describes the old evidence step, and the API help
        // names the token's value, never the old battery secret (decision 30, step 3c).
        var apiHelp = Read("web", "Views", "Help", "Index.cshtml");

        Assert.DoesNotContain("Evidence readiness", Read("web", "Views", "Help", "Guide.cshtml"), StringComparison.Ordinal);
        Assert.Contains("Token Value", apiHelp, StringComparison.Ordinal);
        Assert.DoesNotContain("create, validate, sign and publish passport token", apiHelp, StringComparison.Ordinal);
        Assert.DoesNotContain("Battery Secret", apiHelp, StringComparison.Ordinal);
    }

    [GeneratedRegex(@"(?im)\bpassword\b\s*[:=]\s*`?[^\s`|]{4,}")]
    private static partial Regex PasswordValue();
}
