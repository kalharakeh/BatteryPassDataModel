using BatteryPassWeb.Services;
using MongoDB.Bson;
using MongoDB.Driver;

namespace BatteryPassWeb.Tests.Dpp;

/// <summary>Decision 30: the team can delete an account, with its memberships; not their own, and not the last team account.</summary>
[Collection(MongoCollection.Name)]
public sealed class AccountDeletionTests(MongoFixture mongo)
{
    [Fact]
    public async Task DeletingAnAccount_ShouldRemoveItAndItsMemberships()
    {
        var rig = await PublicationTesting.RigAsync(mongo);
        var repository = new ClusterRepository(rig.Context);
        await repository.UpsertUserAsync("someone@example.com", "Someone", ["member"], "hash", CancellationToken.None);
        await repository.UpsertClusterMembershipAsync("someone@example.com", "north", "member", CancellationToken.None);
        await repository.UpsertUserAsync("other@example.com", "Other", ["member"], "hash", CancellationToken.None);

        await repository.DeleteUserAsync("Someone@Example.com ");

        Assert.Null(await repository.GetUserByEmailAsync("someone@example.com"));
        Assert.Empty(await repository.GetClusterMembershipsForUserAsync("someone@example.com"));
        Assert.NotNull(await repository.GetUserByEmailAsync("other@example.com"));
    }

    [Fact]
    public void TheDeleteAction_ShouldKeepYourOwnAndTheLastTeamAccount()
    {
        var controller = File.ReadAllText(DppTestFiles.RepoFile("web", "Controllers", "AdminController.cs"));
        var page = File.ReadAllText(DppTestFiles.RepoFile("web", "Views", "Admin", "Clusters.cshtml"));

        Assert.Contains("[HttpPost(\"clusters/delete-account\")]", controller);
        Assert.Contains("You can't delete your own account.", controller);
        Assert.Contains("This is the last team account, so it stays.", controller);
        Assert.Contains("\"account.deleted\"", controller);
        Assert.Contains("action=\"/admin/clusters/delete-account\"", page);
        Assert.Contains("data-confirm-title=\"Delete account\"", page); // asked first
    }
}
