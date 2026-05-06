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
    public void ExternalApiController_ShouldNotMarkPassportDirtyForHttpWrites()
    {
        var source = File.ReadAllText(RepoFile("web", "Controllers", "ExternalApiController.cs"));

        Assert.DoesNotContain("MarkCanonicalDirtyAsync", source);
        Assert.Contains("UpdateFieldsAsync(passportId, setValues, cancellationToken)", source);
    }

    [Fact]
    public void AdminController_ShouldMarkCanonicalSavesDirty()
    {
        var source = File.ReadAllText(RepoFile("web", "Controllers", "AdminController.cs"));

        Assert.Contains("MarkCanonicalDirtyAsync(passportId", source);
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
