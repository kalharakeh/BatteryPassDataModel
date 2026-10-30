using BatteryPassWeb.Services;
using MongoDB.Bson;
using MongoDB.Bson.IO;
using MongoDB.Driver;

namespace V3Reset;

/// <summary>Carries out a checked plan: first a safety copy of everything it removes or changes, then the changes, then one audit entry.</summary>
public static class ResetRunner
{
    public const string AuditEventType = "data.reset";

    public static async Task<BsonDocument> RunAsync(
        MongoContext context,
        ResetPlan plan,
        string copyFolder,
        string actor,
        CancellationToken cancellationToken = default)
    {
        var database = context.Database ?? throw new InvalidOperationException("No database connection.", context.ConnectionError);
        await WriteSafetyCopyAsync(database, plan, copyFolder, cancellationToken);

        foreach (var collection in plan.Emptied)
        {
            await database.GetCollection<BsonDocument>(collection.Name).DeleteManyAsync(FilterDefinition<BsonDocument>.Empty, cancellationToken);
        }

        foreach (var collection in plan.Removed)
        {
            await database.DropCollectionAsync(collection.Name, cancellationToken);
        }

        // The app's own code removes clusters (with their memberships) and accounts (with theirs), as its pages do.
        var clusters = new ClusterRepository(context);
        await database.GetCollection<BsonDocument>("apiTokens").DeleteManyAsync(Builders<BsonDocument>.Filter.In("tokenId", plan.TokensDeleted), cancellationToken);
        foreach (var clusterId in plan.ClustersDeleted)
        {
            await clusters.DeleteClusterAsync(clusterId, cancellationToken);
        }

        foreach (var email in plan.AccountsDeleted)
        {
            await clusters.DeleteUserAsync(email, cancellationToken);
        }

        var now = DateTime.UtcNow.ToString("O");
        foreach (var change in plan.RoleChanges)
        {
            await database.GetCollection<BsonDocument>("users").UpdateOneAsync(
                Builders<BsonDocument>.Filter.Eq("email", change.Email),
                Builders<BsonDocument>.Update.Set("roles", new BsonArray(change.To)).Set("updatedAt", now),
                cancellationToken: cancellationToken);
        }

        return await new ApplicationAuditService(context).AppendApplicationAuditEventAsync(
            AuditEventType,
            actor,
            "admin",
            "v3-reset",
            "battery_pass_v3 was reset: the old app's data and the new test data were removed.",
            new BsonDocument
            {
                ["emptied"] = new BsonDocument(plan.Emptied.Select(collection => new BsonElement(collection.Name, collection.Count))),
                ["removed"] = new BsonDocument(plan.Removed.Select(collection => new BsonElement(collection.Name, collection.Count))),
                ["accountsDeleted"] = plan.AccountsDeleted.Count,
                ["rolesChanged"] = plan.RoleChanges.Count,
                ["clustersDeleted"] = new BsonArray(plan.ClustersDeleted),
                ["tokensDeleted"] = plan.TokensDeleted.Count,
            },
            "database",
            ResetCollections.Database,
            cancellationToken: cancellationToken);
    }

    /// <summary>Writes every document the reset removes or changes, one JSON file per collection, before anything changes.</summary>
    private static async Task WriteSafetyCopyAsync(IMongoDatabase database, ResetPlan plan, string folder, CancellationToken cancellationToken)
    {
        if (Directory.Exists(folder) && Directory.EnumerateFileSystemEntries(folder).Any())
        {
            throw new InvalidOperationException($"The safety copy folder {folder} isn't empty; choose a new one.");
        }

        Directory.CreateDirectory(folder);
        foreach (var collection in plan.Emptied.Concat(plan.Removed))
        {
            await CopyAsync(database, collection.Name, FilterDefinition<BsonDocument>.Empty, Path.Combine(folder, $"{collection.Name}.json"), cancellationToken);
        }

        var memberships = Builders<BsonDocument>.Filter.Or(
            Builders<BsonDocument>.Filter.In("email", plan.AccountsDeleted),
            Builders<BsonDocument>.Filter.In("clusterId", plan.ClustersDeleted));
        await CopyAsync(database, "users", Builders<BsonDocument>.Filter.In("email", plan.AccountsDeleted.Concat(plan.RoleChanges.Select(change => change.Email))), Path.Combine(folder, "users.deleted-or-changed.json"), cancellationToken);
        await CopyAsync(database, "clusterMemberships", memberships, Path.Combine(folder, "clusterMemberships.deleted.json"), cancellationToken);
        await CopyAsync(database, "clusters", Builders<BsonDocument>.Filter.In("clusterId", plan.ClustersDeleted), Path.Combine(folder, "clusters.deleted.json"), cancellationToken);
        await CopyAsync(database, "apiTokens", Builders<BsonDocument>.Filter.In("tokenId", plan.TokensDeleted), Path.Combine(folder, "apiTokens.deleted.json"), cancellationToken);
        await File.WriteAllTextAsync(
            Path.Combine(folder, "README.txt"),
            "Safety copy of what the battery_pass_v3 reset removed or changed, one file per collection (MongoDB extended JSON).\r\n"
            + "It holds removed accounts' password hashes and removed tokens' hashes: keep it on this PC only, and delete the folder once you're satisfied with the reset.\r\n",
            cancellationToken);
    }

    private static async Task CopyAsync(IMongoDatabase database, string collection, FilterDefinition<BsonDocument> filter, string path, CancellationToken cancellationToken)
    {
        var settings = new JsonWriterSettings { OutputMode = JsonOutputMode.CanonicalExtendedJson };
        await using var writer = new StreamWriter(path);
        await writer.WriteLineAsync("[");
        var first = true;
        using var cursor = await database.GetCollection<BsonDocument>(collection).FindAsync(filter, cancellationToken: cancellationToken);
        while (await cursor.MoveNextAsync(cancellationToken))
        {
            foreach (var document in cursor.Current)
            {
                await writer.WriteLineAsync((first ? string.Empty : ",") + document.ToJson(settings));
                first = false;
            }
        }

        await writer.WriteLineAsync("]");
    }
}
