using System.Net.Mail;
using MongoDB.Bson;
using MongoDB.Driver;

namespace BatteryPassWeb.Services;

public enum FirstTeamAccountOutcome
{
    /// <summary>The setting is empty: nothing to do.</summary>
    NotSet,

    /// <summary>The setting holds something that is not one e-mail address.</summary>
    NotAnAddress,

    /// <summary>The database has a team account already: the setting does nothing.</summary>
    TeamAccountExists,

    /// <summary>An account with that address exists but is not on the team: it is left as it is.</summary>
    AddressInUse,

    /// <summary>This start made the account.</summary>
    Created,

    /// <summary>The app has no database connection.</summary>
    NoDatabase,
}

public sealed record FirstTeamAccountResult(FirstTeamAccountOutcome Outcome, string Email)
{
    /// <summary>
    /// The one line the start-up log says about the step, or null when there is nothing to say. Only the address, never a
    /// password, a token or a link.
    /// </summary>
    public string? LogLine => Outcome switch
    {
        FirstTeamAccountOutcome.Created =>
            $"The first team account was created for {Email}. It has no password: the person uses \"Forgot password\" on the sign-in page.",
        FirstTeamAccountOutcome.TeamAccountExists => $"{FirstTeamAccount.Setting} is ignored: a team account exists.",
        FirstTeamAccountOutcome.NotAnAddress => $"{FirstTeamAccount.Setting} is ignored: it is not one e-mail address.",
        FirstTeamAccountOutcome.AddressInUse =>
            $"{FirstTeamAccount.Setting} is ignored: an account with {Email} exists that is not on the team, and a setting never changes an account's role. Use another address, or have that account deleted first.",
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

    public async Task<FirstTeamAccountResult> RunAsync(string? setting, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(setting))
        {
            return new(FirstTeamAccountOutcome.NotSet, string.Empty);
        }

        var email = AddressOrNull(setting);
        if (email is null)
        {
            return new(FirstTeamAccountOutcome.NotAnAddress, string.Empty);
        }

        if (mongoContext.Database is not { } database)
        {
            return new(FirstTeamAccountOutcome.NoDatabase, email);
        }

        var users = database.GetCollection<BsonDocument>("users");
        var teamAccounts = Builders<BsonDocument>.Filter.AnyEq("roles", AccessControlService.RoleAdmin);
        var ignoreCase = new CountOptions { Collation = ClusterRepository.IgnoreCase, Limit = 1 };
        if (await users.CountDocumentsAsync(teamAccounts, ignoreCase, cancellationToken) > 0)
        {
            return new(FirstTeamAccountOutcome.TeamAccountExists, email);
        }

        if (await ClusterRepository.FindUserAsync(database, email, cancellationToken) is { } existing)
        {
            // An instance that started a moment earlier may have made the team account since the look above.
            var onTheTeam = existing.TryGetValue("roles", out var roles) && roles is BsonArray list
                && list.Any(role => string.Equals(role.ToString(), AccessControlService.RoleAdmin, StringComparison.OrdinalIgnoreCase));
            return new(onTheTeam ? FirstTeamAccountOutcome.TeamAccountExists : FirstTeamAccountOutcome.AddressInUse, email);
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
                // Another instance made it between the look above and this write.
                return new(FirstTeamAccountOutcome.TeamAccountExists, email);
            }
        }
        catch (MongoWriteException exception) when (exception.WriteError.Category == ServerErrorCategory.DuplicateKey)
        {
            return new(FirstTeamAccountOutcome.TeamAccountExists, email);
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

    /// <summary>The setting as one plain address in the app's form (outer spaces and capitals removed), or null.</summary>
    private static string? AddressOrNull(string setting)
    {
        var typed = setting.Trim();
        if (typed.Length == 0 || typed.Any(char.IsWhiteSpace) || typed.Contains(',') || typed.Contains(';'))
        {
            return null;
        }

        return MailAddress.TryCreate(typed, out var parsed) && parsed.Address.Equals(typed, StringComparison.Ordinal) && parsed.Host.Contains('.')
            ? ClusterRepository.NormalizeEmail(typed)
            : null;
    }
}
