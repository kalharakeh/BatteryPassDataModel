using BatteryPassWeb.Services;
using BatteryPassWeb.Tests.Dpp;
using MongoDB.Bson;
using MongoDB.Driver;
using V3Reset;

namespace BatteryPassWeb.Tests.Reset;

/// <summary>Step 3d-2: the reset removes the old and the test data, applies the decisions, keeps the rest, copies first and logs once.</summary>
[Collection(MongoCollection.Name)]
public sealed class ResetRunnerTests(MongoFixture mongo)
{
    private async Task<(MongoContext Context, IMongoDatabase Database, ResetPlan Plan, string Copy)> PlannedAsync(Func<DecisionRow, DecisionRow>? decide = null)
    {
        var context = mongo.NewContext();
        var database = context.Database!;
        await ResetTesting.SeedAsync(database);
        var inventory = await Inventory.ReadAsync(database);
        var rows = DecisionsFile.Defaults(inventory).Select(decide ?? (row => row)).ToList();
        var (plan, errors) = ResetPlanner.Build(inventory, rows);
        Assert.Empty(errors);
        return (context, database, plan!, ResetTesting.NewFolder());
    }

    [Fact]
    public async Task TheReset_ShouldEmptyTheTestDataAndRemoveTheOldData()
    {
        var (context, database, plan, copy) = await PlannedAsync();

        await ResetRunner.RunAsync(context, plan, copy, "test");

        var names = await ResetTesting.CollectionNames(database);
        foreach (var collection in ResetTesting.NewTestData.Append("passwordResetRequests"))
        {
            Assert.Contains(collection, names);
            Assert.Equal(0, await ResetTesting.Count(database, collection));
        }

        Assert.DoesNotContain(names, name => ResetTesting.OldData.Contains(name));
        Assert.Equal(1, await ResetTesting.Count(database, "ownerNotes"));
        Assert.Equal(1, await ResetTesting.Count(database, "applicationSettings"));
        Assert.Equal(1, await ResetTesting.Count(database, "auditEvents"));
        Assert.Equal(1, await ResetTesting.Count(database, "batteryAuditEvents"));
        Directory.Delete(copy, recursive: true);
    }

    [Fact]
    public async Task TheReset_ShouldKeepAccountsClustersAndRealTokensUnlessMarkedDelete()
    {
        var (context, database, plan, copy) = await PlannedAsync();

        await ResetRunner.RunAsync(context, plan, copy, "test");

        Assert.Equal(["admin@team.test", "demo@demo.test", "north-admin@north.test", "old-li@repair.test"], await ResetTesting.Values(database, "users", "email"));
        Assert.Equal(3, await ResetTesting.Count(database, "clusterMemberships"));
        Assert.Equal(["demo-cluster", "north"], await ResetTesting.Values(database, "clusters", "clusterId"));
        Assert.Equal(["demo-listed", "demo-read", "north-read", "team-full"], await ResetTesting.Values(database, "apiTokens", "tokenId"));
        Directory.Delete(copy, recursive: true);
    }

    [Fact]
    public async Task TheReset_ShouldApplyTheOwnersDecisions()
    {
        var (context, database, plan, copy) = await PlannedAsync(row => row switch
        {
            { Kind: "account", Id: "demo@demo.test" } => row with { Decision = "delete" },
            { Kind: "account", Id: "old-li@repair.test" } => row with { Role = "liRepair" },
            { Kind: "cluster", Id: "demo-cluster" } => row with { Decision = "delete" },
            { Kind: "token", Id: "north-read" } => row with { Decision = "delete" },
            _ => row,
        });

        await ResetRunner.RunAsync(context, plan, copy, "test");

        Assert.Equal(["admin@team.test", "north-admin@north.test", "old-li@repair.test"], await ResetTesting.Values(database, "users", "email"));
        var oldLi = await database.GetCollection<BsonDocument>("users").Find(Builders<BsonDocument>.Filter.Eq("email", "old-li@repair.test")).SingleAsync();
        Assert.Equal(new BsonArray { "liRepair" }, oldLi["roles"]);
        Assert.Equal("hash-of-old-li@repair.test", oldLi["passwordHash"].AsString);
        Assert.Equal(["north", "north"], await ResetTesting.Values(database, "clusterMemberships", "clusterId"));
        Assert.Equal(["north"], await ResetTesting.Values(database, "clusters", "clusterId"));
        Assert.Equal(["team-full"], await ResetTesting.Values(database, "apiTokens", "tokenId"));
        Directory.Delete(copy, recursive: true);
    }

