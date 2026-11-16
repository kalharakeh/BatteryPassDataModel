using System.Text.RegularExpressions;
using BatteryPassWeb.Dpp.Assembly;
using BatteryPassWeb.Dpp.Values;
using BatteryPassWeb.Services;
using MongoDB.Bson;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;

namespace BatteryPassWeb.Dpp.Store;

/// <summary>
/// A pack from before S1 round two whose battery ID has the same item key as another's (<paramref name="HeldBy"/>, which
/// keeps it): it is left without a stored key, so the unique index can be made and the app starts; the rule's look-ups
/// still find it. Someone must decide which of the two is right.
/// </summary>
public sealed record ItemKeyCollision(string PackId, string ItemKey, string HeldBy);

/// <summary>What giving one pack its item key did (S1 round three).</summary>
public enum ItemKeyFill
{
    /// <summary>The key was written.</summary>
    Written,

    /// <summary>The pack changed since it was read (a key by now, or another ID after a swap): nothing written.</summary>
    Changed,

    /// <summary>Another pack holds the key: this one is left without it (see <see cref="ItemKeyFillResult.Collision"/>).</summary>
    Held,

    /// <summary>The ID gives no key (no letters or digits).</summary>
    NoKey,
}

public sealed record ItemKeyFillResult(ItemKeyFill Outcome, ItemKeyCollision? Collision = null);

/// <summary>A battery's names for the release check (S1 round three): only the fields it needs.</summary>
public sealed record BatteryNames(string PackId, string? SerialNumber, string? Identifier, string? ItemKey);

/// <summary>Every name a removal marker holds, without regard to capitals, and the item keys of its battery IDs.</summary>
public sealed record RemovedNames(IReadOnlySet<string> Names, IReadOnlySet<string> ItemKeys);

/// <summary>
/// Reads and writes the passport source records in MongoDB (design 4.1). Without a configured database, reads
/// return nothing and writes throw, like the app's other repositories.
/// </summary>
public sealed class DppStore
{
    public const string Families = "dpp_families";
    public const string Models = "dpp_models";
    public const string Sites = "dpp_sites";
    public const string Batches = "dpp_batches";
    public const string Operators = "dpp_operators";
    public const string Packs = "dpp_packs";
    public const string Versions = "dpp_passport_versions";
    public const string HandOvers = "dpp_transfers";
    public const string Uploads = "dpp_uploads";
    public const string UploadChecks = "dpp_upload_checks";

    /// <summary>The battery IDs the team released in advance (step 6d), keyed by the serial number's letters and digits.</summary>
    public const string BatteryIds = "dpp_battery_ids";

    /// <summary>The markers of removed passports (finding R08).</summary>
    public const string RemovedPassports = "dpp_removed_passports";

    private const string UniqueProductIdentifierField = "Values.uniqueProductIdentifier.Value";
    private const string SerialNumberField = "Values.batterySerialNumber.Value";

    private static readonly Dictionary<Type, string> CollectionNames = new()
    {
        [typeof(BatteryFamily)] = Families,
        [typeof(BatteryModel)] = Models,
        [typeof(ManufacturingSite)] = Sites,
        [typeof(ProductionBatch)] = Batches,
        [typeof(EconomicOperator)] = Operators,
        [typeof(BatteryPack)] = Packs,
        [typeof(PassportVersion)] = Versions,
        [typeof(HandOver)] = HandOvers,
        [typeof(UploadRecord)] = Uploads,
        [typeof(UploadCheck)] = UploadChecks,
        [typeof(Identifiers.ReleasedBatteryId)] = BatteryIds,
        [typeof(RemovedPassport)] = RemovedPassports,
    };

    private readonly IMongoDatabase? _database;
    private readonly Microsoft.Extensions.Logging.ILogger<DppStore>? _logger;

    public DppStore(MongoContext mongoContext, Microsoft.Extensions.Logging.ILogger<DppStore>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(mongoContext);
        _database = mongoContext.Database;
        _logger = logger;
    }

    /// <summary>The unique index on <see cref="BatteryPack.ItemKey"/> (S1 round two).</summary>
    public const string ItemKeyIndex = "item_key";

    /// <summary>
    /// An index on the key that also holds the packs without one (not sparse), so that "which packs have no key yet" is
    /// answered by the index at every start (S1 round three).
    /// </summary>
    public const string ItemKeyFindIndex = "item_key_find";

    /// <summary>
    /// True when a write was refused because another pack has the same battery ID or the same item key (the one rule, S1
    /// round two): whichever of the two unique indexes the database names.
    /// </summary>
    public static bool IsDuplicateItem(MongoWriteException exception) =>
        exception?.WriteError?.Category == ServerErrorCategory.DuplicateKey
        && (exception.WriteError.Message.Contains(ItemKeyIndex, StringComparison.Ordinal) || exception.WriteError.Message.Contains("unique_product_identifier", StringComparison.Ordinal));

    public bool IsAvailable => _database is not null;

    /// <summary>The released battery IDs (step 6d), for <see cref="Identifiers.BatteryIdPool"/>.</summary>
    public IMongoCollection<Identifiers.ReleasedBatteryId> ReleasedIds => Collection<Identifiers.ReleasedBatteryId>();

    /// <summary>
    /// Stores a whole release or none of it (step 6d): in one transaction where the database has them (a replica set, as
    /// on Atlas), otherwise in order, taking back what went in when one fails. False when an ID was already released (its
    /// _id, the serial number's letters and digits, is unique), so two releases at once can't share one.
    /// </summary>
    /// <summary>
    /// True when the database takes transactions: a replica-set primary or a sharded router (Atlas), however the app is
    /// connected to it (a direct connection reports the cluster as standalone).
    /// </summary>
    public static bool SupportsTransactions(IMongoClient client) =>
        client.Cluster.Description.Servers.Any(server => server.Type is MongoDB.Driver.Core.Servers.ServerType.ReplicaSetPrimary or MongoDB.Driver.Core.Servers.ServerType.ShardRouter);

    public async Task<bool> InsertReleaseAsync(IReadOnlyList<Identifiers.ReleasedBatteryId> records, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(records);
        var options = new InsertManyOptions { IsOrdered = true };
        var client = RequireDatabase().Client;
        if (SupportsTransactions(client))
        {
            using var session = await client.StartSessionAsync(cancellationToken: cancellationToken);
            try
            {
                await session.WithTransactionAsync(
                    async (handle, token) =>
                    {
                        await ReleasedIds.InsertManyAsync(handle, records, options, token);
                        return true;
                    },
                    cancellationToken: cancellationToken);
                return true;
            }
            catch (MongoBulkWriteException)
            {
                return false;
            }
        }

        try
        {
            await ReleasedIds.InsertManyAsync(records, options, cancellationToken);
            return true;
        }
        catch (MongoBulkWriteException)
        {
            var releaseIds = records.Select(record => record.ReleaseId).Distinct().ToArray();
            await ReleasedIds.DeleteManyAsync(Builders<Identifiers.ReleasedBatteryId>.Filter.In(record => record.ReleaseId, releaseIds), CancellationToken.None);
            return false;
        }
    }

