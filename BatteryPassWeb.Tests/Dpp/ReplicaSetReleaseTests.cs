using BatteryPassWeb.Configuration;
using BatteryPassWeb.Dpp.Identifiers;
using BatteryPassWeb.Dpp.Store;
using BatteryPassWeb.Services;
using EphemeralMongo;
using Microsoft.Extensions.Options;

namespace BatteryPassWeb.Tests.Dpp;

/// <summary>
/// Step 6d-2: on a replica set, as on Atlas, a release goes in through one transaction. Its own throwaway single-node
/// replica set (the shared test database is a plain single node, which takes the in-order path instead).
/// </summary>
public sealed class ReplicaSetReleaseTests : IAsyncLifetime
{
    private IMongoRunner? _runner;

    public async Task InitializeAsync() =>
        _runner = await MongoRunner.RunAsync(new MongoRunnerOptions { Version = MongoVersion.V8, UseSingleNodeReplicaSet = true });

    public Task DisposeAsync()
    {
        _runner?.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task OverlappingReleasesAtOnce_ShouldNeverShareAnId_InATransaction()
    {
        var context = new MongoContext(Options.Create(new BatteryPassOptions { MongoDbUri = _runner!.ConnectionString, MongoDbName = $"dpp_rs_{Guid.NewGuid():N}" }));
        var store = new DppStore(context);
        await store.EnsureIndexesAsync();
        var pool = new BatteryIdPool(store, new TestIdentifierScheme(), new ApplicationAuditService(context), TimeProvider.System);

        var results = await Task.WhenAll(Enumerable.Range(0, 6).Select(index =>
            pool.ReleaseAsync("team@example.test", new ReleaseRequest($"PO race {index}", null, [new IdRange("RACE-0001", "RACE-0020", "20")], null))));

        Assert.True(DppStore.SupportsTransactions(context.Database!.Client)); // the transaction path
        Assert.Single(results, result => result.Done);
        var released = await pool.ListAsync(new ReleasedIdFilter());
        Assert.Equal(20, released.Count);
        Assert.Single(released.Select(item => item.ReleaseId).Distinct()); // the others left nothing behind
    }
}
