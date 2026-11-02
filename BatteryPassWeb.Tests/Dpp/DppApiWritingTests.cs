using System.Reflection;
using System.Text.Json;
using BatteryPassWeb.Configuration;
using BatteryPassWeb.Controllers;
using BatteryPassWeb.Dpp.Catalog;
using BatteryPassWeb.Dpp.Store;
using BatteryPassWeb.Dpp.Values;
using BatteryPassWeb.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace BatteryPassWeb.Tests.Dpp;

/// <summary>The external API's writing endpoints (step 3c-2), called with real tokens of each kind.</summary>
[Collection(MongoCollection.Name)]
public sealed class DppApiWritingTests(MongoFixture mongo)
{
    [Fact]
    public void TheWritingEndpoints_ShouldReplaceTheOldControllerWithTheirLimits()
    {
        var type = typeof(DppApiController);
        var posts = type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(action => action.GetCustomAttribute<HttpPostAttribute>() is not null)
            .ToDictionary(action => action.GetCustomAttribute<HttpPostAttribute>()!.Template!, action => action.GetCustomAttribute<EnableRateLimitingAttribute>()?.PolicyName);

        Assert.Equal(
            new Dictionary<string, string?>
            {
                ["batteries"] = SecurityRateLimitPolicyNames.ExternalApiWrite,
                ["batteries/{id}/live-values"] = SecurityRateLimitPolicyNames.ExternalApiWrite,
                ["batteries/{id}/publish"] = SecurityRateLimitPolicyNames.ExternalApiLifecycle,
                ["uploads"] = SecurityRateLimitPolicyNames.ExternalApiWrite,
            },
            posts);
        Assert.Null(type.Assembly.GetType("BatteryPassWeb.Controllers.ExternalApiController"));
        Assert.Null(type.Assembly.GetType("BatteryPassWeb.Services.BatteryCreationService"));
        Assert.Null(type.Assembly.GetType("BatteryPassWeb.Services.BatteryTelemetryRepository"));
    }

