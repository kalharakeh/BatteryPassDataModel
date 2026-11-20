using BatteryPassWeb.Dpp.Editing;
using BatteryPassWeb.Services;
using MongoDB.Bson;
using MongoDB.Driver;

namespace BatteryPassWeb.Dpp.Access;

/// <summary>
/// Finding R11: the use notice and the log for restricted data. <see cref="Notice"/> is the one line a reader gets next to
/// data that isn't public. The first opening of a battery's restricted data, or of a restricted document, per reader and
/// UTC day is an audit entry of its own kind (<see cref="EventType"/>, shown in the team's audit log under "Restricted data
/// opened"): who, their role, which battery, which sections or which document, when. It holds no passport value. The team's
/// own reads and a cluster admin's reads of their own batteries are not logged (<see cref="IsOwnData"/>).
/// </summary>
public sealed class RestrictedReads(MongoContext context, ApplicationAuditService audit, TimeProvider clock)
{
    public const string EventType = "restricted.opened";

    /// <summary>One entry's key per reader, subject and day: the first writer wins, so two openings at once make one entry.</summary>
    public const string Collection = "restrictedReads";

    /// <summary>The one use notice, with the reader's role in it.</summary>
    public static string Notice(string roleLabel) =>
        $"This data is shared with you under the Battery Regulation for your role as {roleLabel}; don't pass it on or keep it longer than you need it.";

    /// <summary>Whether a read is the owner's own (the team's, or a cluster admin's of a battery of one of their clusters): not logged, no notice.</summary>
    public static bool IsOwnData(ChangeSender? editor, string? packClusterId) =>
        editor is { Kind: SenderKind.Team }
        || (editor is { Kind: SenderKind.ClusterAdmin } && packClusterId is not null && editor.ClusterIds.Contains(packClusterId));

    private int _indexed;

    /// <summary>Where the page finds the reader's role for the notice; absent when the reader needs no notice.</summary>
    public const string RoleKey = "RestrictedNoticeRole";

    /// <summary>
    /// The role to name in the notice (and so to log) for this signed-in reader of data of a battery in
    /// <paramref name="packClusterId"/>; null for nobody signed in and for the owner's own reads (<see cref="IsOwnData"/>).
    /// </summary>
    public static async Task<string?> RoleForAsync(DppReaders readers, System.Security.Claims.ClaimsPrincipal user, string? packClusterId, CancellationToken cancellationToken = default) =>
        user.Identity?.IsAuthenticated != true || IsOwnData(await readers.EditorForAsync(user, cancellationToken), packClusterId)
            ? null
            : AccessControlService.DisplayRoleLabel(user);

    public async Task EnsureIndexesAsync(CancellationToken cancellationToken = default)
    {
        if (context.Database is null)
        {
            return;
        }

        // The keys only keep one entry per day; two days on, they are of no use.
        await context.Database.GetCollection<BsonDocument>(Collection).Indexes.CreateOneAsync(
            new CreateIndexModel<BsonDocument>(Builders<BsonDocument>.IndexKeys.Ascending("at"), new CreateIndexOptions { ExpireAfter = TimeSpan.FromDays(3) }),
            cancellationToken: cancellationToken);
        _indexed = 1;
    }

    /// <summary>The kinds of page, each with its own key per reader and day (H1 round two).</summary>
    public const string DetailPage = "detail";
    public const string AccidentsPage = "accidents";
    public const string RegistryPage = "registry";

    /// <summary>A claimed key that never got its entry (the app stopped in between) is taken again after this long.</summary>
    public static readonly TimeSpan StaleClaim = TimeSpan.FromMinutes(2);

    /// <summary>Logs a reader's first opening today of a battery's restricted data on this kind of page; false when it was logged already.</summary>
    public Task<bool> NoteBatteryAsync(string who, string roleLabel, string page, string packId, string batteryName, IReadOnlyList<string> sections, CancellationToken cancellationToken = default) =>
        NoteAsync(
            $"{Key(who)}|{page}|{packId}",
            who,
            roleLabel,
            $"opened the restricted data of battery {batteryName}: {string.Join(", ", sections)}",
            new BsonDocument
            {
                ["page"] = page,
                ["battery"] = batteryName,
                ["sections"] = new BsonArray(sections.Select(section => (BsonValue)section)),
            },
            "dpp-pack",
            packId,
            cancellationToken);

