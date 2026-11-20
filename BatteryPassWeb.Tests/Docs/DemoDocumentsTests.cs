using BatteryPassWeb.Tests.Dpp;

namespace BatteryPassWeb.Tests.Docs;

/// <summary>
/// Finding D14: the demo document says what the app shows after "Reset demo data" (checked on the running app: DEMO-C7M-0002
/// is ready to publish, DEMO-C13M-0002 and DEMO-CORE-0002 each miss one value), and the design document writes the battery
/// ID with its dashes (decision 38).
/// </summary>
public sealed class DemoDocumentsTests
{
    private static string Read(params string[] parts) => File.ReadAllText(DppTestFiles.RepoFile(parts));

    [Theory]
    [InlineData("| DEMO-C7M-0002 | Compact, Compact 7M | demo-north | Draft, ready to publish |")]
    [InlineData("| DEMO-C13M-0002 | Compact, Compact 13M | demo-south | Draft, 1 value missing |")]
    [InlineData("| DEMO-CORE-0002 | Core, Core | demo-fleet | Draft, 1 value missing |")]
    public void TheDemoDocument_ShouldSayWhatTheAppShows(string row) =>
        Assert.Contains(row, Read("docs", "sample-cluster-test-accounts.md"), StringComparison.Ordinal);

    [Fact]
    public void TheDemoDocument_ShouldNotSayTheReadyDraftMissesValues() =>
        Assert.DoesNotContain("| DEMO-C7M-0002 | Compact, Compact 7M | demo-north | Draft | Missing values", Read("docs", "sample-cluster-test-accounts.md"), StringComparison.Ordinal);

    [Fact]
    public void TheDesignsSection10_ShouldWriteTheBatteryIdWithItsDashes()
    {
        var design = Read("docs", "superpowers", "specs", "2026-10-28-dpp-v3-compliance-rebuild-design.md");
        var section = design[design.IndexOf("## 10. Identifiers", StringComparison.Ordinal)..design.IndexOf("## 11.", StringComparison.Ordinal)];

        Assert.Contains("/25S/UN778474481-VPC-12221", section, StringComparison.Ordinal);
        Assert.DoesNotContain("UN778474481VPC", section, StringComparison.Ordinal);
        Assert.Contains("`-R1`", section, StringComparison.Ordinal);
    }
}
