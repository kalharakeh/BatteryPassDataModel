using System.Text.Json.Nodes;
using BatteryPassWeb.Dpp.Documents;
using BatteryPassWeb.Dpp.Editing;
using BatteryPassWeb.Dpp.Identifiers;
using BatteryPassWeb.Dpp.Store;
using BatteryPassWeb.Services;

namespace BatteryPassWeb.Tests.Dpp;

[Collection(MongoCollection.Name)]
public sealed class PackCreationServiceTests(MongoFixture mongo)
{
    private static readonly ChangeSender Team = ChangeSender.Team("team@example.com");

    [Fact]
    public async Task Team_ShouldCreateADraftPackWithATestIdentifierAndStatusOriginal()
    {
        var (store, service, seed) = await ServiceAsync();

        var result = await service.CreateAsync(Team, NewPack(seed, ("batterySerialNumber", "\"VPC9-0001\""), ("manufacturingDate", "\"2026-10\""), ("warrantyPeriod", "\"P8Y\"")));

        Assert.Null(result.Problem);
        var pack = await store.GetAsync<BatteryPack>(result.Pack!.Id);
        Assert.NotNull(pack);
        var identifier = pack.Values["uniqueProductIdentifier"].Value.GetValue<string>();
        Assert.StartsWith(TestIdentifierScheme.Prefix, identifier, StringComparison.Ordinal);
        Assert.Equal("original", pack.Values["batteryStatus"].Value["batteryStatusValues"]!.GetValue<string>());
        Assert.Equal("VPC9-0001", pack.Values["batterySerialNumber"].Value.GetValue<string>());
        Assert.Equal("cluster-test", pack.ClusterId);
        Assert.Equal("system", pack.Values["uniqueProductIdentifier"].Source);
        Assert.Equal("form", pack.Values["warrantyPeriod"].Source);
        Assert.Equal(DppStoreTesting.Now.UtcDateTime, pack.Values["manufacturingDate"].GeneratedAtUtc);
        Assert.Equal(DocumentTesting.Links.AccidentPageUrl(pack.Id), pack.Values["informationOnAccidents"].Value["url"]!.GetValue<string>());
    }

    [Fact]
    public async Task WithoutAnAppBaseUrl_PackCreationShouldStopWithAClearMessage()
    {
        var (store, context, seed) = await DppStoreTesting.SeededStoreAsync(mongo);
        var service = new PackCreationService(
            store,
            TestDemo.For(store, context),
            SavePipelineTesting.Pipeline(store, context),
            new TestIdentifierScheme(),
            DocumentTesting.NoLinks,
            new ApplicationAuditService(context),
            new FixedTimeProvider(DppStoreTesting.Now));

        var result = await service.CreateAsync(Team, NewPack(seed, ("batterySerialNumber", "\"VPC9-0006\"")));

        Assert.Equal(DppLinks.NotConfigured, result.Problem);
        Assert.Empty(await store.FindPacksBySerialNumberAsync("VPC9-0006"));
    }

    [Fact]
    public async Task EachPack_ShouldGetItsOwnIdentifier()
    {
        var (_, service, seed) = await ServiceAsync();

        var first = await service.CreateAsync(Team, NewPack(seed, ("batterySerialNumber", "\"VPC9-0002\"")));
        var second = await service.CreateAsync(Team, NewPack(seed, ("batterySerialNumber", "\"VPC9-0012\"")));

        Assert.NotEqual(
            first.Pack!.Values["uniqueProductIdentifier"].Value.GetValue<string>(),
            second.Pack!.Values["uniqueProductIdentifier"].Value.GetValue<string>());
    }

    [Fact]
    public async Task OnlyTheTeam_ShouldCreatePacks()
    {
        var (store, service, seed) = await ServiceAsync();

        var result = await service.CreateAsync(ChangeSender.ClusterAdmin("admin@example.com", "cluster-test"), NewPack(seed, ("batterySerialNumber", "\"VPC9-0003\"")));

        Assert.Contains("only the team", result.Problem, StringComparison.Ordinal);
        Assert.Empty(await store.FindPacksBySerialNumberAsync("VPC9-0003"));
    }

    [Theory]
    [InlineData("no-such-batch", "operator-test", "batch")]
    [InlineData("batch-test-2026", "no-such-operator", "economic operator")]
    public async Task MissingBatchOrOperator_ShouldStopCreation(string batchId, string operatorId, string expected)
    {
        var (_, service, _) = await ServiceAsync();

        var result = await service.CreateAsync(Team, new NewPack(batchId, operatorId, null, Values(("batterySerialNumber", "\"VPC9-0004\""))));

        Assert.Null(result.Pack);
        Assert.Contains(expected, result.Problem, StringComparison.Ordinal);
    }

    [Fact]
    public async Task MissingSerialNumber_ShouldStopCreation()
    {
        var (_, service, seed) = await ServiceAsync();

        var result = await service.CreateAsync(Team, NewPack(seed, ("warrantyPeriod", "\"P8Y\"")));

        Assert.Null(result.Pack);
        Assert.Contains("#11", result.Problem, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("manufacturingDate", "\"2026-10-14\"")] // decision 18
    [InlineData("capacityFade", "4")] // re-stated values only through the status change
    [InlineData("stateOfChargeSoC", "120")]
    public async Task AnyRejectedValue_ShouldSaveNothing(string key, string json)
    {
        var (store, service, seed) = await ServiceAsync();

        var result = await service.CreateAsync(Team, NewPack(seed, ("batterySerialNumber", "\"VPC9-0005\""), (key, json)));

        Assert.Null(result.Pack);
        Assert.Equal(KeyOutcome.Rejected, result.Keys.Single(k => k.Key == key).Outcome);
        Assert.Empty(await store.FindPacksBySerialNumberAsync("VPC9-0005"));
    }

    private async Task<(DppStore Store, PackCreationService Service, SeedPassport Seed)> ServiceAsync()
    {
        var (store, context, seed) = await DppStoreTesting.SeededStoreAsync(mongo);
        var service = new PackCreationService(
            store,
            TestDemo.For(store, context),
            SavePipelineTesting.Pipeline(store, context),
            new TestIdentifierScheme(),
            DocumentTesting.Links,
            new ApplicationAuditService(context),
            new FixedTimeProvider(DppStoreTesting.Now));
        return (store, service, seed);
    }

    private static NewPack NewPack(SeedPassport seed, params (string Key, string Json)[] values) =>
        new(seed.Sources.Batch.Id, seed.Sources.Operator.Id, "cluster-test", Values(values));

    private static Dictionary<string, JsonNode?> Values(params (string Key, string Json)[] values) =>
        values.ToDictionary(v => v.Key, v => JsonNode.Parse(v.Json), StringComparer.Ordinal);
}
