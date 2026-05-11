using System.Security.Claims;
using BatteryPassWeb.Configuration;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;

namespace BatteryPassWeb.Services;

public sealed class AuthService
{
    private readonly MongoContext _mongoContext;
    private readonly BatteryPassOptions _options;

    public AuthService(MongoContext mongoContext, IOptions<BatteryPassOptions> options)
    {
        _mongoContext = mongoContext;
        _options = options.Value;
    }

    public async Task<ClaimsPrincipal?> AuthenticateAsync(string email, string password, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            return null;
        }

        var normalizedEmail = email.Trim();

        try
        {
            if (_mongoContext.Database != null)
            {
                var users = _mongoContext.Database.GetCollection<BsonDocument>("users");
                var user = await users.Find(Builders<BsonDocument>.Filter.Eq("email", normalizedEmail)).FirstOrDefaultAsync(cancellationToken);

                if (user != null)
                {
                    var passwordHash = BsonHelpers.GetString(user, "passwordHash");
                    if (!string.IsNullOrWhiteSpace(passwordHash) && BCrypt.Net.BCrypt.Verify(password, passwordHash))
                    {
                        var roles = ExtractRoles(user).ToList();
                        if (!roles.Contains(AccessControlService.RoleAdmin, StringComparer.OrdinalIgnoreCase))
                        {
                            var hasClusterAdminMembership = await _mongoContext.Database
                                .GetCollection<BsonDocument>("clusterMemberships")
                                .Find(Builders<BsonDocument>.Filter.And(
                                    Builders<BsonDocument>.Filter.Eq("email", normalizedEmail.ToLowerInvariant()),
                                    Builders<BsonDocument>.Filter.Eq("role", AccessControlService.RoleClusterAdmin)))
                                .AnyAsync(cancellationToken);
                            if (hasClusterAdminMembership)
                            {
                                roles.Add(AccessControlService.RoleClusterAdmin);
                            }
                        }

                        return BuildPrincipal(normalizedEmail, BsonHelpers.GetString(user, "name"), roles);
                    }

                    return null;
                }
            }
        }
        catch
        {
            // Mongo unavailable fallback handled below.
        }

        if (normalizedEmail.Equals(_options.DemoAdminEmail, StringComparison.OrdinalIgnoreCase)
            && password == _options.DemoAdminPassword)
        {
            return BuildPrincipal(_options.DemoAdminEmail, "Demo Administrator", [AccessControlService.RoleAdmin]);
        }

        return null;
    }

    public async Task<ClaimsPrincipal?> CreatePrincipalForUserAsync(string email, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return null;
        }

        var normalizedEmail = email.Trim().ToLowerInvariant();
        if (_mongoContext.Database != null)
        {
            var user = await _mongoContext.Database.GetCollection<BsonDocument>("users")
                .Find(Builders<BsonDocument>.Filter.Eq("email", normalizedEmail))
                .FirstOrDefaultAsync(cancellationToken);
            if (user != null)
            {
                var roles = ExtractRoles(user).ToList();
                if (!roles.Contains(AccessControlService.RoleAdmin, StringComparer.OrdinalIgnoreCase))
                {
                    var hasClusterAdminMembership = await _mongoContext.Database
                        .GetCollection<BsonDocument>("clusterMemberships")
                        .Find(Builders<BsonDocument>.Filter.And(
                            Builders<BsonDocument>.Filter.Eq("email", normalizedEmail),
                            Builders<BsonDocument>.Filter.Eq("role", AccessControlService.RoleClusterAdmin)))
                        .AnyAsync(cancellationToken);
                    if (hasClusterAdminMembership)
                    {
                        roles.Add(AccessControlService.RoleClusterAdmin);
                    }
                }

                return BuildPrincipal(normalizedEmail, BsonHelpers.GetString(user, "name"), roles);
            }
        }

        if (normalizedEmail.Equals(_options.DemoAdminEmail, StringComparison.OrdinalIgnoreCase))
        {
            return BuildPrincipal(_options.DemoAdminEmail, "Demo Administrator", [AccessControlService.RoleAdmin]);
        }

        return null;
    }

    private static ClaimsPrincipal BuildPrincipal(string email, string name, IReadOnlyList<string> roles)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, email),
            new(ClaimTypes.Name, string.IsNullOrWhiteSpace(name) ? email : name),
            new(ClaimTypes.Email, email),
        };

        foreach (var role in roles.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            claims.Add(new Claim(ClaimTypes.Role, role));
        }

        var identity = new ClaimsIdentity(claims, "BatteryPassCookie");
        return new ClaimsPrincipal(identity);
    }

    private static IReadOnlyList<string> ExtractRoles(BsonDocument user)
    {
        if (!user.TryGetValue("roles", out var rolesValue) || rolesValue is not BsonArray roleArray)
        {
            return [AccessControlService.RoleNormalUser];
        }

        var roles = roleArray
            .Select(role => NormalizeRole(role.ToString() ?? string.Empty))
            .Where(role => !string.IsNullOrWhiteSpace(role))
            .Select(role => role!)
            .ToList();

        return roles.Count > 0 ? roles : [AccessControlService.RoleNormalUser];
    }

    private static string NormalizeRole(string role)
    {
        return role.Equals("viewer", StringComparison.OrdinalIgnoreCase)
            ? AccessControlService.RoleNormalUser
            : role;
    }
}
