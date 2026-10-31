using BatteryPassWeb.Services;
using MongoDB.Bson;

namespace BatteryPassWeb.Tests;

public sealed class AdminFeedbackFollowupTests
{
    [Fact]
    public void PassportReportHero_ShouldNotShowPassportStatusAbovePassportId()
    {
        var hero = File.ReadAllText(RepoFile("web", "Views", "Passport", "_PassportHero.cshtml"));

        Assert.DoesNotContain("bp-pill bp-pill-muted", hero);
        Assert.Contains("<div><dt>Passport status</dt><dd>@Model.Text(2)</dd></div>", hero);
    }

    [Fact]
    public void ReportActions_ShouldUseConsistentIconButtonsWithDesignedTooltips()
    {
        var registry = File.ReadAllText(RepoFile("web", "Views", "Registry", "Index.cshtml"));
        var hero = File.ReadAllText(RepoFile("web", "Views", "Passport", "_PassportHero.cshtml"));
        var css = File.ReadAllText(RepoFile("web", "wwwroot", "css", "site.css"));

        var combined = string.Join(Environment.NewLine, registry, hero);
        Assert.Contains("bp-report-action", combined);
        Assert.Contains("View more about this passport", hero);
        Assert.Contains("Back to summary", hero);
        Assert.Contains("bp-report-action-icon", combined);
        Assert.Contains(".bp-report-action::after", css);
        Assert.Contains("top: calc(-100% -", css);
    }

    [Fact]
    public void ReportActions_ShouldUseHeroCtaWithoutOldHeaderRailLabels()
    {
        var hero = File.ReadAllText(RepoFile("web", "Views", "Passport", "_PassportHero.cshtml"));
        var css = File.ReadAllText(RepoFile("web", "wwwroot", "css", "site.css"));

        Assert.Contains("bp-report-header", hero);
        Assert.Contains("bp-report-title-block", hero);
        Assert.Contains("bp-report-hero-cta", hero);
        Assert.Contains("View more about this passport", hero);
        Assert.Contains("Back to summary", hero);
        Assert.DoesNotContain("bp-report-header-actions", hero);
        Assert.DoesNotContain("bp-report-action-strip", hero);
        Assert.DoesNotContain("bp-report-action-label", hero);
        Assert.DoesNotContain(">Latest</span>", hero);
        Assert.DoesNotContain(">Detailed report</span>", hero);
        Assert.Contains("bp-latest-passport-action-icon", hero);
        Assert.Contains(".bp-report-title-block", css);
        Assert.Contains(".bp-report-hero-cta", css);
        Assert.Contains("font-weight: 400;", css);
        Assert.DoesNotContain(".bp-report-action-label", css);
    }

    [Fact]
    public void PassportReportHero_ShouldUseApprovedSplitHeaderDesign()
    {
        var hero = File.ReadAllText(RepoFile("web", "Views", "Passport", "_PassportHero.cshtml"));
        var tabs = File.ReadAllText(RepoFile("web", "Views", "Passport", "_PassportTabs.cshtml"));
        var summary = File.ReadAllText(RepoFile("web", "Views", "Passport", "Summary.cshtml"));
        var css = File.ReadAllText(RepoFile("web", "wwwroot", "css", "site.css"));

        Assert.Contains("bp-report-hero-card", hero);
        Assert.Contains("bp-report-wide-section", summary);
        Assert.Contains("bp-report-wide-section", tabs);
        Assert.Contains("bp-report-hero-layout", hero);
        Assert.Contains("bp-report-hero-media", hero);
        Assert.Contains("View more about this passport", hero);
        Assert.Contains("Back to summary", hero);
        Assert.Contains("bp-report-hero-qr", hero);
        Assert.Contains(".bp-report-hero-card", css);
        Assert.Contains(".bp-report-wide-section", css);
        Assert.Contains("width: min(1860px, calc(100vw - 96px));", css);
        Assert.Contains("grid-template-columns: repeat(4, minmax(120px, 1fr));", css);
        Assert.Contains(".bp-report-hero-media .bp-image-wrap", css);
        Assert.Contains("background: transparent;", css);
        Assert.Contains(".bp-report-hero-cta", css);
        Assert.Contains("font-weight: 400;", css);
    }

