using BatteryPassWeb.Services;
using MongoDB.Bson;
using MongoDB.Driver;

namespace V3Reset;

public sealed record CollectionCount(string Name, long Count, CollectionFate Fate);

/// <summary>An account as the review shows it; its roles are the stored ones, joined with " + ".</summary>
public sealed record AccountRow(string Email, string Name, string Roles, string Memberships, string CreatedAt);

public sealed record ClusterRow(string ClusterId, string Name, long Members, long Tokens, long Batteries);

/// <summary>A token as the review shows it: never its value or hash.</summary>
public sealed record TokenRow(string TokenId, string Name, string AccessMode, string? AutoClusterId, IReadOnlyList<string> ClusterIds, bool GlobalAccess, bool IsSample, string LastUsedAt);

public sealed record InventoryReport(
    IReadOnlyList<CollectionCount> Collections,
    IReadOnlyList<AccountRow> Accounts,
    IReadOnlyList<ClusterRow> Clusters,
    IReadOnlyList<TokenRow> Tokens);

/// <summary>Reads what battery_pass_v3 holds, for the owner's review. It only reads.</summary>
public static class Inventory
{
    public static async Task<InventoryReport> ReadAsync(IMongoDatabase database, CancellationToken cancellationToken = default)
    {
        var names = await (await database.ListCollectionNamesAsync(cancellationToken: cancellationToken)).ToListAsync(cancellationToken);
        var collections = new List<CollectionCount>();
        foreach (var name in names.Where(name => !name.StartsWith("system.", StringComparison.Ordinal)).Order(StringComparer.Ordinal))
        {
            var count = await database.GetCollection<BsonDocument>(name).CountDocumentsAsync(FilterDefinition<BsonDocument>.Empty, cancellationToken: cancellationToken);
            collections.Add(new CollectionCount(name, count, ResetCollections.FateOf(name)));
        }

        var memberships = await All(database, "clusterMemberships", cancellationToken);
        var tokens = (await All(database, "apiTokens", cancellationToken)).Select(ToTokenRow).OrderBy(token => token.Name, StringComparer.OrdinalIgnoreCase).ToList();
        var accounts = (await All(database, "users", cancellationToken))
            .Select(user =>
            {
                var email = BsonHelpers.GetString(user, "email");
                var clusters = memberships
                    .Where(membership => BsonHelpers.GetString(membership, "email").Equals(email, StringComparison.OrdinalIgnoreCase))
                    .Select(membership => $"{BsonHelpers.GetString(membership, "clusterId")} ({BsonHelpers.GetString(membership, "role")})")
                    .Order(StringComparer.Ordinal);
                return new AccountRow(email, BsonHelpers.GetString(user, "name"), RolesText(user), string.Join("; ", clusters), BsonHelpers.GetString(user, "createdAt"));
            })
            .OrderBy(account => account.Email, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var packs = database.GetCollection<BsonDocument>("dpp_packs");
        var clusters = new List<ClusterRow>();
        foreach (var cluster in await All(database, "clusters", cancellationToken))
        {
            var clusterId = BsonHelpers.GetString(cluster, "clusterId");
            clusters.Add(new ClusterRow(
                clusterId,
                BsonHelpers.GetString(cluster, "name"),
                memberships.Count(membership => BsonHelpers.GetString(membership, "clusterId") == clusterId),
                tokens.Count(token => BelongsTo(token, clusterId)),
                await packs.CountDocumentsAsync(Builders<BsonDocument>.Filter.Eq("ClusterId", clusterId), cancellationToken: cancellationToken)));
        }

        return new InventoryReport(collections, accounts, clusters.OrderBy(cluster => cluster.ClusterId, StringComparer.Ordinal).ToList(), tokens);
    }

    /// <summary>A token that belongs to one cluster only, so it goes when that cluster goes.</summary>
    public static bool BelongsTo(TokenRow token, string clusterId) =>
        token.AutoClusterId == clusterId || (token.AutoClusterId is null && token.ClusterIds.Count == 1 && token.ClusterIds[0] == clusterId);

    /// <summary>The stored roles, joined with " + ", as the decisions file shows them.</summary>
    public static string RolesText(BsonDocument user) =>
        user.TryGetValue("roles", out var roles) && roles is BsonArray array
            ? string.Join(" + ", array.Where(role => role.IsString).Select(role => role.AsString))
            : string.Empty;

    private static TokenRow ToTokenRow(BsonDocument token)
    {
        var clusterIds = token.TryGetValue("clusterIds", out var ids) && ids is BsonArray array
            ? array.Where(id => id.IsString).Select(id => id.AsString).ToList()
            : [];
        var autoClusterId = BsonHelpers.GetString(token, "autoClusterId");
        return new TokenRow(
            BsonHelpers.GetString(token, "tokenId"),
            BsonHelpers.GetString(token, "name"),
            BsonHelpers.GetString(token, "accessMode"),
            string.IsNullOrWhiteSpace(autoClusterId) ? null : autoClusterId,
            clusterIds,
            token.GetValue("globalAccess", false).ToBoolean(),
            token.GetValue("isSample", false).ToBoolean(),
            BsonHelpers.GetString(token, "lastUsedAt"));
    }

    private static Task<List<BsonDocument>> All(IMongoDatabase database, string collection, CancellationToken cancellationToken) =>
        database.GetCollection<BsonDocument>(collection).Find(FilterDefinition<BsonDocument>.Empty).ToListAsync(cancellationToken);
}
