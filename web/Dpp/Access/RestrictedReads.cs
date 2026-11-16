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

    /// <summary>Logs a reader's first opening of a battery's restricted sections today; false when it was logged already.</summary>
    public async Task<bool> NoteBatteryAsync(string who, string roleLabel, string packId, string batteryName, IReadOnlyList<string> sections, CancellationToken cancellationToken = default)
    {
        if (!await FirstTodayAsync($"{Key(who)}|battery|{packId}", cancellationToken))
        {
            return false;
        }

        await audit.AppendApplicationAuditEventAsync(
            EventType,
            who,
            roleLabel,
            "restricted-read",
            $"opened the restricted data of battery {batteryName}: {string.Join(", ", sections)}",
            new BsonDocument
            {
                ["battery"] = batteryName,
                ["sections"] = new BsonArray(sections.Select(section => (BsonValue)section)),
            },
            entityType: "dpp-pack",
            entityId: packId,
            cancellationToken: cancellationToken);
        return true;
    }

    /// <summary>Logs a reader's first download of a restricted document today; false when it was logged already.</summary>
    public async Task<bool> NoteDocumentAsync(string who, string roleLabel, string documentId, string documentName, string? packId, CancellationToken cancellationToken = default)
    {
        if (!await FirstTodayAsync($"{Key(who)}|document|{documentId}", cancellationToken))
        {
            return false;
        }

        var metadata = new BsonDocument { ["document"] = documentName };
        if (packId is not null)
        {
            metadata["packId"] = packId;
        }

        await audit.AppendApplicationAuditEventAsync(
            EventType,
            who,
            roleLabel,
            "restricted-read",
            $"opened the restricted document {documentName}",
            metadata,
            entityType: "dpp-document",
            entityId: documentId,
            cancellationToken: cancellationToken);
        return true;
    }

    private static string Key(string who) => ClusterRepository.NormalizeEmail(who);

    /// <summary>Takes today's place for this key; false when it was taken. One write decides, so it is the same for every app instance.</summary>
    private async Task<bool> FirstTodayAsync(string key, CancellationToken cancellationToken)
    {
        if (context.Database is null)
        {
            return false;
        }

        var now = clock.GetUtcNow().UtcDateTime;
        try
        {
            if (_indexed == 0)
            {
                await EnsureIndexesAsync(cancellationToken); // once per app start; the index only cleans up old keys
            }

            await context.Database.GetCollection<BsonDocument>(Collection).InsertOneAsync(
                new BsonDocument { ["_id"] = $"{key}|{now:yyyy-MM-dd}", ["at"] = now },
                cancellationToken: cancellationToken);
            return true;
        }
        catch (MongoWriteException exception) when (exception.WriteError?.Category == ServerErrorCategory.DuplicateKey)
        {
            return false;
        }
    }
}
