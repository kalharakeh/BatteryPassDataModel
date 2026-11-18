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
    public void AClusterUser_ShouldBeACustomerUserOnTheirOwnPages_AndAClusterMemberOnTheTeams()
    {
        var member = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Role, AccessControlService.RoleNormalUser)], "test"));

        // Decision 55 (W07): "customer" on the pages customers see; "cluster" stays on the team's pages.
        Assert.Equal("Customer user", AccessControlService.DisplayRoleLabel(member));
        Assert.Equal("Customer user", AccessControlService.DisplayRoleLabel(AccessControlService.RoleNormalUser));
        Assert.Contains("_ => \"Cluster member\"", Read("web", "Views", "Admin", "Clusters.cshtml"), StringComparison.Ordinal); // the team's list
    }

    [Fact]
    public void TheTopBar_ShouldLeaveNoClusterOut_ForReadersWhoNeverHaveOne()
    {
        // Since the one bar (L04) the account menu's customer line comes from TopBar: none for the app-wide roles.
        var layout = Read("web", "Views", "Shared", "_Layout.cshtml");
        var bar = Read("web", "Services", "TopBar.cs");

        Assert.DoesNotContain("No cluster", layout, StringComparison.Ordinal);
        Assert.Contains("if (!AccessControlService.HasAppWideRole(", bar, StringComparison.Ordinal);
        Assert.Contains("customer = customers.Length > 0 ? string.Join(\", \", customers) : \"none yet\";", bar, StringComparison.Ordinal);
        Assert.Equal(1, layout.Split("@if (account.Customer is { } customer)").Length - 1);
    }
}
