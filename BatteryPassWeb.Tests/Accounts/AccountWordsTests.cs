using System.Security.Claims;
using BatteryPassWeb.Services;
using BatteryPassWeb.Tests.Dpp;

namespace BatteryPassWeb.Tests.Accounts;

/// <summary>
/// Finding W04: the account page speaks to the reader ("Your name", "Your e-mail") and shows the role as text; the top bar
/// names a cluster user as the user lists do and leaves "No cluster" out for readers who never have one.
/// </summary>
public sealed class AccountWordsTests
{
    private static string Read(params string[] parts) => File.ReadAllText(DppTestFiles.RepoFile(parts));

    [Fact]
    public void TheAccountPage_ShouldSayYourNameAndYourEmail_AndShowTheRoleAsText()
    {
        var page = Read("web", "Views", "Account", "Index.cshtml");

        Assert.Contains("Your name", page, StringComparison.Ordinal);
        Assert.Contains("Your e-mail", page, StringComparison.Ordinal);
        Assert.DoesNotContain("Customer", page, StringComparison.Ordinal);
        Assert.DoesNotContain("value=\"@Model.RoleLabel\"", page, StringComparison.Ordinal);
        Assert.Contains("<strong>@Model.RoleLabel</strong>", page, StringComparison.Ordinal);
    }

    [Fact]
    public void AClusterUser_ShouldBeNamedAsTheUserListsDo()
    {
        var member = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Role, AccessControlService.RoleNormalUser)], "test"));

        Assert.Equal("Cluster member", AccessControlService.DisplayRoleLabel(member));
        Assert.Equal("Cluster member", AccessControlService.DisplayRoleLabel(AccessControlService.RoleNormalUser));
        Assert.Contains("_ => \"Cluster member\"", Read("web", "Views", "Admin", "Clusters.cshtml"), StringComparison.Ordinal); // the team's list
    }

    [Fact]
    public void TheTopBar_ShouldLeaveNoClusterOut_ForReadersWhoNeverHaveOne()
    {
        var layout = Read("web", "Views", "Shared", "_Layout.cshtml");

        Assert.DoesNotContain(": \"No cluster\")", layout, StringComparison.Ordinal);
        Assert.Contains("var clusterLine = clusterNames.Count > 0", layout, StringComparison.Ordinal);
        Assert.Contains("AccessControlService.HasAppWideRole", layout, StringComparison.Ordinal);
        Assert.Equal(2, layout.Split("@if (!string.IsNullOrEmpty(clusterLine))").Length - 1);
    }
}
