namespace BatteryPassWeb.Tests;

public sealed class BatterySnapshotWorkflowTests
{
    [Fact]
    public void Program_ShouldRegisterBatteryServices()
    {
        var program = File.ReadAllText(RepoFile("web", "Program.cs"));

        Assert.Contains("AddSingleton<BatteryRepository>", program);
        Assert.Contains("AddSingleton<BatteryPassportSnapshotService>", program);
    }

    [Fact]
    public void BatteryRepository_ShouldUseBatteriesCollectionAndBatteryIdIndexes()
    {
        var source = File.ReadAllText(RepoFile("web", "Services", "BatteryRepository.cs"));

        Assert.Contains("GetCollection<BsonDocument>(\"batteries\")", source);
        Assert.Contains("EnsureIndexesAsync", source);
        Assert.Contains("Ascending(\"batteryId\")", source);
        Assert.Contains("Unique = true", source);
        Assert.Contains("CreateBatteryAsync", source);
        Assert.Contains("UpdateBatteryFieldsAsync", source);
        Assert.Contains("GetByBatteryIdAsync", source);
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
