using System.Text.Json.Nodes;
using BatteryPassWeb.Services;
using BatteryPassWeb.Tests.Dpp;

namespace BatteryPassWeb.Tests.Docs;

/// <summary>
/// Package D4: the documents say what the app says after N1 (new-app-v3.6) and FU (new-app-v3.7): the demo accounts by the
/// name and role the app shows, the Help's "Find a passport" and where each reader lands, the runbook's version and its
/// answers over a limit, and the release notes' two sections.
/// </summary>
public sealed class DocumentsForV36AndV37Tests
{
    private static string Read(params string[] path) => File.ReadAllText(DppTestFiles.RepoFile(path)).Replace("\r\n", "\n");

    private static string Section(string text, string start, string end)
    {
        var from = text.IndexOf(start, StringComparison.Ordinal);
        Assert.True(from >= 0, start);
        var to = text.IndexOf(end, from + start.Length, StringComparison.Ordinal);
        Assert.True(to > from, end);
        return text[from..to];
    }

    [Fact]
    public void EveryDemoAccount_ShouldBeListedWithTheNameAndRoleTheAppShows()
    {
        var accounts = Read("docs", "sample-cluster-test-accounts.md");
        var catalogue = JsonNode.Parse(Read("web", "Dpp", "Demo", "demo-catalogue.json"))!["accounts"]!.AsArray();

        foreach (var account in catalogue)
        {
            var role = account!["clusterRole"]?.GetValue<string>() is { } inCluster
                ? AccessControlService.DisplayRoleLabel(inCluster == "clusterAdmin" ? AccessControlService.RoleClusterAdmin : AccessControlService.RoleNormalUser)
                : AccessControlService.DisplayRoleLabel(account["role"]!.GetValue<string>());
            var row = $"| `{account["email"]!.GetValue<string>()}` | {account["name"]!.GetValue<string>()} | {role} |";

            Assert.Contains(row, accounts, StringComparison.Ordinal);
        }
    }

    [Theory]
    [InlineData("Recycling and battery treatment")]
    [InlineData("Cluster admin")]
    [InlineData("Cluster user")]
    [InlineData("Cluster member")]
    [InlineData("Demo north cluster admin")]
    public void TheTestDocuments_ShouldNotUseAnOldRoleName(string old)
    {
        foreach (var name in new[] { "sample-cluster-test-accounts.md", "qa-test-pack.md", "end-user-testing-guide.md" })
        {
            Assert.DoesNotContain(old, Read("docs", name), StringComparison.Ordinal);
        }
    }

