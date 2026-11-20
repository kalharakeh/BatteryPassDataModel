using System.Globalization;
using MongoDB.Bson;
using MongoDB.Driver;

namespace BatteryPassWeb.Services;

/// <summary>
/// The sign-in lock (finding W4; H1 round two, the manager's rule): guessing is slowed for the guesser, never for the owner
/// of the account. Wrong passwords are counted per account and per sender (the client address the rate limiter uses):
/// <see cref="MaxWrongPerSender"/> from one sender within <see cref="Window"/> lock that sender out of that account for
/// <see cref="LockFor"/>; nobody else. <see cref="MaxWrongPerAccount"/> from all senders together stop a password alone from
/// signing the account in from a sender that never signed in to it (<see cref="KnownFor"/>); a known sender still gets in.
/// A temporary password from "Forgot password" is never refused because of a lock. The account is the one the look-up
/// found (its id), whatever spelling was typed; with no account, the typed address cleaned of invisible characters, so
/// the lock says nothing about which addresses have an account. Everything is in the database, for every app instance.
/// </summary>
public static class SignInAttempts
{
    public const string Collection = "signInAttempts";
    public const int MaxWrongPerSender = 10;
    public const int MaxWrongPerAccount = 50;
    public static readonly TimeSpan Window = TimeSpan.FromMinutes(15);
    public static readonly TimeSpan LockFor = TimeSpan.FromMinutes(15);
    public static readonly TimeSpan KnownFor = TimeSpan.FromDays(90);

    /// <summary>What the sign-in page says for every refusal: a wrong password, no such account, a lock.</summary>
    public const string Words = "Invalid email or password. Try again later.";

    private static IMongoCollection<BsonDocument> Of(IMongoDatabase database) => database.GetCollection<BsonDocument>(Collection);

    /// <summary>The account a try counts against: the found account's id, else the typed address without invisible characters.</summary>
    public static string AccountKey(BsonDocument? account, string? typed) =>
        account is not null && account.TryGetValue("_id", out var id)
            ? $"id:{id}"
            : $"addr:{new string(ClusterRepository.NormalizeEmail(typed).Where(character => CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.Format).ToArray())}";

    /// <summary>The sender: the client address as the rate limiter sees it (behind the proxy's forwarded header); "unknown" without one.</summary>
    public static string SenderKey(string? sender) => string.IsNullOrWhiteSpace(sender) ? "unknown" : sender.Trim();

    public static async Task EnsureIndexesAsync(IMongoDatabase database, CancellationToken cancellationToken = default)
    {
        await Of(database).Indexes.CreateManyAsync(
        [
            new CreateIndexModel<BsonDocument>(Builders<BsonDocument>.IndexKeys.Ascending("account").Ascending("sender").Ascending("at")),
            // Old records go by themselves; the queries look at the time as well, as the cleaning runs once a minute.
            new CreateIndexModel<BsonDocument>(Builders<BsonDocument>.IndexKeys.Ascending("at"), new CreateIndexOptions { ExpireAfter = Window }),
            new CreateIndexModel<BsonDocument>(Builders<BsonDocument>.IndexKeys.Ascending("lockedUntil"), new CreateIndexOptions { ExpireAfter = TimeSpan.Zero }),
            new CreateIndexModel<BsonDocument>(Builders<BsonDocument>.IndexKeys.Ascending("seenAt"), new CreateIndexOptions { ExpireAfter = KnownFor }),
        ], cancellationToken);
    }

    private static string SenderLockId(string account, string sender) => $"lock:{account}|{sender}";

    private static string AccountLockId(string account) => $"wide:{account}";

    private static string KnownId(string account, string sender) => $"known:{account}|{sender}";

    private static Task<bool> LockedAsync(IMongoDatabase database, string id, DateTime now, CancellationToken cancellationToken) =>
        Of(database).Find(Builders<BsonDocument>.Filter.Eq("_id", id) & Builders<BsonDocument>.Filter.Gt("lockedUntil", now)).AnyAsync(cancellationToken);

