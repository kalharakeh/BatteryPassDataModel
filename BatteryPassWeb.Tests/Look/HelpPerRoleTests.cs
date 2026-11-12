using BatteryPassWeb.Tests.Dpp;

namespace BatteryPassWeb.Tests.Look;

/// <summary>
/// Step 6b-1, the Help per role (approved by the product owner on 4 Nov 2026): a part for every kind of reader of design
/// 5.2, the two badges' statuses in one shared list, printing, and a small Help for visitors who aren't signed in (Q1:
/// Find a passport, What each reader sees and Statuses only, with a Help link beside Login).
/// </summary>
public sealed class HelpPerRoleTests
{
    private static string Read(params string[] parts) => File.ReadAllText(DppTestFiles.RepoFile(parts)).Replace("\r\n", "\n");

    private static readonly string Guide = Read("web", "Views", "Help", "Guide.cshtml");
    private static readonly string Contents = Read("web", "Views", "Shared", "_HelpContents.cshtml");

    private static string Part(string id)
    {
        var start = Guide.IndexOf($"id=\"{id}\"", StringComparison.Ordinal);
        Assert.True(start >= 0, $"no part {id}");
        var end = Guide.IndexOf("</section>", start, StringComparison.Ordinal);
        return Guide[start..end];
    }

    [Fact]
    public void EveryKindOfReader_ShouldHaveTheirPart()
    {
        Assert.Contains("var inACluster = !isTeam && !runsACluster && User.IsInRole(AccessControlService.RoleNormalUser);", Guide, StringComparison.Ordinal);
        Assert.Contains("var isMsa = User.IsInRole(AccessControlService.RoleMarketSurveillanceAuthority);", Guide, StringComparison.Ordinal);
        foreach (var role in new[] { "RoleNotifiedBody", "RoleCommission", "RoleLiRepair", "RoleLiRecycling", "RoleLiTreatment" })
        {
            Assert.Contains($"AccessControlService.{role}", Guide, StringComparison.Ordinal);
            Assert.Contains($"AccessControlService.{role}", Contents, StringComparison.Ordinal);
        }

        foreach (var (condition, id) in new[] { ("@if (inACluster)", "your-batteries"), ("@if (isMsa)", "msa"), ("@if (authorityOrInterest.Length > 0)", "your-access"), ("@if (runsACluster)", "look-after"), ("@if (isTeam)", "lifecycle") })
        {
            Assert.True(Guide.IndexOf(condition, StringComparison.Ordinal) < Guide.IndexOf($"id=\"{id}\"", StringComparison.Ordinal), id);
            Assert.Contains($"Part(\"{id}\")", Contents, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void TheOtherRoles_ShouldBeToldExactlyWhatTheyAdd()
    {
        Assert.Contains("Sees: \"the authorities' values: the economic operator's and manufacturer's IDs, the service provider and the test reports\"", Guide, StringComparison.Ordinal);
        Assert.Contains("and the authorities' values; not the purchaser's values\"", Guide, StringComparison.Ordinal);
        Assert.Contains("Sees: \"the materials, the dismantling manuals, the part numbers, the spare parts sources and the safety measures\"", Guide, StringComparison.Ordinal);
        Assert.Contains("Sees: \"the materials, the dismantling manuals and the safety measures\"", Guide, StringComparison.Ordinal);
        Assert.Contains("Sees: \"the safety measures\"", Guide, StringComparison.Ordinal);
    }

    [Fact]
    public void TheStatuses_ShouldBeOneSharedList()
    {
        var meanings = Read("web", "Views", "Shared", "_StatusMeanings.cshtml");

        Assert.Contains("@await Html.PartialAsync(\"_StatusMeanings\")", Part("statuses"), StringComparison.Ordinal);
        Assert.Contains("@await Html.PartialAsync(\"_StatusMeanings\")", Read("web", "Views", "Shared", "_StatusHelp.cshtml"), StringComparison.Ordinal);
        Assert.Contains("<dl class=\"bp-status-meanings\">", meanings, StringComparison.Ordinal);
        Assert.Equal(5, meanings.Split("StatusBadge.Battery(").Length - 1);
        Assert.Equal(6, meanings.Split("StatusBadge.Passport(").Length - 1);
        Assert.Contains(".bp-status-meanings {\n  display: grid;", Read("web", "wwwroot", "css", "bp-status.css"), StringComparison.Ordinal);
    }

    [Fact]
    public void AClusterAdmin_ShouldLearnTheBatteryPagesActions()
    {
        var part = Part("look-after");

        foreach (var words in new[] { "<dt>Live values</dt>", "<dt>Accident reports</dt>", "\"Add a report\"", "<dt>Change status</dt>", "Repaired, Prepared for re-use and placed on the market again, Remanufactured, Repurposed or Waste", "<dt>Add a note</dt>", "<dt>Hand over…</dt>", "<dt>Waiting for you</dt>", "Accept or Decline", "is read only" })
        {
            Assert.Contains(words, part, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void MarketSurveillance_ShouldLearnToMarkAPassportInvalid()
    {
        var part = Part("msa");

        Assert.Contains("<strong>Mark as invalid…</strong>", part, StringComparison.Ordinal);
        Assert.Contains("only the team can undo it", part, StringComparison.Ordinal);
    }

    [Fact]
    public void AVisitor_ShouldGetTheSmallPublicHelp()
    {
        // Signed out: Find a passport, What each reader sees and Statuses (the owner, Q1); printing and the API stay signed-in.
        Assert.Contains("[AllowAnonymous]\n    [HttpGet(\"\")]\n    public IActionResult Guide() => View(\"Guide\");", Read("web", "Controllers", "HelpController.cs"), StringComparison.Ordinal);
        foreach (var page in new[] { Guide, Contents })
        {
            var signedInOnly = page.IndexOf("@if (signedIn)", StringComparison.Ordinal);
            Assert.True(signedInOnly >= 0);
            Assert.True(signedInOnly < page.IndexOf(page == Guide ? "id=\"print\"" : "Part(\"print\")", StringComparison.Ordinal));
            Assert.True(page.IndexOf("Statuses", StringComparison.Ordinal) < signedInOnly);
        }

        Assert.True(Guide.LastIndexOf("@if (signedIn)", StringComparison.Ordinal) < Guide.IndexOf("id=\"api\"", StringComparison.Ordinal));
        Assert.True(Contents.LastIndexOf("@if (signedIn)", StringComparison.Ordinal) < Contents.IndexOf("<p>For systems</p>", StringComparison.Ordinal));
        Assert.Contains("<a href=\"/help\">Help</a>\n                            <a href=\"/login\">Login</a>", Read("web", "Views", "Shared", "_Layout.cshtml"), StringComparison.Ordinal);
    }
}
