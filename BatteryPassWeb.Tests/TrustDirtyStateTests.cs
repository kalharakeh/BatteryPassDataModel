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
