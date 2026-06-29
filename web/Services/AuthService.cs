using System.Security.Claims;
using System.Security.Cryptography;
using BatteryPassWeb.Configuration;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;

namespace BatteryPassWeb.Services;

public enum LoginAuthenticationStatus
{
    Invalid,
    Authenticated,
    RequiresTemporaryPasswordChange
}

public sealed class LoginAuthenticationResult
{
    public LoginAuthenticationStatus Status { get; init; }
    public ClaimsPrincipal? Principal { get; init; }
    public string Email { get; init; } = string.Empty;

    public static LoginAuthenticationResult Invalid() => new()
    {
        Status = LoginAuthenticationStatus.Invalid
    };

    public static LoginAuthenticationResult Authenticated(ClaimsPrincipal principal) => new()
    {
        Status = LoginAuthenticationStatus.Authenticated,
        Principal = principal,
        Email = AccessControlService.CurrentEmail(principal)
    };

    public static LoginAuthenticationResult RequiresTemporaryPasswordChange(string email) => new()
    {
        Status = LoginAuthenticationStatus.RequiresTemporaryPasswordChange,
        Email = email
    };
}

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
        var result = await AuthenticateLoginAsync(email, password, cancellationToken);
        return result.Status == LoginAuthenticationStatus.Authenticated ? result.Principal : null;
    }

    public async Task<LoginAuthenticationResult> AuthenticateLoginAsync(string email, string password, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            return LoginAuthenticationResult.Invalid();
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

                        return LoginAuthenticationResult.Authenticated(BuildPrincipal(normalizedEmail, BsonHelpers.GetString(user, "name"), roles));
                    }

                    if (await IsActiveTemporaryPasswordAsync(normalizedEmail, password, cancellationToken))
                    {
                        return LoginAuthenticationResult.RequiresTemporaryPasswordChange(normalizedEmail.Trim().ToLowerInvariant());
                    }

                    return LoginAuthenticationResult.Invalid();
                }
            }
        }
        catch
        {
            // Mongo unavailable fallback handled below.
        }

        if (CanUseDemoAdminFallback()
            && normalizedEmail.Equals(_options.DemoAdminEmail, StringComparison.OrdinalIgnoreCase)
            && password == _options.DemoAdminPassword)
        {
            return LoginAuthenticationResult.Authenticated(BuildPrincipal(_options.DemoAdminEmail, "Demo Administrator", [AccessControlService.RoleAdmin]));
        }

        return LoginAuthenticationResult.Invalid();
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

        if (CanUseDemoAdminFallback()
            && normalizedEmail.Equals(_options.DemoAdminEmail, StringComparison.OrdinalIgnoreCase))
        {
            return BuildPrincipal(_options.DemoAdminEmail, "Demo Administrator", [AccessControlService.RoleAdmin]);
        }

        return null;
    }

    private bool CanUseDemoAdminFallback() =>
        _options.EnableDemoAdminFallback
        && !string.IsNullOrWhiteSpace(_options.DemoAdminEmail)
        && !string.IsNullOrWhiteSpace(_options.DemoAdminPassword);

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

        var temporaryPassword = string.Empty;
        if (user != null)
        {
            temporaryPassword = CreateTemporaryPassword();
            request["temporaryPasswordHash"] = BCrypt.Net.BCrypt.HashPassword(temporaryPassword);
        }

        await resetRequests.InsertOneAsync(request, cancellationToken: cancellationToken);

        if (user == null || string.IsNullOrWhiteSpace(temporaryPassword))
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
            await _emailSender.SendTemporaryPasswordAsync(normalizedEmail, temporaryPassword, cancellationToken);
            await resetRequests.UpdateOneAsync(
                Builders<BsonDocument>.Filter.Eq("_id", request["_id"]),
                Builders<BsonDocument>.Update
                    .Set("status", "temporary-password-sent")
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

    public async Task<bool> ConsumeTemporaryPasswordAsync(
        string email,
        string temporaryPassword,
        string newPassword,
        CancellationToken cancellationToken = default)
    {
        if (_mongoContext.Database == null
            || string.IsNullOrWhiteSpace(email)
            || string.IsNullOrWhiteSpace(temporaryPassword)
            || string.IsNullOrWhiteSpace(newPassword)
            || newPassword.Length < 8)
        {
            return false;
        }

        var normalizedEmail = email.Trim().ToLowerInvariant();
        var now = DateTimeOffset.UtcNow.ToString("O");
        var request = await FindActiveTemporaryPasswordRequestAsync(normalizedEmail, cancellationToken);
        if (request == null
            || !BCrypt.Net.BCrypt.Verify(temporaryPassword.Trim(), BsonHelpers.GetString(request, "temporaryPasswordHash")))
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

        await _mongoContext.Database.GetCollection<BsonDocument>("passwordResetRequests").UpdateOneAsync(
            Builders<BsonDocument>.Filter.Eq("_id", request["_id"]),
            Builders<BsonDocument>.Update
                .Set("usedAt", now)
                .Set("status", "used"),
            cancellationToken: cancellationToken);
        return true;
    }

    private async Task<bool> IsActiveTemporaryPasswordAsync(string email, string password, CancellationToken cancellationToken)
    {
        var request = await FindActiveTemporaryPasswordRequestAsync(email.Trim().ToLowerInvariant(), cancellationToken);
        return request != null
            && BCrypt.Net.BCrypt.Verify(password, BsonHelpers.GetString(request, "temporaryPasswordHash"));
    }

    private async Task<BsonDocument?> FindActiveTemporaryPasswordRequestAsync(string normalizedEmail, CancellationToken cancellationToken)
    {
        if (_mongoContext.Database == null || string.IsNullOrWhiteSpace(normalizedEmail))
        {
            return null;
        }

        var now = DateTimeOffset.UtcNow.ToString("O");
        var filter = Builders<BsonDocument>.Filter.And(
            Builders<BsonDocument>.Filter.Eq("email", normalizedEmail),
            Builders<BsonDocument>.Filter.Eq("status", "temporary-password-sent"),
            Builders<BsonDocument>.Filter.Eq("usedAt", BsonNull.Value),
            Builders<BsonDocument>.Filter.Gt("expiresAt", now));

        return await _mongoContext.Database.GetCollection<BsonDocument>("passwordResetRequests")
            .Find(filter)
            .SortByDescending(document => document["requestedAt"])
            .FirstOrDefaultAsync(cancellationToken);
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

    private static string CreateTemporaryPassword()
    {
        const string upper = "ABCDEFGHJKLMNPQRSTUVWXYZ";
        const string lower = "abcdefghijkmnopqrstuvwxyz";
        const string digits = "23456789";
        const string symbols = "!@$?_-";
        const string all = upper + lower + digits + symbols;
        var characters = new List<char>
        {
            RandomCharacter(upper),
            RandomCharacter(lower),
            RandomCharacter(digits),
            RandomCharacter(symbols)
        };

        while (characters.Count < 16)
        {
            characters.Add(RandomCharacter(all));
        }

        for (var index = characters.Count - 1; index > 0; index--)
        {
            var swapIndex = RandomNumberGenerator.GetInt32(index + 1);
            (characters[index], characters[swapIndex]) = (characters[swapIndex], characters[index]);
        }

        return new string(characters.ToArray());
    }

    private static char RandomCharacter(string alphabet) => alphabet[RandomNumberGenerator.GetInt32(alphabet.Length)];
}
