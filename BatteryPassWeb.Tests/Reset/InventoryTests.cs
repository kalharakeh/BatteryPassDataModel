using BatteryPassWeb.Tests.Dpp;
using V3Reset;

namespace BatteryPassWeb.Tests.Reset;

/// <summary>Step 3d-2: the inventory shows the owner what battery_pass_v3 holds, and only reads.</summary>
[Collection(MongoCollection.Name)]
public sealed class InventoryTests(MongoFixture mongo)
{
    [Fact]
    public async Task Inventory_ShouldSayWhatTheResetDoesWithEachCollection()
    {
        var database = mongo.NewContext().Database!;
        await ResetTesting.SeedAsync(database);

        var inventory = await Inventory.ReadAsync(database);

        var fates = inventory.Collections.ToDictionary(collection => collection.Name, collection => collection.Fate);
        Assert.Equal(CollectionFate.NewTestData, fates["dpp_packs"]);
        Assert.Equal(CollectionFate.OldData, fates["passports"]);
        Assert.Equal(CollectionFate.Cleared, fates["passwordResetRequests"]);
        Assert.Equal(CollectionFate.Reviewed, fates["users"]);
        Assert.Equal(CollectionFate.Kept, fates["auditEvents"]);
        Assert.Equal(CollectionFate.Unknown, fates["ownerNotes"]);
        Assert.Equal(3, inventory.Collections.Single(collection => collection.Name == "dpp_packs").Count);
    }

    [Fact]
    public async Task Inventory_ShouldListEachAccountWithItsRolesAndClusters()
    {
        var database = mongo.NewContext().Database!;
        await ResetTesting.SeedAsync(database);

        var inventory = await Inventory.ReadAsync(database);

        Assert.Equal(["admin@team.test", "demo@demo.test", "north-admin@north.test", "old-li@repair.test"], inventory.Accounts.Select(account => account.Email));
        var northAdmin = inventory.Accounts.Single(account => account.Email == "north-admin@north.test");
        Assert.Equal("member", northAdmin.Roles);
        Assert.Equal("north (clusterAdmin)", northAdmin.Memberships);
        Assert.Equal("legitimateInterest", inventory.Accounts.Single(account => account.Email == "old-li@repair.test").Roles);
    }

    [Fact]
    public async Task Inventory_ShouldCountEachClustersMembersTokensAndBatteries()
    {
        var database = mongo.NewContext().Database!;
        await ResetTesting.SeedAsync(database);

        var inventory = await Inventory.ReadAsync(database);

        var north = inventory.Clusters.Single(cluster => cluster.ClusterId == "north");
        Assert.Equal(("Customer North", 2L, 1L, 2L), (north.Name, north.Members, north.Tokens, north.Batteries));
        var demo = inventory.Clusters.Single(cluster => cluster.ClusterId == "demo-cluster");
        Assert.Equal((1L, 2L, 1L), (demo.Members, demo.Tokens, demo.Batteries));
    }

    [Fact]
    public async Task Inventory_ShouldListTokensButNeverTheirHashes()
    {
        var database = mongo.NewContext().Database!;
        await ResetTesting.SeedAsync(database);

        var inventory = await Inventory.ReadAsync(database);
        var file = DecisionsFile.Write(DecisionsFile.Defaults(inventory));

        Assert.Equal(5, inventory.Tokens.Count);
        Assert.True(inventory.Tokens.Single(token => token.TokenId == "sample-read-token").IsSample);
        Assert.DoesNotContain("secret-hash", file, StringComparison.Ordinal);
        Assert.DoesNotContain("hash-of", file, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Inventory_ShouldChangeNothing()
    {
        var database = mongo.NewContext().Database!;
        await ResetTesting.SeedAsync(database);
        var before = await ResetTesting.Snapshot(database);

        await Inventory.ReadAsync(database);

        Assert.Equal(before, await ResetTesting.Snapshot(database));
    }
}