    /// <summary>Whether this sender is locked out of this account.</summary>
    public static Task<bool> IsSenderLockedAsync(IMongoDatabase database, string account, string sender, DateTime now, CancellationToken cancellationToken = default) =>
        LockedAsync(database, SenderLockId(account, sender), now, cancellationToken);

    /// <summary>Whether the account had too many wrong passwords from all senders together: a new sender's password alone isn't enough.</summary>
    public static Task<bool> IsAccountLockedAsync(IMongoDatabase database, string account, DateTime now, CancellationToken cancellationToken = default) =>
        LockedAsync(database, AccountLockId(account), now, cancellationToken);

    /// <summary>Whether this sender signed in to this account within <see cref="KnownFor"/>.</summary>
    public static Task<bool> IsKnownSenderAsync(IMongoDatabase database, string account, string sender, CancellationToken cancellationToken = default) =>
        Of(database).Find(Builders<BsonDocument>.Filter.Eq("_id", KnownId(account, sender))).AnyAsync(cancellationToken);

    /// <summary>Notes a wrong password from a sender; the tenth from it, or the fiftieth from all, within the window locks.</summary>
    public static async Task RecordWrongAsync(IMongoDatabase database, string account, string sender, DateTime now, CancellationToken cancellationToken = default)
    {
        var attempts = Of(database);
        await attempts.InsertOneAsync(new BsonDocument { ["account"] = account, ["sender"] = sender, ["at"] = now }, cancellationToken: cancellationToken);
        var recent = Builders<BsonDocument>.Filter.Eq("account", account) & Builders<BsonDocument>.Filter.Gt("at", now - Window);
        if (await attempts.CountDocumentsAsync(recent & Builders<BsonDocument>.Filter.Eq("sender", sender), cancellationToken: cancellationToken) >= MaxWrongPerSender)
        {
            await LockAsync(attempts, SenderLockId(account, sender), account, now, cancellationToken);
        }

        if (await attempts.CountDocumentsAsync(recent, cancellationToken: cancellationToken) >= MaxWrongPerAccount)
        {
            await LockAsync(attempts, AccountLockId(account), account, now, cancellationToken);
        }
    }

    private static Task LockAsync(IMongoCollection<BsonDocument> attempts, string id, string account, DateTime now, CancellationToken cancellationToken) =>
        attempts.ReplaceOneAsync(
            Builders<BsonDocument>.Filter.Eq("_id", id),
            new BsonDocument { ["_id"] = id, ["account"] = account, ["lockedUntil"] = now + LockFor },
            new ReplaceOptions { IsUpsert = true },
            cancellationToken);

    /// <summary>After a sign-in: forgets this sender's wrong passwords and its lock, and remembers the sender as known.</summary>
    public static async Task SignedInAsync(IMongoDatabase database, string account, string sender, DateTime now, CancellationToken cancellationToken = default)
    {
        var attempts = Of(database);
        await attempts.DeleteManyAsync(
            (Builders<BsonDocument>.Filter.Eq("account", account) & Builders<BsonDocument>.Filter.Eq("sender", sender))
            | Builders<BsonDocument>.Filter.Eq("_id", SenderLockId(account, sender)),
            cancellationToken);
        await attempts.ReplaceOneAsync(
            Builders<BsonDocument>.Filter.Eq("_id", KnownId(account, sender)),
            new BsonDocument { ["_id"] = KnownId(account, sender), ["knownFor"] = account, ["seenAt"] = now },
            new ReplaceOptions { IsUpsert = true },
            cancellationToken);
    }

    /// <summary>After a new password (a temporary one used, or one the team or a cluster admin set): forgets every count and lock of the account; its known senders stay.</summary>
    public static async Task ClearAccountAsync(IMongoDatabase database, string account, CancellationToken cancellationToken = default) =>
        await Of(database).DeleteManyAsync(Builders<BsonDocument>.Filter.Eq("account", account), cancellationToken);
}
