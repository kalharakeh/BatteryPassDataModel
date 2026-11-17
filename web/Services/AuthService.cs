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
    RequiresTemporaryPasswordChange,

    /// <summary>The address is locked after too many wrong passwords (finding W4); said as a wrong password plus "try again later".</summary>
    Locked
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

    public static LoginAuthenticationResult Locked() => new()
    {
        Status = LoginAuthenticationStatus.Locked
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

/// <summary>What the next request does with an open session (finding S02): goes on, goes on with fresh rights, or is signed out.</summary>
public sealed record SessionCheck(bool SignOut, ClaimsPrincipal? Fresh)
{
    public static readonly SessionCheck Unchanged = new(false, null);
    public static readonly SessionCheck Ended = new(true, null);
}

public sealed class AuthService
{
    /// <summary>A short mark of the password a session signed in with; another password ends the session (finding S02).</summary>
    public const string PasswordStampClaim = "bp:password-stamp";

    /// <summary>Marks a session of the fallback admin, which has no account in the database.</summary>
    public const string FallbackClaim = "bp:fallback";

    public const int MinimumPasswordLength = 12;
    public const string PasswordRuleLine = "At least 12 characters.";
    public const string PasswordTooShort = "Use at least 12 characters for the new password.";
    public const string CurrentPasswordNeeded = "Type your current password to change your password or e-mail.";
    public const string CurrentPasswordWrong = "That isn't your current password.";

    /// <summary>Finding S06: why a typed new password can't be used, or null.</summary>
    public static string? NewPasswordProblem(string? password) =>
        string.IsNullOrEmpty(password) || password.Length < MinimumPasswordLength ? PasswordTooShort : null;

    /// <summary>
    /// Whether <paramref name="password"/> is this account's current password (the fallback admin's included). The Account
    /// page's check counts and is limited for its own session's sender only (H1 round two): what other senders did never
    /// blocks it.
    /// </summary>
    public async Task<bool> IsCurrentPasswordAsync(string email, string password, string? sender, CancellationToken cancellationToken = default) =>
        (await AuthenticateCoreAsync(email, password, sender, accountPage: true, cancellationToken)).Status == LoginAuthenticationStatus.Authenticated;

    public Task<bool> IsCurrentPasswordAsync(string email, string password, CancellationToken cancellationToken = default) =>
        IsCurrentPasswordAsync(email, password, sender: null, cancellationToken);

    private readonly MongoContext _mongoContext;
    private readonly BatteryPassOptions _options;
    private readonly IEmailSender _emailSender;

    private readonly BatteryPassWeb.Dpp.Demo.DemoDataSwitch _demoData;
    private readonly TimeProvider _clock;

    public AuthService(MongoContext mongoContext, IOptions<BatteryPassOptions> options, IEmailSender emailSender, BatteryPassWeb.Dpp.Demo.DemoDataSwitch? demoData = null, TimeProvider? clock = null)
    {
        _mongoContext = mongoContext;
        _options = options.Value;
        _emailSender = emailSender;
        _demoData = demoData ?? BatteryPassWeb.Dpp.Demo.DemoDataSwitch.On;
        _clock = clock ?? TimeProvider.System;
    }

    /// <summary>A hash nobody has the password of: an address with no account costs the same time as one with an account (finding W4).</summary>
    private static readonly string NoAccountHash = BCrypt.Net.BCrypt.HashPassword(Guid.NewGuid().ToString("N"));

    /// <summary>A demo account (marked by the demo data) when this site's demo data is switched off: it can't sign in (finding R03).</summary>
    private bool IsSwitchedOffDemoAccount(BsonDocument user) =>
        !_demoData.Enabled && user.GetValue(BatteryPassWeb.Dpp.Demo.DemoDataService.AccountMark, false) is { IsBoolean: true, AsBoolean: true };

    public Task<ClaimsPrincipal?> AuthenticateAsync(string email, string password, CancellationToken cancellationToken = default) =>
        AuthenticateAsync(email, password, sender: null, cancellationToken);

    /// <param name="sender">The client address, as the rate limiter sees it (H1 round two): the lock counts per account and sender.</param>
    public async Task<ClaimsPrincipal?> AuthenticateAsync(string email, string password, string? sender, CancellationToken cancellationToken = default)
    {
        var result = await AuthenticateLoginAsync(email, password, sender, cancellationToken);
        return result.Status == LoginAuthenticationStatus.Authenticated ? result.Principal : null;
    }

    public Task<LoginAuthenticationResult> AuthenticateLoginAsync(string email, string password, CancellationToken cancellationToken = default) =>
        AuthenticateLoginAsync(email, password, sender: null, cancellationToken);

    /// <param name="sender">The client address, as the rate limiter sees it (H1 round two): the lock counts per account and sender.</param>
    public Task<LoginAuthenticationResult> AuthenticateLoginAsync(string email, string password, string? sender, CancellationToken cancellationToken = default) =>
        AuthenticateCoreAsync(email, password, sender, accountPage: false, cancellationToken);

    /// <summary>
    /// The one password check: the sign-in page, the sign-in API and the Account page's current password. H1 round two: a
    /// temporary password is looked at before any lock and is never refused by one; then this sender's own lock; then,
    /// except for the Account page (an open session) and a sender that signed in to the account before, the lock of the
    /// account as a whole; then the password. A refusal costs one password check, as a wrong password does.
    /// </summary>
    private async Task<LoginAuthenticationResult> AuthenticateCoreAsync(string email, string password, string? sender, bool accountPage, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            return LoginAuthenticationResult.Invalid();
        }

        // Finding W6: an address that is too long or holds a line break or other control character is no sign-in.
        if (!EmailAddressRule.IsClean(email))
        {
            return LoginAuthenticationResult.Invalid();
        }

        var normalizedEmail = ClusterRepository.NormalizeEmail(email);
        BsonDocument? user = null;
        var database = _mongoContext.Database;
        try
        {
            if (database != null)
            {
                user = await ClusterRepository.FindUserAsync(database, email, cancellationToken);
            }
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            database = null; // the database is out of reach: only the fallback admin can sign in, as before
        }

        if (user != null && IsSwitchedOffDemoAccount(user))
        {
            _ = BCrypt.Net.BCrypt.Verify(password, NoAccountHash);
            return LoginAuthenticationResult.Invalid();
        }

        // "Forgot password": the temporary password is the owner's way back, so no lock ever stands in front of it.
        if (user != null && await IsActiveTemporaryPasswordAsync(normalizedEmail, password, cancellationToken))
        {
            return LoginAuthenticationResult.RequiresTemporaryPasswordChange(normalizedEmail);
        }

        var account = SignInAttempts.AccountKey(user, email);
        var from = SignInAttempts.SenderKey(sender);
        var now = _clock.GetUtcNow().UtcDateTime;
        if (database != null && await LockedAsync(database, account, from, now, accountPage, cancellationToken))
        {
            _ = BCrypt.Net.BCrypt.Verify(password, NoAccountHash); // as long as a password check
            return LoginAuthenticationResult.Locked();
        }

        var result = CheckPassword(user, normalizedEmail, password, database != null);
        if (result.Status == LoginAuthenticationStatus.Authenticated)
        {
            result = user is null ? result : LoginAuthenticationResult.Authenticated(await PrincipalForAsync(user, cancellationToken));
            await ChangeCountAsync(() => SignInAttempts.SignedInAsync(database!, account, from, now, cancellationToken), database);
        }
        else
        {
            await ChangeCountAsync(() => SignInAttempts.RecordWrongAsync(database!, account, from, now, cancellationToken), database);
        }

        return result;
    }

    private static async Task<bool> LockedAsync(IMongoDatabase database, string account, string sender, DateTime now, bool accountPage, CancellationToken cancellationToken)
    {
        try
        {
            return await SignInAttempts.IsSenderLockedAsync(database, account, sender, now, cancellationToken)
                || (!accountPage
                    && await SignInAttempts.IsAccountLockedAsync(database, account, now, cancellationToken)
                    && !await SignInAttempts.IsKnownSenderAsync(database, account, sender, cancellationToken));
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return false; // a database error never keeps anyone out
        }
    }

    /// <summary>Writes the count; a database error here never changes the answer of the sign-in.</summary>
    private static async Task ChangeCountAsync(Func<Task> change, IMongoDatabase? database)
    {
        if (database == null)
        {
            return;
        }

        try
        {
            await change();
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
        }
    }

    /// <summary>The account's own password, or the fallback admin's; one password check whether the account exists or not.</summary>
    private LoginAuthenticationResult CheckPassword(BsonDocument? user, string normalizedEmail, string password, bool databaseReached)
    {
        if (user != null)
        {
            var passwordHash = BsonHelpers.GetString(user, "passwordHash");
            return !string.IsNullOrWhiteSpace(passwordHash) && BCrypt.Net.BCrypt.Verify(password, passwordHash)
                ? LoginAuthenticationResult.Authenticated(new ClaimsPrincipal())
                : LoginAuthenticationResult.Invalid();
        }

        if (databaseReached)
        {
            _ = BCrypt.Net.BCrypt.Verify(password, NoAccountHash);
        }

        return CanUseDemoAdminFallback()
            && normalizedEmail.Equals(_options.DemoAdminEmail, StringComparison.OrdinalIgnoreCase)
            && password == _options.DemoAdminPassword
                ? LoginAuthenticationResult.Authenticated(FallbackPrincipal())
                : LoginAuthenticationResult.Invalid();
    }

    /// <summary>Clears every count and lock of the account with this address (a new password was set); its known senders stay.</summary>
    public static async Task ClearSignInCountsAsync(IMongoDatabase database, string email, CancellationToken cancellationToken = default)
    {
        try
        {
            var user = await ClusterRepository.FindUserAsync(database, email, cancellationToken);
            await SignInAttempts.ClearAccountAsync(database, SignInAttempts.AccountKey(user, email), cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
        }
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
            var user = await ClusterRepository.FindUserAsync(_mongoContext.Database, email, cancellationToken);
            if (user != null)
            {
                return await PrincipalForAsync(user, cancellationToken);
            }
        }

        if (CanUseDemoAdminFallback()
            && normalizedEmail.Equals(_options.DemoAdminEmail, StringComparison.OrdinalIgnoreCase))
        {
            return FallbackPrincipal();
        }

        return null;
    }

    /// <summary>
    /// Finding S02: the open session <paramref name="session"/> against its account, on each request. A deleted account, a
    /// changed password or a fallback session whose address now has an account ends it; changed rights refresh it. A
    /// session from before this check (no password mark) counts as unchanged and gets the mark. A database error changes
    /// nothing.
    /// </summary>
    public async Task<SessionCheck> CheckSessionAsync(ClaimsPrincipal session, CancellationToken cancellationToken = default)
    {
        var email = AccessControlService.CurrentEmail(session);
        if (_mongoContext.Database == null || string.IsNullOrWhiteSpace(email))
        {
            return SessionCheck.Unchanged;
        }

        try
        {
            return await CheckSessionAgainstAsync(session, email, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return SessionCheck.Unchanged;
        }
    }

    private async Task<SessionCheck> CheckSessionAgainstAsync(ClaimsPrincipal session, string email, CancellationToken cancellationToken)
    {
        var user = await ClusterRepository.FindUserAsync(_mongoContext.Database!, email, cancellationToken);

        var isFallbackAddress = email.Trim().Equals(_options.DemoAdminEmail?.Trim(), StringComparison.OrdinalIgnoreCase);
        var fallbackSession = session.HasClaim(claim => claim.Type == FallbackClaim)
            || (isFallbackAddress && session.FindFirst(PasswordStampClaim) is null && session.Identity?.Name == FallbackName);
        if (user is null)
        {
            return fallbackSession && isFallbackAddress && CanUseDemoAdminFallback() ? SessionCheck.Unchanged : SessionCheck.Ended;
        }

        if (IsSwitchedOffDemoAccount(user))
        {
            return SessionCheck.Ended;
        }

        if (fallbackSession)
        {
            // An account in the database with the fallback's address always wins over the fallback (finding R21).
            return SessionCheck.Ended;
        }

        var stamp = session.FindFirst(PasswordStampClaim)?.Value;
        if (stamp is not null && stamp != PasswordStamp(user))
        {
            return SessionCheck.Ended;
        }

        var fresh = await PrincipalForAsync(user, cancellationToken);
        return stamp is not null && SameRights(session, fresh) ? SessionCheck.Unchanged : new SessionCheck(false, fresh);
    }

    private static bool SameRights(ClaimsPrincipal session, ClaimsPrincipal fresh)
    {
        static string[] Roles(ClaimsPrincipal principal) =>
            principal.FindAll(ClaimTypes.Role).Select(claim => claim.Value).Order(StringComparer.Ordinal).ToArray();

        return Roles(session).SequenceEqual(Roles(fresh))
            && session.Identity?.Name == fresh.Identity?.Name
            && AccessControlService.CurrentEmail(session) == AccessControlService.CurrentEmail(fresh);
    }

    /// <summary>A short mark of the stored password hash, kept in the (encrypted) session cookie.</summary>
    private static string PasswordStamp(BsonDocument user) =>
        Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(BsonHelpers.GetString(user, "passwordHash"))))[..16];

    private const string FallbackName = "Demo Administrator";

    private ClaimsPrincipal FallbackPrincipal()
    {
        var principal = BuildPrincipal(_options.DemoAdminEmail, FallbackName, [AccessControlService.RoleAdmin]);
        ((ClaimsIdentity)principal.Identity!).AddClaim(new Claim(FallbackClaim, "1"));
        return principal;
    }

    private async Task<ClaimsPrincipal> PrincipalForAsync(BsonDocument user, CancellationToken cancellationToken)
    {
        var roles = await RolesWithClusterAdminAsync(user, cancellationToken);
        var principal = BuildPrincipal(BsonHelpers.GetString(user, "email"), BsonHelpers.GetString(user, "name"), roles);
        ((ClaimsIdentity)principal.Identity!).AddClaim(new Claim(PasswordStampClaim, PasswordStamp(user)));
        return principal;
    }

    /// <summary>The account's roles, with "clusterAdmin" when it administers a cluster (its memberships, whatever their capitals).</summary>
    private async Task<List<string>> RolesWithClusterAdminAsync(BsonDocument user, CancellationToken cancellationToken)
    {
        var roles = ExtractRoles(user).ToList();
        if (ClusterRepository.IsDemo(user))
        {
            // A demo account is never the team (finding R03): the team's pages act on every battery and every account.
            roles.RemoveAll(role => role.Equals(AccessControlService.RoleAdmin, StringComparison.OrdinalIgnoreCase));
            if (roles.Count == 0)
            {
                roles.Add(AccessControlService.RoleNormalUser);
            }
        }

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
        if (_mongoContext.Database == null || string.IsNullOrWhiteSpace(email) || !EmailAddressRule.IsClean(email))
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
            || !EmailAddressRule.IsClean(email)
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
        // A reset clears every count and lock of the account (finding W4, H1 round two).
        await ChangeCountAsync(() => SignInAttempts.ClearAccountAsync(_mongoContext.Database!, SignInAttempts.AccountKey(account, normalizedEmail), CancellationToken.None), _mongoContext.Database);
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
