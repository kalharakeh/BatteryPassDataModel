using BatteryPassWeb.Services;

namespace BatteryPassWeb.Tests;

public sealed class LocalAdminEditableFieldPolicyTests
{
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
