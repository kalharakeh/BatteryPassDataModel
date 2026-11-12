using System.Text.Json.Nodes;
using BatteryPassWeb.Dpp.Demo;
using BatteryPassWeb.Dpp.Editing;
using BatteryPassWeb.Dpp.Identifiers;
using BatteryPassWeb.Dpp.Pages;
using BatteryPassWeb.Dpp.Store;
using BatteryPassWeb.Dpp.Values;
using BatteryPassWeb.Services;
using BatteryPassWeb.Tests.Demo;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Bson;
using MongoDB.Driver;

namespace BatteryPassWeb.Tests.Dpp;

/// <summary>
/// Round two D: a battery with a real ID doesn't go into a demo customer, when it is created, when the team changes its
/// cluster and when its cluster admin hands it over; the forms mark the demo customers. A test battery may go anywhere.
/// </summary>
[Collection(MongoCollection.Name)]
public sealed class RealBatteryDemoCustomerTests(MongoFixture mongo)
{
    private const string Real = "https://www.sib-scania-eubp.eu/25S/UN778474481-VPC-0001";
    private const string Test = "https://www.sib-scania-eubp.eu/25S/UN778474481-TEST-0001";

    private static async Task<PublicationRig> RigAsync(MongoFixture mongo, string identifier)
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo).WithClustersAsync();
        await rig.Store.SetValuesAsync<BatteryPack>(
            rig.PackId,
            new Dictionary<string, AttributeValue> { ["uniqueProductIdentifier"] = new() { Value = JsonValue.Create(identifier)!, GeneratedAtUtc = rig.Clock.Now.UtcDateTime, Source = "system" } });
        await rig.Context.Database!.GetCollection<BsonDocument>("clusters").UpdateOneAsync(
            Builders<BsonDocument>.Filter.Eq("clusterId", BatteryTesting.South),
            Builders<BsonDocument>.Update.Set(ClusterRepository.DemoMark, true)); // "Customer South" is a demo customer
        return rig;
    }

    [Theory]
    [InlineData(Real, DemoDataRule.DemoCustomerHandOver)]
    [InlineData(Test, null)]
    public async Task AHandOverToADemoCustomer_ShouldBeRefused_OnlyForARealBattery(string identifier, string? problem)
    {
        var rig = await RigAsync(mongo, identifier);
        var northAdmin = ChangeSender.ClusterAdmin("north-admin@example.com", BatteryTesting.North);

        var asked = await HandOverTests.Service(rig).AskAsync(northAdmin, rig.PackId, BatteryTesting.South, confirm: true);

        Assert.Equal(problem, asked.Problem);
    }

    [Theory]
    [InlineData(Real, DemoDataRule.DemoCustomer)]
    [InlineData(Test, null)]
    public async Task TheTeamsClusterChange_ShouldRefuseADemoCustomer_OnlyForARealBattery(string identifier, string? problem)
    {
        var rig = await RigAsync(mongo, identifier);

        Assert.Equal(problem?.TrimEnd('.'), await BatteryTesting.Pages(rig).SetClusterAsync(PublicationTesting.Team.Who, rig.PackId, BatteryTesting.South));
        Assert.Equal(problem is null ? BatteryTesting.South : BatteryTesting.North, (await rig.Store.GetAsync<BatteryPack>(rig.PackId))!.ClusterId);
    }

    [Fact]
    public async Task TheClusterChoices_ShouldMarkTheDemoCustomers()
    {
        var rig = await RigAsync(mongo, Real);

        var choices = await BatteryTesting.Pages(rig).ClustersAsync();

        Assert.True(choices.Single(choice => choice.Id == BatteryTesting.South).IsDemo);
        Assert.False(choices.Single(choice => choice.Id == BatteryTesting.North).IsDemo);
        foreach (var view in new[] { "NewBattery.cshtml", "Battery.cshtml" })
        {
            Assert.Contains("(cluster.IsDemo ? $\"{cluster.Name} (demo customer: test IDs only)\" : cluster.Name)", File.ReadAllText(DppTestFiles.RepoFile("web", "Views", "Batteries", view)), StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task CreatingARealBatteryInADemoCustomer_ShouldBeRefused_AndItsIdStayAvailable_WhileATestOneGoesAnywhere()
    {
        using var rig = await DemoRig.StartAsync(mongo, dunsIds: true);
        await rig.Demo.SeedAsync(DemoRig.Who);
        var pages = rig.Services.GetRequiredService<ProductDataPages>();
        var (familyId, _) = await pages.CopyFamilyAsRealAsync(DemoRig.Who, (await rig.Store.ListAsync<BatteryFamily>()).Single(family => family.Name == "Compact").Id, "Compact S");
        var (operatorId, _) = await pages.CopyOperatorAsRealAsync(DemoRig.Who, (await rig.Store.ListAsync<EconomicOperator>()).Single(item => item.Name == "Scania Industrial Batteries (demo)").Id, "Scania Industrial Batteries");
        var batch = (await rig.Store.ListBatchesAsync((await rig.Store.ListModelsAsync(familyId!)).First().Id)).Single();
        var demoCluster = DemoCatalogue.Load().Clusters[0].Id;
        var pool = rig.Services.GetRequiredService<BatteryIdPool>();
        await pool.ReleaseAsync(DemoRig.Who, new ReleaseRequest("PO 1", null, [], "VPC-12221"));
        await pool.ReleaseAsync(DemoRig.Who, new ReleaseRequest("Try-out", null, [], "TRY-0001", IsDemo: true));
        var creation = rig.Services.GetRequiredService<PackCreationService>();
        NewPack Request(string serial, string? cluster) => new(batch.Id, operatorId!, cluster, new Dictionary<string, JsonNode?> { ["batterySerialNumber"] = serial }, FromPool: true);

        var refused = await creation.CreateAsync(ChangeSender.Team(DemoRig.Who), Request("VPC-12221", demoCluster));
        var test = await creation.CreateAsync(ChangeSender.Team(DemoRig.Who), Request("TRY-0001", demoCluster));
        var withoutCustomer = await creation.CreateAsync(ChangeSender.Team(DemoRig.Who), Request("VPC-12221", null));

        Assert.Equal(DemoDataRule.DemoCustomer, refused.Problem);
        Assert.Null(test.Problem);
        Assert.Null(withoutCustomer.Problem); // the refused ID was available again
    }
}
