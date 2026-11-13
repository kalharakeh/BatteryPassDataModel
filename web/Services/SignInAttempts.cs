using MongoDB.Bson;
using MongoDB.Driver;

namespace BatteryPassWeb.Services;

/// <summary>
/// Finding W4, the lockout: 10 wrong passwords for one address within 15 minutes lock that address for 15 minutes, whatever
/// the account or the place the tries come from. It is kept per typed address (lower case, outer spaces off), account or
/// not, so the lock says nothing about which addresses have an account. The count lives in the database, so it holds across
/// restarts and app instances; a sign-in or any new password clears it.
/// </summary>
public static class SignInAttempts
{
    public const string Collection = "signInAttempts";
    public const int MaxWrong = 10;
    public static readonly TimeSpan Window = TimeSpan.FromMinutes(15);
    public static readonly TimeSpan LockFor = TimeSpan.FromMinutes(15);

    /// <summary>What the sign-in page says for a wrong password.</summary>
    public const string WrongWords = "Invalid email or password.";

    /// <summary>What it says when the address is locked: the same, and to try later.</summary>
    public const string LockedWords = "Invalid email or password. Try again later.";

    private const string LockPrefix = "lock:";

    private static IMongoCollection<BsonDocument> Of(IMongoDatabase database) => database.GetCollection<BsonDocument>(Collection);

    public static string Key(string? email) => ClusterRepository.NormalizeEmail(email);

    public static async Task EnsureIndexesAsync(IMongoDatabase database, CancellationToken cancellationToken = default)
    {
        var attempts = Of(database);
        await attempts.Indexes.CreateManyAsync(
        [
            new CreateIndexModel<BsonDocument>(Builders<BsonDocument>.IndexKeys.Ascending("email").Ascending("at")),
            // Old records go by themselves; the queries below look at the time as well, as the cleaning runs once a minute.
            new CreateIndexModel<BsonDocument>(Builders<BsonDocument>.IndexKeys.Ascending("at"), new CreateIndexOptions { ExpireAfter = Window }),
            new CreateIndexModel<BsonDocument>(Builders<BsonDocument>.IndexKeys.Ascending("lockedUntil"), new CreateIndexOptions { ExpireAfter = TimeSpan.Zero }),
        ], cancellationToken);
    }

    /// <summary>Whether the address is locked at <paramref name="now"/>.</summary>
    public static async Task<bool> IsLockedAsync(IMongoDatabase database, string? email, DateTime now, CancellationToken cancellationToken = default)
    {
        var key = Key(email);
        if (key.Length == 0)
        {
            return false;
        }

        return await Of(database)
            .Find(Builders<BsonDocument>.Filter.Eq("_id", LockPrefix + key) & Builders<BsonDocument>.Filter.Gt("lockedUntil", now))
            .AnyAsync(cancellationToken);
    }

    /// <summary>Notes a wrong password; the tenth within the window locks the address.</summary>
    public static async Task RecordWrongAsync(IMongoDatabase database, string? email, DateTime now, CancellationToken cancellationToken = default)
    {
        var key = Key(email);
        if (key.Length == 0)
        {
            return;
        }

        var attempts = Of(database);
        await attempts.InsertOneAsync(new BsonDocument { ["email"] = key, ["at"] = now }, cancellationToken: cancellationToken);
        var recent = await attempts.CountDocumentsAsync(
            Builders<BsonDocument>.Filter.Eq("email", key) & Builders<BsonDocument>.Filter.Gt("at", now - Window),
            cancellationToken: cancellationToken);
        if (recent >= MaxWrong)
        {
            await attempts.ReplaceOneAsync(
                Builders<BsonDocument>.Filter.Eq("_id", LockPrefix + key),
                new BsonDocument { ["_id"] = LockPrefix + key, ["email"] = key, ["lockedUntil"] = now + LockFor },
                new ReplaceOptions { IsUpsert = true },
                cancellationToken);
            await attempts.DeleteManyAsync(Builders<BsonDocument>.Filter.Eq("email", key) & Builders<BsonDocument>.Filter.Exists("at"), cancellationToken);
        }
    }

    /// <summary>Forgets the count and the lock of an address: after a sign-in or a new password.</summary>
    public static async Task ClearAsync(IMongoDatabase database, string? email, CancellationToken cancellationToken = default)
    {
        var key = Key(email);
        if (key.Length == 0)
        {
            return;
        }

        await Of(database).DeleteManyAsync(Builders<BsonDocument>.Filter.Eq("email", key), cancellationToken);
    }
}