    /// <summary>
    /// #6 is unique; serial numbers are not (design 8.2), so they are indexed for lookup only.
    /// A batch is unique per model, site and year (decision 3). Safe to run at every start. Before the unique index on the
    /// item key (S1 round two), every pack without a key gets one; a pack whose key another already has is left without
    /// it, named in the log and returned, so that a start never fails on old data.
    /// </summary>
    public async Task<IReadOnlyList<ItemKeyCollision>> EnsureIndexesAsync(CancellationToken cancellationToken = default)
    {
        if (_database is null)
        {
            return [];
        }

        // The indexes first (S1 round three): nothing has a key yet on an old database, so the unique one builds; every
        // key written after it is held to it, by this start and by any other instance.
        try
        {
            await Collection<BatteryPack>().Indexes.CreateManyAsync(
            [
                new CreateIndexModel<BatteryPack>(
                    Builders<BatteryPack>.IndexKeys.Ascending(pack => pack.ItemKey),
                    new CreateIndexOptions { Unique = true, Sparse = true, Name = ItemKeyIndex }),
                new CreateIndexModel<BatteryPack>(
                    Builders<BatteryPack>.IndexKeys.Ascending(pack => pack.ItemKey).Ascending(pack => pack.Id),
                    new CreateIndexOptions { Name = ItemKeyFindIndex }),
            ], cancellationToken);
        }
        catch (MongoException exception)
        {
            _logger?.LogError(exception, "The battery ID key indexes couldn't be made; the app starts without them, and two batteries with the same ID apart from capitals and dashes are then refused by the app's checks only.");
        }

        var collisions = await FillItemKeysAsync(cancellationToken);

        await Collection<BatteryPack>().Indexes.CreateManyAsync(
        [
            new CreateIndexModel<BatteryPack>(
                Builders<BatteryPack>.IndexKeys.Ascending(UniqueProductIdentifierField),
                new CreateIndexOptions { Unique = true, Sparse = true, Name = "unique_product_identifier" }),
            new CreateIndexModel<BatteryPack>(
                Builders<BatteryPack>.IndexKeys.Ascending(SerialNumberField),
                new CreateIndexOptions { Name = "serial_number" }),
            new CreateIndexModel<BatteryPack>(
                Builders<BatteryPack>.IndexKeys.Ascending(pack => pack.ClusterId),
                new CreateIndexOptions { Name = "cluster" }),
            new CreateIndexModel<BatteryPack>(
                Builders<BatteryPack>.IndexKeys.Ascending(pack => pack.BatchId),
                new CreateIndexOptions { Name = "batch" }),
        ], cancellationToken);
        await Collection<ProductionBatch>().Indexes.CreateOneAsync(
            new CreateIndexModel<ProductionBatch>(
                Builders<ProductionBatch>.IndexKeys.Ascending(batch => batch.ModelId).Ascending(batch => batch.SiteId).Ascending(batch => batch.Year),
                new CreateIndexOptions { Unique = true, Name = "model_site_year" }),
            cancellationToken: cancellationToken);
        await Collection<BatteryModel>().Indexes.CreateOneAsync(
            new CreateIndexModel<BatteryModel>(
                Builders<BatteryModel>.IndexKeys.Ascending(model => model.FamilyId),
                new CreateIndexOptions { Name = "family" }),
            cancellationToken: cancellationToken);
        await Collection<PassportVersion>().Indexes.CreateManyAsync(
        [
            new CreateIndexModel<PassportVersion>(
                Builders<PassportVersion>.IndexKeys.Ascending(version => version.DigitalProductPassportId),
                new CreateIndexOptions { Unique = true, Name = "passport_id" }),
            new CreateIndexModel<PassportVersion>(
                Builders<PassportVersion>.IndexKeys.Ascending(version => version.PackId).Ascending(version => version.Number),
                new CreateIndexOptions { Unique = true, Name = "pack_number" }),
            new CreateIndexModel<PassportVersion>(
                Builders<PassportVersion>.IndexKeys.Ascending(version => version.PackId).Ascending(version => version.DppStatus),
                new CreateIndexOptions { Name = "pack_status" }),
        ], cancellationToken);
        await Collection<HandOver>().Indexes.CreateManyAsync(
        [
            new CreateIndexModel<HandOver>(
                Builders<HandOver>.IndexKeys.Ascending(handOver => handOver.PackId).Ascending(handOver => handOver.State),
                new CreateIndexOptions { Name = "pack_state" }),
            new CreateIndexModel<HandOver>(
                Builders<HandOver>.IndexKeys.Ascending(handOver => handOver.ToClusterId).Ascending(handOver => handOver.State),
                new CreateIndexOptions { Name = "to_state" }),
        ], cancellationToken);
        await Collection<UploadRecord>().Indexes.CreateManyAsync(
        [
            new CreateIndexModel<UploadRecord>(Builders<UploadRecord>.IndexKeys.Descending(upload => upload.AtUtc), new CreateIndexOptions { Name = "at" }),
            new CreateIndexModel<UploadRecord>(Builders<UploadRecord>.IndexKeys.Ascending("Batteries.PackId").Descending(upload => upload.AtUtc), new CreateIndexOptions { Name = "pack_at" }),
        ], cancellationToken);
        await Collection<RemovedPassport>().Indexes.CreateOneAsync(
            new CreateIndexModel<RemovedPassport>(Builders<RemovedPassport>.IndexKeys.Ascending(removed => removed.Ids)),
            cancellationToken: cancellationToken);
        await Collection<UploadCheck>().Indexes.CreateOneAsync(
            new CreateIndexModel<UploadCheck>(
                Builders<UploadCheck>.IndexKeys.Ascending(check => check.ExpiresAtUtc),
                new CreateIndexOptions { Name = "expires", ExpireAfter = TimeSpan.Zero }),
            cancellationToken: cancellationToken);
        await ReleasedIds.Indexes.CreateManyAsync(
        [
            new CreateIndexModel<Identifiers.ReleasedBatteryId>(Builders<Identifiers.ReleasedBatteryId>.IndexKeys.Ascending(item => item.ReleaseId), new CreateIndexOptions { Name = "release" }),
            new CreateIndexModel<Identifiers.ReleasedBatteryId>(Builders<Identifiers.ReleasedBatteryId>.IndexKeys.Ascending(item => item.PackId), new CreateIndexOptions { Name = "pack", Sparse = true }),
            new CreateIndexModel<Identifiers.ReleasedBatteryId>(Builders<Identifiers.ReleasedBatteryId>.IndexKeys.Ascending(item => item.Status).Ascending(item => item.ModelId), new CreateIndexOptions { Name = "status_model" }),
        ], cancellationToken);
        return collisions;
    }

    /// <summary>The packs that have a battery ID and no item key yet: the index answers it (<see cref="ItemKeyFindIndex"/>).</summary>
    private static FilterDefinition<BsonDocument> Keyless =>
        Builders<BsonDocument>.Filter.Eq(nameof(BatteryPack.ItemKey), BsonNull.Value) & Builders<BsonDocument>.Filter.Exists(UniqueProductIdentifierField);

    /// <summary>
    /// Gives every pack that has a battery ID and no item key its key (the packs made before S1 round two), oldest _id
    /// first, one guarded update each (<see cref="FillItemKeyAsync"/>), reading only the _id and the ID. A pack whose key
    /// another holds is left without it and named. Never fails the start: a database error is logged and the start goes on.
    /// A later start reads only what the index finds without a key (the pairs named before).
    /// </summary>
    private async Task<IReadOnlyList<ItemKeyCollision>> FillItemKeysAsync(CancellationToken cancellationToken)
    {
        var collisions = new List<ItemKeyCollision>();
        try
        {
            var packs = RequireDatabase().GetCollection<BsonDocument>(Packs);
            var keyless = await packs.Find(Keyless)
                .Project(Builders<BsonDocument>.Projection.Include("_id").Include(UniqueProductIdentifierField))
                .Sort(Builders<BsonDocument>.Sort.Ascending("_id"))
                .ToListAsync(cancellationToken);
            foreach (var pack in keyless)
            {
                var id = pack["_id"].AsString;
                var read = pack.GetValue("Values", new BsonDocument()).AsBsonDocument.GetValue("uniqueProductIdentifier", new BsonDocument()).AsBsonDocument.GetValue("Value", BsonNull.Value);
                if (!read.IsString)
                {
                    continue;
                }

                if ((await FillItemKeyAsync(id, read.AsString, cancellationToken)).Collision is { } collision)
                {
                    collisions.Add(collision);
                }
            }
        }
        catch (MongoException exception)
        {
            _logger?.LogError(exception, "Giving the batteries made before this version their battery ID key stopped; the next start goes on with the rest.");
        }

        return collisions;
    }

    /// <summary>
    /// Gives one pack the item key of <paramref name="identifierRead"/>, in one update that holds only while the pack has no
    /// key yet and its ID is still the one read (S1 round three): a swap in between keeps the key it wrote, and two
    /// instances never write a key twice. A duplicate the database reports names the pack holding the key (never this one).
    /// </summary>
    public async Task<ItemKeyFillResult> FillItemKeyAsync(string packId, string identifierRead, CancellationToken cancellationToken = default)
    {
        var key = Identifiers.IdentifierForms.ItemKey(identifierRead);
        if (key.Length == 0)
        {
            return new ItemKeyFillResult(ItemKeyFill.NoKey);
        }

        var packs = RequireDatabase().GetCollection<BsonDocument>(Packs);
        var guarded = Builders<BsonDocument>.Filter.Eq("_id", packId)
            & Builders<BsonDocument>.Filter.Eq(nameof(BatteryPack.ItemKey), BsonNull.Value)
            & Builders<BsonDocument>.Filter.Eq(UniqueProductIdentifierField, identifierRead);
        try
        {
            var result = await packs.UpdateOneAsync(guarded, Builders<BsonDocument>.Update.Set(nameof(BatteryPack.ItemKey), key), cancellationToken: cancellationToken);
            return new ItemKeyFillResult(result.ModifiedCount == 1 ? ItemKeyFill.Written : ItemKeyFill.Changed);
        }
        catch (MongoWriteException exception) when (IsDuplicateItem(exception))
        {
            var holder = await packs.Find(Builders<BsonDocument>.Filter.Eq(nameof(BatteryPack.ItemKey), key) & Builders<BsonDocument>.Filter.Ne("_id", packId))
                .Project(Builders<BsonDocument>.Projection.Include("_id"))
                .FirstOrDefaultAsync(cancellationToken);
            var heldBy = holder?["_id"].AsString ?? "another battery";
            _logger?.LogWarning("Battery {PackId} has the same battery ID as battery {HeldBy}, apart from capitals and dashes ({ItemKey}); it was left without its key. Decide which of the two is right.", packId, heldBy, key);
            return new ItemKeyFillResult(ItemKeyFill.Held, new ItemKeyCollision(packId, key, heldBy));
        }
    }

    /// <summary>The query plan of "which packs have no key yet", for the test that shows a later start reads the index only.</summary>
    public async Task<string> ExplainKeylessQueryAsync(CancellationToken cancellationToken = default)
    {
        var command = new BsonDocument
        {
            ["explain"] = new BsonDocument { ["find"] = Packs, ["filter"] = Keyless.Render(new RenderArgs<BsonDocument>(MongoDB.Bson.Serialization.BsonSerializer.LookupSerializer<BsonDocument>(), MongoDB.Bson.Serialization.BsonSerializer.SerializerRegistry)) },
            ["verbosity"] = "queryPlanner",
        };
        return (await RequireDatabase().RunCommandAsync<BsonDocument>(command, cancellationToken: cancellationToken)).ToJson();
    }

    /// <summary>Logs an upload (step 5a).</summary>
    public async Task AddUploadAsync(UploadRecord upload, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(upload);
        if (_database is not null)
        {
            await Collection<UploadRecord>().InsertOneAsync(upload, cancellationToken: cancellationToken);
        }
    }

    /// <summary>Marks an upload as the demo data's (decision 32); clearing the demo data removes it.</summary>
    public Task SetUploadDemoAsync(string uploadId, CancellationToken cancellationToken = default) =>
        Collection<UploadRecord>().UpdateOneAsync(upload => upload.Id == uploadId, Builders<UploadRecord>.Update.Set(upload => upload.IsDemo, true), cancellationToken: cancellationToken);

    /// <summary>The latest uploads, newest first.</summary>
    public async Task<IReadOnlyList<UploadRecord>> ListUploadsAsync(int limit, CancellationToken cancellationToken = default)
    {
        if (_database is null)
        {
            return [];
        }

        return await Collection<UploadRecord>().Find(Builders<UploadRecord>.Filter.Empty).SortByDescending(upload => upload.AtUtc).Limit(limit).ToListAsync(cancellationToken);
    }

