using BatteryPassWeb.Tests.Dpp;

namespace BatteryPassWeb.Tests.Look;

/// <summary>
/// Step 6d-3: the Help and the test documents for battery IDs released in advance (6d-2, in tabs since 6d-2b): the
/// team's own part (the tabs, a release's page, the red notice), the workflow's new first step, a label scanned too
/// early, and the test pack, script and accounts page brought up to date.
/// </summary>
public sealed class BatteryIdsHelpTests
{
    private static string Read(params string[] parts) => File.ReadAllText(DppTestFiles.RepoFile(parts)).Replace("\r\n", "\n");

    private static readonly string Guide = Read("web", "Views", "Help", "Guide.cshtml");

    private static string Part(string id)
    {
        var start = Guide.IndexOf($"id=\"{id}\"", StringComparison.Ordinal);
        Assert.True(start >= 0, $"no part {id}");
        return Guide[start..Guide.IndexOf("</section>", start, StringComparison.Ordinal)];
    }

    [Fact]
    public void TheTeam_ShouldHaveAPartOnBatteryIds_InItsContents()
    {
        var part = Part("battery-ids");

        Assert.True(Guide.IndexOf("@if (isTeam)", StringComparison.Ordinal) < Guide.IndexOf("id=\"battery-ids\"", StringComparison.Ordinal)); // the team's only
        Assert.Contains("<a href=\"@Part(\"battery-ids\")\">Battery IDs</a>", Read("web", "Views", "Shared", "_HelpContents.cshtml"), StringComparison.Ordinal);
        foreach (var words in new[] { "href=\"/admin/battery-ids\"", "all or nothing", "the app opens the release's page", "Releases", "All IDs", "Show demo IDs", "A release whose IDs are all voided shows only with \"Show voided\" ticked", "voided IDs only with \"Show voided\" ticked or the status Voided chosen", "⋯ menu", "Export to Excel", "Available, Used", "Voided", "Void unused…", "Change its battery ID…", "Removing a draft gives its ID back", "-R1, -R2", "The battery ID settings aren't set", "DPP_ID_BASE_URL", "The demo data works without them" })
        {
            Assert.Contains(words, part, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void TheWorkflow_ShouldReleaseFirst_AndPickTheSerialNumber()
    {
        var workflow = Part("workflow");

        Assert.True(workflow.IndexOf("Release battery IDs", StringComparison.Ordinal) < workflow.IndexOf("Create the battery", StringComparison.Ordinal));
        Assert.Contains("pick its serial number from the released IDs", workflow, StringComparison.Ordinal);
        Assert.DoesNotContain("type its serial number", workflow, StringComparison.Ordinal);
        Assert.Contains("A label scanned before its battery's passport is published says \"Not published yet\".", Part("find"), StringComparison.Ordinal); // for everyone
    }

    [Fact]
    public void TheTestPack_ShouldCoverTheBatteryIdsTab()
    {
        var pack = Read("docs", "qa-test-pack.md");
        var script = Read("docs", "end-user-testing-guide.md");

        Assert.Contains("| Mark as invalid | Battery IDs tab |", pack, StringComparison.Ordinal); // a column for every reader
        Assert.Equal(11, pack.Split('\n').Count(line => line.StartsWith('|') && (line.EndsWith("| – (Not allowed) |", StringComparison.Ordinal) || line.EndsWith("| – (sign-in) |", StringComparison.Ordinal) || line.EndsWith("| ✓ (and undo) | ✓ |", StringComparison.Ordinal))));
        foreach (var check in new[] { "Battery IDs: a range releases", "lists every reason and releases nothing", "Create battery picks a released serial number", "opens the release's page", "The Battery IDs page opens on Releases", "\"Show demo IDs\" ticked", "voided IDs only with \"Show voided\" ticked", "a release whose IDs are all voided only with it ticked", "red notice naming it", "\"Change its battery ID…\" on a draft", "\"Not published yet\"" })
        {
            Assert.Contains(check, pack, StringComparison.Ordinal);
        }

        Assert.DoesNotContain("until the identifier scheme is built", pack, StringComparison.Ordinal);
        Assert.Contains("Battery IDs tab: release VPC-12221 to VPC-12230 with \"Release new IDs\"", script, StringComparison.Ordinal);
        Assert.Contains("the release's page opens", script, StringComparison.Ordinal);
        Assert.Contains("in its row's ⋯ menu, Void…", script, StringComparison.Ordinal);
        Assert.Contains("it leaves the list until \"Show voided\" is ticked", script, StringComparison.Ordinal);
        Assert.Contains("\"batterySerialNumber\": \"DEMO-SPARE-0001\"", script, StringComparison.Ordinal);
        Assert.Contains("DEMO-SPARE-0001 to DEMO-SPARE-0004", Read("docs", "sample-cluster-test-accounts.md"), StringComparison.Ordinal);
    }

    [Fact]
    public void TheHelpAndTheDocs_ShouldWriteTheBatteryIdWithItsDash()
    {
        // 6d-5 (the owner, 9 Nov 2026): the serial as written, after one dash; successors -R1, -R2; a demo's -TEST-.
        var part = Part("battery-ids");
        var pack = Read("docs", "qa-test-pack.md");
        var script = Read("docs", "end-user-testing-guide.md");
        var runbook = Read("docs", "aws-v3-cutover.md");
        var accounts = Read("docs", "sample-cluster-test-accounts.md");

        Assert.Contains("<code>UN778474481-VPC-12221</code>", part, StringComparison.Ordinal);
        Assert.Contains("capital letters, digits and single dashes", part, StringComparison.Ordinal);
        Assert.Contains("adds -R1, -R2, …", part, StringComparison.Ordinal);
        Assert.Contains("`UN778474481-TEST-DEMO-C7M-0001`", pack, StringComparison.Ordinal);
        Assert.Contains("`UN778474481-TEST-DEMO-C7M-0001`", accounts, StringComparison.Ordinal);
        Assert.Contains("(`UN778474481-VPC-12221` …)", script, StringComparison.Ordinal);
        Assert.Contains("`/25S/UN778474481-VPC-12223`", script, StringComparison.Ordinal);
        Assert.Contains("A demo ID reads `UN778474481-TEST-…`", runbook, StringComparison.Ordinal);
        foreach (var text in new[] { Guide, pack, script, runbook, accounts })
        {
            Assert.DoesNotMatch("UN778474481[A-Z0-9]", text); // no ID left in the old form
        }
    }

    [Fact]
    public void TheDemoSpares_ShouldBeTheOnesTheDocsName() =>
        Assert.Equal(["DEMO-SPARE-0001", "DEMO-SPARE-0002", "DEMO-SPARE-0003", "DEMO-SPARE-0004"], BatteryPassWeb.Dpp.Demo.DemoDataService.SpareSerials);
}