    [Fact]
    public async Task TheSafetyCopy_ShouldHoldEverythingRemovedOrChanged()
    {
        var (context, _, plan, copy) = await PlannedAsync(row => row switch
        {
            { Kind: "account", Id: "demo@demo.test" } => row with { Decision = "delete" },
            { Kind: "account", Id: "old-li@repair.test" } => row with { Role = "liRepair" },
            _ => row,
        });

        await ResetRunner.RunAsync(context, plan, copy, "test");

        Assert.Equal(3, BsonSerializerArray(Path.Combine(copy, "dpp_packs.json")).Count);
        Assert.Single(BsonSerializerArray(Path.Combine(copy, "passports.json")));
        Assert.Equal(["demo@demo.test", "old-li@repair.test"], BsonSerializerArray(Path.Combine(copy, "users.deleted-or-changed.json")).Select(user => user["email"].AsString).Order(StringComparer.Ordinal));
        Assert.Equal(["sample-read-token"], BsonSerializerArray(Path.Combine(copy, "apiTokens.deleted.json")).Select(token => token["tokenId"].AsString));
        Assert.Contains("delete the folder once you're satisfied", await File.ReadAllTextAsync(Path.Combine(copy, "README.txt")), StringComparison.Ordinal);
        Directory.Delete(copy, recursive: true);
    }

    [Fact]
    public async Task AFolderInUse_ShouldStopTheResetBeforeAnyChange()
    {
        var (context, database, plan, copy) = await PlannedAsync();
        Directory.CreateDirectory(copy);
        await File.WriteAllTextAsync(Path.Combine(copy, "older.json"), "[]");
        var before = await ResetTesting.Snapshot(database);

        await Assert.ThrowsAsync<InvalidOperationException>(() => ResetRunner.RunAsync(context, plan, copy, "test"));

        Assert.Equal(before, await ResetTesting.Snapshot(database));
        Directory.Delete(copy, recursive: true);
    }

    [Fact]
    public async Task TheReset_ShouldAddOneAuditEntryWithTheCounts()
    {
        var (context, database, plan, copy) = await PlannedAsync();

        await ResetRunner.RunAsync(context, plan, copy, "v3-reset (owner)");

        var entries = await database.GetCollection<BsonDocument>("applicationAuditEvents").Find(Builders<BsonDocument>.Filter.Eq("eventType", ResetRunner.AuditEventType)).ToListAsync();
        var entry = Assert.Single(entries);
        Assert.Equal("v3-reset (owner)", entry["actor"].AsString);
        Assert.Equal(3, entry["metadata"]["emptied"]["dpp_packs"].ToInt64());
        Assert.Equal(1, entry["metadata"]["removed"]["passports"].ToInt64());
        Assert.Equal(1, entry["metadata"]["tokensDeleted"].ToInt32());
        Assert.Equal(2, await ResetTesting.Count(database, "applicationAuditEvents"));
        Directory.Delete(copy, recursive: true);
    }

    private static List<BsonDocument> BsonSerializerArray(string path) =>
        MongoDB.Bson.Serialization.BsonSerializer.Deserialize<BsonArray>(File.ReadAllText(path)).Select(value => value.AsBsonDocument).ToList();
}
