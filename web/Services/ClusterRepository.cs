using MongoDB.Bson;
using MongoDB.Driver;

namespace BatteryPassWeb.Services;

public sealed class ClusterRepository
{
    private readonly MongoContext _mongoContext;

    /// <summary>
    /// Compares addresses without regard to capitals (finding U28): new writers store them in lower case, but an older
    /// one may not have, and no index forces it.
    /// </summary>
    /// <summary>The cluster a cluster admin made the account in (finding R19); the team's accounts have none.</summary>
    public const string CreatedInClusterField = "createdInCluster";

    public static readonly Collation IgnoreCase = new("en", strength: CollationStrength.Secondary);

    public ClusterRepository(MongoContext mongoContext)
    {
        _mongoContext = mongoContext;
    }

    /// <summary>An address as the app writes it: no outer spaces, lower case.</summary>
    public static string NormalizeEmail(string? email) => (email ?? string.Empty).Trim().ToLowerInvariant();

    /// <summary>
    /// The account with this address whatever its capitals and outer spaces (finding U28): the address exactly as typed
    /// first (so two old accounts that differ only in capitals each still sign in as before), then in lower case, as the
    /// app stores it, then one stored with other capitals. Every account look-up goes through here, sign-in included.
    /// </summary>
    public static async Task<BsonDocument?> FindUserAsync(IMongoDatabase database, string? email, CancellationToken cancellationToken = default)
    {
        var typed = (email ?? string.Empty).Trim();
        var normalizedEmail = NormalizeEmail(email);
        if (normalizedEmail.Length == 0)
        {
            return null;
        }

        var users = database.GetCollection<BsonDocument>("users");
        if (typed != normalizedEmail
            && await users.Find(Builders<BsonDocument>.Filter.Eq("email", typed)).FirstOrDefaultAsync(cancellationToken) is { } asTyped)
        {
            return asTyped;
        }

        return await users.Find(Builders<BsonDocument>.Filter.Eq("email", normalizedEmail)).FirstOrDefaultAsync(cancellationToken)
            ?? await users.Find(Builders<BsonDocument>.Filter.Eq("email", normalizedEmail), new FindOptions { Collation = IgnoreCase })
                .FirstOrDefaultAsync(cancellationToken);
    }

    /// <summary>The memberships of this address, whatever the capitals they were stored with (finding U28).</summary>
    public static FilterDefinition<BsonDocument> MembershipsOf(string? email) =>
        Builders<BsonDocument>.Filter.Eq("email", NormalizeEmail(email));

    public async Task EnsureIndexesAsync(CancellationToken cancellationToken = default)
    {
        if (_mongoContext.Database == null)
        {
            return;
        }

        await _mongoContext.Database.GetCollection<BsonDocument>("clusters").Indexes.CreateOneAsync(
            new CreateIndexModel<BsonDocument>(
                Builders<BsonDocument>.IndexKeys.Ascending("clusterId"),
                new CreateIndexOptions { Unique = true }),
            cancellationToken: cancellationToken);
        await _mongoContext.Database.GetCollection<BsonDocument>("clusterMemberships").Indexes.CreateOneAsync(
            new CreateIndexModel<BsonDocument>(
                Builders<BsonDocument>.IndexKeys.Ascending("email").Ascending("clusterId"),
                new CreateIndexOptions { Unique = true }),
            cancellationToken: cancellationToken);
    }

    public async Task<IReadOnlyList<BsonDocument>> ListClustersAsync(CancellationToken cancellationToken = default)
    {
        if (_mongoContext.Database == null)
        {
            return [];
        }

        return await _mongoContext.Database.GetCollection<BsonDocument>("clusters")
            .Find(Builders<BsonDocument>.Filter.Empty)
            .SortBy(cluster => cluster["name"])
            .ToListAsync(cancellationToken);
    }

