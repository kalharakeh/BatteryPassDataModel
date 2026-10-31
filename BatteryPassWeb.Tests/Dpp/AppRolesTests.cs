using System.Reflection;
using System.Security.Claims;
using System.Text.RegularExpressions;
using BatteryPassWeb.Controllers;
using BatteryPassWeb.Services;

namespace BatteryPassWeb.Tests.Dpp;

/// <summary>The 9 app roles of step 3 (design 5.2): today's 7, with "legitimateInterest" split into the three LI roles of decision 6.</summary>
public sealed class AppRolesTests
{
    private static readonly string[] LiRoles = ["liRepair", "liRecycling", "liTreatment"];

    private static string View(params string[] path) => File.ReadAllText(DppTestFiles.RepoFile(["web", "Views", .. path]));

    private static IReadOnlyList<string> Keys(string view, string dictionary)
    {
        var start = view.IndexOf($"{dictionary} = new", StringComparison.Ordinal);
        var end = view.IndexOf("};", start, StringComparison.Ordinal);
        return Regex.Matches(view[start..end], @"\[""(\w+)""\] =").Select(match => match.Groups[1].Value).ToArray();
    }

    [Fact]
    public void TheUsersPage_ShouldOfferTheNineRoles()
    {
        var users = View("Admin", "Clusters.cshtml");

        Assert.Equal(
            ["member", "admin", "notifiedBody", "marketSurveillanceAuthority", "commission", "liRepair", "liRecycling", "liTreatment"],
            Keys(users, "globalAccessOptions"));
        Assert.Equal(["member", "clusterAdmin"], Keys(users, "clusterMembershipRoleOptions"));
        Assert.DoesNotContain("legitimateInterest", users, StringComparison.Ordinal);
        Assert.DoesNotContain("Person with Legitimate Interest", users, StringComparison.Ordinal);
    }

    [Fact]
    public void EveryLabel_ShouldNameTheRoleTheSameWay()
    {
        var users = View("Admin", "Clusters.cshtml");
        var clusterUsers = View("ClusterAdmin", "Users.cshtml");

        foreach (var role in LiRoles)
        {
            var label = AccessControlService.DisplayRoleLabel(role);
            Assert.StartsWith("Legitimate interest: ", label, StringComparison.Ordinal);
            Assert.Contains($"[\"{role}\"] = \"{label}\"", users, StringComparison.Ordinal);
            Assert.Contains($"\"{label}\" => \"{role}\"", users, StringComparison.Ordinal);
            Assert.Contains($"\"{role}\" => \"{label}\"", clusterUsers, StringComparison.Ordinal);
            Assert.Equal(label, AccessControlService.DisplayRoleLabel(User(role)));
        }

        Assert.Equal(3, LiRoles.Select(AccessControlService.DisplayRoleLabel).Distinct().Count());
        Assert.DoesNotContain("legitimateInterest", clusterUsers, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("liRepair", "liRepair")]
    [InlineData("liRecycling", "liRecycling")]
    [InlineData("liTreatment", "liTreatment")]
    [InlineData("legitimateInterest", "member")]
    public void SavingAUser_ShouldKeepTheThreeRolesAndDropTheOldOne(string given, string stored)
    {
        var normalizeSystemRole = typeof(AdminController).GetMethod("NormalizeSystemRole", BindingFlags.NonPublic | BindingFlags.Static)!;

        Assert.Equal(stored, normalizeSystemRole.Invoke(null, [given]));
        Assert.Equal(stored, ClusterRepository.NormalizeClusterMembershipRole(given));
    }

    [Fact]
    public void TheOldRole_ShouldGiveNothingBeyondPublicData()
    {
        var old = User("legitimateInterest");

        Assert.False(AccessControlService.HasLegitimateInterestRole(old));
        Assert.Equal("Cluster Member", AccessControlService.DisplayRoleLabel(old));
        Assert.All(LiRoles, role => Assert.True(AccessControlService.HasLegitimateInterestRole(User(role))));
    }

    private static ClaimsPrincipal User(string role) =>
        new(new ClaimsIdentity([new Claim(ClaimTypes.Email, "someone@example.com"), new Claim(ClaimTypes.Role, role)], "test"));
}
