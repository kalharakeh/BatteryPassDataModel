using System.Security.Claims;
using System.Text.Json.Nodes;
using BatteryPassWeb.Controllers;
using BatteryPassWeb.Dpp.Access;
using BatteryPassWeb.Dpp.Api;
using BatteryPassWeb.Dpp.Editing;
using BatteryPassWeb.Dpp.Identifiers;
using BatteryPassWeb.Dpp.Store;
using BatteryPassWeb.Services;
using BatteryPassWeb.Tests.Demo;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Bson;
using MongoDB.Driver;

namespace BatteryPassWeb.Tests.Dpp;

/// <summary>
/// Finding R03, the manager's point 4: a demo identity can't learn that real things exist. Outside the demo it gets exactly
/// what an unknown ID gets, and on create the demo check comes before any look-up of real records.
/// </summary>
[Collection(MongoCollection.Name)]
public sealed class DemoUnknownAnswersTests(MongoFixture mongo)
{
    private static ChangeSender DemoTeam => ChangeSender.Team("api-token:demo") with { DemoOnly = true };

    private static NewPack Request(string batchId, string operatorId, string serial) =>
        new(batchId, operatorId, null, new Dictionary<string, JsonNode?> { ["batterySerialNumber"] = serial }, FromPool: true);

    /// <summary>The demo data; DEMO-C7M-0001's batch, model and operator made real, and a real battery made on them.</summary>
    private static async Task<(DemoRig Rig, BatteryPack RealLike, BatteryPack DemoLike)> SeededAsync(MongoFixture mongo)
    {
        var rig = await DemoRig.StartAsync(mongo, dunsIds: true);
        await rig.Demo.SeedAsync(DemoRig.Who);
        var realLike = (await rig.Store.FindPacksBySerialNumberAsync("DEMO-C7M-0001")).Single();
        var demoLike = (await rig.Store.FindPacksBySerialNumberAsync("DEMO-CORE-0001")).Single();
        var batch = (await rig.Store.GetAsync<ProductionBatch>(realLike.BatchId))!;
        foreach (var (collection, id) in new[] { (DppStore.Batches, batch.Id), (DppStore.Models, batch.ModelId), (DppStore.Operators, realLike.OperatorId) })
        {
            await rig.Database.GetCollection<BsonDocument>(collection).UpdateOneAsync(Builders<BsonDocument>.Filter.Eq("_id", id), Builders<BsonDocument>.Update.Set("IsDemo", false));
        }

        await rig.Services.GetRequiredService<BatteryIdPool>().ReleaseAsync(DemoRig.Who, new ReleaseRequest("PO 1", null, [], "VPC-12221"));
        Assert.NotNull((await rig.Services.GetRequiredService<PackCreationService>().CreateAsync(ChangeSender.Team(DemoRig.Who), Request(realLike.BatchId, realLike.OperatorId, "VPC-12221"))).Pack);
        return (rig, realLike, demoLike);
    }

    [Fact]
    public async Task OnCreate_ADemoTokenShouldGetForRealRecordsWhatItGetsForUnknownOnes()
    {
        var (rig, realLike, demoLike) = await SeededAsync(mongo);
        using var _ = rig;
        var creation = rig.Services.GetRequiredService<PackCreationService>();
        async Task<string?> ProblemAsync(NewPack request) => (await creation.CreateAsync(DemoTeam, request)).Problem;

        // A real batch, a real operator, a serial a real battery has: each as if it didn't exist.
        Assert.Equal(await ProblemAsync(Request("no-such-batch", demoLike.OperatorId, "DEMO-SPARE-0001")).ContinueWith(t => t.Result?.Replace("no-such-batch", "X")),
            (await ProblemAsync(Request(realLike.BatchId, demoLike.OperatorId, "DEMO-SPARE-0001")))?.Replace(realLike.BatchId, "X"));
        Assert.Equal(await ProblemAsync(Request(demoLike.BatchId, "no-such-operator", "DEMO-SPARE-0001")).ContinueWith(t => t.Result?.Replace("no-such-operator", "X")),
            (await ProblemAsync(Request(demoLike.BatchId, realLike.OperatorId, "DEMO-SPARE-0001")))?.Replace(realLike.OperatorId, "X"));
        Assert.Equal(await ProblemAsync(Request(demoLike.BatchId, demoLike.OperatorId, "VPC-99999")),
            await ProblemAsync(Request(demoLike.BatchId, demoLike.OperatorId, "VPC-12221")));
        Assert.Empty(await rig.Store.FindPacksBySerialNumberAsync("VPC-99999"));
    }

    [Fact]
    public async Task OnCreate_ADemoTokenShouldGetForARealClusterWhatItGetsForAnUnknownOne()
    {
        var (rig, _, demoLike) = await SeededAsync(mongo);
        using var __ = rig;
        await rig.Services.GetRequiredService<ClusterRepository>().UpsertClusterAsync("real-customer", "Real customer");
        var api = rig.Services.GetRequiredService<BatteryApi>();
        var values = new Dictionary<string, JsonNode?> { ["batterySerialNumber"] = "DEMO-SPARE-0001" };

        var unknown = (await api.CreateAsync(DemoTeam, demoLike.BatchId, demoLike.OperatorId, "no-such-cluster", values)).Problem;
        var real = (await api.CreateAsync(DemoTeam, demoLike.BatchId, demoLike.OperatorId, "real-customer", values)).Problem;

        Assert.Equal(unknown?.Replace("no-such-cluster", "X"), real?.Replace("real-customer", "X"));
    }

    [Fact]
    public async Task ADemoAuthoritysDirectMark_ShouldGetWhatAnUnknownPassportGets()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo).WithClustersAsync();
        var clusters = new ClusterRepository(rig.Context);
        await clusters.UpsertUserAsync("demo.msa@example.test", "Demo", [AccessControlService.RoleMarketSurveillanceAuthority], "hash");
        await rig.Context.Database!.GetCollection<BsonDocument>("users").UpdateOneAsync(
            Builders<BsonDocument>.Filter.Eq("email", "demo.msa@example.test"), Builders<BsonDocument>.Update.Set(ClusterRepository.DemoMark, true));
        var controller = new PassportController(new DppReaders(clusters), PassportPagesTests.Pages(rig));
        var http = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Email, "demo.msa@example.test"), new Claim(ClaimTypes.Role, AccessControlService.RoleMarketSurveillanceAuthority)], "test")),
        };
        controller.ControllerContext = new ControllerContext { HttpContext = http };
        controller.TempData = new TempDataDictionary(http, new NoTempData());

        var real = await controller.MarkInvalid(Uri.EscapeDataString(PassportPagesTests.Identifier(rig)), "a reason", PassportMarkTests.Service(rig), CancellationToken.None, TestDemo.For(rig));
        var unknown = await controller.MarkInvalid("no-such-passport", "a reason", PassportMarkTests.Service(rig), CancellationToken.None, TestDemo.For(rig));

        Assert.IsType<NotFoundResult>(unknown);
        Assert.IsType<NotFoundResult>(real);
        Assert.Equal(DppStatuses.Active, await rig.Store.PassportStatusAsync(rig.PackId));
    }

    private sealed class NoTempData : ITempDataProvider
    {
        public IDictionary<string, object> LoadTempData(HttpContext context) => new Dictionary<string, object>();

        public void SaveTempData(HttpContext context, IDictionary<string, object> values)
        {
        }
    }
}
