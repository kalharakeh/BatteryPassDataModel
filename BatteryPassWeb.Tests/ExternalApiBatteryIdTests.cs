namespace BatteryPassWeb.Tests;

public sealed class ExternalApiBatteryIdTests
{
    [Fact]
    public void ExternalApi_ShouldUseBatteryIdForBatteryOperations()
    {
        var source = File.ReadAllText(RepoFile("web", "Controllers", "ExternalApiController.cs"));

        Assert.Contains("[HttpGet(\"batteries/{batteryId}\")]", source);
        Assert.Contains("[HttpPost(\"batteries/{batteryId}/telemetry\")]", source);
        Assert.Contains("[HttpPatch(\"batteries/{batteryId}/battery-model\")]", source);
        Assert.Contains("[HttpPost(\"batteries/{batteryId}/passports\")]", source);
        Assert.Contains("RejectPassportIdForBatteryRouteAsync", source);
        Assert.DoesNotContain("[HttpPatch(\"batteries/{passportId}/battery-version\")]", source);
    }

    [Fact]
    public void ExternalApi_ShouldUsePassportIdForTrustOperationsAndPublish()
    {
        var source = File.ReadAllText(RepoFile("web", "Controllers", "ExternalApiController.cs"));
        var repository = File.ReadAllText(RepoFile("web", "Services", "ExternalApiRepository.cs"));
        var workflow = File.ReadAllText(RepoFile("web", "Services", "PassportTrustWorkflowService.cs"));

        Assert.Contains("[HttpPost(\"passports/{passportId}/validate\")]", source);
        Assert.Contains("[HttpPost(\"passports/{passportId}/sign\")]", source);
        Assert.Contains("[HttpPost(\"passports/{passportId}/publish\")]", source);
        Assert.Contains("PublishAsync", source);
        Assert.Contains("PublishAsync", workflow);
        Assert.Contains("Passport published.", workflow);
        Assert.Contains("ValidateSignPublish", repository);
        Assert.DoesNotContain("external-api.publish\",\r\n            cancellationToken);", source);
    }

    [Fact]
    public void ExternalApiHelp_ShouldDocumentBatteryIdAndBatteryModel()
    {
        var help = File.ReadAllText(RepoFile("web", "Views", "Help", "Index.cshtml"));

        Assert.Contains("Battery ID", help);
        Assert.Contains("Battery Model", help);
        Assert.Contains("/battery-model", help);
        Assert.Contains("/passports/{passportId}/publish", help);
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
