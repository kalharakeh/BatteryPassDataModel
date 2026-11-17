using MongoDB.Bson;
using MongoDB.Driver;

namespace BatteryPassWeb.Services;

public enum FirstTeamAccountOutcome
{
    /// <summary>The setting is empty: nothing to do.</summary>
    NotSet,

    /// <summary>The setting holds something that "Forgot password" would not take as one e-mail address.</summary>
    NotAnAddress,

    /// <summary>The database has a team account already: the setting does nothing.</summary>
    TeamAccountExists,

    /// <summary>An account with that address exists but is not on the team: it is left as it is.</summary>
    AddressInUse,

    /// <summary>This start made the account.</summary>
    Created,

    /// <summary>The app has no database connection.</summary>
    NoDatabase,

    /// <summary>
    /// The account this setting made earlier is still there but no longer on the team, and no team account is left: it is
    /// left as it is (a setting never gives team access back), and the log says what to do.
    /// </summary>
    FirstAccountNoLongerOnTheTeam,
}

public sealed record FirstTeamAccountResult(FirstTeamAccountOutcome Outcome, string Email)
{
    /// <summary>
    /// The one line the start-up log says about the step, or null when there is nothing to say. Only the address, never a
    /// password, a token or a link; for a refused setting not even the setting, which may hold anything.
    /// </summary>
    public string? LogLine => Outcome switch
    {
        FirstTeamAccountOutcome.Created =>
            $"The first team account was created for {Email}. It has no password: the person uses \"Forgot password\" on the sign-in page.",
        FirstTeamAccountOutcome.TeamAccountExists => $"{FirstTeamAccount.Setting} is ignored: a team account exists.",
        FirstTeamAccountOutcome.NotAnAddress =>
            $"{FirstTeamAccount.Setting} is ignored: it is not one e-mail address that \"Forgot password\" on the sign-in page takes. {EmailAddressRule.Words}",
        FirstTeamAccountOutcome.AddressInUse =>
            $"{FirstTeamAccount.Setting} is ignored: an account with {Email} exists that is not on the team, and a setting never changes an account's role. Use another address, or have that account deleted first.",
        FirstTeamAccountOutcome.FirstAccountNoLongerOnTheTeam =>
            $"{FirstTeamAccount.Setting} is ignored: the account it made earlier, {Email}, is no longer on the team and no team account is left. "
            + $"A setting never gives team access back. Delete that account in the database (collection users, _id \"{FirstTeamAccount.AccountId}\") and start the app again: the account is then made anew, with no password, for the address in the setting.",
        _ => null,
    };
}

/// <summary>
/// The first team account on a database that has none (the manager's decision 78). In Production the fallback admin can't
/// be on, so a new live database has nobody who could sign in. With FIRST_TEAM_ACCOUNT_EMAIL set and no team account in
/// the database, the app makes one for that address with no password at all; the person asks for a temporary password with
/// "Forgot password" on the sign-in page. A team account that exists makes the setting do nothing.
/// </summary>
public sealed class FirstTeamAccount(MongoContext mongoContext, ApplicationAuditService audit)
{
    public const string Setting = "FIRST_TEAM_ACCOUNT_EMAIL";

    /// <summary>
    /// The account's fixed id: the database allows one document with it, so instances that start at the same moment can't
    /// make two accounts, whatever they each saw a moment before.
    /// </summary>
    public const string AccountId = "first-team-account";

    /// <summary>
    /// A team account exactly as the app treats one (PL round two, point 3): not a demo account, and "admin" in its list of
    /// roles, written just so. Sign-in gives such an account the team's rights and no other (see the team pages' own count of
    /// team accounts); a role in other capitals, a demo account or roles that are not a list never act as the team.
    /// </summary>
    public static bool IsTeamAccount(BsonDocument user) =>
        !ClusterRepository.IsDemo(user)
        && AccessControlService.RolesOf(user).Contains(AccessControlService.RoleAdmin, StringComparer.Ordinal);

    public async Task<FirstTeamAccountResult> RunAsync(string? setting, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(setting))
        {
            return new(FirstTeamAccountOutcome.NotSet, string.Empty);
        }

        // The same rule as "Forgot password": an address it would refuse makes an account nobody could claim.
        if (EmailAddressRule.Problem(setting) is not null)
        {
            return new(FirstTeamAccountOutcome.NotAnAddress, string.Empty);
        }

        var email = ClusterRepository.NormalizeEmail(setting);
        if (mongoContext.Database is not { } database)
        {
            return new(FirstTeamAccountOutcome.NoDatabase, email);
        }

        var users = database.GetCollection<BsonDocument>("users");
        var candidates = await users
            .Find(Builders<BsonDocument>.Filter.AnyEq("roles", AccessControlService.RoleAdmin))
            .ToListAsync(cancellationToken);
        if (candidates.Any(IsTeamAccount))
        {
            return new(FirstTeamAccountOutcome.TeamAccountExists, email);
        }

        if (await FirstAccountAsync(users, cancellationToken) is { } earlier)
        {
            return earlier;
        }

        if (await ClusterRepository.FindUserAsync(database, email, cancellationToken) is { } existing)
        {
            // An instance that started a moment earlier may have made the team account since the look above.
            return new(IsTeamAccount(existing) ? FirstTeamAccountOutcome.TeamAccountExists : FirstTeamAccountOutcome.AddressInUse, email);
        }

        var now = DateTime.UtcNow.ToString("O");
        try
        {
            var made = await users.UpdateOneAsync(
                Builders<BsonDocument>.Filter.Eq("_id", AccountId),
                Builders<BsonDocument>.Update
                    .SetOnInsert("email", email)
                    .SetOnInsert("name", email)
                    .SetOnInsert("roles", new BsonArray { AccessControlService.RoleAdmin })
                    .SetOnInsert("createdAt", now)
                    .SetOnInsert("updatedAt", now),
                new UpdateOptions { IsUpsert = true },
                cancellationToken);
            if (made.UpsertedId is null)
            {
                return await FirstAccountAsync(users, cancellationToken) ?? new(FirstTeamAccountOutcome.TeamAccountExists, email);
            }
        }
        catch (MongoWriteException exception) when (exception.WriteError.Category == ServerErrorCategory.DuplicateKey)
        {
            return await FirstAccountAsync(users, cancellationToken) ?? new(FirstTeamAccountOutcome.TeamAccountExists, email);
        }

        await audit.AppendApplicationAuditEventAsync(
            "account.firstTeamAccountCreated",
            "start-up",
            "system",
            "start-up",
            $"The first team account was created for {email}. It has no password.",
            new BsonDocument { ["email"] = email, ["setting"] = Setting },
            "account",
            email,
            cancellationToken: cancellationToken);
        return new(FirstTeamAccountOutcome.Created, email);
    }

    /// <summary>
    /// What the account this setting made earlier says, if it is still there: on the team (another instance made it a moment
    /// ago), or no longer on the team. Null when there is no such account.
    /// </summary>
    private static async Task<FirstTeamAccountResult?> FirstAccountAsync(IMongoCollection<BsonDocument> users, CancellationToken cancellationToken)
    {
        var record = await users.Find(Builders<BsonDocument>.Filter.Eq("_id", AccountId)).FirstOrDefaultAsync(cancellationToken);
        if (record is null)
        {
            return null;
        }

        var address = record.GetValue("email", string.Empty).ToString() ?? string.Empty;
        return IsTeamAccount(record)
            ? new(FirstTeamAccountOutcome.TeamAccountExists, address)
            : new(FirstTeamAccountOutcome.FirstAccountNoLongerOnTheTeam, address);
    }
}
