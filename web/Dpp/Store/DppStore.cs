using BatteryPassWeb.Dpp.Assembly;
using BatteryPassWeb.Dpp.Values;
using BatteryPassWeb.Services;
using MongoDB.Driver;

namespace BatteryPassWeb.Dpp.Store;

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
    };

    private readonly IMongoDatabase? _database;

    public DppStore(MongoContext mongoContext)
    {
        ArgumentNullException.ThrowIfNull(mongoContext);
        _database = mongoContext.Database;
    }

    public bool IsAvailable => _database is not null;

    /// <summary>
    /// #6 is unique; serial numbers are not (design 8.2), so they are indexed for lookup only.
    /// A batch is unique per model, site and year (decision 3). Safe to run at every start.
    /// </summary>
    public async Task EnsureIndexesAsync(CancellationToken cancellationToken = default)
    {
        if (_database is null)
        {
            return;
        }

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
        return Collection<T>().InsertOneAsync(record, cancellationToken: cancellationToken);
    }

    /// <summary>
    /// Sets the given values on one record, leaving its other values as they are; false when no record has that id.
    /// Each key is set on its own, so a live feed and a form saving at the same time don't overwrite each other.
    /// </summary>
    public async Task<bool> SetValuesAsync<T>(string id, IReadOnlyDictionary<string, AttributeValue> values, CancellationToken cancellationToken = default)
        where T : class, IDppRecord
    {
        ArgumentNullException.ThrowIfNull(values);
        RequireDatabase();
        if (values.Count == 0)
        {
            throw new ArgumentException("There are no values to set.", nameof(values));
        }

        var update = Builders<T>.Update.Combine(values.Select(entry => Builders<T>.Update.Set($"Values.{entry.Key}", entry.Value)));
        var result = await Collection<T>().UpdateOneAsync(Builders<T>.Filter.Eq("_id", id), update, cancellationToken: cancellationToken);
        return result.MatchedCount == 1;
    }

    /// <summary>All packs with this serial number (#11). Serial numbers are not unique.</summary>
    public async Task<IReadOnlyList<BatteryPack>> FindPacksBySerialNumberAsync(string serialNumber, CancellationToken cancellationToken = default)
    {
        if (_database is null || string.IsNullOrWhiteSpace(serialNumber))
        {
            return [];
        }

        return await Collection<BatteryPack>().Find(Builders<BatteryPack>.Filter.Eq(SerialNumberField, serialNumber)).ToListAsync(cancellationToken);
    }

    /// <summary>The pack with this unique product identifier (#6), if any.</summary>
    public async Task<BatteryPack?> FindPackByUniqueProductIdentifierAsync(string identifier, CancellationToken cancellationToken = default)
    {
        if (_database is null || string.IsNullOrWhiteSpace(identifier))
        {
            return null;
        }

        return await Collection<BatteryPack>().Find(Builders<BatteryPack>.Filter.Eq(UniqueProductIdentifierField, identifier)).FirstOrDefaultAsync(cancellationToken);
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

    /// <summary>A version by its passport ID (#5), whatever its status.</summary>
    public async Task<PassportVersion?> GetVersionByPassportIdAsync(string passportId, CancellationToken cancellationToken = default)
    {
        if (_database is null || string.IsNullOrWhiteSpace(passportId))
        {
            return null;
        }

        return await Collection<PassportVersion>().Find(version => version.DigitalProductPassportId == passportId).FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<PassportVersion>> ListVersionsAsync(string packId, CancellationToken cancellationToken = default)
    {
        if (_database is null || string.IsNullOrWhiteSpace(packId))
        {
            return [];
        }

        return await Collection<PassportVersion>().Find(version => version.PackId == packId).SortBy(version => version.Number).ToListAsync(cancellationToken);
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
    public Task ArchiveOtherVersionsAsync(string packId, string activeVersionId, CancellationToken cancellationToken = default) =>
        Collection<PassportVersion>().UpdateManyAsync(
            version => version.PackId == packId && version.DppStatus == DppStatuses.Active && version.Id != activeVersionId,
            Builders<PassportVersion>.Update.Set(version => version.DppStatus, DppStatuses.Archived),
            cancellationToken: cancellationToken);

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

        var published = await Collection<PassportVersion>()
            .Find(version => packIds.Contains(version.PackId) && version.DppStatus == DppStatuses.Active)
            .Project(version => version.PackId)
            .ToListAsync(cancellationToken);
        var clusters = await Collection<BatteryPack>()
            .Find(pack => published.Contains(pack.Id) && pack.ClusterId != null)
            .Project(pack => pack.ClusterId)
            .ToListAsync(cancellationToken);
        return clusters.OfType<string>().Distinct(StringComparer.Ordinal).ToArray();
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

    /// <summary>Points a family at its new picture (decision 20); false when no family has that id.</summary>
    public async Task<bool> SetFamilyPictureAsync(string familyId, string fileId, string contentType, CancellationToken cancellationToken = default)
    {
        var result = await Collection<BatteryFamily>().UpdateOneAsync(
            family => family.Id == familyId,
            Builders<BatteryFamily>.Update.Set(family => family.PictureFileId, fileId).Set(family => family.PictureContentType, contentType),
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