    public async Task<BsonDocument?> GetClusterByIdAsync(string clusterId, CancellationToken cancellationToken = default)
    {
        if (_mongoContext.Database == null || string.IsNullOrWhiteSpace(clusterId))
        {
            return null;
        }

        return await _mongoContext.Database.GetCollection<BsonDocument>("clusters")
            .Find(Builders<BsonDocument>.Filter.Eq("clusterId", NormalizeClusterId(clusterId)))
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<BsonDocument>> ListClusterMembershipsAsync(CancellationToken cancellationToken = default)
    {
        if (_mongoContext.Database == null)
        {
            return [];
        }

        return await _mongoContext.Database.GetCollection<BsonDocument>("clusterMemberships")
            .Find(Builders<BsonDocument>.Filter.Empty)
            .SortBy(membership => membership["email"])
            .ThenBy(membership => membership["clusterId"])
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<BsonDocument>> GetClusterMembershipsForUserAsync(string email, CancellationToken cancellationToken = default)
    {
        if (_mongoContext.Database == null || string.IsNullOrWhiteSpace(email))
        {
            return [];
        }

        return await _mongoContext.Database.GetCollection<BsonDocument>("clusterMemberships")
            .Find(MembershipsOf(email), new FindOptions { Collation = IgnoreCase })
            .SortBy(membership => membership["clusterId"])
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<BsonDocument>> ListUsersAsync(CancellationToken cancellationToken = default)
    {
        if (_mongoContext.Database == null)
        {
            return [];
        }

        return await _mongoContext.Database.GetCollection<BsonDocument>("users")
            .Find(Builders<BsonDocument>.Filter.Empty)
            .Project(Builders<BsonDocument>.Projection.Exclude("passwordHash"))
            .SortBy(user => user["email"])
            .ToListAsync(cancellationToken);
    }

    public async Task<BsonDocument?> GetUserByEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        if (_mongoContext.Database == null || string.IsNullOrWhiteSpace(email))
        {
            return null;
        }

        return await FindUserAsync(_mongoContext.Database, email, cancellationToken);
    }

    public async Task<long> CountMembershipsByClusterAsync(string clusterId, CancellationToken cancellationToken = default)
    {
        if (_mongoContext.Database == null || string.IsNullOrWhiteSpace(clusterId))
        {
            return 0;
        }

        return await _mongoContext.Database.GetCollection<BsonDocument>("clusterMemberships")
            .CountDocumentsAsync(Builders<BsonDocument>.Filter.Eq("clusterId", NormalizeClusterId(clusterId)), cancellationToken: cancellationToken);
    }

    public async Task UpsertUserAsync(
        string email,
        string name,
        IReadOnlyList<string> roles,
        string? passwordHash = null,
        CancellationToken cancellationToken = default)
    {
        if (_mongoContext.Database == null || string.IsNullOrWhiteSpace(email))
        {
            return;
        }

        var normalizedEmail = email.Trim().ToLowerInvariant();
        var now = DateTime.UtcNow.ToString("O");
        var normalizedRoles = roles
            .Where(role => !string.IsNullOrWhiteSpace(role))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (normalizedRoles.Count == 0)
        {
            normalizedRoles.Add(AccessControlService.RoleNormalUser);
        }

        var updates = new List<UpdateDefinition<BsonDocument>>
        {
            Builders<BsonDocument>.Update.SetOnInsert("email", normalizedEmail),
            Builders<BsonDocument>.Update.Set("name", string.IsNullOrWhiteSpace(name) ? normalizedEmail : name.Trim()),
            Builders<BsonDocument>.Update.Set("roles", new BsonArray(normalizedRoles)),
            Builders<BsonDocument>.Update.Set("updatedAt", now),
            Builders<BsonDocument>.Update.SetOnInsert("createdAt", now)
        };

        if (!string.IsNullOrWhiteSpace(passwordHash))
        {
            updates.Add(Builders<BsonDocument>.Update.Set("passwordHash", passwordHash));
        }

        // An account stored with capitals is updated where it is, so no second one is made (finding U28).
        await _mongoContext.Database.GetCollection<BsonDocument>("users").UpdateOneAsync(
            await UserFilterAsync(normalizedEmail, cancellationToken),
            Builders<BsonDocument>.Update.Combine(updates),
            new UpdateOptions { IsUpsert = true },
            cancellationToken);
    }

    /// <summary>
    /// A cluster admin's new account: a member with this password, marked with the cluster it was made in, so that cluster's
    /// admins may later change its name and password (finding R19). Only ever inserts: an account that exists meanwhile is
    /// left as it is.
    /// </summary>
    public async Task CreateAccountInClusterAsync(string email, string name, string passwordHash, string clusterId, CancellationToken cancellationToken = default)
    {
        if (_mongoContext.Database == null || string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(clusterId))
        {
            return;
        }

        var normalizedEmail = NormalizeEmail(email);
        var now = DateTime.UtcNow.ToString("O");
        await _mongoContext.Database.GetCollection<BsonDocument>("users").UpdateOneAsync(
            Builders<BsonDocument>.Filter.Eq("email", normalizedEmail),
            Builders<BsonDocument>.Update
                .SetOnInsert("email", normalizedEmail)
                .SetOnInsert("name", string.IsNullOrWhiteSpace(name) ? normalizedEmail : name.Trim())
                .SetOnInsert("roles", new BsonArray { AccessControlService.RoleNormalUser })
                .SetOnInsert("passwordHash", passwordHash)
                .SetOnInsert(CreatedInClusterField, NormalizeClusterId(clusterId))
                .SetOnInsert("createdAt", now)
                .SetOnInsert("updatedAt", now),
            new UpdateOptions { IsUpsert = true, Collation = IgnoreCase },
            cancellationToken);
    }

    public async Task UpdateUserProfileAsync(
        string email,
        string name,
        string? passwordHash = null,
        CancellationToken cancellationToken = default)
    {
        if (_mongoContext.Database == null || string.IsNullOrWhiteSpace(email))
        {
            return;
        }

        var normalizedEmail = email.Trim().ToLowerInvariant();
        var updates = new List<UpdateDefinition<BsonDocument>>
        {
            Builders<BsonDocument>.Update.Set("name", string.IsNullOrWhiteSpace(name) ? normalizedEmail : name.Trim()),
            Builders<BsonDocument>.Update.Set("updatedAt", DateTime.UtcNow.ToString("O"))
        };
        if (!string.IsNullOrWhiteSpace(passwordHash))
        {
            updates.Add(Builders<BsonDocument>.Update.Set("passwordHash", passwordHash));
        }

        await _mongoContext.Database.GetCollection<BsonDocument>("users").UpdateOneAsync(
            await UserFilterAsync(normalizedEmail, cancellationToken),
            Builders<BsonDocument>.Update.Combine(updates),
            cancellationToken: cancellationToken);
    }

    /// <summary>The stored account by its id when it exists (whatever its capitals), else the lower-case address.</summary>
    private async Task<FilterDefinition<BsonDocument>> UserFilterAsync(string email, CancellationToken cancellationToken) =>
        await FindUserAsync(_mongoContext.Database!, email, cancellationToken) is { } user
            ? Builders<BsonDocument>.Filter.Eq("_id", user["_id"])
            : Builders<BsonDocument>.Filter.Eq("email", NormalizeEmail(email));

    public async Task SetPasswordHashAsync(string email, string passwordHash, CancellationToken cancellationToken = default)
    {
        if (_mongoContext.Database == null || string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(passwordHash))
        {
            return;
        }

        await _mongoContext.Database.GetCollection<BsonDocument>("users").UpdateOneAsync(
            await UserFilterAsync(email, cancellationToken),
            Builders<BsonDocument>.Update
                .Set("passwordHash", passwordHash)
                .Set("updatedAt", DateTime.UtcNow.ToString("O")),
            cancellationToken: cancellationToken);
    }

    public async Task UpdateUserEmailAsync(string currentEmail, string newEmail, CancellationToken cancellationToken = default)
    {
        if (_mongoContext.Database == null || string.IsNullOrWhiteSpace(currentEmail) || string.IsNullOrWhiteSpace(newEmail))
        {
            return;
        }

        var current = currentEmail.Trim().ToLowerInvariant();
        var next = newEmail.Trim().ToLowerInvariant();
        if (current.Equals(next, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var now = DateTime.UtcNow.ToString("O");
        await _mongoContext.Database.GetCollection<BsonDocument>("users").UpdateOneAsync(
            await UserFilterAsync(current, cancellationToken),
            Builders<BsonDocument>.Update
                .Set("email", next)
                .Set("updatedAt", now),
            cancellationToken: cancellationToken);
        await _mongoContext.Database.GetCollection<BsonDocument>("clusterMemberships").UpdateManyAsync(
            MembershipsOf(current),
            Builders<BsonDocument>.Update
                .Set("email", next)
                .Set("updatedAt", now),
            new UpdateOptions { Collation = IgnoreCase },
            cancellationToken);
    }

    public async Task UpsertClusterAsync(string clusterId, string name, CancellationToken cancellationToken = default)
    {
        if (_mongoContext.Database == null || string.IsNullOrWhiteSpace(clusterId) || string.IsNullOrWhiteSpace(name))
        {
            return;
        }

        var normalizedClusterId = NormalizeClusterId(clusterId);
        var now = DateTime.UtcNow.ToString("O");
        await _mongoContext.Database.GetCollection<BsonDocument>("clusters").UpdateOneAsync(
            Builders<BsonDocument>.Filter.Eq("clusterId", normalizedClusterId),
            Builders<BsonDocument>.Update
                .Set("clusterId", normalizedClusterId)
                .Set("name", name)
                .Set("updatedAt", now)
                .SetOnInsert("createdAt", now),
            new UpdateOptions { IsUpsert = true },
            cancellationToken);
    }

    public async Task UpdateClusterNameAsync(string clusterId, string name, CancellationToken cancellationToken = default)
    {
        if (_mongoContext.Database == null || string.IsNullOrWhiteSpace(clusterId) || string.IsNullOrWhiteSpace(name))
        {
            return;
        }

        var normalizedClusterId = NormalizeClusterId(clusterId);
        var now = DateTime.UtcNow.ToString("O");
        await _mongoContext.Database.GetCollection<BsonDocument>("clusters").UpdateOneAsync(
            Builders<BsonDocument>.Filter.Eq("clusterId", normalizedClusterId),
            Builders<BsonDocument>.Update
                .Set("name", name)
                .Set("updatedAt", now),
            cancellationToken: cancellationToken);
    }

    public async Task DeleteClusterAsync(string clusterId, CancellationToken cancellationToken = default)
    {
        if (_mongoContext.Database == null || string.IsNullOrWhiteSpace(clusterId))
        {
            return;
        }

        await _mongoContext.Database.GetCollection<BsonDocument>("clusters")
            .DeleteOneAsync(Builders<BsonDocument>.Filter.Eq("clusterId", NormalizeClusterId(clusterId)), cancellationToken);
        await _mongoContext.Database.GetCollection<BsonDocument>("clusterMemberships")
            .DeleteManyAsync(Builders<BsonDocument>.Filter.Eq("clusterId", NormalizeClusterId(clusterId)), cancellationToken);
    }

    public async Task UpsertClusterMembershipAsync(string email, string clusterId, string role, CancellationToken cancellationToken = default)
    {
        if (_mongoContext.Database == null || string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(clusterId))
        {
            return;
        }

        var normalizedEmail = email.Trim().ToLowerInvariant();
        var normalizedClusterId = NormalizeClusterId(clusterId);
        var normalizedRole = NormalizeClusterMembershipRole(role);
        var now = DateTime.UtcNow.ToString("O");
        await _mongoContext.Database.GetCollection<BsonDocument>("clusterMemberships").UpdateOneAsync(
            Builders<BsonDocument>.Filter.And(
                MembershipsOf(normalizedEmail),
                Builders<BsonDocument>.Filter.Eq("clusterId", normalizedClusterId)),
            Builders<BsonDocument>.Update
                .SetOnInsert("email", normalizedEmail)
                .SetOnInsert("clusterId", normalizedClusterId)
                .Set("role", normalizedRole)
                .Set("updatedAt", now)
                .SetOnInsert("createdAt", now),
            new UpdateOptions { IsUpsert = true, Collation = IgnoreCase },
            cancellationToken);
    }

    /// <summary>Deletes an account and every cluster membership it has (decision 30).</summary>
    public async Task DeleteUserAsync(string email, CancellationToken cancellationToken = default)
    {
        if (_mongoContext.Database == null || string.IsNullOrWhiteSpace(email))
        {
            return;
        }

        await _mongoContext.Database.GetCollection<BsonDocument>("clusterMemberships")
            .DeleteManyAsync(MembershipsOf(email), new DeleteOptions { Collation = IgnoreCase }, cancellationToken);
        await _mongoContext.Database.GetCollection<BsonDocument>("users")
            .DeleteOneAsync(await UserFilterAsync(email, cancellationToken), cancellationToken);
    }

    /// <summary>"North User is no longer in Customer North.": what removing a membership did, by name (finding U29).</summary>
    public async Task<string> MembershipRemovedMessageAsync(string email, string clusterId, CancellationToken cancellationToken = default)
    {
        var user = await GetUserByEmailAsync(email, cancellationToken);
        var cluster = await GetClusterByIdAsync(clusterId, cancellationToken);
        var person = user is null ? string.Empty : BsonHelpers.GetString(user, "name");
        var clusterName = cluster is null ? string.Empty : BsonHelpers.GetString(cluster, "name");
        return $"{(string.IsNullOrWhiteSpace(person) ? NormalizeEmail(email) : person)} is no longer in {(string.IsNullOrWhiteSpace(clusterName) ? clusterId : clusterName)}.";
    }

    public async Task DeleteClusterMembershipAsync(string email, string clusterId, CancellationToken cancellationToken = default)
    {
        if (_mongoContext.Database == null || string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(clusterId))
        {
            return;
        }

        await _mongoContext.Database.GetCollection<BsonDocument>("clusterMemberships").DeleteManyAsync(
            Builders<BsonDocument>.Filter.And(
                MembershipsOf(email),
                Builders<BsonDocument>.Filter.Eq("clusterId", NormalizeClusterId(clusterId))),
            new DeleteOptions { Collation = IgnoreCase },
            cancellationToken);
    }

    public static string NormalizeClusterId(string clusterId) =>
        clusterId.Trim().ToLowerInvariant();

    public const string OnlyTwoClusterRoles = "A person is a user or a cluster admin in a cluster.";

    /// <summary>Whether a form's membership role is one of the two a cluster has (finding S08).</summary>
    public static bool IsClusterMembershipRole(string? role) =>
        role?.Trim() is AccessControlService.RoleNormalUser or AccessControlService.RoleClusterAdmin;

    public static string NormalizeClusterMembershipRole(string role)
    {
        // A cluster has two roles (finding S08); anything else is stored, and read, as a plain user.
        return role.Trim() == AccessControlService.RoleClusterAdmin
            ? AccessControlService.RoleClusterAdmin
            : AccessControlService.RoleNormalUser;
    }
}
