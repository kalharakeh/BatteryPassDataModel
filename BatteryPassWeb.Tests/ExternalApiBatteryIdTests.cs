namespace BatteryPassWeb.Tests;

public sealed class ExternalApiBatteryIdTests
{
    [Fact]
    public void ExternalApiHelp_ShouldDocumentBatteryIdsAndNoneOfTheOldBatteryCalls()
    {
        var help = File.ReadAllText(RepoFile("web", "Views", "Help", "Index.cshtml"));

        Assert.Contains("Battery ID", help);
        Assert.Contains("/batteries/{id}/publish", help);
        Assert.DoesNotContain("/battery-model", help);
        Assert.DoesNotContain("/passports/{passportId}", help);
        Assert.DoesNotContain("Battery version", help);
        Assert.DoesNotContain("/battery-version", help);
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
