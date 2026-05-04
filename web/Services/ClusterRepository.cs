using MongoDB.Bson;
using MongoDB.Driver;

namespace BatteryPassWeb.Services;

public sealed class ClusterRepository
{
    private readonly MongoContext _mongoContext;

    public ClusterRepository(MongoContext mongoContext)
    {
        _mongoContext = mongoContext;
    }

    public async Task<IReadOnlyList<BsonDocument>> ListClustersAsync(CancellationToken cancellationToken = default)
    {
        if (_mongoContext.Database == null)
        {
            return [];
        }

        return await _mongoContext.Database.GetCollection<BsonDocument>("clusters")
            .Find(Builders<BsonDocument>.Filter.Empty)
            .SortBy(cluster => cluster["name"])
            .ToListAsync(cancellationToken);
    }
}