    /// <summary>The latest uploads that named this battery, newest first (step 5c).</summary>
    public async Task<IReadOnlyList<UploadRecord>> ListUploadsForPackAsync(string packId, int limit, CancellationToken cancellationToken = default)
    {
        if (_database is null)
        {
            return [];
        }

        return await Collection<UploadRecord>()
            .Find(Builders<UploadRecord>.Filter.ElemMatch(upload => upload.Batteries, battery => battery.PackId == packId))
            .SortByDescending(upload => upload.AtUtc)
            .Limit(limit)
            .ToListAsync(cancellationToken);
    }

    public async Task<T?> GetAsync<T>(string id, CancellationToken cancellationToken = default)
        where T : class, IDppRecord
    {
        if (_database is null || string.IsNullOrWhiteSpace(id))
        {
            return null;
        }

        return await Collection<T>().Find(Builders<T>.Filter.Eq("_id", id)).FirstOrDefaultAsync(cancellationToken);
    }

    public Task InsertAsync<T>(T record, CancellationToken cancellationToken = default)
        where T : class, IDppRecord
    {
        ArgumentNullException.ThrowIfNull(record);
        RequireDatabase();
        if (record is BatteryPack pack)
        {
            // The one rule's key goes with the ID (S1 round two); the unique index refuses a second pack with it.
            pack.ItemKey = Identifiers.IdentifierForms.ItemKey(pack.Values.GetValueOrDefault("uniqueProductIdentifier")?.Value is System.Text.Json.Nodes.JsonValue value && value.TryGetValue<string>(out var id) ? id : null) is { Length: > 0 } key ? key : null;
        }

        return Collection<T>().InsertOneAsync(record, cancellationToken: cancellationToken);
    }

    /// <summary>
    /// Sets the given values on one record, leaving its other values as they are; false when no record has that id.
    /// Each key is set on its own, so a live feed and a form saving at the same time don't overwrite each other.
    /// </summary>
    public Task<bool> SetValuesAsync<T>(string id, IReadOnlyDictionary<string, AttributeValue> values, CancellationToken cancellationToken = default)
        where T : class, IDppRecord =>
        SetValuesAsync<T>(id, values, [], cancellationToken);

    /// <summary>Sets the given values and removes the <paramref name="removals"/> in one update; false when no record has that id.</summary>
    public async Task<bool> SetValuesAsync<T>(string id, IReadOnlyDictionary<string, AttributeValue> values, IReadOnlyCollection<string> removals, CancellationToken cancellationToken = default)
        where T : class, IDppRecord
    {
        ArgumentNullException.ThrowIfNull(values);
        ArgumentNullException.ThrowIfNull(removals);
        RequireDatabase();
        if (values.Count == 0 && removals.Count == 0)
        {
            throw new ArgumentException("There are no values to set.", nameof(values));
        }

        var updates = values.Select(entry => Builders<T>.Update.Set($"Values.{entry.Key}", entry.Value))
            .Concat(removals.Select(key => Builders<T>.Update.Unset($"Values.{key}")))
            .ToList();
        if (typeof(T) == typeof(BatteryPack) && values.TryGetValue("uniqueProductIdentifier", out var identifier))
        {
            // A battery's ID changes only by a swap: its key changes with it, in this one update (S1 round two).
            var key = Identifiers.IdentifierForms.ItemKey(identifier.Value is System.Text.Json.Nodes.JsonValue text && text.TryGetValue<string>(out var given) ? given : null);
            updates.Add(key.Length > 0 ? Builders<T>.Update.Set(nameof(BatteryPack.ItemKey), key) : Builders<T>.Update.Unset(nameof(BatteryPack.ItemKey)));
        }

        var result = await Collection<T>().UpdateOneAsync(Builders<T>.Filter.Eq("_id", id), Builders<T>.Update.Combine(updates), cancellationToken: cancellationToken);
        return result.MatchedCount == 1;
    }

    /// <summary>
    /// As <see cref="SetValuesAsync{T}(string, IReadOnlyDictionary{string, AttributeValue}, IReadOnlyCollection{string}, CancellationToken)"/>,
    /// only while the record still has no value for <paramref name="mustBeMissing"/>; false when it has one by now
    /// (round two, F: two corrections adding the warranty at the same moment, only one is saved).
    /// </summary>
    public async Task<bool> SetValuesIfMissingAsync<T>(string id, IReadOnlyDictionary<string, AttributeValue> values, string mustBeMissing, CancellationToken cancellationToken = default)
        where T : class, IDppRecord
    {
        ArgumentNullException.ThrowIfNull(values);
        RequireDatabase();
        var update = Builders<T>.Update.Combine(values.Select(entry => Builders<T>.Update.Set($"Values.{entry.Key}", entry.Value)));
        var filter = Builders<T>.Filter.Eq("_id", id) & Builders<T>.Filter.Exists($"Values.{mustBeMissing}", false);
        return (await Collection<T>().UpdateOneAsync(filter, update, cancellationToken: cancellationToken)).MatchedCount == 1;
    }

    /// <summary>All packs with this serial number (#11). Serial numbers are not unique.</summary>
    /// <summary>
    /// The pack and every earlier and later passport of the same battery (design 7.5): they share its serial number. A
    /// battery placed on the market twice has three, not just a pack's direct neighbours (redesign R6).
    /// </summary>
    public async Task<IReadOnlyCollection<string>> ChainPackIdsAsync(string packId, CancellationToken cancellationToken = default)
    {
        var ids = new List<string>();
        for (var at = (string?)packId; at is not null && !ids.Contains(at); at = (await GetAsync<BatteryPack>(at, cancellationToken))?.SuccessorOf)
        {
            ids.Add(at);
        }

        for (var at = (await GetAsync<BatteryPack>(packId, cancellationToken))?.Succession?.SuccessorPackId; at is not null && !ids.Contains(at); at = (await GetAsync<BatteryPack>(at, cancellationToken))?.Succession?.SuccessorPackId)
        {
            ids.Add(at);
        }

        return ids;
    }

