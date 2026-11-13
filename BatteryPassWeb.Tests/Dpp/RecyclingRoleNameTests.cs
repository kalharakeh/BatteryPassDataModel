using BatteryPassWeb.Services;

namespace BatteryPassWeb.Tests.Dpp;

/// <summary>
/// Finding R12: the draft act gives training centres the recycling role's data, so the role is shown as "Recycling and
/// training" wherever its name is shown. Its stored value stays "liRecycling", so no account changes.
/// </summary>
public sealed class RecyclingRoleNameTests
{
    private static string Read(params string[] parts) => File.ReadAllText(DppTestFiles.RepoFile(parts));

    [Fact]
    public void TheRole_ShouldBeNamedRecyclingAndTraining()
    {
        Assert.Equal("liRecycling", AccessControlService.RoleLiRecycling);
        Assert.Equal("Legitimate interest: recycling and training", AccessControlService.DisplayRoleLabel(AccessControlService.RoleLiRecycling));
        Assert.Contains("Who: \"Recycling and training\"", Read("web", "Views", "Help", "Guide.cshtml"), StringComparison.Ordinal);
        Assert.Contains("\"liRecycling\" => \"Legitimate interest: recycling and training\"", Read("web", "Views", "Admin", "Clusters.cshtml"), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("web", "Services", "AccessControlService.cs")]
    [InlineData("web", "Views", "Admin", "Clusters.cshtml")]
    [InlineData("web", "Views", "Help", "Guide.cshtml")]
    public void TheOldName_ShouldBeGone(params string[] file) =>
        Assert.DoesNotContain("recycling and battery treatment", Read(file), StringComparison.OrdinalIgnoreCase);
}
