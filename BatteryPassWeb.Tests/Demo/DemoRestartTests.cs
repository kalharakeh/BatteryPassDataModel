using BatteryPassWeb.Services;
using BatteryPassWeb.Tests.Dpp;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Bson;
using MongoDB.Driver;

namespace BatteryPassWeb.Tests.Demo;

/// <summary>Step 3e: an app restart between seeding and clearing. At its start the app gives every cluster without them a
/// read and a read-write token of its own (autoClusterId). The demo cluster tokens are those, so the start adds none; and a
/// demo cluster's own tokens go with it when it is cleared, which also repairs databases seeded before this fix.</summary>
[Collection(MongoCollection.Name)]
public sealed class DemoRestartTests(MongoFixture mongo)
{
    private static Task RestartAsync(DemoRig rig) =>
        new ExternalApiInitializer(rig.Services.GetRequiredService<ExternalApiRepository>(), rig.Services.GetRequiredService<ClusterRepository>()).InitializeAsync();

    [Fact]
    public async Task ARestart_ShouldAddNoTokensAndClearingShouldStillRemoveEverything()
    {
        using var rig = await DemoRig.StartAsync(mongo);
        await rig.Demo.SeedAsync(DemoRig.Who);

        await RestartAsync(rig);
        Assert.Equal(7, await rig.Count("apiTokens"));

        var result = await rig.Demo.ClearAsync(DemoRig.Who);

        Assert.Equal(0, await rig.Count("clusters"));
        Assert.Equal(0, await rig.Count("apiTokens"));
        Assert.Empty(result.Notes);
    }

    [Fact]
    public async Task ClearingADatabaseThatAlreadyHasTheExtraTokens_ShouldRemoveThemWithTheirClusters()
    {
        using var rig = await DemoRig.StartAsync(mongo);
        await rig.Demo.SeedAsync(DemoRig.Who);
        // As the seed made them before this fix: the demo cluster tokens weren't the cluster's own, so a restart added six.
        await rig.Database.GetCollection<BsonDocument>("apiTokens").UpdateManyAsync(FilterDefinition<BsonDocument>.Empty, Builders<BsonDocument>.Update.Unset("autoClusterId"));
        await RestartAsync(rig);
        Assert.Equal(13, await rig.Count("apiTokens"));

        var result = await rig.Demo.ClearAsync(DemoRig.Who);

        Assert.Equal(0, await rig.Count("clusters"));
        Assert.Equal(0, await rig.Count("apiTokens"));
        Assert.Empty(result.Notes);
        Assert.Contains("13 API tokens removed", result.Done);
    }

    [Fact]
    public async Task ARealTokenOnADemoCluster_ShouldStillKeepIt()
    {
        using var rig = await DemoRig.StartAsync(mongo);
        await rig.Demo.SeedAsync(DemoRig.Who);
        await RestartAsync(rig);
        await rig.Services.GetRequiredService<ExternalApiRepository>().CreateTokenAsync("North integration", ExternalTokenAccessMode.Read, ["demo-north"], false, false, "team@example.test");

        var result = await rig.Demo.ClearAsync(DemoRig.Who);

        Assert.Equal(1, await rig.Count("clusters"));
        Assert.Contains(result.Notes, note => note.StartsWith("Kept cluster demo-north: 1 real API tokens", StringComparison.Ordinal));
    }
}
