namespace BatteryPassWeb.Tests;

public sealed class BatteryCreationServiceTests
{
    [Fact]
    public void BatteryCreationService_ShouldOwnCoreCreationInterlocks()
    {
        var source = File.ReadAllText(RepoFile("web", "Services", "BatteryCreationService.cs"));

        Assert.Contains("GetBySerialNumberAsync", source);
        Assert.Contains("CreateBatteryId(product.ProductName, serialNumber)", source);
        Assert.Contains("GetClusterByIdAsync", source);
        Assert.Contains("SoftwareVersions.FirstOrDefault", source);
        Assert.Contains("GetByBatteryIdAsync(batteryId", source);
        Assert.Contains("MongoWriteException", source);
        Assert.Contains("ServerErrorCategory.DuplicateKey", source);
        Assert.Contains("AppendBatteryAuditEventAsync", source);
        Assert.Contains("CustomizeBatteryBeforeInsert", source);
    }

    [Fact]
    public void BatteryCreationService_ShouldRejectInvalidInputsBeforeInsert()
    {
        var source = File.ReadAllText(RepoFile("web", "Services", "BatteryCreationService.cs"));
        var insertIndex = source.IndexOf("CreateBatteryAsync(battery", StringComparison.Ordinal);

        Assert.True(insertIndex > 0);
        Assert.True(source.IndexOf("Battery serial number is required.", StringComparison.Ordinal) < insertIndex);
        Assert.True(source.IndexOf("Battery serial number already exists.", StringComparison.Ordinal) < insertIndex);
        Assert.True(source.IndexOf("Unknown Battery Family.", StringComparison.Ordinal) < insertIndex);
        Assert.True(source.IndexOf("Unknown Battery Model.", StringComparison.Ordinal) < insertIndex);
        Assert.True(source.IndexOf("Unknown Software Version.", StringComparison.Ordinal) < insertIndex);
        Assert.True(source.IndexOf("Battery cluster is required.", StringComparison.Ordinal) < insertIndex);
        Assert.True(source.IndexOf("Token cannot access this cluster scope.", StringComparison.Ordinal) < insertIndex);
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
