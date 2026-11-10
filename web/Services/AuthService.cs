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
    public const int MinimumPasswordLength = 12;
    public const string PasswordRuleLine = "At least 12 characters.";
    public const string PasswordTooShort = "Use at least 12 characters for the new password.";
    public const string CurrentPasswordNeeded = "Type your current password to change your password or e-mail.";
    public const string CurrentPasswordWrong = "That isn't your current password.";

    /// <summary>Finding S06: why a typed new password can't be used, or null.</summary>
    public static string? NewPasswordProblem(string? password) =>
        string.IsNullOrEmpty(password) || password.Length < MinimumPasswordLength ? PasswordTooShort : null;

    /// <summary>Whether <paramref name="password"/> is this account's current password (the fallback admin's included).</summary>
    public async Task<bool> IsCurrentPasswordAsync(string email, string password, CancellationToken cancellationToken = default) =>
        (await AuthenticateLoginAsync(email, password, cancellationToken)).Status == LoginAuthenticationStatus.Authenticated;

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

        // The same address whatever its capitals and outer spaces (finding U28).
        var normalizedEmail = ClusterRepository.NormalizeEmail(email);

        try
        {
            if (_mongoContext.Database != null)
            {
                var user = await ClusterRepository.FindUserAsync(_mongoContext.Database, normalizedEmail, cancellationToken);

                if (user != null)
                {
                    var passwordHash = BsonHelpers.GetString(user, "passwordHash");
                    if (!string.IsNullOrWhiteSpace(passwordHash) && BCrypt.Net.BCrypt.Verify(password, passwordHash))
                    {
                        var roles = await RolesWithClusterAdminAsync(user, cancellationToken);
                        return LoginAuthenticationResult.Authenticated(BuildPrincipal(BsonHelpers.GetString(user, "email"), BsonHelpers.GetString(user, "name"), roles));
                    }

                    if (await IsActiveTemporaryPasswordAsync(normalizedEmail, password, cancellationToken))
                    {
                        return LoginAuthenticationResult.RequiresTemporaryPasswordChange(normalizedEmail);
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

        var normalizedEmail = ClusterRepository.NormalizeEmail(email);
        if (_mongoContext.Database != null)
        {
            var user = await ClusterRepository.FindUserAsync(_mongoContext.Database, normalizedEmail, cancellationToken);
            if (user != null)
            {
                var roles = await RolesWithClusterAdminAsync(user, cancellationToken);
                return BuildPrincipal(BsonHelpers.GetString(user, "email"), BsonHelpers.GetString(user, "name"), roles);
            }
        }

        if (CanUseDemoAdminFallback()
            && normalizedEmail.Equals(_options.DemoAdminEmail, StringComparison.OrdinalIgnoreCase))
        {
            return BuildPrincipal(_options.DemoAdminEmail, "Demo Administrator", [AccessControlService.RoleAdmin]);
        }

        return null;
    }

    /// <summary>The account's roles, with "clusterAdmin" when it administers a cluster (its memberships, whatever their capitals).</summary>
    private async Task<List<string>> RolesWithClusterAdminAsync(BsonDocument user, CancellationToken cancellationToken)
    {
        var roles = ExtractRoles(user).ToList();
        if (_mongoContext.Database != null && !roles.Contains(AccessControlService.RoleAdmin, StringComparer.OrdinalIgnoreCase))
        {
            var hasClusterAdminMembership = await _mongoContext.Database
                .GetCollection<BsonDocument>("clusterMemberships")
                .Find(
                    Builders<BsonDocument>.Filter.And(
                        ClusterRepository.MembershipsOf(BsonHelpers.GetString(user, "email")),
                        Builders<BsonDocument>.Filter.Eq("role", AccessControlService.RoleClusterAdmin)),
                    new FindOptions { Collation = ClusterRepository.IgnoreCase })
                .AnyAsync(cancellationToken);
            if (hasClusterAdminMembership)
            {
                roles.Add(AccessControlService.RoleClusterAdmin);
            }
        }

        return roles;
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
        var user = await ClusterRepository.FindUserAsync(_mongoContext.Database, normalizedEmail, cancellationToken);
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
            || NewPasswordProblem(newPassword) is not null)
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
        if (await ClusterRepository.FindUserAsync(_mongoContext.Database, normalizedEmail, cancellationToken) is not { } account)
        {
            return false;
        }

        var userUpdate = await users.UpdateOneAsync(
            Builders<BsonDocument>.Filter.Eq("_id", account["_id"]),
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