    public async Task<IReadOnlyList<BatteryPack>> FindPacksBySerialNumberAsync(string serialNumber, CancellationToken cancellationToken = default)
    {
        if (_database is null || string.IsNullOrWhiteSpace(serialNumber) || !Identifiers.IdentifierForms.IsLookup(serialNumber))
        {
            return [];
        }

        // As stored first (indexed, and all a battery released before decision 80 ever needed); otherwise by the rule:
        // capital and small letters, and dashes, don't tell two serial numbers apart.
        var packs = Collection<BatteryPack>();
        var asStored = await packs.Find(Builders<BatteryPack>.Filter.Eq(SerialNumberField, serialNumber)).ToListAsync(cancellationToken);
        return asStored.Count > 0 || SameSerial(serialNumber) is not { } same
            ? asStored
            : await packs.Find(Builders<BatteryPack>.Filter.Regex(SerialNumberField, same)).ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Matches a stored serial number that is the same as <paramref name="serialNumber"/> by the one rule (decision 80,
    /// <see cref="Identifiers.IdentifierForms.SameSerial"/>): the same letters and digits in the same order, whatever the
    /// case of the letters and whatever else stands between them. Null for a text with no letter or digit.
    /// </summary>
    private static BsonRegularExpression? SameSerial(string? serialNumber)
    {
        const string Other = "[^A-Za-z0-9]*";
        var key = Identifiers.IdentifierForms.Clean(serialNumber); // only A–Z and 0–9: nothing to escape
        return key.Length == 0 ? null : new BsonRegularExpression($"^{Other}{string.Join(Other, key.Select(letter => letter.ToString()))}{Other}$", "i");
    }

    /// <summary>Matches a stored ID that is <paramref name="text"/> whichever way its letters are typed (decision 80).</summary>
    private static BsonRegularExpression SameLetters(string text) => new($"^{Regex.Escape(text)}$", "i");

    /// <summary>
    /// True for text a case-blind look-up may take: printable ASCII only. The database's case rules would fold ſ and ı
    /// into s and i (S1 round two); an ID or a serial number holds a to z only, so other text is compared as it is.
    /// </summary>
    private static bool CaseBlind(string text) => text.All(letter => letter is >= ' ' and <= '~');

    /// <summary>
    /// True when another pack already has this serial number (#11) by the one rule (decision 80): the same letters and
    /// digits, whatever their case and their dashes. <paramref name="exceptPackId"/> is the pack being corrected, which may keep its own.
    /// </summary>
    public Task<bool> SerialNumberTakenAsync(string serialNumber, string? exceptPackId = null, CancellationToken cancellationToken = default) =>
        SerialNumberTakenAsync(serialNumber, exceptPackId is null ? [] : [exceptPackId], cancellationToken);

    /// <summary>
    /// The same, with several packs that may keep it: a battery's new passport shares its old one's serial number, as it
    /// is the same battery (design 7.5, step 4c-1).
    /// </summary>
    public async Task<bool> SerialNumberTakenAsync(string serialNumber, IReadOnlyCollection<string> exceptPackIds, CancellationToken cancellationToken = default)
    {
        var wanted = serialNumber?.Trim();
        if (_database is null || string.IsNullOrEmpty(wanted) || !Identifiers.IdentifierForms.IsLookup(wanted))
        {
            return false; // a serial number with a control character is refused by the serial rule before this
        }

        // A text with no letter or digit has no key: it is compared as typed, as before.
        var sameSerial = SameSerial(wanted) is { } byKey
            ? Builders<BatteryPack>.Filter.Regex(SerialNumberField, byKey)
            : CaseBlind(wanted)
                ? Builders<BatteryPack>.Filter.Regex(SerialNumberField, new BsonRegularExpression($@"^\s*{Regex.Escape(wanted)}\s*$", "i"))
                : Builders<BatteryPack>.Filter.Eq(SerialNumberField, wanted);
        var filter = exceptPackIds.Count == 0 ? sameSerial : sameSerial & Builders<BatteryPack>.Filter.Nin("_id", exceptPackIds);
        return await Collection<BatteryPack>().Find(filter).Limit(1).AnyAsync(cancellationToken);
    }

    /// <summary>Moves a pack to another cluster, or to none; false when no pack has that id. The cluster isn't passport data.</summary>
    public async Task<bool> SetPackClusterAsync(string packId, string? clusterId, CancellationToken cancellationToken = default)
    {
        var result = await Collection<BatteryPack>().UpdateOneAsync(
            pack => pack.Id == packId,
            Builders<BatteryPack>.Update.Set(pack => pack.ClusterId, clusterId),
            cancellationToken: cancellationToken);
        return result.MatchedCount == 1;
    }

    /// <summary>Writes the marker of a removed passport (finding R08); writing it again changes nothing.</summary>
    public async Task AddRemovedAsync(RemovedPassport removed, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(removed);
        await Collection<RemovedPassport>().ReplaceOneAsync(item => item.Id == removed.Id, removed, new ReplaceOptions { IsUpsert = true }, cancellationToken);
    }

    /// <summary>
    /// The marker of the removed passport that answered to this ID: a battery ID in either form, a passport ID or the pack
    /// ID, as an address may carry it (encoded once), whichever way its letters are typed (decision 80). Null when no
    /// removed passport had it (finding R08).
    /// </summary>
    public async Task<RemovedPassport?> FindRemovedAsync(string? id, CancellationToken cancellationToken = default)
    {
        var text = Uri.UnescapeDataString(id?.Trim() ?? string.Empty);
        if (_database is null || text.Length == 0 || !Identifiers.IdentifierForms.IsLookup(text))
        {
            return null;
        }

        var forms = new[] { text, Identifiers.IdentifierForms.Short(text) }.Distinct(StringComparer.Ordinal).ToArray();
        var named = Builders<RemovedPassport>.Filter.AnyIn(removed => removed.Ids, forms);
        if (CaseBlind(text))
        {
            named |= Builders<RemovedPassport>.Filter.Or(forms.Select(form => Builders<RemovedPassport>.Filter.Regex("Ids", SameLetters(form))));
        }


        // Without regard to capitals only (S1 round three): an address answers "removed" for the removed passport's own
        // IDs. Whether an ID is taken, dashes ignored too, is IsItemKeyRemovedAsync's question.
        return await Collection<RemovedPassport>().Find(named).FirstOrDefaultAsync(cancellationToken);
    }

    /// <summary>
    /// "Is this ID taken?" for a removed passport (the one rule, S1 round two): true when a removal marker holds a battery
    /// ID with this item key, capitals and dashes ignored. Asked by release, creation, swap and a new passport's R-number;
    /// never by the serving routes, which ask <see cref="FindRemovedAsync"/>.
    /// </summary>
    public async Task<bool> IsItemKeyRemovedAsync(string key, CancellationToken cancellationToken = default) =>
        _database is not null && key.Length > 0
        && await Collection<RemovedPassport>().Find(Builders<RemovedPassport>.Filter.Regex("Ids", SameItem(key, anchored: true))).AnyAsync(cancellationToken);

    /// <summary>
    /// Every name the removal markers hold (without regard to capitals) and the item keys of their battery IDs, in one
    /// query, for a release's check of all its serial numbers at once (S1 round three).
    /// </summary>
    public async Task<RemovedNames> RemovedNamesAsync(CancellationToken cancellationToken = default)
    {
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var keys = new HashSet<string>(StringComparer.Ordinal);
        if (_database is not null)
        {
            foreach (var ids in await Collection<RemovedPassport>().Find(FilterDefinition<RemovedPassport>.Empty).Project(removed => removed.Ids).ToListAsync(cancellationToken))
            {
                foreach (var id in ids)
                {
                    names.Add(id);
                    if (Identifiers.IdentifierForms.Short(id) is var item && Identifiers.IdentifierForms.IsItem(item))
                    {
                        keys.Add(Identifiers.IdentifierForms.ItemKey(item));
                    }
                }
            }
        }

        return new RemovedNames(names, keys);
    }

    /// <summary>Every battery's serial number, battery ID and item key, and nothing else, in one query (S1 round three).</summary>
    public async Task<IReadOnlyList<BatteryNames>> ListBatteryNamesAsync(CancellationToken cancellationToken = default)
    {
        if (_database is null)
        {
            return [];
        }

        static string? Text(BsonDocument pack, string property) =>
            pack.GetValue("Values", new BsonDocument()).AsBsonDocument.GetValue(property, new BsonDocument()).AsBsonDocument.GetValue("Value", BsonNull.Value) is { IsString: true } value ? value.AsString : null;

        var packs = await RequireDatabase().GetCollection<BsonDocument>(Packs).Find(FilterDefinition<BsonDocument>.Empty)
            .Project(Builders<BsonDocument>.Projection.Include("_id").Include(SerialNumberField).Include(UniqueProductIdentifierField).Include(nameof(BatteryPack.ItemKey)))
            .ToListAsync(cancellationToken);
        return packs.Select(pack => new BatteryNames(
            pack["_id"].AsString,
            Text(pack, "batterySerialNumber"),
            Text(pack, "uniqueProductIdentifier"),
            pack.GetValue(nameof(BatteryPack.ItemKey), BsonNull.Value) is { IsString: true } key ? key.AsString : null)).ToArray();
    }

    /// <summary>
    /// The item keys taken by <paramref name="firstItem"/> and its R-numbers, apart from capitals and dashes: by a pack, a
    /// released ID or a removal marker. Read once, in three queries, when a new passport's R-number is chosen (S1 round three).
    /// </summary>
    public async Task<IReadOnlySet<string>> TakenItemKeysAsync(string firstItem, CancellationToken cancellationToken = default)
    {
        var taken = new HashSet<string>(StringComparer.Ordinal);
        var key = Identifiers.IdentifierForms.ItemKey(firstItem);
        if (_database is null || key.Length == 0)
        {
            return taken;
        }

        var variants = new BsonRegularExpression($"/{Identifiers.IdentifierForms.DataIdentifier}/{string.Join("-?", key.Select(letter => letter.ToString()))}(-?R[0-9]+)?$", "i");
        var packs = RequireDatabase().GetCollection<BsonDocument>(Packs);
        foreach (var pack in await packs
                     .Find(Builders<BsonDocument>.Filter.Regex(nameof(BatteryPack.ItemKey), new BsonRegularExpression($"^{key}R?[0-9]*$")) | Builders<BsonDocument>.Filter.Regex(UniqueProductIdentifierField, variants))
                     .Project(Builders<BsonDocument>.Projection.Include(UniqueProductIdentifierField))
                     .ToListAsync(cancellationToken))
        {
            if (pack.GetValue("Values", new BsonDocument()).AsBsonDocument.GetValue("uniqueProductIdentifier", new BsonDocument()).AsBsonDocument.GetValue("Value", BsonNull.Value) is { IsString: true } id)
            {
                taken.Add(Identifiers.IdentifierForms.ItemKey(id.AsString));
            }
        }

        foreach (var released in await ReleasedIds.Find(Builders<Identifiers.ReleasedBatteryId>.Filter.Regex(item => item.Identifier, variants)).Project(item => item.Identifier).ToListAsync(cancellationToken))
        {
            taken.Add(Identifiers.IdentifierForms.ItemKey(released));
        }

        var anchored = new BsonRegularExpression($"^(?:https?://[^/]+/{Identifiers.IdentifierForms.DataIdentifier}/)?{string.Join("-?", key.Select(letter => letter.ToString()))}(-?R[0-9]+)?$", "i");
        foreach (var ids in await Collection<RemovedPassport>().Find(Builders<RemovedPassport>.Filter.Regex("Ids", anchored)).Project(removed => removed.Ids).ToListAsync(cancellationToken))
        {
            foreach (var id in ids.Where(id => Identifiers.IdentifierForms.IsItem(Identifiers.IdentifierForms.Short(id))))
            {
                taken.Add(Identifiers.IdentifierForms.ItemKey(id));
            }
        }

        return taken;
    }

    /// <summary>Whether anything of a removed pack is left: the pack itself or a version (finding R08).</summary>
    public async Task<bool> IsAnythingLeftAsync(string packId, CancellationToken cancellationToken = default) =>
        await Collection<BatteryPack>().Find(pack => pack.Id == packId).AnyAsync(cancellationToken)
        || await Collection<PassportVersion>().Find(version => version.PackId == packId).AnyAsync(cancellationToken);

    /// <summary>Deletes every version of a pack (finding R08).</summary>
    public Task DeleteVersionsAsync(string packId, CancellationToken cancellationToken = default) =>
        Collection<PassportVersion>().DeleteManyAsync(version => version.PackId == packId, cancellationToken);

    /// <summary>
    /// Empties a pack being removed (finding R08): its values and live values, modules, repairs, marks and every other app
    /// record on it, so what is left until the pack itself goes holds no data.
    /// </summary>
    public Task ClearPackDataAsync(string packId, CancellationToken cancellationToken = default) =>
        Collection<BatteryPack>().UpdateOneAsync(
            pack => pack.Id == packId,
            Builders<BatteryPack>.Update
                .Set(pack => pack.Values, new Dictionary<string, AttributeValue>(StringComparer.Ordinal))
                .Set(pack => pack.Modules, [])
                .Set(pack => pack.Repairs, [])
                .Unset(pack => pack.RemainingCapacity)
                .Unset(pack => pack.Succession)
                .Unset(pack => pack.Mark)
                .Unset(pack => pack.Registry)
                .Unset(pack => pack.ChangesWaitingSinceUtc)
                .Unset(pack => pack.ChangesWaitingToken),
            cancellationToken: cancellationToken);



    /// <summary>Writes every marker of a removal in one bulk write (round three); writing them again changes nothing.</summary>
    public async Task AddRemovalMarkersAsync(IReadOnlyList<RemovedPassport> markers, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(markers);
        await Collection<RemovedPassport>().BulkWriteAsync(
            markers.Select(marker => new ReplaceOneModel<RemovedPassport>(Builders<RemovedPassport>.Filter.Eq(item => item.Id, marker.Id), marker) { IsUpsert = true }),
            new BulkWriteOptions { IsOrdered = false },
            cancellationToken);
    }

    /// <summary>Writes the markers of a removal that aren't there yet, in one bulk write; existing ones stay as they are.</summary>
    public async Task AddMissingRemovalMarkersAsync(IReadOnlyList<RemovedPassport> markers, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(markers);
        var ids = markers.Select(marker => marker.Id).ToArray();
        var there = (await Collection<RemovedPassport>().Find(Builders<RemovedPassport>.Filter.In(item => item.Id, ids)).Project(item => item.Id).ToListAsync(cancellationToken)).ToHashSet(StringComparer.Ordinal);
        var missing = markers.Where(marker => !there.Contains(marker.Id)).ToList();
        if (missing.Count > 0)
        {
            await AddRemovalMarkersAsync(missing, cancellationToken);
        }
    }

    /// <summary>Whether the removal whose newest passport is this pack has written its one audit entry.</summary>
    public Task<bool> RemovalEntryExistsAsync(string newestPackId, CancellationToken cancellationToken = default) =>
        RequireDatabase().GetCollection<BsonDocument>("applicationAuditEvents")
            .Find(Builders<BsonDocument>.Filter.Eq("eventType", Editing.PassportRemoval.RemovedEvent) & Builders<BsonDocument>.Filter.Eq("entityId", newestPackId))
            .AnyAsync(cancellationToken);

    /// <summary>Notes on these markers that the removal's one audit entry is written.</summary>
    public Task SetRemovalAuditWrittenAsync(IEnumerable<string> packIds, CancellationToken cancellationToken = default) =>
        Collection<RemovedPassport>().UpdateManyAsync(Builders<RemovedPassport>.Filter.In(item => item.Id, packIds), Builders<RemovedPassport>.Update.Set(item => item.AuditWritten, true), cancellationToken: cancellationToken);

    /// <summary>The documents withdrawn from these packs earlier, from their withdrawal rows (their own rows name them only by ID).</summary>
    public async Task<IReadOnlyList<string>> WithdrawnDocumentIdsAsync(IEnumerable<string> packIds, CancellationToken cancellationToken = default) =>
        (await RequireDatabase().GetCollection<BsonDocument>("applicationAuditEvents")
            .Find(Builders<BsonDocument>.Filter.Eq("eventType", Documents.DocumentService.WithdrawnEvent) & Builders<BsonDocument>.Filter.In("entityId", packIds))
            .ToListAsync(cancellationToken))
            .Select(row => row.GetValue("metadata", new BsonDocument()).AsBsonDocument.GetValue("documentId", BsonNull.Value))
            .Where(id => id.IsString)
            .Select(id => id.AsString)
            .ToList();

    /// <summary>The drafts a successor made for these packs left (cancelled or not), from the packs' own rows.</summary>
    public async Task<IReadOnlyList<string>> SuccessorDraftIdsAsync(IEnumerable<string> packIds, CancellationToken cancellationToken = default) =>
        (await RequireDatabase().GetCollection<BsonDocument>("applicationAuditEvents")
            .Find(Builders<BsonDocument>.Filter.In("entityId", packIds) & Builders<BsonDocument>.Filter.Exists("metadata.successor"))
            .ToListAsync(cancellationToken))
            .Select(row => row["metadata"]["successor"])
            .Where(id => id.IsString)
            .Select(id => id.AsString)
            .Distinct(StringComparer.Ordinal)
            .ToList();

    private static FilterDefinition<UploadedBattery> LineOf(IReadOnlyCollection<string> packIds, IReadOnlyCollection<string> names) =>
        Builders<UploadedBattery>.Filter.In(line => line.PackId, packIds)
        | (Builders<UploadedBattery>.Filter.Eq(line => line.PackId, null) & Builders<UploadedBattery>.Filter.In(line => line.NamedAs, names));

    /// <summary>The upload records with a line about these packs, or refused before a pack was found under one of these names.</summary>
    public async Task<IReadOnlyList<string>> UploadIdsNamingAsync(IReadOnlyCollection<string> packIds, IReadOnlyCollection<string> names, CancellationToken cancellationToken = default) =>
        await Collection<UploadRecord>().Find(Builders<UploadRecord>.Filter.ElemMatch(upload => upload.Batteries, LineOf(packIds, names))).Project(upload => upload.Id).ToListAsync(cancellationToken);

    /// <summary>How many of those records' lines are the battery's.</summary>
    public async Task<int> CountUploadLinesAsync(IReadOnlyCollection<string> uploadIds, IReadOnlyCollection<string> packIds, IReadOnlyCollection<string> names, CancellationToken cancellationToken = default) =>
        (await Collection<UploadRecord>().Find(Builders<UploadRecord>.Filter.In(upload => upload.Id, uploadIds)).ToListAsync(cancellationToken))
            .Sum(upload => upload.Batteries.Count(line => (line.PackId is not null && packIds.Contains(line.PackId)) || (line.PackId is null && names.Contains(line.NamedAs))));

    /// <summary>
    /// Takes the battery's lines, with the values as sent, out of the upload records this removal wrote down (decision 72),
    /// and deletes those of them left without a line; no other record is touched (round three).
    /// </summary>
    public async Task RemoveUploadLinesAsync(IReadOnlyCollection<string> uploadIds, IReadOnlyCollection<string> packIds, IReadOnlyCollection<string> names, CancellationToken cancellationToken = default)
    {
        if (uploadIds.Count == 0)
        {
            return;
        }

        var uploads = Collection<UploadRecord>();
        var these = Builders<UploadRecord>.Filter.In(upload => upload.Id, uploadIds);
        await uploads.UpdateManyAsync(these, Builders<UploadRecord>.Update.PullFilter(upload => upload.Batteries, LineOf(packIds, names)), cancellationToken: cancellationToken);
        await uploads.DeleteManyAsync(these & Builders<UploadRecord>.Filter.Size(upload => upload.Batteries, 0), cancellationToken);
    }

    /// <summary>Which of these upload records still exist.</summary>
    public async Task<IReadOnlyList<string>> ExistingUploadIdsAsync(IReadOnlyCollection<string> uploadIds, CancellationToken cancellationToken = default) =>
        await Collection<UploadRecord>().Find(Builders<UploadRecord>.Filter.In(upload => upload.Id, uploadIds)).Project(upload => upload.Id).ToListAsync(cancellationToken);

    /// <summary>
    /// Deletes every audit row of a removed battery (decision 72): rows about its packs, its documents, its emptied uploads
    /// and drafts it left, and rows naming one of its IDs; the removal's own entry (<paramref name="keep"/>) stays.
    /// </summary>
    public Task DeleteAuditRowsAsync(IEnumerable<string> entityIds, IEnumerable<string> ids, string keep, CancellationToken cancellationToken = default)
    {
        var filter = Builders<BsonDocument>.Filter;
        var named = ids.Where(id => id.Length > 0).Distinct(StringComparer.Ordinal).ToArray();
        return RequireDatabase().GetCollection<BsonDocument>("applicationAuditEvents").DeleteManyAsync(
            filter.Ne("eventType", keep)
            & (filter.In("entityId", entityIds) | filter.In("batteryId", named) | filter.In("passportId", named)),
            cancellationToken);
    }

    /// <summary>Takes these names out of the passport check's own entries, which list the serial numbers they changed.</summary>
    public Task PullFromCheckEntriesAsync(IEnumerable<string> names, CancellationToken cancellationToken = default) =>
        RequireDatabase().GetCollection<BsonDocument>("applicationAuditEvents").UpdateManyAsync(
            Builders<BsonDocument>.Filter.Eq("eventType", Publication.PublicationService.CheckedEvent),
            Builders<BsonDocument>.Update.PullAll("metadata.batteries", names.Distinct(StringComparer.Ordinal).Select(name => (BsonValue)name)),
            cancellationToken: cancellationToken);

    /// <summary>Deletes a removed pack's hand-overs and kept upload checks (finding R08).</summary>
    public async Task DeleteHandOversAndChecksAsync(string packId, CancellationToken cancellationToken = default)
    {
        await Collection<HandOver>().DeleteManyAsync(Builders<HandOver>.Filter.Eq("PackId", packId), cancellationToken);
        await Collection<UploadCheck>().DeleteManyAsync(Builders<UploadCheck>.Filter.Eq("PackId", packId), cancellationToken);
    }


    /// <summary>Records (or corrects) the pack's EU registry registration (finding R02); nothing else changes. False when the pack doesn't exist.</summary>
    public async Task<bool> SetRegistryAsync(string packId, RegistryRecord record, CancellationToken cancellationToken = default)
    {
        var result = await Collection<BatteryPack>().UpdateOneAsync(
            pack => pack.Id == packId,
            Builders<BatteryPack>.Update.Set(pack => pack.Registry, record),
            cancellationToken: cancellationToken);
        return result.MatchedCount == 1;
    }

    /// <summary>Adds an entry to a pack's repair history (design 7.9). False when the pack doesn't exist.</summary>
    public async Task<bool> AddRepairAsync(string packId, RepairEntry entry, CancellationToken cancellationToken = default)
    {
        var result = await Collection<BatteryPack>().UpdateOneAsync(
            pack => pack.Id == packId,
            Builders<BatteryPack>.Update.Push(pack => pack.Repairs, entry),
            cancellationToken: cancellationToken);
        return result.MatchedCount == 1;
    }

    /// <summary>
    /// Keeps a remaining capacity reading (step 5b) unless the pack already has a newer one. False when nothing changed.
    /// </summary>
    public async Task<bool> SetRemainingCapacityAsync(string packId, RemainingCapacityReading reading, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(reading);
        var result = await Collection<BatteryPack>().UpdateOneAsync(
            Builders<BatteryPack>.Filter.Eq(pack => pack.Id, packId)
            & (Builders<BatteryPack>.Filter.Eq(pack => pack.RemainingCapacity, null)
               | Builders<BatteryPack>.Filter.Lte(pack => pack.RemainingCapacity!.MeasuredAtUtc, reading.MeasuredAtUtc)),
            Builders<BatteryPack>.Update.Set(pack => pack.RemainingCapacity, reading),
            cancellationToken: cancellationToken);
        return result.ModifiedCount == 1;
    }

    /// <summary>Replaces a pack's module list (decision 31). False when the pack doesn't exist.</summary>
    public async Task<bool> SetPackModulesAsync(string packId, IReadOnlyList<BatteryModule> modules, CancellationToken cancellationToken = default)
    {
        var result = await Collection<BatteryPack>().UpdateOneAsync(
            pack => pack.Id == packId,
            Builders<BatteryPack>.Update.Set(pack => pack.Modules, modules.ToList()),
            cancellationToken: cancellationToken);
        return result.MatchedCount == 1;
    }

    /// <summary>Sets or clears a model's module description (decision 31). False when the model doesn't exist.</summary>
    public async Task<bool> SetModelModuleDescriptionAsync(string modelId, string? description, CancellationToken cancellationToken = default)
    {
        var result = await Collection<BatteryModel>().UpdateOneAsync(
            model => model.Id == modelId,
            Builders<BatteryModel>.Update.Set(model => model.ModuleDescription, description),
            cancellationToken: cancellationToken);
        return result.MatchedCount == 1;
    }

    /// <summary>Sets or clears a model's serial number format (decision 81). False when the model doesn't exist.</summary>
    public async Task<bool> SetModelSerialFormatAsync(string modelId, string? format, CancellationToken cancellationToken = default)
    {
        var result = await Collection<BatteryModel>().UpdateOneAsync(
            model => model.Id == modelId,
            Builders<BatteryModel>.Update.Set(model => model.SerialFormat, format),
            cancellationToken: cancellationToken);
        return result.MatchedCount == 1;
    }

    /// <summary>Sets a model's answers to its three questions (R01); null clears one. False when the model doesn't exist.</summary>
    public async Task<bool> SetModelAnswersAsync(string modelId, bool? commercialWarranty, bool? cyclingUse, bool? cadmiumOrLeadAboveLimit, CancellationToken cancellationToken = default)
    {
        var result = await Collection<BatteryModel>().UpdateOneAsync(
            model => model.Id == modelId,
            Builders<BatteryModel>.Update
                .Set(model => model.HasCommercialWarranty, commercialWarranty)
                .Set(model => model.MadeForCyclingUse, cyclingUse)
                .Set(model => model.CadmiumOrLeadAboveLimit, cadmiumOrLeadAboveLimit),
            cancellationToken: cancellationToken);
        return result.MatchedCount == 1;
    }

    /// <summary>How many packs belong to this cluster.</summary>
    public async Task<long> CountPacksInClusterAsync(string clusterId, CancellationToken cancellationToken = default)
    {
        if (_database is null || string.IsNullOrWhiteSpace(clusterId))
        {
            return 0;
        }

        return await Collection<BatteryPack>().CountDocumentsAsync(pack => pack.ClusterId == clusterId, cancellationToken: cancellationToken);
    }

    /// <summary>Takes every pack of this cluster out of it (they then have no cluster); returns how many.</summary>
    public async Task<long> ClearClusterAsync(string clusterId, CancellationToken cancellationToken = default)
    {
        if (_database is null || string.IsNullOrWhiteSpace(clusterId))
        {
            return 0;
        }

        var result = await Collection<BatteryPack>().UpdateManyAsync(
            pack => pack.ClusterId == clusterId,
            Builders<BatteryPack>.Update.Set(pack => pack.ClusterId, null),
            cancellationToken: cancellationToken);
        return result.ModifiedCount;
    }

    /// <summary>
    /// The pack with this unique product identifier (#6), if any: as stored, or by its short item ID
    /// ("UN778474481-VBC-92921", or "UN778474481VPC12221" before 6d-5) for a battery ID written as a web address (step 6d),
    /// whichever way its letters are typed (decision 80).
    /// </summary>
    public async Task<BatteryPack?> FindPackByUniqueProductIdentifierAsync(string identifier, CancellationToken cancellationToken = default)
    {
        if (_database is null || string.IsNullOrWhiteSpace(identifier) || !Identifiers.IdentifierForms.IsLookup(identifier))
        {
            return null;
        }

        var filter = Builders<BatteryPack>.Filter.Eq(UniqueProductIdentifierField, identifier);
        if (Identifiers.IdentifierForms.IsItem(identifier))
        {
            filter |= Builders<BatteryPack>.Filter.Regex(UniqueProductIdentifierField, new BsonRegularExpression($"/{Identifiers.IdentifierForms.DataIdentifier}/{Regex.Escape(identifier)}$", "i"));
        }
        else if (CaseBlind(identifier) && (identifier.Contains($"/{Identifiers.IdentifierForms.DataIdentifier}/", StringComparison.OrdinalIgnoreCase) || identifier.StartsWith("urn:", StringComparison.OrdinalIgnoreCase)))
        {
            filter |= Builders<BatteryPack>.Filter.Regex(UniqueProductIdentifierField, SameLetters(identifier)); // a full ID typed in other letters
        }

        return await Collection<BatteryPack>().Find(filter).FirstOrDefaultAsync(cancellationToken);
    }

    /// <summary>
    /// The pack whose web-form battery ID is <paramref name="item"/> but for its dashes, if any (6d-5): serial numbers
    /// that differ only in their dashes can't both have one, and an ID made before 6d-5 (no dashes) counts too. Nor
    /// can serial numbers that differ only in capital and small letters (decision 80).
    /// </summary>
    public async Task<BatteryPack?> FindPackByItemIgnoringDashesAsync(string item, CancellationToken cancellationToken = default)
    {
        var key = Identifiers.IdentifierForms.Clean(item); // only A–Z and 0–9: nothing to escape
        if (_database is null || key.Length == 0)
        {
            return null;
        }

        return await Collection<BatteryPack>().Find(
            Builders<BatteryPack>.Filter.Eq(pack => pack.ItemKey, key) | Builders<BatteryPack>.Filter.Regex(UniqueProductIdentifierField, SameItem(key, anchored: false)))
            .FirstOrDefaultAsync(cancellationToken);
    }

    /// <summary>
    /// A released ID whose battery ID has this item key (the one rule, S1 round two), whatever its status: a new
    /// passport's R-number skips it, so a label that may be printed stays usable. Null when none.
    /// </summary>
    public async Task<Identifiers.ReleasedBatteryId?> FindReleasedByItemKeyAsync(string key, CancellationToken cancellationToken = default) =>
        _database is null || key.Length == 0
            ? null
            : await ReleasedIds.Find(Builders<Identifiers.ReleasedBatteryId>.Filter.Regex(item => item.Identifier, SameItem(key, anchored: false))).FirstOrDefaultAsync(cancellationToken);

    /// <summary>
    /// Matches a battery ID whose item part has this key (only A–Z and 0–9: nothing to escape): its letters and digits
    /// in order, a dash or none between them, any capitals. As a web-form address's end, or, <paramref name="anchored"/>,
    /// the whole text (a removal marker holds both forms).
    /// </summary>
    private static BsonRegularExpression SameItem(string key, bool anchored)
    {
        var item = string.Join("-?", key.Select(letter => letter.ToString()));
        return new BsonRegularExpression(anchored ? $"^(?:https?://[^/]+/{Identifiers.IdentifierForms.DataIdentifier}/)?{item}$" : $"/{Identifiers.IdentifierForms.DataIdentifier}/{item}$", "i");
    }

    /// <summary>
    /// The battery IDs (#6) that are <paramref name="address"/> or <paramref name="address"/> with a successor's R-number
    /// (<c>…-R1</c>, <c>…-R2</c>; <c>…R1</c> before 6d-5), for picking the next free one (step 6d).
    /// </summary>
    public async Task<IReadOnlySet<string>> FindPackIdentifiersWithSuffixesAsync(string address, CancellationToken cancellationToken = default)
    {
        var filter = Builders<BatteryPack>.Filter.Regex(UniqueProductIdentifierField, new BsonRegularExpression($"^{Regex.Escape(address)}(-?R[0-9]+)?$", "i"));
        var packs = await Collection<BatteryPack>().Find(filter).ToListAsync(cancellationToken);
        return packs.Select(pack => pack.Values["uniqueProductIdentifier"].Value!.GetValue<string>()).ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>Everything one pack's passport is built from; null if the pack or a record it links to is missing.</summary>
    public async Task<PassportSources?> LoadSourcesAsync(string packId, CancellationToken cancellationToken = default)
    {
        var pack = await GetAsync<BatteryPack>(packId, cancellationToken);
        var batch = pack is null ? null : await GetAsync<ProductionBatch>(pack.BatchId, cancellationToken);
        if (pack is null || batch is null)
        {
            return null;
        }

        var model = await GetAsync<BatteryModel>(batch.ModelId, cancellationToken);
        var site = await GetAsync<ManufacturingSite>(batch.SiteId, cancellationToken);
        var economicOperator = await GetAsync<EconomicOperator>(pack.OperatorId, cancellationToken);
        return model is null || site is null || economicOperator is null
            ? null
            : new PassportSources(pack, batch, model, site, economicOperator);
    }

    /// <summary>The pack's active version; the newest one if two briefly overlap while a version is published.</summary>
    public async Task<PassportVersion?> GetActiveVersionAsync(string packId, CancellationToken cancellationToken = default)
    {
        if (_database is null || string.IsNullOrWhiteSpace(packId))
        {
            return null;
        }

        return await Collection<PassportVersion>()
            .Find(version => version.PackId == packId && version.DppStatus == DppStatuses.Active)
            .SortByDescending(version => version.Number)
            .FirstOrDefaultAsync(cancellationToken);
    }

    /// <summary>The pack's newest version, whatever its status; null for a draft.</summary>
    public async Task<PassportVersion?> GetLatestVersionAsync(string packId, CancellationToken cancellationToken = default)
    {
        if (_database is null || string.IsNullOrWhiteSpace(packId))
        {
            return null;
        }

        return await Collection<PassportVersion>().Find(version => version.PackId == packId).SortByDescending(version => version.Number).FirstOrDefaultAsync(cancellationToken);
    }

    /// <summary>The passport's DPP status (#2): its newest version's; null for a draft (step 4c).</summary>
    public async Task<string?> PassportStatusAsync(string packId, CancellationToken cancellationToken = default) =>
        (await GetLatestVersionAsync(packId, cancellationToken))?.DppStatus;

    /// <summary>Sets a version's DPP status and #4 (the status is made available then); #2 isn't signed (4.3).</summary>
    public Task SetVersionStatusAsync(string versionId, string status, DateTime lastUpdatedUtc, CancellationToken cancellationToken = default) =>
        Collection<PassportVersion>().UpdateOneAsync(
            version => version.Id == versionId,
            Builders<PassportVersion>.Update.Set(version => version.DppStatus, status).Set(version => version.LastUpdatedUtc, lastUpdatedUtc),
            cancellationToken: cancellationToken);

    /// <summary>The battery's hand-over waiting for an answer (step 4d); null when none is.</summary>
    public async Task<HandOver?> FindWaitingHandOverAsync(string packId, CancellationToken cancellationToken = default)
    {
        if (_database is null || string.IsNullOrWhiteSpace(packId))
        {
            return null;
        }

        return await Collection<HandOver>().Find(handOver => handOver.PackId == packId && handOver.State == HandOverStates.Waiting).FirstOrDefaultAsync(cancellationToken);
    }

    /// <summary>The hand-overs waiting for an answer from these receiving clusters, oldest first.</summary>
    public async Task<IReadOnlyList<HandOver>> ListWaitingHandOversToAsync(IEnumerable<string> clusterIds, CancellationToken cancellationToken = default)
    {
        if (_database is null)
        {
            return [];
        }

        var to = clusterIds.ToArray();
        return await Collection<HandOver>()
            .Find(Builders<HandOver>.Filter.In(handOver => handOver.ToClusterId, to) & Builders<HandOver>.Filter.Eq(handOver => handOver.State, HandOverStates.Waiting))
            .SortBy(handOver => handOver.AskedAtUtc)
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Ends a waiting hand-over (accepted, declined or cancelled); false when it no longer waits, so two answers at once
    /// can't both count.
    /// </summary>
    public async Task<bool> SetHandOverStateAsync(string handOverId, string state, string by, DateTime atUtc, CancellationToken cancellationToken = default)
    {
        var result = await Collection<HandOver>().UpdateOneAsync(
            handOver => handOver.Id == handOverId && handOver.State == HandOverStates.Waiting,
            Builders<HandOver>.Update.Set(handOver => handOver.State, state).Set(handOver => handOver.AnsweredBy, by).Set(handOver => handOver.AnsweredAtUtc, atUtc),
            cancellationToken: cancellationToken);
        return result.ModifiedCount == 1;
    }

    /// <summary>Sets or clears a pack's invalid or deletion mark (step 4c-2). False when the pack doesn't exist.</summary>
    /// <summary>
    /// Marks these packs as having a saved change waiting for their published passports, in one write (round four, 1):
    /// a new token each time, and the first time kept. Done before any publishing, so an interrupted run leaves them marked.
    /// </summary>
    /// <returns>How many packs it marked.</returns>
    public async Task<long> MarkChangesWaitingAsync(IReadOnlyCollection<string> packIds, DateTime nowUtc, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(packIds);
        if (packIds.Count == 0)
        {
            return 0;
        }

        var set = new BsonDocument("$set", new BsonDocument
        {
            [nameof(BatteryPack.ChangesWaitingToken)] = Guid.NewGuid().ToString("N"),
            [nameof(BatteryPack.ChangesWaitingSinceUtc)] = new BsonDocument("$ifNull", new BsonArray { "$" + nameof(BatteryPack.ChangesWaitingSinceUtc), new BsonDateTime(nowUtc) }),
        });
        return (await Collection<BatteryPack>().UpdateManyAsync(
            Builders<BatteryPack>.Filter.In(pack => pack.Id, packIds),
            Builders<BatteryPack>.Update.Pipeline(new BsonDocument[] { set }),
            cancellationToken: cancellationToken)).ModifiedCount;
    }

    /// <summary>The packs among these that have at least one passport version (active, marked or inactive).</summary>
    public async Task<IReadOnlyList<string>> PublishedPackIdsAsync(IReadOnlyCollection<string> packIds, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(packIds);
        if (packIds.Count == 0)
        {
            return [];
        }

        var found = await Collection<PassportVersion>().DistinctAsync(version => version.PackId, Builders<PassportVersion>.Filter.In(version => version.PackId, packIds), cancellationToken: cancellationToken);
        return await found.ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Clears a pack's marker of a waiting change, only while it still holds <paramref name="tokenSeen"/>: the token the
    /// clearing attempt read before the data it published (round four, 2). A change marked meanwhile stays marked. False
    /// when nothing was cleared.
    /// </summary>
    public async Task<bool> ClearChangesWaitingAsync(string packId, string? tokenSeen, CancellationToken cancellationToken = default)
    {
        var result = await Collection<BatteryPack>().UpdateOneAsync(
            Builders<BatteryPack>.Filter.Eq(pack => pack.Id, packId)
            & Builders<BatteryPack>.Filter.Ne(pack => pack.ChangesWaitingSinceUtc, null)
            & Builders<BatteryPack>.Filter.Eq(pack => pack.ChangesWaitingToken, tokenSeen),
            Builders<BatteryPack>.Update.Unset(pack => pack.ChangesWaitingSinceUtc).Unset(pack => pack.ChangesWaitingToken),
            cancellationToken: cancellationToken);
        return result.ModifiedCount == 1;
    }

    public async Task<bool> SetMarkAsync(string packId, PassportMark? mark, CancellationToken cancellationToken = default)
    {
        var result = await Collection<BatteryPack>().UpdateOneAsync(
            pack => pack.Id == packId,
            Builders<BatteryPack>.Update.Set(pack => pack.Mark, mark),
            cancellationToken: cancellationToken);
        return result.MatchedCount == 1;
    }

    /// <summary>Sets or clears a pack's succession (design 7.5). False when the pack doesn't exist.</summary>
    public async Task<bool> SetSuccessionAsync(string packId, Succession? succession, CancellationToken cancellationToken = default)
    {
        var result = await Collection<BatteryPack>().UpdateOneAsync(
            pack => pack.Id == packId,
            Builders<BatteryPack>.Update.Set(pack => pack.Succession, succession),
            cancellationToken: cancellationToken);
        return result.MatchedCount == 1;
    }

    /// <summary>A version by its passport ID (#5), whatever its status.</summary>
    public async Task<PassportVersion?> GetVersionByPassportIdAsync(string passportId, CancellationToken cancellationToken = default)
    {
        if (_database is null || string.IsNullOrWhiteSpace(passportId) || !Identifiers.IdentifierForms.IsLookup(passportId))
        {
            return null;
        }

        // As stored first (indexed); a passport ID typed in other letters is found without regard to their case (decision 80).
        var versions = Collection<PassportVersion>();
        var version = await versions.Find(version => version.DigitalProductPassportId == passportId).FirstOrDefaultAsync(cancellationToken);
        if (version is null && CaseBlind(passportId) && (passportId.Contains("/dpp/", StringComparison.OrdinalIgnoreCase) || passportId.StartsWith("urn:", StringComparison.OrdinalIgnoreCase)))
        {
            version = await versions.Find(Builders<PassportVersion>.Filter.Regex(version => version.DigitalProductPassportId, SameLetters(passportId))).FirstOrDefaultAsync(cancellationToken);
        }

        // Only the battery's highest active version is served as active (round six, 2).
        return version is { DppStatus: DppStatuses.Active } && (await GetActiveVersionAsync(version.PackId, cancellationToken))?.Number > version.Number
            ? version.ServedAs(DppStatuses.Archived)
            : version;
    }

    public async Task<IReadOnlyList<PassportVersion>> ListVersionsAsync(string packId, CancellationToken cancellationToken = default)
    {
        if (_database is null || string.IsNullOrWhiteSpace(packId))
        {
            return [];
        }

        return PassportVersion.AsServed(await Collection<PassportVersion>().Find(version => version.PackId == packId).SortBy(version => version.Number).ToListAsync(cancellationToken));
    }

    /// <summary>The highest version number of the pack; 0 before its first publication.</summary>
    public async Task<int> GetLatestVersionNumberAsync(string packId, CancellationToken cancellationToken = default)
    {
        var latest = await Collection<PassportVersion>()
            .Find(version => version.PackId == packId)
            .SortByDescending(version => version.Number)
            .Project(version => version.Number)
            .FirstOrDefaultAsync(cancellationToken);
        return latest;
    }

    /// <summary>Marks every other active version of the pack as archived.</summary>
    /// <remarks>
    /// Only versions with a lower number than the one just made active are archived (round five): a slower publisher's
    /// archive step then can't archive a faster publisher's newer version and leave the battery with no active version.
    /// </remarks>
    /// <returns>How many versions it archived.</returns>
    public async Task<long> ArchiveOtherVersionsAsync(string packId, string activeVersionId, int activeNumber, CancellationToken cancellationToken = default) =>
        (await Collection<PassportVersion>().UpdateManyAsync(
            version => version.PackId == packId && version.DppStatus == DppStatuses.Active && version.Id != activeVersionId && version.Number < activeNumber,
            Builders<PassportVersion>.Update.Set(version => version.DppStatus, DppStatuses.Archived),
            cancellationToken: cancellationToken)).ModifiedCount;

    /// <summary>Sets #4 of a version (a live update made available now).</summary>
    public Task SetVersionLastUpdatedAsync(string versionId, DateTime lastUpdatedUtc, CancellationToken cancellationToken = default) =>
        Collection<PassportVersion>().UpdateOneAsync(
            version => version.Id == versionId,
            Builders<PassportVersion>.Update.Set(version => version.LastUpdatedUtc, lastUpdatedUtc),
            cancellationToken: cancellationToken);

    /// <summary>The packs whose passport is built from this model, batch, site or operator record.</summary>
    public async Task<IReadOnlyList<string>> FindPackIdsUsingAsync(string? modelId = null, string? siteId = null, string? batchId = null, string? operatorId = null, CancellationToken cancellationToken = default)
    {
        if (_database is null)
        {
            return [];
        }

        if (operatorId is not null)
        {
            return await Collection<BatteryPack>().Find(pack => pack.OperatorId == operatorId).Project(pack => pack.Id).ToListAsync(cancellationToken);
        }

        var batchIds = batchId is not null
            ? [batchId]
            : await Collection<ProductionBatch>()
                .Find(batch => (modelId != null && batch.ModelId == modelId) || (siteId != null && batch.SiteId == siteId))
                .Project(batch => batch.Id)
                .ToListAsync(cancellationToken);
        return await Collection<BatteryPack>().Find(pack => batchIds.Contains(pack.BatchId)).Project(pack => pack.Id).ToListAsync(cancellationToken);
    }

    /// <summary>
    /// The clusters of the published packs whose passport uses this model, batch or pack record. Draft packs count for
    /// nobody: a draft is visible to the team only (J-6).
    /// </summary>
    public async Task<IReadOnlyList<string>> FindPublishedClusterIdsUsingAsync(string? modelId = null, string? batchId = null, string? packId = null, CancellationToken cancellationToken = default)
    {
        var published = await FindPublishedPackIdsUsingAsync(modelId, batchId, packId, cancellationToken);
        if (published.Count == 0)
        {
            return [];
        }

        var clusters = await Collection<BatteryPack>()
            .Find(pack => published.Contains(pack.Id) && pack.ClusterId != null)
            .Project(pack => pack.ClusterId)
            .ToListAsync(cancellationToken);
        return clusters.OfType<string>().Distinct(StringComparer.Ordinal).ToArray();
    }

    /// <summary>
    /// True when a published passport is built from this model, batch or pack record, whether or not its pack has a
    /// cluster. Until then the record's documents are the team's only (J-6).
    /// </summary>
    public async Task<bool> HasPublishedPackUsingAsync(string? modelId = null, string? batchId = null, string? packId = null, CancellationToken cancellationToken = default) =>
        (await FindPublishedPackIdsUsingAsync(modelId, batchId, packId, cancellationToken)).Count > 0;

    private async Task<IReadOnlyList<string>> FindPublishedPackIdsUsingAsync(string? modelId, string? batchId, string? packId, CancellationToken cancellationToken)
    {
        if (_database is null)
        {
            return [];
        }

        var packIds = packId is not null
            ? [packId]
            : (await FindPackIdsUsingAsync(modelId: modelId, batchId: batchId, cancellationToken: cancellationToken)).ToList();
        if (packIds.Count == 0)
        {
            return [];
        }

        // The battery's newest passport counts while it is active, and while a mark freezes it (invalid, marked for
        // deletion): its cluster reads it as when it was active (redesign R7, like PassportReader's live values). An
        // archived version or a passport a newer one replaced (inactive) doesn't.
        return await Collection<PassportVersion>()
            .Find(version => packIds.Contains(version.PackId)
                && (version.DppStatus == DppStatuses.Active || version.DppStatus == DppStatuses.Invalid || version.DppStatus == DppStatuses.MarkedForDeletion))
            .Project(version => version.PackId)
            .ToListAsync(cancellationToken);
    }

    /// <summary>Every record of one kind. Families, sites and operators are few; the pages sort them by name.</summary>
    public async Task<IReadOnlyList<T>> ListAsync<T>(CancellationToken cancellationToken = default)
        where T : class, IDppRecord
    {
        if (_database is null)
        {
            return [];
        }

        return await Collection<T>().Find(Builders<T>.Filter.Empty).ToListAsync(cancellationToken);
    }

    /// <summary>Every passport version's battery, id, number and status, and nothing else (round seven, 2).</summary>
    public async Task<IReadOnlyList<VersionSummary>> ListVersionSummariesAsync(CancellationToken cancellationToken = default)
    {
        if (_database is null)
        {
            return [];
        }

        return await Collection<PassportVersion>()
            .Find(Builders<PassportVersion>.Filter.Empty)
            .Project(version => new VersionSummary(version.PackId, version.Id, version.Number, version.DppStatus))
            .ToListAsync(cancellationToken);
    }

    /// <summary>Every active passport version, in full: what the check compares (round seven, 2).</summary>
    public async Task<IReadOnlyList<PassportVersion>> ListAllActiveVersionsAsync(CancellationToken cancellationToken = default)
    {
        if (_database is null)
        {
            return [];
        }

        return await Collection<PassportVersion>().Find(version => version.DppStatus == DppStatuses.Active).ToListAsync(cancellationToken);
    }

    /// <summary>The records with these ids; ids that don't exist are left out.</summary>
    public async Task<IReadOnlyList<T>> GetManyAsync<T>(IEnumerable<string> ids, CancellationToken cancellationToken = default)
        where T : class, IDppRecord
    {
        ArgumentNullException.ThrowIfNull(ids);
        var wanted = ids.Distinct(StringComparer.Ordinal).ToArray();
        if (_database is null || wanted.Length == 0)
        {
            return [];
        }

        return await Collection<T>().Find(Builders<T>.Filter.In("_id", wanted)).ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<BatteryModel>> ListModelsAsync(string familyId, CancellationToken cancellationToken = default)
    {
        if (_database is null)
        {
            return [];
        }

        return await Collection<BatteryModel>().Find(model => model.FamilyId == familyId).SortBy(model => model.Name).ToListAsync(cancellationToken);
    }

    /// <summary>A model's batches, newest year first.</summary>
    public async Task<IReadOnlyList<ProductionBatch>> ListBatchesAsync(string modelId, CancellationToken cancellationToken = default)
    {
        if (_database is null)
        {
            return [];
        }

        return await Collection<ProductionBatch>().Find(batch => batch.ModelId == modelId).SortByDescending(batch => batch.Year).ToListAsync(cancellationToken);
    }

    /// <summary>The active versions of these packs: one per published pack, none for a draft.</summary>
    public async Task<IReadOnlyList<PassportVersion>> ListActiveVersionsAsync(IEnumerable<string> packIds, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(packIds);
        var ids = packIds.Distinct(StringComparer.Ordinal).ToArray();
        if (_database is null || ids.Length == 0)
        {
            return [];
        }

        var versions = await Collection<PassportVersion>()
            .Find(version => ids.Contains(version.PackId) && version.DppStatus == DppStatuses.Active)
            .ToListAsync(cancellationToken);
        return versions.GroupBy(version => version.PackId).Select(group => group.MaxBy(version => version.Number)!).ToArray();
    }

    /// <summary>For each of these batches: how many packs it has, and how many of them are published.</summary>
    public async Task<IReadOnlyDictionary<string, (int Packs, int Published)>> CountPacksByBatchAsync(IEnumerable<string> batchIds, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(batchIds);
        var ids = batchIds.Distinct(StringComparer.Ordinal).ToArray();
        var counts = ids.ToDictionary(id => id, _ => (Packs: 0, Published: 0), StringComparer.Ordinal);
        if (_database is null || ids.Length == 0)
        {
            return counts;
        }

        var packs = await Collection<BatteryPack>()
            .Find(pack => ids.Contains(pack.BatchId))
            .Project(pack => new { pack.Id, pack.BatchId })
            .ToListAsync(cancellationToken);
        var published = (await ListActiveVersionsAsync(packs.Select(pack => pack.Id), cancellationToken))
            .Select(version => version.PackId)
            .ToHashSet(StringComparer.Ordinal);
        foreach (var pack in packs)
        {
            var (total, live) = counts[pack.BatchId];
            counts[pack.BatchId] = (total + 1, live + (published.Contains(pack.Id) ? 1 : 0));
        }

        return counts;
    }

    /// <summary>Renames a family, model, site or operator; false when no record has that id.</summary>
    public async Task<bool> RenameAsync<T>(string id, string name, CancellationToken cancellationToken = default)
        where T : class, IDppRecord
    {
        if (typeof(T) != typeof(BatteryFamily) && typeof(T) != typeof(BatteryModel) && typeof(T) != typeof(ManufacturingSite) && typeof(T) != typeof(EconomicOperator))
        {
            throw new ArgumentException($"{typeof(T).Name} has no name.");
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        var result = await Collection<T>().UpdateOneAsync(Builders<T>.Filter.Eq("_id", id), Builders<T>.Update.Set("Name", name.Trim()), cancellationToken: cancellationToken);
        return result.MatchedCount == 1;
    }

    /// <summary>Deletes one record; false when no record has that id. Callers check first that nothing uses it.</summary>
    public async Task<bool> DeleteAsync<T>(string id, CancellationToken cancellationToken = default)
        where T : class, IDppRecord
    {
        var result = await Collection<T>().DeleteOneAsync(Builders<T>.Filter.Eq("_id", id), cancellationToken);
        return result.DeletedCount == 1;
    }

    /// <summary>Points a family at its new picture (decision 20); false when no family has that id.</summary>
    public async Task<bool> SetFamilyPictureAsync(string familyId, string fileId, string contentType, CancellationToken cancellationToken = default)
    {
        var result = await Collection<BatteryFamily>().UpdateOneAsync(
            family => family.Id == familyId,
            Builders<BatteryFamily>.Update.Set(family => family.PictureFileId, fileId).Set(family => family.PictureContentType, contentType),
            cancellationToken: cancellationToken);
        return result.MatchedCount == 1;
    }

    /// <summary>Points a model at its own picture, or back at its family's with nulls (redesign R2); false when no model has that id.</summary>
    public async Task<bool> SetModelPictureAsync(string modelId, string? fileId, string? contentType, CancellationToken cancellationToken = default)
    {
        var result = await Collection<BatteryModel>().UpdateOneAsync(
            model => model.Id == modelId,
            Builders<BatteryModel>.Update.Set(model => model.PictureFileId, fileId).Set(model => model.PictureContentType, contentType),
            cancellationToken: cancellationToken);
        return result.MatchedCount == 1;
    }

    private IMongoCollection<T> Collection<T>() => RequireDatabase().GetCollection<T>(CollectionName<T>());

    private IMongoDatabase RequireDatabase() =>
        _database ?? throw new InvalidOperationException("MongoDB is not configured (MONGODB_URI).");

    private static string CollectionName<T>() =>
        CollectionNames.TryGetValue(typeof(T), out var name)
            ? name
            : throw new ArgumentException($"{typeof(T).Name} is not a passport record.");
}
