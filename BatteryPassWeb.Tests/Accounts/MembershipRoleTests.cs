using BatteryPassWeb.Services;
using BatteryPassWeb.Tests.Dpp;

namespace BatteryPassWeb.Tests.Accounts;

/// <summary>Finding S08: a membership's role is a cluster user or a cluster admin, from every form; a stored odd value reads as a user.</summary>
[Collection(MongoCollection.Name)]
public sealed class MembershipRoleTests(MongoFixture mongo)
{
    private async Task<string> RoleInAsync(AccountRig rig, string email, string cluster) =>
        BsonHelpers.GetString((await rig.Clusters.GetClusterMembershipsForUserAsync(email)).Single(membership => BsonHelpers.GetString(membership, "clusterId") == cluster), "role");

    [Theory]
    [InlineData("cluster")]
    [InlineData("")]
    public async Task AClusterAdminsSave_ShouldRefuseAnotherRole(string change)
    {
        using var rig = await AccountRig.StartAsync(mongo);
        var northAdmin = AccountRig.Form(rig.ClusterAdmin(AccountRig.NorthAdmin),
            ("change", change), ("email", AccountRig.NorthUser), ("clusterId", "north"), ("role", AccessControlService.RoleMarketSurveillanceAuthority));

        await northAdmin.SaveUser(CancellationToken.None);

        Assert.Equal(ClusterRepository.OnlyTwoClusterRoles, northAdmin.TempData["ErrorMessage"]);
        Assert.Equal(AccessControlService.RoleNormalUser, await RoleInAsync(rig, AccountRig.NorthUser, "north"));
    }

    [Fact]
    public async Task AClusterAdminsAddUser_ShouldMakeNoAccount_WithAnotherRole()
    {
        using var rig = await AccountRig.StartAsync(mongo);
        await AccountRig.Form(rig.ClusterAdmin(AccountRig.NorthAdmin),
            ("email", "new@example.test"), ("password", AccountRig.Typed), ("passwordConfirmation", AccountRig.Typed), ("clusterId", "north"), ("role", "commission")).SaveUser(CancellationToken.None);

        Assert.Null(await rig.Clusters.GetUserByEmailAsync("new@example.test"));
    }

    [Fact]
    public async Task TheTeamsForms_ShouldRefuseAnotherRole()
    {
        using var rig = await AccountRig.StartAsync(mongo);
        var assign = AccountRig.Form(rig.TeamMember(), ("email", AccountRig.SouthUser), ("clusterId", "north"), ("role", AccessControlService.RoleMarketSurveillanceAuthority));
        await assign.AssignUserMembership(CancellationToken.None);
        var create = AccountRig.Form(rig.TeamMember(),
            ("email", "new@example.test"), ("password", AccountRig.Typed), ("passwordConfirmation", AccountRig.Typed), ("clusterId", "north"), ("role", "notifiedBody"));
        await create.SaveUser(CancellationToken.None);

        Assert.Equal(ClusterRepository.OnlyTwoClusterRoles, assign.TempData["ErrorMessage"]);
        Assert.Equal(ClusterRepository.OnlyTwoClusterRoles, create.TempData["ErrorMessage"]);
        Assert.Equal(["south"], await rig.ClustersOfAsync(AccountRig.SouthUser));
        Assert.Null(await rig.Clusters.GetUserByEmailAsync("new@example.test"));
    }

    [Theory]
    [InlineData("clusterAdmin", true)]
    [InlineData("member", true)]
    [InlineData(" clusterAdmin ", true)]
    [InlineData("marketSurveillanceAuthority", false)]
    [InlineData("admin", false)]
    [InlineData("ClusterAdmin", false)]
    public void OnlyTheTwoRoles_ShouldBeClusterRoles(string role, bool accepted) =>
        Assert.Equal(accepted, ClusterRepository.IsClusterMembershipRole(role));

    [Fact]
    public void AStoredOddRole_ShouldReadAsAUser()
    {
        Assert.Equal(AccessControlService.RoleNormalUser, ClusterRepository.NormalizeClusterMembershipRole(AccessControlService.RoleMarketSurveillanceAuthority));
        foreach (var page in new[] { DppTestFiles.RepoFile("web", "Views", "ClusterAdmin", "Users.cshtml"), DppTestFiles.RepoFile("web", "Views", "Admin", "Clusters.cshtml") })
        {
            var text = File.ReadAllText(page);
            var label = text[text.IndexOf("string MembershipRoleLabel(string role)", StringComparison.Ordinal)..];
            label = label[..label.IndexOf("};", StringComparison.Ordinal)];
            Assert.DoesNotContain("Market surveillance", label, StringComparison.Ordinal);
            Assert.DoesNotContain("Legitimate interest", label, StringComparison.Ordinal);
        }
    }
}
