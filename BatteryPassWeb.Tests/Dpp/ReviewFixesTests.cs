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
    public void Help_ShouldBeOnePlaceWithTheTeamsPartsAndTheApi()
    {
        var contents = Read("web", "Views", "Shared", "_HelpContents.cshtml");
        var help = Read("web", "Controllers", "HelpController.cs");

        Assert.Contains("<a href=\"/help/api\"", contents);
        Assert.Contains("User.IsInRole(AccessControlService.RoleAdmin)", contents); // the team's parts only for the team (step 3e)
        Assert.Contains("[HttpGet(\"api\")]", help);
        Assert.Contains("public IActionResult Guide() => View(\"Guide\");", help);
        foreach (var page in new[] { Read("web", "Views", "Help", "Guide.cshtml"), Read("web", "Views", "Help", "Index.cshtml") })
        {
            Assert.Contains("_HelpContents", page);
        }

        Assert.Contains("href=\"/help/api\">Developer help</a>", Read("web", "Views", "Shared", "_Layout.cshtml"));
    }

    [Fact]
    public void TheTeamsBatteryPage_ShouldLinkItsPassportPagesQrCodeAndAuditEntries()
    {
        var page = DppTestFiles.BatteryPageSource();

        Assert.Contains("href=\"/dpp/@batteryKey\"", page);
        Assert.Contains("href=\"/dpp/@batteryKey/detail\"", page);
        Assert.Contains("href=\"/qr/@batteryKey/svg\"", page);
        Assert.Contains("href=\"/qr/@batteryKey/download\"", page);
        Assert.Contains("href=\"/admin/audit?q=@Uri.EscapeDataString(Model.Pack.Id)\"", page);
        Assert.Contains("/dpp/@Uri.EscapeDataString(row.Version.DigitalProductPassportId)/detail", page); // each version opens
        Assert.Contains("@row.Version.Proof.Hash", page); // and shows its signature details
    }

    [Fact]
    public void EveryConditionalAttribute_ShouldSayWhenItApplies()
    {
        var conditional = BatteryPassWeb.Dpp.Catalog.AttributeCatalog.FileAttributes.Where(a => a.Requirement == BatteryPassWeb.Dpp.Catalog.Requirement.Conditional).ToArray();

        Assert.Equal(13, conditional.Length);
        Assert.All(conditional, a => Assert.False(string.IsNullOrWhiteSpace(BatteryPassWeb.Dpp.Catalog.AttributeCatalog.ConditionOf(a))));
        Assert.All(BatteryPassWeb.Dpp.Catalog.AttributeCatalog.All.Except(conditional), a => Assert.Null(BatteryPassWeb.Dpp.Catalog.AttributeCatalog.ConditionOf(a)));
        Assert.Equal("Not needed when the battery contains no nickel.", BatteryPassWeb.Dpp.Catalog.AttributeCatalog.ConditionOf(BatteryPassWeb.Dpp.Catalog.AttributeCatalog.ByNumber(49)));
        Assert.Contains("AttributeCatalog.ConditionOf(definition)", Read("web", "Views", "ProductData", "_EditorField.cshtml"));
    }

    [Fact]
    public void TheSharedNote_ShouldNameTreatmentFacilities()
    {
        Assert.Contains("repairers, recyclers and treatment facilities", Read("web", "Dpp", "Pages", "PassportPages.cs"));
    }
}
