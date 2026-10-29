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

    [Fact]
    public void ExternalApi_ShouldListBatteryPassportsAndUsePassportLifecycleLabel()
    {
        var controller = File.ReadAllText(RepoFile("web", "Controllers", "DppApiController.cs"));
        var help = File.ReadAllText(RepoFile("web", "Views", "Help", "Index.cshtml"));
        var seed = File.ReadAllText(RepoFile("web", "Services", "ProductTemplateService.cs"));

        Assert.Contains("[HttpGet(\"batteries/{id}/passports\")]", controller);
        Assert.Contains("api.VersionsAsync", controller);
        Assert.Contains("Passport Lifecycle", help);
        Assert.DoesNotContain("create, validate, sign and publish passport", help);
        Assert.Contains("Sample token (Passport Lifecycle)", seed);
        Assert.DoesNotContain("Sample token (create, validate, sign and publish passport)", seed);
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
