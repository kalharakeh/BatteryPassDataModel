namespace BatteryPassWeb.Tests;

public sealed class TrustDirtyStateTests
{
    [Fact]
    public void PassportRepository_ShouldExposeCanonicalDirtyMethod()
    {
        var source = File.ReadAllText(RepoFile("web", "Services", "PassportRepository.cs"));

        Assert.Contains("MarkCanonicalDirtyAsync", source);
        Assert.Contains("\"trust.isDirty\"", source);
        Assert.Contains("\"trust.state\"", source);
        Assert.Contains("TrustState.Dirty", source);
    }

    [Fact]
    public void ExternalApiController_ShouldWriteBatteryModelChangesToBatteryRecord()
    {
        var source = File.ReadAllText(RepoFile("web", "Controllers", "ExternalApiController.cs"));

        Assert.Contains("UpdateBatteryModel", source);
        Assert.Contains("UpdateBatteryFieldsAsync(batteryId, setValues, cancellationToken)", source);
        Assert.Contains("newPassportRequired = true", source);
        Assert.Contains("CreateBatteryPassport", source);
    }

    [Fact]
    public void AdminController_ShouldMarkCanonicalSavesDirty()
    {
        var source = File.ReadAllText(RepoFile("web", "Controllers", "AdminController.cs"));

        Assert.Contains("MarkCanonicalDirtyAsync(passportId", source);
    }

    [Fact]
    public void ExternalApiAcceptance_ShouldOnlyWriteOperationalFieldsWithoutCallingDirtyMarker()
    {
        var controller = File.ReadAllText(RepoFile("web", "Controllers", "ExternalApiController.cs"));

        Assert.Contains("app.operations.latestTelemetry", controller);
        Assert.Contains("app.operations.locationOfUse", controller);
        Assert.Contains("app.operations.contactPerson", controller);
        Assert.Contains("UpdateBatteryModel", controller);
        Assert.DoesNotContain("MarkCanonicalDirtyAsync", controller);
        Assert.DoesNotContain("trust.isDirty", controller);
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
