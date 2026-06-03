using System.Security.Claims;
using System.Security.Cryptography;
using BatteryPassWeb.Configuration;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;

namespace BatteryPassWeb.Services;

public sealed class AuthService
{
    private readonly MongoContext _mongoContext;
    private readonly BatteryPassOptions _options;
    private readonly IEmailSender _emailSender;

    public AuthService(MongoContext mongoContext, IOptions<BatteryPassOptions> options, IEmailSender emailSender)
    {
        _mongoContext = mongoContext;
        _options = options.Value;
        _emailSender = emailSender;
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

    public async Task StorePasswordResetRequestAsync(string email, string remoteIp, CancellationToken cancellationToken = default)
    {
        await CreatePasswordResetAsync(email, remoteIp, string.Empty, cancellationToken);
    }

    public async Task CreatePasswordResetAsync(
        string email,
        string remoteIp,
        string requestBaseUrl,
        CancellationToken cancellationToken = default)
    {
        if (_mongoContext.Database == null || string.IsNullOrWhiteSpace(email))
        {
            return;
        }

        var normalizedEmail = email.Trim().ToLowerInvariant();
        var now = DateTimeOffset.UtcNow;
        var users = _mongoContext.Database.GetCollection<BsonDocument>("users");
        var user = await users.Find(Builders<BsonDocument>.Filter.Eq("email", normalizedEmail)).FirstOrDefaultAsync(cancellationToken);
        var resetRequests = _mongoContext.Database.GetCollection<BsonDocument>("passwordResetRequests");
        var request = new BsonDocument
        {
            ["email"] = normalizedEmail,
            ["remoteIp"] = remoteIp,
            ["requestedAt"] = now.ToString("O"),
            ["expiresAt"] = now.AddMinutes(60).ToString("O"),
            ["usedAt"] = BsonNull.Value,
            ["status"] = user == null ? "account-not-found" : "created"
        };

        if (user != null)
        {
            var token = CreateResetToken();
            request["tokenHash"] = HashResetToken(token);
            request["resetUrl"] = BuildResetUrl(normalizedEmail, token, requestBaseUrl);
        }

        await resetRequests.InsertOneAsync(request, cancellationToken: cancellationToken);

        if (user == null || !request.TryGetValue("resetUrl", out var resetUrlValue) || resetUrlValue.IsBsonNull)
        {
            return;
        }

        if (!_emailSender.IsConfigured)
        {
            await resetRequests.UpdateOneAsync(
                Builders<BsonDocument>.Filter.Eq("_id", request["_id"]),
                Builders<BsonDocument>.Update.Set("status", "email-not-configured"),
                cancellationToken: cancellationToken);
            return;
        }

        try
        {
            await _emailSender.SendPasswordResetAsync(normalizedEmail, resetUrlValue.AsString, cancellationToken);
            await resetRequests.UpdateOneAsync(
                Builders<BsonDocument>.Filter.Eq("_id", request["_id"]),
                Builders<BsonDocument>.Update
                    .Set("status", "email-sent")
                    .Set("sentAt", DateTimeOffset.UtcNow.ToString("O")),
                cancellationToken: cancellationToken);
        }
        catch (Exception exception)
        {
            await resetRequests.UpdateOneAsync(
                Builders<BsonDocument>.Filter.Eq("_id", request["_id"]),
                Builders<BsonDocument>.Update
                    .Set("status", "email-failed")
                    .Set("deliveryError", exception.Message),
                cancellationToken: cancellationToken);
        }
    }

    public async Task<bool> ConsumePasswordResetTokenAsync(
        string email,
        string token,
        string newPassword,
        CancellationToken cancellationToken = default)
    {
        if (_mongoContext.Database == null
            || string.IsNullOrWhiteSpace(email)
            || string.IsNullOrWhiteSpace(token)
            || string.IsNullOrWhiteSpace(newPassword)
            || newPassword.Length < 8)
        {
            return false;
        }

        var normalizedEmail = email.Trim().ToLowerInvariant();
        var now = DateTimeOffset.UtcNow.ToString("O");
        var tokenHash = HashResetToken(token.Trim());
        var resetRequests = _mongoContext.Database.GetCollection<BsonDocument>("passwordResetRequests");
        var filter = Builders<BsonDocument>.Filter.And(
            Builders<BsonDocument>.Filter.Eq("email", normalizedEmail),
            Builders<BsonDocument>.Filter.Eq("tokenHash", tokenHash),
            Builders<BsonDocument>.Filter.Eq("usedAt", BsonNull.Value),
            Builders<BsonDocument>.Filter.Gt("expiresAt", now));

        var request = await resetRequests.Find(filter)
            .SortByDescending(document => document["requestedAt"])
            .FirstOrDefaultAsync(cancellationToken);
        if (request == null)
        {
            return false;
        }

        var passwordHash = BCrypt.Net.BCrypt.HashPassword(newPassword);
        var users = _mongoContext.Database.GetCollection<BsonDocument>("users");
        var userUpdate = await users.UpdateOneAsync(
            Builders<BsonDocument>.Filter.Eq("email", normalizedEmail),
            Builders<BsonDocument>.Update
                .Set("passwordHash", passwordHash)
                .Set("updatedAt", now),
            cancellationToken: cancellationToken);
        if (userUpdate.MatchedCount == 0)
        {
            return false;
        }

        await resetRequests.UpdateOneAsync(
            Builders<BsonDocument>.Filter.Eq("_id", request["_id"]),
            Builders<BsonDocument>.Update
                .Set("usedAt", now)
                .Set("status", "used"),
            cancellationToken: cancellationToken);
        return true;
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

    private static string CreateResetToken()
    {
        Span<byte> bytes = stackalloc byte[32];
        RandomNumberGenerator.Fill(bytes);
        return Base64Url.Encode(bytes);
    }

    private static string HashResetToken(string token)
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes(token);
        return Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    }

    private string BuildResetUrl(string email, string token, string requestBaseUrl)
    {
        var baseUrl = !string.IsNullOrWhiteSpace(_options.AppBaseUrl)
            ? _options.AppBaseUrl
            : requestBaseUrl;
        baseUrl = string.IsNullOrWhiteSpace(baseUrl) ? string.Empty : baseUrl.TrimEnd('/');
        var path = $"/login/reset-password?email={Uri.EscapeDataString(email)}&token={Uri.EscapeDataString(token)}";
        return string.IsNullOrWhiteSpace(baseUrl) ? path : $"{baseUrl}{path}";
    }
}
