using BatteryPassWeb.Tests.Dpp;

namespace BatteryPassWeb.Tests.Look;

/// <summary>
/// Redesign R6, the owner's choice 5 (3 Nov 2026): the public page had two names ("Public page" on the icons, "summary"
/// on the detail page's button and in help texts); it is "the public page" in every visible word. Code names stay.
/// </summary>
public sealed class PublicPageNameTests
{
    private static readonly string[] Views =
    [
        "Batteries/Battery.cshtml", "Batteries/_ModulesCard.cshtml", "ClusterAdmin/Battery.cshtml", "Help/Guide.cshtml",
        "Passport/_PassportHero.cshtml", "ProductData/Family.cshtml", "Shared/_RowActions.cshtml",
    ];

    [Fact]
    public void ThePublicPage_ShouldHaveOneName()
    {
        foreach (var view in Views)
        {
            var visible = string.Join("\n", File.ReadAllLines(DppTestFiles.RepoFile(["web", "Views", .. view.Split('/')]))
                .Where(line => !line.TrimStart().StartsWith("@*", StringComparison.Ordinal)));
            Assert.DoesNotContain("public summary", visible, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("Back to summary", visible, StringComparison.Ordinal);
        }

        Assert.Contains("<use href=\"#i-back\" /></svg>Back to the public page</a>", File.ReadAllText(DppTestFiles.RepoFile("web", "Views", "Passport", "_PassportHero.cshtml")), StringComparison.Ordinal);
        Assert.Contains("Its batteries' public page shows it", File.ReadAllText(DppTestFiles.RepoFile("web", "Controllers", "ProductDataController.cs")), StringComparison.Ordinal);
    }
}