    [Fact]
    public async Task ABatterySystem_ShouldWriteItsClustersLiveValues()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo).WithClustersAsync();
        var tokens = await TokensAsync(rig);

        var answer = await Write(rig, tokens["north-write"], Serial(rig), """{ "measuredAt": "2026-10-29T07:30:00+01:00", "values": { "stateOfChargeSoC": 64, "batteryStatus": "waste" } }""");

        var data = Data(answer, StatusCodes.Status200OK);
        Assert.Equal(PassportPagesTests.Identifier(rig), data.GetProperty("batteryId").GetString());
        Assert.True(data.GetProperty("saved").GetBoolean());
        Assert.Equal("2026-10-29T06:30:00Z", data.GetProperty("measuredAt").GetString());
        var values = data.GetProperty("values").EnumerateArray().ToArray();
        Assert.Equal(["saved", "rejected"], values.Select(value => value.GetProperty("outcome").GetString()));
        Assert.Equal("#22", values[1].GetProperty("attribute").GetString());
        var stored = (await rig.Store.GetAsync<BatteryPack>(rig.PackId))!.Values["stateOfChargeSoC"];
        Assert.Equal(64, stored.Value!["percentageValue"]!.GetValue<int>());
        Assert.Equal(new DateTime(2026, 10, 29, 6, 30, 0, DateTimeKind.Utc), stored.GeneratedAtUtc);
    }

    [Theory]
    [InlineData("north-read", StatusCodes.Status403Forbidden, "Token does not have write access.")]
    [InlineData("south-write", StatusCodes.Status403Forbidden, DppApiController.NotYours)]
    [InlineData(null, StatusCodes.Status401Unauthorized, null)]
    public async Task OtherTokens_ShouldChangeNothing(string? token, int status, string? message)
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo).WithClustersAsync();
        var tokens = await TokensAsync(rig);

        var answer = Assert.IsType<ObjectResult>(await Write(rig, token is null ? null : tokens[token], Serial(rig), """{ "values": { "stateOfChargeSoC": 10 } }"""));

        Assert.Equal(status, answer.StatusCode);
        if (message is not null)
        {
            Assert.Equal(message, DppApiControllerTests.Json(answer).GetProperty("message").GetString());
        }

        Assert.Equal(76, (await rig.Store.GetAsync<BatteryPack>(rig.PackId))!.Values["stateOfChargeSoC"].Value!["percentageValue"]!.GetValue<int>());
    }

    [Fact]
    public async Task ADraft_ShouldTakeLiveValuesFromTheTeamOnly()
    {
        var rig = await PublicationTesting.RigAsync(mongo).WithClustersAsync();
        var tokens = await TokensAsync(rig);

        var cluster = Assert.IsType<ObjectResult>(await Write(rig, tokens["north-write"], Serial(rig), """{ "values": { "temperatureInformation": 22 } }"""));
        var team = await Write(rig, tokens["global-write"], Serial(rig), """{ "values": { "temperatureInformation": 22 } }""");

        Assert.Equal(StatusCodes.Status404NotFound, cluster.StatusCode);
        Assert.Equal(DppApiController.NoBattery, DppApiControllerTests.Json(cluster).GetProperty("message").GetString());
        Assert.True(Data(team, StatusCodes.Status200OK).GetProperty("saved").GetBoolean());
    }

    [Theory]
    [InlineData("""{ "points": [ { "measuredAt": "2026-10-29T07:00:00Z", "currentChargeLevelPct": 64 } ] }""", DppApiController.LiveValuesBody)]
    [InlineData("""{ "values": {} }""", DppApiController.LiveValuesBody)]
    [InlineData("""{ "measuredAt": "yesterday", "values": { "stateOfChargeSoC": 64 } }""", DppApiController.LiveValuesBody)]
    [InlineData("""{ "measuredAt": "2026-10-29T09:00:00Z", "values": { "stateOfChargeSoC": 64 } }""", "measuredAt lies in the future.")]
    public async Task ABodyThatIsNotOneMeasurement_ShouldBeRefused(string body, string message)
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo).WithClustersAsync();
        var tokens = await TokensAsync(rig);

        var answer = Assert.IsType<ObjectResult>(await Write(rig, tokens["north-write"], Serial(rig), body));

        Assert.Equal(StatusCodes.Status400BadRequest, answer.StatusCode);
        Assert.Equal(message, DppApiControllerTests.Json(answer).GetProperty("message").GetString());
    }

    [Fact]
    public async Task NothingToSave_ShouldBeUnprocessableAndAPreviewShouldSaveNothing()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo).WithClustersAsync();
        var tokens = await TokensAsync(rig);

        var refused = Assert.IsType<ObjectResult>(await Write(rig, tokens["north-write"], Serial(rig), """{ "values": { "batteryMass": 500, "stateOfChargeSoC": 140 } }"""));
        var preview = await Write(rig, tokens["north-write"], Serial(rig), """{ "values": { "stateOfChargeSoC": 12 } }""", preview: true);

        Assert.Equal(StatusCodes.Status422UnprocessableEntity, refused.StatusCode);
        var data = Data(preview, StatusCodes.Status200OK);
        Assert.True(data.GetProperty("preview").GetBoolean());
        Assert.False(data.GetProperty("saved").GetBoolean());
        Assert.Equal(76, (await rig.Store.GetAsync<BatteryPack>(rig.PackId))!.Values["stateOfChargeSoC"].Value!["percentageValue"]!.GetValue<int>());
    }

    [Fact]
    public async Task TheHistory_ShouldBeCutToTheToken()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo).WithClustersAsync();
        var tokens = await TokensAsync(rig);
        await Write(rig, tokens["north-write"], Serial(rig), """{ "measuredAt": "2026-10-29T07:00:00Z", "values": { "stateOfChargeSoC": 64, "timeSpentInExtremeTemperaturesBelowBoundary": 30 } }""");

        var own = Data(await DppApiControllerTests.Controller(rig, tokens["north-read"]).LiveValueHistory(Serial(rig), 24, CancellationToken.None), StatusCodes.Status200OK);
        var other = Data(await DppApiControllerTests.Controller(rig, tokens["south-write"]).LiveValueHistory(Serial(rig), null, CancellationToken.None), StatusCodes.Status200OK);
        var tooLong = Assert.IsType<ObjectResult>(await DppApiControllerTests.Controller(rig, tokens["north-read"]).LiveValueHistory(Serial(rig), 169, CancellationToken.None));

        var values = own.GetProperty("values").EnumerateArray().ToArray();
        Assert.Equal(["#63", "E2"], values.Select(value => value.GetProperty("attribute").GetString()).Order());
        Assert.All(values, value => Assert.Equal("2026-10-29T07:00:00Z", value.GetProperty("measuredAt").GetString()));
        Assert.All(values, value => Assert.Equal("battery system", value.GetProperty("source").GetString()));
        Assert.Equal(AttributeCatalog.Extensions[1].Unit, values.Single(value => value.GetProperty("attribute").GetString() == "E2").GetProperty("unit").GetString());
        Assert.Equal(168, other.GetProperty("hours").GetInt32());
        Assert.Empty(other.GetProperty("values").EnumerateArray());
        Assert.Equal(StatusCodes.Status400BadRequest, tooLong.StatusCode);
    }

    [Fact]
    public async Task ATeamToken_ShouldCreateADraftBattery()
    {
        var rig = await PublicationTesting.RigAsync(mongo).WithClustersAsync();
        var tokens = await TokensAsync(rig);
        var controller = DppApiControllerTests.Controller(rig, tokens["global-write"]);
        var batch = Data(await controller.Batches(CancellationToken.None), StatusCodes.Status200OK).GetProperty("batches")[0];
        var economicOperator = Data(await controller.Operators(CancellationToken.None), StatusCodes.Status200OK).GetProperty("operators")[0];

        var body = Body($$"""{ "batchId": "{{batch.GetProperty("batchId").GetString()}}", "operatorId": "{{economicOperator.GetProperty("operatorId").GetString()}}", "clusterId": "{{BatteryTesting.South}}", "values": { "batterySerialNumber": "VPC9-5001" } }""");
        var created = Data(await controller.CreateBattery(body, CancellationToken.None), StatusCodes.Status201Created);

        Assert.Equal("VPC9-5001", created.GetProperty("serialNumber").GetString());
        Assert.Equal("draft", created.GetProperty("dppStatus").GetString());
        Assert.Equal(BatteryTesting.South, created.GetProperty("clusterId").GetString());
        Assert.EndsWith("/publish", created.GetProperty("publishPath").GetString());
        Assert.NotNull(await rig.Store.FindPackByUniqueProductIdentifierAsync(created.GetProperty("batteryId").GetString()!));
    }

    [Theory]
    [InlineData("north-lifecycle", StatusCodes.Status403Forbidden, DppApiController.TeamOnlyCreate)]
    [InlineData("global-read", StatusCodes.Status403Forbidden, "Token does not have write access.")]
    [InlineData("global-write", StatusCodes.Status400BadRequest, "The battery was not created: the battery serial number (#11) is required.")]
    public async Task ABatteryThatCannotBeCreated_ShouldSayWhy(string token, int status, string message)
    {
        var rig = await PublicationTesting.RigAsync(mongo).WithClustersAsync();
        var tokens = await TokensAsync(rig);
        var body = Body($$"""{ "batchId": "{{rig.Seed.Sources.Batch.Id}}", "operatorId": "{{rig.Seed.Sources.Operator.Id}}", "values": { "manufacturingDate": "2026-09" } }""");

        var answer = Assert.IsType<ObjectResult>(await DppApiControllerTests.Controller(rig, tokens[token]).CreateBattery(body, CancellationToken.None));

        Assert.Equal(status, answer.StatusCode);
        Assert.Equal(message, DppApiControllerTests.Json(answer).GetProperty("message").GetString());
    }

    [Fact]
    public async Task ATeamToken_ShouldPublishAndRepublishOnlyWhatChanged()
    {
        var rig = await PublicationTesting.RigAsync(mongo).WithClustersAsync();
        var tokens = await TokensAsync(rig);
        var controller = DppApiControllerTests.Controller(rig, tokens["global-lifecycle"]);

        var first = Data(await controller.Publish(Serial(rig), CancellationToken.None), StatusCodes.Status200OK);
        var again = Data(await controller.Publish(PassportPagesTests.Identifier(rig), CancellationToken.None), StatusCodes.Status200OK);

        Assert.Equal(1, first.GetProperty("version").GetInt32());
        Assert.Equal("active", first.GetProperty("dppStatus").GetString());
        Assert.False(first.GetProperty("unchanged").GetBoolean());
        Assert.True(again.GetProperty("unchanged").GetBoolean());
        Assert.Equal(first.GetProperty("passportId").GetString(), again.GetProperty("passportId").GetString());
    }

    [Theory]
    [InlineData("north-lifecycle", DppApiController.TeamOnlyPublish)]
    [InlineData("global-write", "Token does not have sign access.")]
    public async Task OtherTokens_ShouldNotPublish(string token, string message)
    {
        var rig = await PublicationTesting.RigAsync(mongo).WithClustersAsync();
        var tokens = await TokensAsync(rig);

        var answer = Assert.IsType<ObjectResult>(await DppApiControllerTests.Controller(rig, tokens[token]).Publish(Serial(rig), CancellationToken.None));

        Assert.Equal(StatusCodes.Status403Forbidden, answer.StatusCode);
        Assert.Equal(message, DppApiControllerTests.Json(answer).GetProperty("message").GetString());
        Assert.Null(await rig.Store.GetActiveVersionAsync(rig.PackId));
    }

    [Fact]
    public async Task ABatteryThatCannotBePublished_ShouldListWhatBlocksIt()
    {
        var rig = await PublicationTesting.RigAsync(mongo).WithClustersAsync();
        await rig.Store.SetValuesAsync<BatteryPack>(rig.PackId, new Dictionary<string, AttributeValue>(), new[] { "stateOfChargeSoC" });
        var tokens = await TokensAsync(rig);

        var answer = Assert.IsType<ObjectResult>(await DppApiControllerTests.Controller(rig, tokens["global-lifecycle"]).Publish(Serial(rig), CancellationToken.None));

        Assert.Equal(StatusCodes.Status409Conflict, answer.StatusCode);
        var blocking = DppApiControllerTests.Json(answer).GetProperty("data").GetProperty("blocking").EnumerateArray().Select(item => item.GetString()).ToArray();
        Assert.Contains(blocking, item => item!.Contains("#63", StringComparison.Ordinal));
    }

    /// <summary>Tokens of each access mode for the seed pack's cluster (north), another cluster (south) and the team (global).</summary>
    internal static async Task<Dictionary<string, string>> TokensAsync(PublicationRig rig)
    {
        var repository = DppApiControllerTests.Repository(rig);
        var tokens = new Dictionary<string, string>();
        foreach (var (name, mode, clusters, global) in new (string, ExternalTokenAccessMode, string[], bool)[]
        {
            ("north-read", ExternalTokenAccessMode.Read, [BatteryTesting.North], false),
            ("north-write", ExternalTokenAccessMode.ReadWrite, [BatteryTesting.North], false),
            ("north-lifecycle", ExternalTokenAccessMode.Lifecycle, [BatteryTesting.North], false),
            ("south-write", ExternalTokenAccessMode.ReadWrite, [BatteryTesting.South], false),
            ("global-read", ExternalTokenAccessMode.Read, [], true),
            ("global-write", ExternalTokenAccessMode.ReadWrite, [], true),
            ("global-lifecycle", ExternalTokenAccessMode.Lifecycle, [], true),
        })
        {
            tokens[name] = (await repository.CreateTokenAsync(name, mode, clusters, false, global, "test")).PlainToken;
        }

        return tokens;
    }

    private static Task<IActionResult> Write(PublicationRig rig, string? token, string id, string body, bool preview = false) =>
        DppApiControllerTests.Controller(rig, token).WriteLiveValues(id, Body(body), preview, CancellationToken.None);

    private static JsonElement Body(string json) => JsonDocument.Parse(json).RootElement.Clone();

    private static JsonElement Data(IActionResult result, int status)
    {
        var answer = Assert.IsType<ObjectResult>(result);
        Assert.Equal(status, answer.StatusCode);
        return DppApiControllerTests.Json(answer).GetProperty("data");
    }

    private static string Serial(PublicationRig rig) => rig.Seed.Sources.Pack.Values["batterySerialNumber"].Value!.GetValue<string>();
}
