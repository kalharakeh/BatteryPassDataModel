using BatteryPassWeb.Tests.Dpp;

namespace BatteryPassWeb.Tests.Accounts;

/// <summary>
/// Finding U29: the "x" beside a user's cluster asks first, in the window the other delete buttons use, naming the person
/// and the cluster; afterwards a message says what happened.
/// </summary>
[Collection(MongoCollection.Name)]
public sealed class RemoveFromClusterTests(MongoFixture mongo)
{
    private static string Read(params string[] parts) => File.ReadAllText(DppTestFiles.RepoFile(parts));

    [Theory]
    [InlineData("ClusterAdmin", "Users.cshtml", "/cluster-admin/users/delete-membership")]
    [InlineData("Admin", "Clusters.cshtml", "/admin/clusters/delete-user")]
    public void TheX_ShouldAskFirst_NamingThePersonAndTheCluster(string folder, string view, string action)
    {
        var page = Read("web", "Views", folder, view);
        var form = page[page.IndexOf($"action=\"{action}\"", StringComparison.Ordinal)..];
        var button = form[form.IndexOf("<button", StringComparison.Ordinal)..form.IndexOf("</button>", StringComparison.Ordinal)];

        Assert.Contains("data-confirm-action data-confirm-title=\"Remove from cluster\"", button, StringComparison.Ordinal);
        Assert.Contains("data-confirm-message=\"Remove @displayName from @ClusterName(membership.ClusterId)?", button, StringComparison.Ordinal);
        Assert.Contains("data-confirm-submit=\"Remove\"", button, StringComparison.Ordinal);
        Assert.Contains("@await Html.PartialAsync(\"_ConfirmModal\")", page, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AfterTheX_AClusterAdmin_ShouldReadWhatHappened()
    {
        using var rig = await AccountRig.StartAsync(mongo);
        var northAdmin = AccountRig.Form(rig.ClusterAdmin(AccountRig.NorthAdmin), ("email", AccountRig.NorthUser), ("clusterId", "north"));

        await northAdmin.DeleteUserMembership(CancellationToken.None);

        Assert.Empty(await rig.ClustersOfAsync(AccountRig.NorthUser));
        Assert.Equal("North User is no longer in Customer North.", northAdmin.TempData["StatusMessage"]);
    }

    [Fact]
    public async Task AfterTheX_TheTeam_ShouldReadWhatHappened()
    {
        using var rig = await AccountRig.StartAsync(mongo);
        var team = AccountRig.Form(rig.TeamMember(), ("email", AccountRig.SouthUser), ("clusterId", "south"));

        await team.DeleteUserMembership(CancellationToken.None);

        Assert.Empty(await rig.ClustersOfAsync(AccountRig.SouthUser));
        Assert.Equal("South User is no longer in Customer South.", team.TempData["StatusMessage"]);
    }
}
