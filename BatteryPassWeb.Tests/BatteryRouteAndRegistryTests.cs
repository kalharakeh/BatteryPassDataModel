using System.Runtime.CompilerServices;

namespace BatteryPassWeb.Tests;

public sealed class BatteryRouteAndRegistryTests
{
    private static readonly string TestSourceFile = CurrentSourceFile();

    [Fact]
    public void Registry_ShouldUseBatteryRowsAndPassportCounts()
    {
        var controller = File.ReadAllText(RepoFile("web", "Controllers", "RegistryController.cs"));
        var view = File.ReadAllText(RepoFile("web", "Views", "Shared", "_BatteryTable.cshtml"));

        Assert.Contains("BatteryTableService", controller);
        Assert.Contains("BatteryTablePageViewModel", File.ReadAllText(RepoFile("web", "Models", "ViewModels", "BatteryViewModels.cs")));
        Assert.Contains("PassportCount", view);
        Assert.Contains("passports", view);
        Assert.Contains("@row.BatteryId", view);
        Assert.Contains("@row.BatteryModel", view);
        Assert.DoesNotContain("@row.PassportId</span></td>", view);
    }

    [Fact]
    public void RegistrySearch_ShouldSupportPassportBatterySerialAndAllClusterReadSearch()
    {
        var controller = File.ReadAllText(RepoFile("web", "Services", "BatteryTableService.cs"));
        var repository = File.ReadAllText(RepoFile("web", "Services", "BatteryRepository.cs"));
        var view = File.ReadAllText(RepoFile("web", "Views", "Shared", "_BatteryTable.cshtml"));

        Assert.Contains("ResolveSearchAsync", controller);
        Assert.Contains("SearchByClusterAsync", repository);
        Assert.Contains("HasAllClusterReadScope", controller);
        Assert.Contains("Cluster search is not available for this role.", controller);
        Assert.Contains("AccessMessage", view);
    }

    [Fact]
    public void AccessControl_ShouldAllowClusterMembersToSeeSignedOrPublishedRegistryPassports()
    {
        var source = File.ReadAllText(RepoFile("web", "Services", "AccessControlService.cs"));

        Assert.Contains("registryStatus.Equals(\"published\"", source);
        Assert.Contains("registryStatus.Equals(\"signed\"", source);
        Assert.Contains("passportPublishPolicyService.HasCurrentValidSignature(passport)", source);
    }

    [Fact]
    public void PassportReports_ShouldShowBatteryIdPassportIdAndHistoricalLatestState()
    {
        var hero = File.ReadAllText(RepoFile("web", "Views", "Passport", "_PassportHero.cshtml"));

        Assert.Contains("Battery ID", hero);
        Assert.Contains("Passport ID", hero);
        Assert.Contains(">Archived<", hero);
        Assert.Contains(">Active<", hero);
        Assert.Contains("Battery model", hero);
        Assert.DoesNotContain("Battery version", hero);
    }

    [Fact]
    public void PassportReports_ShouldUseSerialHeaderAndIconLatestAction()
    {
        var hero = File.ReadAllText(RepoFile("web", "Views", "Passport", "_PassportHero.cshtml"));

        Assert.Contains("<h1>@Model.Text(11)</h1>", hero);
        Assert.DoesNotContain(">Open latest passport<", hero);
        Assert.Contains("aria-label=\"Open the current version\"", hero);
        Assert.Contains("bp-latest-passport-action-icon", hero);
    }

    [Fact]
    public void PassportSummaryLockedDetail_ShouldReplaceHeroCtaAndReturnAfterLogin()
    {
        var loginController = File.ReadAllText(RepoFile("web", "Controllers", "LoginController.cs"));
        var hero = File.ReadAllText(RepoFile("web", "Views", "Passport", "_PassportHero.cshtml"));

        Assert.Contains("[FromQuery] bool switchAccount", loginController);
        Assert.Contains("&& !switchAccount", loginController);
        Assert.Contains("Model.ReaderSeesMore", hero);
        Assert.Contains("href=\"@detailHref\">View more about this passport", hero);
        Assert.Contains("bp-report-hero-cta bp-report-hero-cta-warning", hero);
        Assert.Contains("&switchAccount=true", hero);
        Assert.Contains("/login?next=", hero);
    }

    [Fact]
    public void AdminBatteryList_ShouldUseCompactBatteryRowsWithoutEmbeddedHistory()
    {
        var view = File.ReadAllText(RepoFile("web", "Views", "Shared", "_BatteryTable.cshtml"));
        var model = File.ReadAllText(RepoFile("web", "Models", "ViewModels", "BatteryViewModels.cs"));
        var css = File.ReadAllText(RepoFile("web", "wwwroot", "css", "site.css"));

        Assert.Contains("@row.PassportCount", view);
        Assert.Contains("@row.PassportCount passports", view);
        Assert.DoesNotContain("bp-passport-history-row", view);
        Assert.DoesNotContain("data-battery-passport-history", view);
        Assert.Contains("row.HistoryUrl", view);
        Assert.Contains("NewPassportRequired", model);
        Assert.Contains(".bp-admin-battery-table .bp-battery-id-cell", css);
        Assert.Contains("max-width: none", css);
    }

    private static string RepoFile(params string[] parts)
    {
        var roots = new[]
            {
                AppContext.BaseDirectory,
                Directory.GetCurrentDirectory(),
                Path.GetDirectoryName(TestSourceFile) ?? string.Empty
            }
            .Where(root => !string.IsNullOrWhiteSpace(root))
            .Distinct(StringComparer.OrdinalIgnoreCase);

        foreach (var root in roots)
        {
            var directory = new DirectoryInfo(root);
            while (directory != null)
            {
                var candidate = Path.Combine(new[] { directory.FullName }.Concat(parts).ToArray());
                if (File.Exists(candidate))
                {
                    return candidate;
                }

                directory = directory.Parent;
            }
        }

        throw new FileNotFoundException($"Could not find repository file: {Path.Combine(parts)}");
    }

    private static string CurrentSourceFile([CallerFilePath] string sourceFile = "") => sourceFile;
}
