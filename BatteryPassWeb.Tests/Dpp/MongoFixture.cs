using System.Text.Json.Nodes;
using BatteryPassWeb.Configuration;
using BatteryPassWeb.Dpp.Schemas;
using BatteryPassWeb.Dpp.Store;
using BatteryPassWeb.Dpp.Values;
using BatteryPassWeb.Services;
using EphemeralMongo;
using Microsoft.Extensions.Options;

namespace BatteryPassWeb.Tests.Dpp;

/// <summary>
/// One throwaway MongoDB for all database tests (decision 22). The first run downloads mongod (about 100 MB, cached
/// in %LOCALAPPDATA%\ephemeral-mongo); later runs start it in about a second.
/// </summary>
public sealed class MongoFixture : IAsyncLifetime
{
    private IMongoRunner? _runner;

    public async Task InitializeAsync() =>
        _runner = await MongoRunner.RunAsync(new MongoRunnerOptions { Version = MongoVersion.V8 });

    public Task DisposeAsync()
    {
        _runner?.Dispose();
        return Task.CompletedTask;
    }

    /// <summary>A context on a new, empty database, so tests never see each other's data.</summary>
    public MongoContext NewContext() => new(Options.Create(new BatteryPassOptions
    {
        MongoDbUri = _runner!.ConnectionString,
        MongoDbName = $"dpp_test_{Guid.NewGuid():N}",
    }));
}

[CollectionDefinition(Name)]
public sealed class MongoCollection : ICollectionFixture<MongoFixture>
{
    public const string Name = "DPP MongoDB";
}

/// <summary>A clock that always shows the same time.</summary>
internal sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => now;
}

/// <summary>Shared set-up for the database tests: the seed pack in a fresh store.</summary>
internal static class DppStoreTesting
{
    public static readonly OfficialSchemas Schemas = OfficialSchemas.Load();
    public static readonly DateTimeOffset Now = new(2026, 10, 29, 8, 0, 0, TimeSpan.Zero);

    public static async Task<(DppStore Store, MongoContext Context, SeedPassport Seed)> SeededStoreAsync(MongoFixture mongo)
    {
        var context = mongo.NewContext();
        var store = new DppStore(context);
        await store.EnsureIndexesAsync();
        var seed = DppTestSeed.Load();
        await store.InsertAsync(new BatteryFamily { Id = seed.Sources.Model.FamilyId, Name = "Test battery family" });
        await store.InsertAsync(seed.Sources.Model);
        await store.InsertAsync(seed.Sources.Site);
        await store.InsertAsync(seed.Sources.Batch);
        await store.InsertAsync(seed.Sources.Operator);
        await store.InsertAsync(seed.Sources.Pack);
        return (store, context, seed);
    }

    public static AttributeValue Value(JsonNode value) =>
        new() { Value = value, GeneratedAtUtc = Now.UtcDateTime, Source = "seed" };
}
