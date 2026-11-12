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

    /// <summary>The owner's identifier settings (step 6d), for the rigs that issue DUNS battery IDs.</summary>
    public static BatteryPassOptions WithDunsIds(BatteryPassOptions options)
    {
        options.IdAgency = "UN";
        options.IdCompany = "778474481";
        options.IdBaseUrl = "https://www.sib-scania-eubp.eu";
        return options;
    }

    public static async Task<DemoRig> StartAsync(MongoFixture mongo, string? password = Password, string? appBaseUrl = DocumentTesting.BaseUrl, bool dunsIds = false, bool demoData = true)
    {
        var context = mongo.NewContext();
        var options = new BatteryPassOptions { AppBaseUrl = appBaseUrl ?? string.Empty, DemoUsersPassword = password };
        var services = Build(context, dunsIds ? WithDunsIds(options) : options, demoData);
        await services.GetRequiredService<DppStore>().EnsureIndexesAsync();
        await services.GetRequiredService<DocumentStore>().EnsureIndexesAsync();
        return new DemoRig(services, context.Database!);
    }

    /// <summary>The same database and settings as <paramref name="rig"/>, with ENABLE_DEMO_DATA as given (finding R03).</summary>
    public static DemoRig Over(DemoRig rig, bool demoData) =>
        new(Build(rig.Services.GetRequiredService<MongoContext>(), rig.Services.GetRequiredService<IOptions<BatteryPassOptions>>().Value, demoData), rig.Database);

    private static ServiceProvider Build(MongoContext context, BatteryPassOptions options, bool demoData) =>
        new ServiceCollection()
            .AddSingleton(Options.Create(options))
            .AddSingleton(context)
            .AddSingleton(new DemoDataSwitch(demoData))
            .AddSingleton<ApplicationAuditService>()
            .AddSingleton<ClusterRepository>()
            .AddSingleton(SigningTesting.Keys)
            .AddSingleton(new ExternalApiSecurityService("demo-test-key-for-the-token-hashes-0000"))
            .AddSingleton<ExternalApiRepository>()
            .AddDpp()
            .AddSingleton<DemoDataService>()
            .BuildServiceProvider();

    public Task<long> Count(string collection, FilterDefinition<BsonDocument>? filter = null) =>
        Database.GetCollection<BsonDocument>(collection).CountDocumentsAsync(filter ?? FilterDefinition<BsonDocument>.Empty);

    public Task<List<BsonDocument>> All(string collection, FilterDefinition<BsonDocument>? filter = null) =>
        Database.GetCollection<BsonDocument>(collection).Find(filter ?? FilterDefinition<BsonDocument>.Empty).ToListAsync();

    public void Dispose() => Services.Dispose();
}
