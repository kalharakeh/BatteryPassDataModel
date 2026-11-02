using System.Runtime.CompilerServices;

namespace BatteryPassWeb.Tests;

public sealed class BatteryRouteAndRegistryTests
{
    private static readonly string TestSourceFile = CurrentSourceFile();

    [Fact]
    public void PassportReports_ShouldShowBatteryIdPassportIdAndHistoricalLatestState()
    {
        var hero = File.ReadAllText(RepoFile("web", "Views", "Passport", "_PassportHero.cshtml"));

        Assert.Contains("Battery ID", hero);
        Assert.Contains("Passport ID", hero);
        Assert.Contains(">@Model.StatusWords<", hero); // archived, inactive, … (step 4c-1)
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
