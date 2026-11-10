using BatteryPassWeb.Services;
using BatteryPassWeb.Tests.Dpp;
using MongoDB.Bson;
using MongoDB.Driver;

namespace BatteryPassWeb.Tests.Accounts;

/// <summary>
/// Finding U28: an address is the same whatever its capitals and outer spaces, wherever an account or a membership is
/// looked up or made. Stored addresses aren't assumed to be lower case: an account stored with capitals still works.
/// </summary>
[Collection(MongoCollection.Name)]
public sealed class EmailCapitalsTests(MongoFixture mongo)
{
    private const string Stored = "Mixed.Case@Example.test";
    private const string Typed = "  mixed.CASE@example.TEST ";

    /// <summary>An account and an admin membership stored with capitals, as an older writer might have left them.</summary>
    private static async Task StoreWithCapitalsAsync(AccountRig rig)
    {
        await rig.Database.GetCollection<BsonDocument>("users").InsertOneAsync(new BsonDocument
        {
            ["email"] = Stored,
            ["name"] = "Mixed Case",
            ["roles"] = new BsonArray { AccessControlService.RoleNormalUser },
            ["passwordHash"] = BCrypt.Net.BCrypt.HashPassword(AccountRig.Password, 4),
        });
        await rig.Database.GetCollection<BsonDocument>("clusterMemberships").InsertOneAsync(new BsonDocument
        {
            ["email"] = Stored,
            ["clusterId"] = "south",
            ["role"] = AccessControlService.RoleClusterAdmin,
        });
    }

    private static Task<long> AccountsAsync(AccountRig rig) =>
        rig.Database.GetCollection<BsonDocument>("users").CountDocumentsAsync(
            Builders<BsonDocument>.Filter.Regex("email", new BsonRegularExpression("^mixed\\.case@example\\.test$", "i")));

    [Fact]
    public async Task SignIn_ShouldIgnoreCapitalsAndOuterSpaces()
    {
        using var rig = await AccountRig.StartAsync(mongo);

        var result = await rig.Auth.AuthenticateLoginAsync("  South.User@Example.TEST ", AccountRig.Password);

        Assert.Equal(LoginAuthenticationStatus.Authenticated, result.Status);
        Assert.Equal(AccountRig.SouthUser, result.Email);
    }

    [Theory]
    [InlineData(Stored)]
    [InlineData(Typed)]
    public async Task AnAccountStoredWithCapitals_ShouldSignInWithItsClusterAdminRole(string typed)
    {
        using var rig = await AccountRig.StartAsync(mongo);
        await StoreWithCapitalsAsync(rig);

        var result = await rig.Auth.AuthenticateLoginAsync(typed, AccountRig.Password);

        Assert.Equal(LoginAuthenticationStatus.Authenticated, result.Status);
        Assert.True(result.Principal!.IsInRole(AccessControlService.RoleClusterAdmin));
        Assert.Equal(["south"], await rig.Access.GetAdministeredClusterIdsForUserAsync(result.Principal));
        Assert.NotNull(await rig.Auth.CreatePrincipalForUserAsync(typed));
    }

    [Fact]
    public async Task Writes_ShouldReachAnAccountStoredWithCapitals_AndMakeNoSecondOne()
    {
        using var rig = await AccountRig.StartAsync(mongo);
        await StoreWithCapitalsAsync(rig);

        await rig.Clusters.UpsertUserAsync(Typed, "Renamed", [AccessControlService.RoleNormalUser]);
        await rig.Clusters.SetPasswordHashAsync(Typed, BCrypt.Net.BCrypt.HashPassword(AccountRig.Typed, 4));
        await rig.Clusters.UpsertClusterMembershipAsync(Typed, "south", AccessControlService.RoleNormalUser);

        Assert.Equal(1, await AccountsAsync(rig));
        Assert.Equal("Renamed", await rig.NameOfAsync(Stored));
        Assert.True(await rig.SignsInAsync(Stored, AccountRig.Typed));
        var membership = Assert.Single(await rig.Clusters.GetClusterMembershipsForUserAsync(Typed));
        Assert.Equal(AccessControlService.RoleNormalUser, BsonHelpers.GetString(membership, "role"));

        await rig.Clusters.DeleteUserAsync(Typed);

        Assert.Equal(0, await AccountsAsync(rig));
        Assert.Empty(await rig.Clusters.GetClusterMembershipsForUserAsync(Stored));
    }

    [Fact]
    public async Task AddUser_ShouldKnowAnAccountStoredWithCapitals()
    {
        using var rig = await AccountRig.StartAsync(mongo);
        await StoreWithCapitalsAsync(rig);
        var northAdmin = AccountRig.Form(rig.ClusterAdmin(AccountRig.NorthAdmin),
            ("email", Typed), ("password", AccountRig.Typed), ("passwordConfirmation", AccountRig.Typed), ("clusterId", "north"));

        await northAdmin.SaveUser(CancellationToken.None);

        Assert.Equal(1, await AccountsAsync(rig));
        Assert.True(await rig.SignsInAsync(Stored, AccountRig.Password));
        Assert.False(await rig.SignsInAsync(Stored, AccountRig.Typed));
    }

    [Fact]
    public async Task AForgottenPassword_ShouldFindAnAccountStoredWithCapitals()
    {
        using var rig = await AccountRig.StartAsync(mongo);
        await StoreWithCapitalsAsync(rig);

        await rig.Auth.CreatePasswordResetAsync(Typed, "127.0.0.1", "http://localhost");

        var request = await rig.Database.GetCollection<BsonDocument>("passwordResetRequests").Find(FilterDefinition<BsonDocument>.Empty).SingleAsync();
        Assert.NotEqual("account-not-found", BsonHelpers.GetString(request, "status"));
    }

    [Fact]
    public async Task TheTeamsDelete_ShouldFindAnAccountStoredWithCapitals()
    {
        using var rig = await AccountRig.StartAsync(mongo);
        await StoreWithCapitalsAsync(rig);

        await AccountRig.Form(rig.TeamMember(), ("email", Typed)).DeleteAccount(CancellationToken.None);

        Assert.Equal(0, await AccountsAsync(rig));
    }
}
