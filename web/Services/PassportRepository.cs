using BatteryPassWeb.Models.ViewModels;
using MongoDB.Bson;
using MongoDB.Driver;

namespace BatteryPassWeb.Services;

public sealed class PassportRepository
{
    private readonly MongoContext _mongoContext;

    public PassportRepository(MongoContext mongoContext)
    {
        _mongoContext = mongoContext;
    }

    public async Task<IReadOnlyList<PassportSummaryViewModel>> SearchAsync(string query, bool includeArchived = false, CancellationToken cancellationToken = default)
    {
        if (_mongoContext.Database == null)
        {
            return [];
        }

        var collection = _mongoContext.Database.GetCollection<BsonDocument>("passports");
        var builder = Builders<BsonDocument>.Filter;
        var filters = new List<FilterDefinition<BsonDocument>>();

        if (!includeArchived)
        {
            filters.Add(builder.Ne("registryInfo.status", "archived"));
        }

        if (!string.IsNullOrWhiteSpace(query))
        {
            var regex = new BsonRegularExpression(query.Trim(), "i");
            filters.Add(builder.Or(
                builder.Regex("passportId", regex),
                builder.Regex("app.display.name", regex),
                builder.Regex("app.display.modelNumber", regex),
                builder.Regex("app.display.serialNumber", regex)
            ));
        }

        var filter = filters.Count switch
        {
            0 => builder.Empty,
            1 => filters[0],
            _ => builder.And(filters)
        };

        var documents = await collection
            .Find(filter)
            .SortByDescending(document => document["registryInfo"]["updatedAt"])
            .Limit(200)
            .ToListAsync(cancellationToken);

        return documents.Select(ToSummary).ToList();
    }

    public async Task<BsonDocument?> GetByPassportIdAsync(string passportId, CancellationToken cancellationToken = default)
    {
        if (_mongoContext.Database == null)
        {
            return null;
        }

        var collection = _mongoContext.Database.GetCollection<BsonDocument>("passports");
        return await collection.Find(Builders<BsonDocument>.Filter.Eq("passportId", passportId)).FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<PassportSummaryViewModel?> GetSummaryAsync(string passportId, CancellationToken cancellationToken = default)
    {
        var document = await GetByPassportIdAsync(passportId, cancellationToken);
        return document == null ? null : ToSummary(document);
    }

    private static PassportSummaryViewModel ToSummary(BsonDocument document)
    {
        var passportId = BsonHelpers.GetString(document, "passportId");
        var name = BsonHelpers.GetString(document, "app", "display", "name");
        var modelNumber = BsonHelpers.GetString(document, "app", "display", "modelNumber");
        var manufacturer = BsonHelpers.GetString(document, "app", "display", "manufacturerName");
        var serialNumber = BsonHelpers.GetString(document, "app", "display", "serialNumber");
        var status = BsonHelpers.GetString(document, "registryInfo", "status");
        var imageUrl = BsonHelpers.GetString(document, "app", "media", "batteryImageUrl");
        var clusterId = BsonHelpers.GetString(document, "clusterId");

        return new PassportSummaryViewModel
        {
            PassportId = passportId,
            DisplayName = string.IsNullOrWhiteSpace(name) ? modelNumber : name,
            ModelNumber = modelNumber,
            ManufacturerName = manufacturer,
            SerialNumber = serialNumber,
            RegistryStatus = status,
            ClusterId = clusterId,
            BatteryImageUrl = string.IsNullOrWhiteSpace(imageUrl) ? "/images/sample-battery.png" : imageUrl
        };
    }
}
