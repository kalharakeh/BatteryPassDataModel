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
    public void ExternalApiController_ShouldOnlyMarkBatteryVersionChangesDirty()
    {
        var source = File.ReadAllText(RepoFile("web", "Controllers", "ExternalApiController.cs"));

        Assert.Contains("UpdateBatteryVersion", source);
        Assert.Contains("MarkCanonicalDirtyAsync(passportId, \"batteryVersionChanged\"", source);
        Assert.Contains("UpdateFieldsAsync(passportId, setValues, cancellationToken)", source);
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
        var batteryVersionEndpointIndex = controller.IndexOf("UpdateBatteryVersion", StringComparison.Ordinal);
        var dirtyMarkerIndex = controller.IndexOf("MarkCanonicalDirtyAsync", StringComparison.Ordinal);
        Assert.True(dirtyMarkerIndex > batteryVersionEndpointIndex, "Only the battery version API should mark signed data dirty; operations writes must remain telemetry-only.");
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
