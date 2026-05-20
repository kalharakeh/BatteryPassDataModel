using BatteryPassWeb.Services;

namespace BatteryPassWeb.Tests;

public sealed class LocalAdminEditableFieldPolicyTests
{
    [Fact]
    public void EditableFieldPolicy_ShouldSeedCreationAfterCreationAndLocalAdminDefaults()
    {
        var policy = EditableFieldPolicyService.CreateDefaultPolicy();

        Assert.True(policy.IsEditableAtCreation("general.clusterId"));
        Assert.True(policy.IsEditableAtCreation("general.batteryFamily"));
        Assert.True(policy.IsEditableAtCreation("general.batteryModel"));
        Assert.True(policy.IsEditableAtCreation("general.serialNumber"));
        Assert.True(policy.IsEditableAtCreation("general.manufacturedDate"));
        Assert.True(policy.IsEditableAtCreation("general.facilityId"));
        Assert.True(policy.IsEditableAtCreation("general.manufacturedBy"));
        Assert.True(policy.IsEditableAtCreation("software.version"));

        Assert.True(policy.IsEditableAfterCreation("general.batteryModel"));
        Assert.True(policy.IsEditableAfterCreation("general.facilityId"));
        Assert.True(policy.IsEditableAfterCreation("software.version"));
        Assert.True(policy.IsEditableAfterCreation("general.clusterId"));

        Assert.True(policy.IsEditableByLocalAdmin("general.batteryModel"));
        Assert.True(policy.IsEditableByLocalAdmin("general.facilityId"));
        Assert.True(policy.IsEditableByLocalAdmin("software.version"));
        Assert.True(policy.IsEditableByLocalAdmin("general.clusterId"));
    }

    [Fact]
    public void EditableFieldPolicy_ShouldEnforcePermissionDependencies()
    {
        var policy = EditableFieldPolicyService.CreateDefaultPolicy(
            new[]
            {
                new EditableFieldPermission("general.facilityId", false, true, true),
                new EditableFieldPermission("software.version", false, false, true),
                new EditableFieldPermission("general.clusterId", false, false, false)
            });

        var facility = policy.PermissionByKey["general.facilityId"];
        Assert.True(facility.EditableAtCreation);
        Assert.True(facility.EditableAfterCreation);
        Assert.True(facility.EditableByLocalAdmin);

        var software = policy.PermissionByKey["software.version"];
        Assert.True(software.EditableAtCreation);
        Assert.True(software.EditableAfterCreation);
        Assert.True(software.EditableByLocalAdmin);

        var cluster = policy.PermissionByKey["general.clusterId"];
        Assert.False(cluster.EditableAtCreation);
        Assert.False(cluster.EditableAfterCreation);
        Assert.False(cluster.EditableByLocalAdmin);
    }

    [Fact]
    public void CreateDefaultPolicy_ShouldAllowOnlyOperationalAndLocalIdentityFields()
    {
        var snapshot = LocalAdminEditableFieldPolicyService.CreateDefaultPolicy();

        Assert.Contains("general.facilityId", snapshot.EditableFieldKeys);
        Assert.Contains("general.batteryImageUrl", snapshot.EditableFieldKeys);
        Assert.Contains("performance.stateOfCharge", snapshot.EditableFieldKeys);
        Assert.Contains("performance.remainingCapacity", snapshot.EditableFieldKeys);
        Assert.Contains("performance.remainingEnergy", snapshot.EditableFieldKeys);
        Assert.Contains("performance.fullCycles", snapshot.EditableFieldKeys);
        Assert.DoesNotContain("general.modelNumber", snapshot.EditableFieldKeys);
    }

    [Fact]
    public void Service_ShouldPersistGlobalPolicyInMongo()
    {
        var source = File.ReadAllText(RepoFile("web", "Services", "LocalAdminEditableFieldPolicyService.cs"));

        Assert.Contains("localAdminEditableFieldPolicies", source);
        Assert.Contains("editableFieldKeys", source);
        Assert.Contains("DataCompletionPolicyService.CreateDefaultPolicy", source);
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