    /// <summary>Logs a reader's first look today at the registry list with battery statuses that aren't theirs; false when it was logged already.</summary>
    public Task<bool> NoteListAsync(string who, string roleLabel, IReadOnlyCollection<string> packIds, CancellationToken cancellationToken = default) =>
        NoteAsync(
            $"{Key(who)}|{RegistryPage}|list",
            who,
            roleLabel,
            $"opened the registry list with the battery statuses of {packIds.Count} {(packIds.Count == 1 ? "battery" : "batteries")}",
            new BsonDocument
            {
                ["page"] = RegistryPage,
                ["batteries"] = packIds.Count,
                ["packIds"] = new BsonArray(packIds.Take(200).Select(id => (BsonValue)id)),
            },
            "dpp-registry",
            "list",
            cancellationToken);

    /// <summary>Logs a reader's first download of a restricted document today; false when it was logged already.</summary>
    public Task<bool> NoteDocumentAsync(string who, string roleLabel, string documentId, string documentName, string? packId, CancellationToken cancellationToken = default)
    {
        var metadata = new BsonDocument { ["document"] = documentName };
        if (packId is not null)
        {
            metadata["packId"] = packId;
        }

        return NoteAsync($"{Key(who)}|document|{documentId}", who, roleLabel, $"opened the restricted document {documentName}", metadata, "dpp-document", documentId, cancellationToken);
    }

    private static string Key(string who) => ClusterRepository.NormalizeEmail(who);

    /// <summary>
    /// One entry per key and UTC day, never lost (H1 round two): the key is claimed (two openings at once make one entry),
    /// the entry is written, then the claim is confirmed, none of it on the request's cancellation. A failed entry gives
    /// the claim back, so the next opening is logged; a claim left by an app that stopped in between is taken again after
    /// <see cref="StaleClaim"/>.
    /// </summary>
    private async Task<bool> NoteAsync(string key, string who, string roleLabel, string message, BsonDocument metadata, string entityType, string entityId, CancellationToken cancellationToken)
    {
        if (context.Database is null)
        {
            return false;
        }

        _ = cancellationToken; // the read itself has happened: nothing here stops on the request's cancellation
        var none = CancellationToken.None;
        var now = clock.GetUtcNow().UtcDateTime;
        var keys = context.Database.GetCollection<BsonDocument>(Collection);
        var id = $"{key}|{now:yyyy-MM-dd}";
        if (_indexed == 0)
        {
            await EnsureIndexesAsync(none); // once per app start; the index only cleans up old keys
        }

        if (!await ClaimAsync(keys, id, now))
        {
            return false;
        }

        try
        {
            await audit.AppendApplicationAuditEventAsync(EventType, who, roleLabel, "restricted-read", message, metadata, entityType: entityType, entityId: entityId, cancellationToken: none);
        }
        catch
        {
            await keys.DeleteOneAsync(Builders<BsonDocument>.Filter.Eq("_id", id) & Builders<BsonDocument>.Filter.Eq("at", now), none);
            throw;
        }

        await keys.UpdateOneAsync(Builders<BsonDocument>.Filter.Eq("_id", id), Builders<BsonDocument>.Update.Set("done", true), cancellationToken: none);
        return true;
    }

    /// <summary>Takes today's key: a new one, or one claimed long ago that never got its entry. One write decides.</summary>
    private static async Task<bool> ClaimAsync(IMongoCollection<BsonDocument> keys, string id, DateTime now)
    {
        try
        {
            await keys.InsertOneAsync(new BsonDocument { ["_id"] = id, ["at"] = now, ["done"] = false }, cancellationToken: CancellationToken.None);
            return true;
        }
        catch (MongoWriteException exception) when (exception.WriteError?.Category == ServerErrorCategory.DuplicateKey)
        {
            var retaken = await keys.UpdateOneAsync(
                Builders<BsonDocument>.Filter.Eq("_id", id) & Builders<BsonDocument>.Filter.Eq("done", false) & Builders<BsonDocument>.Filter.Lt("at", now - StaleClaim),
                Builders<BsonDocument>.Update.Set("at", now),
                cancellationToken: CancellationToken.None);
            return retaken.ModifiedCount == 1;
        }
    }
}
