using System.Text.Json.Nodes;
using BatteryPassWeb.Dpp.Editing;
using BatteryPassWeb.Dpp.Identifiers;
using BatteryPassWeb.Dpp.Store;
using BatteryPassWeb.Services;

namespace BatteryPassWeb.Tests.Dpp;

/// <summary>
/// What the old battery pages enforced and the battery pages restore (design 14, parity check of 29 Oct 2026): a
/// battery's serial number, manufacturing date and warranty (#11, #17, #19) are fixed once its passport is published,
/// no two batteries share a serial number, and a cluster that is deleted counts and releases its batteries.
/// </summary>
[Collection(MongoCollection.Name)]
public sealed class PackRulesTests(MongoFixture mongo)
{
    private static readonly ChangeSender Team = PublicationTesting.Team;

    [Theory]
    [InlineData("batterySerialNumber", "\"VPC8C05-6790\"")]
    [InlineData("manufacturingDate", "\"2026-08\"")]
    [InlineData("warrantyPeriod", "\"P6Y\"")]
    public async Task ADraft_ShouldLetTheTeamCorrectItsFixedValues(string key, string json)
    {
        var rig = await PublicationTesting.RigAsync(mongo);

        var result = await rig.Pipeline.SaveAsync(Team, Change(rig.PackId, key, json));

        Assert.True(result.Saved);
        Assert.Equal(KeyOutcome.Saved, result.Keys.Single().Outcome);
    }

