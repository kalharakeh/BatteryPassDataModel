using BatteryPassWeb.Dpp.Store;
using BatteryPassWeb.Services;
using BatteryPassWeb.Tests.Dpp;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Bson;
using MongoDB.Driver;

namespace BatteryPassWeb.Tests.Demo;

/// <summary>
/// Finding R03, the manager's point 7: the reset never leaves the demo half-made. A demo catalogue cluster that is real, or
/// that real data uses so clearing would keep it as real, stops the reset before anything changes, and the result says
/// which cluster is in the way and what to do. No demo membership is ever written into a cluster that isn't demo.
/// </summary>
[Collection(MongoCollection.Name)]
public sealed class DemoClusterInTheWayTests(MongoFixture mongo)
{
    private static FilterDefinition<BsonDocument> Eq(string field, BsonValue value) => Builders<BsonDocument>.Filter.Eq(field, value);

    /// <summary>What the demo north admin's "Add user" does: another demo account added, and a new (demo) account made.</summary>
    private static async Task DemoAdminAddsAndMakesAsync(ClusterRepository clusters)
    {
        await clusters.UpsertClusterMembershipAsync("demo.msa@example.test", "demo-north", AccessControlService.RoleNormalUser);
        await clusters.UpsertClusterMembershipAsync("demo.south.admin@example.test", "demo-north", AccessControlService.RoleNormalUser);
        await clusters.CreateAccountInClusterAsync("fresh.made@example.test", "Fresh", "hash", "demo-north", isDemo: true);
        await clusters.UpsertClusterMembershipAsync("fresh.made@example.test", "demo-north", AccessControlService.RoleNormalUser);
    }

    private static async Task<List<string>> MembershipsAsync(DemoRig rig) =>
        (await rig.All("clusterMemberships")).Select(membership => $"{membership["email"].AsString} {membership["clusterId"].AsString} {membership["role"].AsString}").Order().ToList();

    /// <summary>The manager's running app: demo use of the user pages never stops the reset, which puts back the catalogue's state.</summary>
    [Fact]
    public async Task DemoAccountsADemoAdminAddedOrMade_ShouldNotStopTheReset_WhichLeavesTheCataloguesState()
    {
        using var rig = await DemoRig.StartAsync(mongo);
        await rig.Demo.SeedAsync(DemoRig.Who);
        var catalogueState = await MembershipsAsync(rig);
        var clusters = rig.Services.GetRequiredService<ClusterRepository>();
        await DemoAdminAddsAndMakesAsync(clusters);
        await rig.Services.GetRequiredService<ExternalApiRepository>().CreateTokenAsync("North feed", ExternalTokenAccessMode.Read, ["demo-north"], false, false, "demo.north.admin@example.test", isDemo: true);

        var result = await rig.Demo.ResetAsync(DemoRig.Who);

        Assert.NotEmpty(result.Done);
        Assert.DoesNotContain(result.Notes, note => note.StartsWith("Nothing was changed", StringComparison.Ordinal));
        Assert.Equal(catalogueState, await MembershipsAsync(rig));
        Assert.Null(await clusters.GetUserByEmailAsync("fresh.made@example.test"));
        Assert.All(await rig.All("clusters"), cluster => Assert.True(cluster["isDemo"].AsBoolean));
    }

    /// <summary>The manager's database: a real test account was made a member of demo-north. Only the real ones are counted.</summary>
    [Fact]
    public async Task ARealMemberOfADemoCluster_ShouldStopTheReset_BeforeAnythingChanges()
    {
        using var rig = await DemoRig.StartAsync(mongo);
        await rig.Demo.SeedAsync(DemoRig.Who);
        var clusters = rig.Services.GetRequiredService<ClusterRepository>();
        await DemoAdminAddsAndMakesAsync(clusters);
        await clusters.UpsertUserAsync("tester@example.test", "A real tester", [AccessControlService.RoleNormalUser], "hash");
        await clusters.UpsertClusterMembershipAsync("tester@example.test", "demo-north", "member");
        var before = (await rig.All(DppStore.Packs)).Select(pack => pack["_id"].AsString).Order().ToList();

        var result = await rig.Demo.ResetAsync(DemoRig.Who);

        Assert.Empty(result.Done);
        Assert.Equal(
            "Nothing was changed: the demo's cluster demo-north is used by real data (1 real accounts), so the demo can't use it. Take the real accounts, batteries and API tokens out of it, or remove the cluster, then reset the demo data again.",
            Assert.Single(result.Notes));
        Assert.Equal(before, (await rig.All(DppStore.Packs)).Select(pack => pack["_id"].AsString).Order().ToList());
        Assert.True((await rig.All("clusters", Eq("clusterId", "demo-north"))).Single()["isDemo"].AsBoolean);
    }

    /// <summary>After an earlier clear kept demo-north and demo-south as real: the reset stops and seeds nothing into them.</summary>
    [Fact]
    public async Task ARealClusterWithTheDemosId_ShouldStopTheReset_AndGetNoDemoMembership()
    {
        using var rig = await DemoRig.StartAsync(mongo);
        var clusters = rig.Services.GetRequiredService<ClusterRepository>();
        await clusters.UpsertClusterAsync("demo-north", "Kept North");
        await clusters.UpsertClusterAsync("demo-south", "Kept South");
        await clusters.UpsertUserAsync("tester@example.test", "A real tester", [AccessControlService.RoleNormalUser], "hash");
        await clusters.UpsertClusterMembershipAsync("tester@example.test", "demo-south", "member");

        var result = await rig.Demo.ResetAsync(DemoRig.Who);

        Assert.Empty(result.Done);
        Assert.Equal(
            [
                "Nothing was changed: the demo's cluster demo-north is there as a real cluster, so the demo can't use it. Remove that cluster, then reset the demo data again.",
                "Nothing was changed: the demo's cluster demo-south is used by real data (1 real accounts), so the demo can't use it. Take the real accounts, batteries and API tokens out of it, or remove the cluster, then reset the demo data again.",
            ],
            result.Notes);
        Assert.Equal(0, await rig.Count(DppStore.Packs));
        Assert.Empty(await rig.All("clusterMemberships", Eq("isDemo", true)));
        Assert.Empty(await rig.All("clusters", Eq("clusterId", "demo-fleet")));
        Assert.Single(await rig.All("clusterMemberships", Eq("clusterId", "demo-south"))); // the tester's, as it was
    }

    [Fact]
    public async Task OnceTheWayIsClear_ShouldResetTheWholeDemo()
    {
        using var rig = await DemoRig.StartAsync(mongo);
        var clusters = rig.Services.GetRequiredService<ClusterRepository>();
        await clusters.UpsertClusterAsync("demo-north", "Kept North");
        Assert.NotEmpty((await rig.Demo.ResetAsync(DemoRig.Who)).Notes);

        await clusters.DeleteClusterAsync("demo-north");
        var result = await rig.Demo.ResetAsync(DemoRig.Who);

        Assert.NotEmpty(result.Done);
        Assert.All(await rig.All("clusters"), cluster => Assert.True(cluster["isDemo"].AsBoolean));
        Assert.Equal(["demo-fleet", "demo-north", "demo-south"], (await rig.All("clusters")).Select(cluster => cluster["clusterId"].AsString).Order());
        Assert.NotEmpty(await rig.All(DppStore.Packs, Eq("ClusterId", "demo-north")));
    }
}