    [Fact]
    public void AdminManagementViews_ShouldUseDesignedConfirmationsAndStructuredLayouts()
    {
        var clusters = File.ReadAllText(RepoFile("web", "Views", "Admin", "Clusters.cshtml"));
        var clusterUsers = File.ReadAllText(RepoFile("web", "Views", "ClusterAdmin", "Users.cshtml"));
        var clusterTokens = File.ReadAllText(RepoFile("web", "Views", "ClusterAdmin", "ApiTokens.cshtml"));
        var css = File.ReadAllText(RepoFile("web", "wwwroot", "css", "site.css"));

        Assert.DoesNotContain("confirm(", clusters);
        Assert.DoesNotContain("confirm(", clusterTokens);
        Assert.Contains("_ConfirmModal", clusters);
        Assert.Contains("bp-confirm-modal", clusterTokens);
        Assert.Contains("data-confirm-action", clusters);
        Assert.Contains("data-confirm-action", clusterTokens);
        Assert.Contains("bp-user-management-table", clusterUsers);
        Assert.DoesNotContain("class=\"bp-tag\"", clusters);
        Assert.DoesNotContain("class=\"bp-tag\"", clusterUsers);
        Assert.Contains("bp-token-console", clusters);
        Assert.Contains("bp-token-cluster-chip-picker", clusters);
        Assert.Contains("bp-token-cluster-chip-picker", clusterTokens);
        Assert.Contains(".bp-confirm-modal", css);
        Assert.Contains(".bp-token-cluster-chip-picker", css);
    }

    [Fact]
    public void PublicSearchEmptyCopy_ShouldSayActivePublicPassport()
    {
        var home = File.ReadAllText(RepoFile("web", "Views", "Home", "Index.cshtml"));

        Assert.Contains("No published battery passport was found", home);
        Assert.DoesNotContain("No published, verified battery passport ID was found", home);
    }

    [Fact]
    public void ReportCharts_ShouldUseTableLegendsAndOriginalPowerHeading()
    {
        var summary = File.ReadAllText(RepoFile("web", "Views", "Passport", "Summary.cshtml"));

        Assert.Contains("<h3 class=\"bp-subheading\">Original power capability</h3>", summary);
        Assert.Contains("bp-chart-value-legend", summary);
    }

    [Fact]
    public void FollowupReportHeader_ShouldUseFixedThreeColumnIdentityGrid()
    {
        var hero = File.ReadAllText(RepoFile("web", "Views", "Passport", "_PassportHero.cshtml"));
        var summary = File.ReadAllText(RepoFile("web", "Views", "Passport", "Summary.cshtml"));
        var css = File.ReadAllText(RepoFile("web", "wwwroot", "css", "site.css"));

        Assert.Contains("bp-report-identity-grid", hero);
        Assert.Contains("bp-report-identity-panel--battery", hero);
        Assert.Contains("bp-report-identity-panel--identifiers", hero);
        Assert.Contains("bp-report-identity-panel--qr", hero);
        Assert.DoesNotContain("bp-report-media-code-row", hero);
        Assert.Contains("CultureInfo.InvariantCulture", summary);
        Assert.Contains("grid-template-columns: minmax(220px, 1fr) minmax(280px, 1.2fr) minmax(160px, 220px);", css);
        Assert.Contains(".bp-report-identity-panel--qr", css);
        Assert.Contains("@media (max-width: 1180px)", css);
        Assert.Contains("@media (max-width: 760px)", css);
    }

    [Fact]
    public void FollowupUserAndClusterUx_ShouldPreserveInputAndDrawerState()
    {
        var adminController = File.ReadAllText(RepoFile("web", "Controllers", "AdminController.cs"));
        var clusterAdminController = File.ReadAllText(RepoFile("web", "Controllers", "ClusterAdminController.cs"));
        var clusters = File.ReadAllText(RepoFile("web", "Views", "Admin", "Clusters.cshtml"));
        var clusterUsers = File.ReadAllText(RepoFile("web", "Views", "ClusterAdmin", "Users.cshtml"));
        var css = File.ReadAllText(RepoFile("web", "wwwroot", "css", "site.css"));

        Assert.Contains("TempData[\"ClusterCreateName\"]", adminController);
        Assert.Contains("TempData[\"ClusterCreateId\"]", adminController);
        Assert.Contains("Cluster ID already exists.", adminController);
        Assert.Contains("[\"member\"] = \"Cluster member (sees only their clusters' batteries)\"", clusters);
        Assert.Contains("selected=\"@(option.Key.Equals(\"member\"", clusters);
        Assert.Contains("data-open-user", clusters);
        Assert.Contains("data-open-user", clusterUsers);
        Assert.Contains("openUser", adminController);
        Assert.Contains("openUser", clusterAdminController);
        Assert.Contains("::-ms-reveal", css);
        Assert.Contains("::-ms-clear", css);
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
