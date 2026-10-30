using BatteryPassWeb.Configuration;
using BatteryPassWeb.Dpp;
using BatteryPassWeb.Dpp.Demo;
using BatteryPassWeb.Dpp.Documents;
using BatteryPassWeb.Dpp.Store;
using BatteryPassWeb.Services;
using BatteryPassWeb.Tests.Dpp;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;

namespace BatteryPassWeb.Tests.Demo;

/// <summary>The app's own services on a fresh throwaway database, as the demo data card uses them.</summary>
internal sealed class DemoRig(ServiceProvider services, IMongoDatabase database) : IDisposable
{
    public const string Who = "team.admin@example.test";
    public const string Password = "demo-users-password-for-tests";

    public ServiceProvider Services { get; } = services;
    public IMongoDatabase Database { get; } = database;
    public DemoDataService Demo => Services.GetRequiredService<DemoDataService>();
    public DppStore Store => Services.GetRequiredService<DppStore>();

    public static async Task<DemoRig> StartAsync(MongoFixture mongo, string? password = Password, string? appBaseUrl = DocumentTesting.BaseUrl)
    {
        var context = mongo.NewContext();
        var services = new ServiceCollection()
            .AddSingleton(Options.Create(new BatteryPassOptions { AppBaseUrl = appBaseUrl ?? string.Empty, DemoUsersPassword = password }))
            .AddSingleton(context)
            .AddSingleton<ApplicationAuditService>()
            .AddSingleton<ClusterRepository>()
            .AddSingleton(SigningTesting.Keys)
            .AddSingleton(new ExternalApiSecurityService("demo-test-key-for-the-token-hashes-0000"))
            .AddSingleton<ExternalApiRepository>()
            .AddDpp()
            .AddSingleton<DemoDataService>()
            .BuildServiceProvider();
        await services.GetRequiredService<DppStore>().EnsureIndexesAsync();
        await services.GetRequiredService<DocumentStore>().EnsureIndexesAsync();
        return new DemoRig(services, context.Database!);
    }

    public Task<long> Count(string collection, FilterDefinition<BsonDocument>? filter = null) =>
        Database.GetCollection<BsonDocument>(collection).CountDocumentsAsync(filter ?? FilterDefinition<BsonDocument>.Empty);

    public Task<List<BsonDocument>> All(string collection, FilterDefinition<BsonDocument>? filter = null) =>
        Database.GetCollection<BsonDocument>(collection).Find(filter ?? FilterDefinition<BsonDocument>.Empty).ToListAsync();

    public void Dispose() => Services.Dispose();
}
