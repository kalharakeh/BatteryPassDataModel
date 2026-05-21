using MongoDB.Bson;
using MongoDB.Driver;

namespace BatteryPassWeb.Services;

public sealed class ClusterRepository
{
    private readonly MongoContext _mongoContext;

    public ClusterRepository(MongoContext mongoContext)
    {
        _mongoContext = mongoContext;
    }

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
            .Find(Builders<BsonDocument>.Filter.Eq("email", email.Trim().ToLowerInvariant()))
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

        var normalizedEmail = email.Trim().ToLowerInvariant();
        return await _mongoContext.Database.GetCollection<BsonDocument>("users")
            .Find(Builders<BsonDocument>.Filter.Eq("email", normalizedEmail))
            .FirstOrDefaultAsync(cancellationToken);
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
            Builders<BsonDocument>.Update.Set("email", normalizedEmail),
            Builders<BsonDocument>.Update.Set("name", string.IsNullOrWhiteSpace(name) ? normalizedEmail : name.Trim()),
            Builders<BsonDocument>.Update.Set("roles", new BsonArray(normalizedRoles)),
            Builders<BsonDocument>.Update.Set("updatedAt", now),
            Builders<BsonDocument>.Update.SetOnInsert("createdAt", now)
        };

        if (!string.IsNullOrWhiteSpace(passwordHash))
        {
            updates.Add(Builders<BsonDocument>.Update.Set("passwordHash", passwordHash));
        }

        await _mongoContext.Database.GetCollection<BsonDocument>("users").UpdateOneAsync(
            Builders<BsonDocument>.Filter.Eq("email", normalizedEmail),
            Builders<BsonDocument>.Update.Combine(updates),
            new UpdateOptions { IsUpsert = true },
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
            Builders<BsonDocument>.Filter.Eq("email", normalizedEmail),
            Builders<BsonDocument>.Update.Combine(updates),
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
            Builders<BsonDocument>.Filter.Eq("email", current),
            Builders<BsonDocument>.Update
                .Set("email", next)
                .Set("updatedAt", now),
            cancellationToken: cancellationToken);
        await _mongoContext.Database.GetCollection<BsonDocument>("clusterMemberships").UpdateManyAsync(
            Builders<BsonDocument>.Filter.Eq("email", current),
            Builders<BsonDocument>.Update
                .Set("email", next)
                .Set("updatedAt", now),
            cancellationToken: cancellationToken);
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
                Builders<BsonDocument>.Filter.Eq("email", normalizedEmail),
                Builders<BsonDocument>.Filter.Eq("clusterId", normalizedClusterId)),
            Builders<BsonDocument>.Update
                .Set("email", normalizedEmail)
                .Set("clusterId", normalizedClusterId)
                .Set("role", normalizedRole)
                .Set("updatedAt", now)
                .SetOnInsert("createdAt", now),
            new UpdateOptions { IsUpsert = true },
            cancellationToken);
    }

    public async Task DeleteClusterMembershipAsync(string email, string clusterId, CancellationToken cancellationToken = default)
    {
        if (_mongoContext.Database == null || string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(clusterId))
        {
            return;
        }

        await _mongoContext.Database.GetCollection<BsonDocument>("clusterMemberships").DeleteOneAsync(
            Builders<BsonDocument>.Filter.And(
                Builders<BsonDocument>.Filter.Eq("email", email.Trim().ToLowerInvariant()),
                Builders<BsonDocument>.Filter.Eq("clusterId", NormalizeClusterId(clusterId))),
            cancellationToken);
    }

    public static string NormalizeClusterId(string clusterId) =>
        clusterId.Trim().ToLowerInvariant();

    public static string NormalizeClusterMembershipRole(string role)
    {
        return role.Trim() switch
        {
            AccessControlService.RoleClusterAdmin => AccessControlService.RoleClusterAdmin,
            AccessControlService.RoleNotifiedBody => AccessControlService.RoleNotifiedBody,
            AccessControlService.RoleMarketSurveillanceAuthority => AccessControlService.RoleMarketSurveillanceAuthority,
            AccessControlService.RoleCommission => AccessControlService.RoleCommission,
            AccessControlService.RoleLegitimateInterest => AccessControlService.RoleLegitimateInterest,
            _ => AccessControlService.RoleNormalUser
        };
    }
}
