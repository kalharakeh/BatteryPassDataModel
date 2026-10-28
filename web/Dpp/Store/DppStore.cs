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

    private IMongoCollection<T> Collection<T>() => RequireDatabase().GetCollection<T>(CollectionName<T>());

    private IMongoDatabase RequireDatabase() =>
        _database ?? throw new InvalidOperationException("MongoDB is not configured (MONGODB_URI).");

    private static string CollectionName<T>() =>
        CollectionNames.TryGetValue(typeof(T), out var name)
            ? name
            : throw new ArgumentException($"{typeof(T).Name} is not a passport record.");
}