    [Theory]
    [InlineData("batterySerialNumber", "\"VPC8C05-6790\"")]
    [InlineData("manufacturingDate", "\"2026-08\"")]
    [InlineData("warrantyPeriod", "\"P6Y\"")]
    public async Task OncePublished_FixedValuesShouldBeRefusedAndNoVersionMade(string key, string json)
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo);

        var result = await rig.Pipeline.SaveAsync(Team, Change(rig.PackId, key, json));

        Assert.False(result.Saved);
        var refused = Assert.Single(result.Keys);
        Assert.Equal(KeyOutcome.Rejected, refused.Outcome);
        Assert.Equal(SavePipeline.FixedOncePublished, refused.Message);
        Assert.Equal(1, await rig.Store.GetLatestVersionNumberAsync(rig.PackId));
    }

    [Fact]
    public async Task OncePublished_AFixedValueShouldNotBeRemovedEitherEvenIfConditional()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo);

        var result = await rig.Pipeline.SaveAsync(Team, new ChangeSet(TargetKind.Pack, rig.PackId, new Dictionary<string, JsonNode?>(), "form", Removals: ["warrantyPeriod"]));

        Assert.False(result.Saved);
        Assert.Equal(SavePipeline.FixedOncePublished, result.Keys.Single().Message);
        Assert.True((await rig.Store.GetAsync<BatteryPack>(rig.PackId))!.Values.ContainsKey("warrantyPeriod"));
    }

    [Fact]
    public async Task OncePublished_TheServiceProviderMayStillChangeAndPublishesAVersion()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo);

        var result = await rig.Pipeline.SaveAsync(Team, Change(rig.PackId, "thirdPartyServiceProvider", "\"Other Hosting AB\""));

        Assert.True(result.Saved);
        Assert.Equal(2, await rig.Store.GetLatestVersionNumberAsync(rig.PackId));
    }

    [Fact]
    public async Task OnlyAPacksOwnFixedValues_ShouldCount()
    {
        var fixedOnes = BatteryPassWeb.Dpp.Catalog.AttributeCatalog.All.Where(SavePipeline.IsFixedPackValue).Select(attribute => attribute.Number);

        Assert.Equal([11, 17, 19], fixedOnes);
    }

    [Theory]
    [InlineData("VPC9-7000")]
    [InlineData("vpc9-7000")]
    [InlineData("  VPC9-7000 ")]
    public async Task ACorrection_ShouldRefuseASerialNumberAnotherBatteryHas(string typed)
    {
        var rig = await PublicationTesting.RigAsync(mongo);
        await rig.Store.InsertAsync(OtherPack(rig, "VPC9-7000"));

        var result = await rig.Pipeline.SaveAsync(Team, Change(rig.PackId, "batterySerialNumber", JsonValue.Create(typed).ToJsonString()));

        Assert.False(result.Saved);
        Assert.Equal(SavePipeline.SerialNumberTaken, result.Keys.Single().Message);
    }

    [Fact]
    public async Task ACorrection_MayKeepTheBatterysOwnSerialNumberInAnotherCase()
    {
        var rig = await PublicationTesting.RigAsync(mongo);

        var result = await rig.Pipeline.SaveAsync(Team, Change(rig.PackId, "batterySerialNumber", "\"vpc8c05-6788\""));

        Assert.True(result.Saved);
    }

    [Theory]
    [InlineData("VPC8C05-6788")]
    [InlineData("vpc8c05-6788")]
    [InlineData(" VPC8C05-6788  ")]
    public async Task Creation_ShouldRefuseASerialNumberAnotherBatteryHas(string typed)
    {
        var rig = await PublicationTesting.RigAsync(mongo);
        var service = new PackCreationService(rig.Store, TestDemo.For(rig), rig.Pipeline, new TestIdentifierScheme(), DocumentTesting.Links, new ApplicationAuditService(rig.Context), rig.Clock);

        var result = await service.CreateAsync(Team, new NewPack(
            rig.Seed.Sources.Batch.Id,
            rig.Seed.Sources.Operator.Id,
            null,
            new Dictionary<string, JsonNode?> { ["batterySerialNumber"] = JsonValue.Create(typed) }));

        Assert.Null(result.Pack);
        var serial = result.Keys.Single(key => key.Key == "batterySerialNumber");
        Assert.Equal(KeyOutcome.Rejected, serial.Outcome);
        Assert.Equal(SavePipeline.SerialNumberTaken, serial.Message);
        Assert.Single(await rig.Store.ListAsync<BatteryPack>());
    }

    [Fact]
    public async Task APack_ShouldMoveBetweenClustersAndLeaveWhenItsClusterIsCleared()
    {
        var rig = await PublicationTesting.RigAsync(mongo);
        await rig.Store.InsertAsync(OtherPack(rig, "VPC9-7001"));

        Assert.True(await rig.Store.SetPackClusterAsync(rig.PackId, "cluster-north"));
        Assert.Equal(1, await rig.Store.CountPacksInClusterAsync("cluster-north"));
        Assert.Equal(1, await rig.Store.CountPacksInClusterAsync("cluster-test"));
        Assert.Equal(1, await rig.Store.ClearClusterAsync("cluster-test"));
        Assert.Equal(0, await rig.Store.CountPacksInClusterAsync("cluster-test"));
        Assert.True(await rig.Store.SetPackClusterAsync(rig.PackId, null));
        Assert.Null((await rig.Store.GetAsync<BatteryPack>(rig.PackId))!.ClusterId);
        Assert.False(await rig.Store.SetPackClusterAsync("no-such-pack", "cluster-north"));
    }

    [Fact]
    public void DeletingACluster_ShouldCountItsBatteriesAndForceDeleteShouldReleaseThem()
    {
        var controller = File.ReadAllText(DppTestFiles.RepoFile("web", "Controllers", "AdminController.cs"));

        Assert.Contains("_dppStore.CountPacksInClusterAsync(clusterId", controller, StringComparison.Ordinal);
        Assert.Contains("batteryCount > 0", controller, StringComparison.Ordinal);
        Assert.Contains("_dppStore.ClearClusterAsync(clusterId", controller, StringComparison.Ordinal);
    }

    private static ChangeSet Change(string packId, string key, string json) =>
        new(TargetKind.Pack, packId, new Dictionary<string, JsonNode?> { [key] = JsonNode.Parse(json) }, "form");

    /// <summary>A second draft pack of the seed's batch, in the seed's cluster, with its own serial number.</summary>
    private static BatteryPack OtherPack(PublicationRig rig, string serialNumber)
    {
        var pack = new BatteryPack
        {
            Id = $"pack-{serialNumber}",
            BatchId = rig.Seed.Sources.Batch.Id,
            OperatorId = rig.Seed.Sources.Operator.Id,
            ClusterId = rig.Seed.Sources.Pack.ClusterId,
        };
        pack.Values["batterySerialNumber"] = DppStoreTesting.Value(JsonValue.Create(serialNumber));
        pack.Values["uniqueProductIdentifier"] = DppStoreTesting.Value(JsonValue.Create($"urn:test:battery:{serialNumber}"));
        return pack;
    }
}
