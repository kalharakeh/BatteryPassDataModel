using BatteryPassWeb.Configuration;
using Microsoft.Extensions.Options;
using MongoDB.Driver;

namespace BatteryPassWeb.Services;

public sealed class MongoContext
{
    public IMongoDatabase? Database { get; }
    public Exception? ConnectionError { get; }

    public MongoContext(IOptions<BatteryPassOptions> options)
    {
        var uri = options.Value.MongoDbUri;
        if (string.IsNullOrWhiteSpace(uri))
        {
            ConnectionError = new InvalidOperationException("MONGODB_URI is missing.");
            return;
        }

        try
        {
            var settings = MongoClientSettings.FromConnectionString(uri);
            settings.ConnectTimeout = TimeSpan.FromSeconds(5);
            settings.ServerSelectionTimeout = TimeSpan.FromSeconds(5);
            var client = new MongoClient(settings);

            Database = client.GetDatabase(options.Value.MongoDbName);
        }
        catch (Exception ex)
        {
            ConnectionError = ex;
        }
    }
}
