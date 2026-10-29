using BatteryPassWeb.Dpp.Pages;

namespace BatteryPassWeb.Tests.Dpp;

/// <summary>Decision 30: what the review of 29 Oct 2026 asked to fix or tidy.</summary>
public sealed class ReviewFixesTests
{
    private static string Read(params string[] parts) => File.ReadAllText(DppTestFiles.RepoFile(parts));

    [Fact]
    public void ASignedInUserWithoutAccess_ShouldSeeNotAllowedInsteadOfARedirectLoop()
    {
        Assert.Contains("options.AccessDeniedPath = \"/not-allowed\";", Read("web", "Program.cs"));
        Assert.Contains("[HttpGet(\"not-allowed\")]", Read("web", "Controllers", "HomeController.cs"));
        Assert.Contains("You can't open this page", Read("web", "Views", "Home", "NotAllowed.cshtml"));
    }

    [Theory]
    [InlineData("form", "typed in the app")]
    [InlineData("system", "set by the app")]
    [InlineData("feed", "battery system")]
    public void WhereAValueCameFrom_ShouldBeInPlainWords(string source, string words)
    {
        Assert.Equal(words, PassportPages.SourceWord(source));
    }

    [Fact]
    public void AClusterAdminsToken_ShouldOnlyReadOrSendLiveValues()
    {
        var form = Read("web", "Views", "ClusterAdmin", "ApiTokens.cshtml");
        var controller = Read("web", "Controllers", "ClusterAdminController.cs");

        Assert.DoesNotContain("Full access</option>", form);
        Assert.DoesNotContain("Passport Lifecycle</option>", form);
        Assert.Contains(": ExternalTokenAccessMode.ReadWrite;", controller); // nothing above read + write, whatever is posted
        Assert.DoesNotContain("Include unassigned batteries", Read("web", "Views", "Admin", "Clusters.cshtml"));
    }

    [Fact]
    public void Help_ShouldBeOneAreaWithAGuideATeamTabAndTheApi()
    {
        var tabs = Read("web", "Views", "Shared", "_HelpTabs.cshtml");
        var help = Read("web", "Controllers", "HelpController.cs");

        Assert.Contains("href=\"/help\">Guide</a>", tabs);
        Assert.Contains("href=\"/help/team\">Team</a>", tabs);
        Assert.Contains("href=\"/help/api\">API</a>", tabs);
        Assert.Contains("User.IsInRole(AccessControlService.RoleAdmin)", tabs); // the team tab only for the team
        Assert.Contains("[HttpGet(\"api\")]", help);
        Assert.Contains("public IActionResult Guide() => View(\"Guide\");", help);
        foreach (var page in new[] { Read("web", "Views", "Help", "Guide.cshtml"), Read("web", "Views", "Help", "Index.cshtml"), Read("web", "Views", "Admin", "Help.cshtml") })
        {
            Assert.Contains("_HelpTabs", page);
        }

        Assert.Contains("href=\"/help/api\">Developer help</a>", Read("web", "Views", "Shared", "_Layout.cshtml"));
    }

    [Fact]
    public void TheSharedNote_ShouldNameTreatmentFacilities()
    {
        Assert.Contains("repairers, recyclers and treatment facilities", Read("web", "Dpp", "Pages", "PassportPages.cs"));
    }
}
