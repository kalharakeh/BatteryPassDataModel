using BatteryPassWeb.Tests.Dpp;

namespace BatteryPassWeb.Tests.Look;

/// <summary>
/// Redesign R6 (the review of the built R5): the passport hero's buttons said "&lt;- Back to summary" and "View more about
/// this passport -&gt;" with typed arrows; they carry the app's arrow icons instead, like every other button.
/// </summary>
public sealed class HeroButtonIconsTests
{
    [Fact]
    public void TheHerosButtons_ShouldUseTheArrowIcons_NotTypedArrows()
    {
        var hero = File.ReadAllText(DppTestFiles.RepoFile("web", "Views", "Passport", "_PassportHero.cshtml"));
        var css = File.ReadAllText(DppTestFiles.RepoFile("web", "wwwroot", "css", "bp-status.css"));

        Assert.DoesNotContain("&lt;-", hero, StringComparison.Ordinal);
        Assert.DoesNotContain("-&gt;", hero, StringComparison.Ordinal);
        Assert.Contains("<use href=\"#i-back\" /></svg>Back to the public page</a>", hero, StringComparison.Ordinal);
        Assert.Contains("View more about this passport<svg class=\"bp-i\" aria-hidden=\"true\"><use href=\"#i-arrow\" /></svg></a>", hero, StringComparison.Ordinal);
        Assert.Contains("Open the battery page<svg class=\"bp-i\" aria-hidden=\"true\"><use href=\"#i-arrow\" /></svg></a>", hero, StringComparison.Ordinal); // one name since R6 T6
        Assert.Contains(".bp-report-hero-cta { gap: 8px; }", css, StringComparison.Ordinal);
    }
}
