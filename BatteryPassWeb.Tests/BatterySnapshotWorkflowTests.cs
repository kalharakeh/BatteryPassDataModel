namespace BatteryPassWeb.Tests;

using BatteryPassWeb.Services;
using MongoDB.Bson;

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

    [Fact]
    public void SnapshotService_ShouldCreatePassportFromBatteryAndRecordSnapshotMetadata()
    {
        var source = File.ReadAllText(RepoFile("web", "Services", "BatteryPassportSnapshotService.cs"));

        Assert.Contains("CreatePassportSnapshotAsync", source);
        Assert.Contains("CreatePassportId", source);
        Assert.Contains("[\"batteryId\"]", source);
        Assert.Contains("[\"snapshot\"]", source);
        Assert.Contains("isLatestForBattery", source);
        Assert.Contains("supersededByPassportId", source);
        Assert.Contains("batteryPassportIdentifier", source);
    }

    [Fact]
    public void PassportRepository_ShouldListAndSupersedePassportsByBatteryId()
    {
        var source = File.ReadAllText(RepoFile("web", "Services", "PassportRepository.cs"));

        Assert.Contains("GetByBatteryIdAsync", source);
        Assert.Contains("ListByBatteryIdAsync", source);
        Assert.Contains("GetLatestPublicByBatteryIdAsync", source);
        Assert.Contains("MarkPreviousLatestSupersededAsync", source);
        Assert.Contains("isLatestForBattery", source);
        Assert.Contains("supersededAt", source);
    }

    [Fact]
    public void ProductTemplateReset_ShouldResetBatteriesPassportsAndTelemetry()
    {
        var service = File.ReadAllText(RepoFile("web", "Services", "ProductTemplateService.cs"));
        var models = File.ReadAllText(RepoFile("web", "Services", "ProductTemplateModels.cs"));

        Assert.Contains("BatteryRepository", service);
        Assert.Contains("BatteryPassportSnapshotService", service);
        Assert.Contains("GetCollection<BsonDocument>(\"batteries\")", service);
        Assert.Contains("GetCollection<BsonDocument>(\"batteryTelemetry\")", service);
        Assert.Contains("Seeded batteries", service);
        Assert.Contains("multiple passports", service);
        Assert.Contains("BuildBatteryFromTemplate", models);
    }

    [Fact]
    public void ExternalApiInitializer_ShouldEnsureBatteryIndexes()
    {
        var source = File.ReadAllText(RepoFile("web", "Services", "ExternalApiInitializer.cs"));

        Assert.Contains("BatteryRepository", source);
        Assert.Contains("_batteryRepository.EnsureIndexesAsync", source);
    }

    [Fact]
    public void TelemetryRepository_ShouldUseBatteryIdInsteadOfPassportId()
    {
        var source = File.ReadAllText(RepoFile("web", "Services", "BatteryTelemetryRepository.cs"));

        Assert.Contains("batteryId", source);
        Assert.Contains("AppendTelemetryAsync(string batteryId", source);
        Assert.Contains("ReadHistoryAsync(string batteryId", source);
        Assert.DoesNotContain("Ascending(\"passportId\")", source);
    }

    [Fact]
    public void PassportViewModel_ShouldExposeBatteryIdAndHistoricalState()
    {
        var model = File.ReadAllText(RepoFile("web", "Models", "ViewModels", "PassportViewModel.cs"));
        var factory = File.ReadAllText(RepoFile("web", "Services", "PassportViewModelFactory.cs"));

        Assert.Contains("BatteryId", model);
        Assert.Contains("BatteryModel", model);
        Assert.Contains("IsLatestForBattery", model);
        Assert.Contains("IsHistoricalPassport", model);
        Assert.Contains("batteryId", factory);
        Assert.Contains("isLatestForBattery", factory);
    }

    [Fact]
    public void BatteryPassportDeltaService_ShouldDetectAndClearEditableDifferences()
    {
        var battery = new BsonDocument
        {
            ["batteryId"] = "battery-1",
            ["clusterId"] = "cluster-a",
            ["identity"] = new BsonDocument { ["batteryModel"] = "2.0" },
            ["app"] = new BsonDocument
            {
                ["display"] = new BsonDocument { ["facilityId"] = "line-a" },
                ["product"] = new BsonDocument { ["softwareVersion"] = "4.0" }
            }
        };
        var latestPassport = battery.DeepClone().AsBsonDocument;

        Assert.False(BatteryPassportDeltaService.HasEditableDifferences(
            battery,
            latestPassport,
            EditableFieldPolicyService.CreateDefaultPolicy()));

        battery["clusterId"] = "cluster-b";
        Assert.True(BatteryPassportDeltaService.HasEditableDifferences(
            battery,
            latestPassport,
            EditableFieldPolicyService.CreateDefaultPolicy()));

        battery["clusterId"] = "cluster-a";
        Assert.False(BatteryPassportDeltaService.HasEditableDifferences(
            battery,
            latestPassport,
            EditableFieldPolicyService.CreateDefaultPolicy()));
    }

    [Fact]
    public void BatteryPassportSnapshotService_ShouldCopyCurrentBatteryCluster()
    {
        var source = File.ReadAllText(RepoFile("web", "Services", "BatteryPassportSnapshotService.cs"));

        Assert.Contains("[\"clusterId\"] = BsonHelpers.GetString(battery, \"clusterId\")", source);
        Assert.DoesNotContain("previousPassport", source);
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