    [Theory]
    [InlineData("qa-test-pack.md")]
    [InlineData("end-user-testing-guide.md")]
    public void TheTestDocuments_ShouldSayLogInAndNotSignIn(string name)
    {
        var text = Read("docs", name).Replace("sign-in keys", string.Empty, StringComparison.Ordinal).Replace("Sign-in keys", string.Empty, StringComparison.Ordinal);

        foreach (var old in new[] { "sign in", "Sign in", "sign-in", "Sign-in", "signed in", "Signed out", "signed out", "Sign out", "sign out" })
        {
            Assert.DoesNotContain(old, text, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void TheTestDocuments_ShouldScriptNearMatchesLandingAndOneVersionPerLoad()
    {
        var pack = Read("docs", "qa-test-pack.md");
        var guide = Read("docs", "end-user-testing-guide.md");

        Assert.Contains("| G17 |", pack, StringComparison.Ordinal);
        Assert.Contains("| G17b |", pack, StringComparison.Ordinal);
        Assert.Contains("lists at most three serial numbers that start with what you typed", pack, StringComparison.Ordinal);
        Assert.Contains("each gets **one** new version", pack, StringComparison.Ordinal);
        Assert.Contains("Publish the waiting changes", Section(pack, "| G6e |", "| G7 |"), StringComparison.Ordinal);
        Assert.Contains("Did you mean:", guide, StringComparison.Ordinal);
        Assert.Contains("you land on \"To do\"", guide, StringComparison.Ordinal);
        Assert.Contains("**One version per load.**", guide, StringComparison.Ordinal);
    }

    [Fact]
    public void TheHelpsFindAPassport_ShouldSayWhatTheSearchOffersAndWhereEachReaderLands()
    {
        var find = Section(Read("web", "Views", "Help", "Guide.cshtml"), "id=\"find\"", "</section>");

        Assert.Contains("Logged out, it offers the serial numbers that start with what you typed", find, StringComparison.Ordinal);
        Assert.Contains("from three characters", find, StringComparison.Ordinal);
        Assert.Contains("up to three", find, StringComparison.Ordinal);
        Assert.Contains("Logged in, it also finds part of any battery ID, passport ID or serial number among the batteries your Registry lists", find, StringComparison.Ordinal);
        Assert.Contains("the team lands on \"To do\" and everyone else on the Registry", find, StringComparison.Ordinal);
        Assert.Contains("under Admin", find, StringComparison.Ordinal);
    }

    [Fact]
    public void TheRunbook_ShouldNameTheVersionToDeploy()
    {
        var runbook = Read("docs", "aws-v3-cutover.md");

        Assert.Contains("`powershell -ExecutionPolicy Bypass -File .\\scripts\\publish-elastic-beanstalk.ps1`", runbook, StringComparison.Ordinal);
        Assert.Contains("check out `new-app-v3.7`", runbook, StringComparison.Ordinal);
        Assert.Contains("--version-label new-app-v3.7", runbook, StringComparison.Ordinal);
        Assert.DoesNotContain("--version-label new-app-v3.5", runbook, StringComparison.Ordinal);
        Assert.DoesNotContain("check out `new-app-v3.5`", runbook, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("ENABLE_RATE_LIMITING")]
    [InlineData("RATE_LIMIT_WINDOW_SECONDS")]
    [InlineData("EXTERNAL_API_READ_RATE_LIMIT_PER_WINDOW")]
    [InlineData("FILE_UPLOAD_RATE_LIMIT_PER_WINDOW")]
    [InlineData("MAX_UPLOAD_BYTES")]
    public void TheRunbooksSettings_ShouldBeReadByTheApp(string setting)
    {
        Assert.Contains($"\"{setting}\"", Read("web", "Program.cs"), StringComparison.Ordinal);
        Assert.Contains($"`{setting}`", Read("docs", "aws-v3-cutover.md"), StringComparison.Ordinal);
    }

    [Fact]
    public void TheRunbook_ShouldSayWhatAnAddressOverALimitAndAnUnknownAddressAnswer()
    {
        var check = Section(Read("docs", "aws-v3-cutover.md"), "18. **Over a limit", "## 7. Restore points");

        Assert.Contains("Too many searches from your address. Try again in a minute.", check, StringComparison.Ordinal);
        Assert.Contains("{\"error\": \"Too many requests.\"}", check, StringComparison.Ordinal);
        Assert.Contains("status 404", check, StringComparison.Ordinal);
        Assert.Contains("{\"error\": \"Something went wrong. Try again later.\"}", check, StringComparison.Ordinal);
        Assert.Contains("a failed search uses two of the public read allowance", check, StringComparison.Ordinal);
        Assert.Contains("counts per signed-in person", Read("docs", "aws-v3-cutover.md"), StringComparison.Ordinal);
    }

    [Fact]
    public void TheReleaseNotes_ShouldHaveASectionForEachOfTheTwoVersions()
    {
        var notes = Read("RELEASE_NOTES.md");
        var v37 = notes.IndexOf("## new-app-v3.7", StringComparison.Ordinal);
        var v36 = notes.IndexOf("## new-app-v3.6", StringComparison.Ordinal);
        var v35 = notes.IndexOf("## new-app-v3.5", StringComparison.Ordinal);

        Assert.True(v37 >= 0 && v37 < v36 && v36 < v35, "newest first: v3.7, v3.6, v3.5");
        Assert.Contains("### Before you update", notes[v37..v36], StringComparison.Ordinal);
        Assert.Contains("### For the team", notes[v36..v35], StringComparison.Ordinal);
    }
}
