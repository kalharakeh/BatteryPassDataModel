using BatteryPassWeb.Services;
using BatteryPassWeb.Tests.Dpp;
using MongoDB.Bson;
using MongoDB.Driver;

namespace BatteryPassWeb.Tests.Reset;

/// <summary>A small database shaped like battery_pass_v3 before the reset, and helpers to look at it.</summary>
internal static class ResetTesting
{
    public static readonly string[] NewTestData = ["dpp_families", "dpp_models", "dpp_sites", "dpp_batches", "dpp_operators", "dpp_packs", "dpp_passport_versions", "dpp_documents", "dpp_files.files", "dpp_files.chunks"];
    public static readonly string[] OldData = ["passports", "batteries", "passportRevisions", "batteryProductTemplates", "passportFiles.files", "passportFiles.chunks"];
    public static readonly string[] Kept = ["applicationSettings", "applicationAuditEvents", "auditEvents", "batteryAuditEvents"];

    /// <summary>Four accounts, two clusters, five tokens, old and new data, the audit log and one collection the app doesn't know.</summary>
    public static async Task SeedAsync(IMongoDatabase database)
    {
        await Insert(database, "users",
            User("admin@team.test", "Team Admin", "admin"),
            User("north-admin@north.test", "North Admin", "member"),
            User("old-li@repair.test", "Old LI", "legitimateInterest"),
            User("demo@demo.test", "Demo User", "member"));
        await Insert(database, "clusterMemberships",
            Membership("north-admin@north.test", "north", "clusterAdmin"),
            Membership("old-li@repair.test", "north", "legitimateInterest"),
            Membership("demo@demo.test", "demo-cluster", "member"));
        await Insert(database, "clusters",
            new BsonDocument { ["clusterId"] = "north", ["name"] = "Customer North" },
            new BsonDocument { ["clusterId"] = "demo-cluster", ["name"] = "Demo cluster" });
        await Insert(database, "apiTokens",
            Token("sample-read-token", "Sample read", "read", isSample: true),
            Token("north-read", "North read", "read", autoClusterId: "north"),
            Token("demo-read", "Demo read", "read", autoClusterId: "demo-cluster"),
            Token("demo-listed", "Demo listed", "readWrite", clusterIds: ["demo-cluster"]),
            Token("team-full", "Team full access", "readWriteSign", globalAccess: true));
        await Insert(database, "passwordResetRequests", new BsonDocument { ["email"] = "demo@demo.test" });
        foreach (var collection in NewTestData.Where(name => name != "dpp_packs"))
        {
            await Insert(database, collection, new BsonDocument { ["note"] = "new test data" });
        }

        await Insert(database, "dpp_packs",
            new BsonDocument { ["ClusterId"] = "north", ["Values"] = new BsonDocument("stateOfCharge", 55) },
            new BsonDocument { ["ClusterId"] = "north" },
            new BsonDocument { ["ClusterId"] = "demo-cluster" });
        foreach (var collection in OldData)
        {
            await Insert(database, collection, new BsonDocument { ["note"] = "v1.2 data" });
        }

        foreach (var collection in Kept)
        {
            await Insert(database, collection, new BsonDocument { ["note"] = "kept" });
        }

        await Insert(database, "ownerNotes", new BsonDocument { ["note"] = "not the app's" });
    }

    public static async Task<long> Count(IMongoDatabase database, string collection) =>
        await database.GetCollection<BsonDocument>(collection).CountDocumentsAsync(FilterDefinition<BsonDocument>.Empty);

    public static async Task<List<string>> CollectionNames(IMongoDatabase database) =>
        await (await database.ListCollectionNamesAsync()).ToListAsync();

    public static async Task<List<string>> Values(IMongoDatabase database, string collection, string field) =>
        (await database.GetCollection<BsonDocument>(collection).Find(FilterDefinition<BsonDocument>.Empty).ToListAsync())
            .Select(document => BsonHelpers.GetString(document, field)).Order(StringComparer.Ordinal).ToList();

    /// <summary>Every collection with its count, to show that nothing changed.</summary>
    public static async Task<Dictionary<string, long>> Snapshot(IMongoDatabase database)
    {
        var counts = new Dictionary<string, long>();
        foreach (var name in await CollectionNames(database))
        {
            counts[name] = await Count(database, name);
        }

        return counts;
    }

    /// <summary>The throwaway server's address, for the tool's own connection.</summary>
    public static string ServerUri(MongoContext context)
    {
        var server = context.Database!.Client.Settings.Server;
        return $"mongodb://{server.Host}:{server.Port}";
    }

    /// <summary>A fresh battery_pass_v3 on the throwaway server: the name the tool insists on.</summary>
    public static async Task<(IMongoDatabase Database, string Uri)> FreshV3Async(MongoFixture mongo)
    {
        var uri = ServerUri(mongo.NewContext());
        var client = new MongoClient(uri);
        await client.DropDatabaseAsync("battery_pass_v3");
        var database = client.GetDatabase("battery_pass_v3");
        await SeedAsync(database);
        return (database, uri);
    }

    public static Dictionary<string, string?> Environment(string uri, string database) =>
        new() { ["MONGODB_URI"] = uri, ["MONGODB_DB"] = database };

    public static string NewFolder() => Path.Combine(Path.GetTempPath(), $"v3reset-test-{Guid.NewGuid():N}");

    private static BsonDocument User(string email, string name, string role) => new()
    {
        ["email"] = email,
        ["name"] = name,
        ["roles"] = new BsonArray { role },
        ["passwordHash"] = $"hash-of-{email}",
        ["createdAt"] = "2026-10-01T08:00:00.0000000Z",
    };

    private static BsonDocument Membership(string email, string clusterId, string role) =>
        new() { ["email"] = email, ["clusterId"] = clusterId, ["role"] = role };

    private static BsonDocument Token(string tokenId, string name, string accessMode, bool isSample = false, string? autoClusterId = null, string[]? clusterIds = null, bool globalAccess = false)
    {
        var token = new BsonDocument
        {
            ["tokenId"] = tokenId,
            ["name"] = name,
            ["accessMode"] = accessMode,
            ["tokenHash"] = $"secret-hash-{tokenId}",
            ["isSample"] = isSample,
            ["globalAccess"] = globalAccess,
            ["clusterIds"] = new BsonArray(clusterIds ?? []),
        };
        if (autoClusterId is not null)
        {
            token["autoClusterId"] = autoClusterId;
        }

        return token;
    }

    private static Task Insert(IMongoDatabase database, string collection, params BsonDocument[] documents) =>
        database.GetCollection<BsonDocument>(collection).InsertManyAsync(documents);
}
