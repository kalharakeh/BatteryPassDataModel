using System.Reflection;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using BatteryPassWeb.Configuration;
using BatteryPassWeb.Controllers;
using BatteryPassWeb.Dpp.Access;
using BatteryPassWeb.Dpp.Api;
using BatteryPassWeb.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace BatteryPassWeb.Tests.Dpp;

/// <summary>The external API's reading endpoints (steps 3c-1 and 3c-2) and the team-only passports list, called with real tokens.</summary>
[Collection(MongoCollection.Name)]
public sealed class DppApiControllerTests(MongoFixture mongo)
{
    private const string Key = "3c1-test-key-for-the-token-hashes-000";

    [Fact]
    public void TheReadingEndpoints_ShouldBeGetsUnderTheApisAddressWithTheReadLimit()
    {
        var type = typeof(DppApiController);

        Assert.Equal("api/external/v1", type.GetCustomAttribute<RouteAttribute>()?.Template);
        Assert.Null(type.GetCustomAttribute<AuthorizeAttribute>());
        var actions = type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(action => action.GetCustomAttribute<HttpGetAttribute>() is not null)
            .ToArray();
        Assert.Equal(
            ["batches", "batteries/{id}", "batteries/{id}/live-values/history", "batteries/{id}/passports", "batteries/{id}/paths", "batteries/{id}/section/{sectionName}", "batteries/{id}/values", "clusters", "clusters/{clusterId}/batteries", "operators"],
            actions.Select(action => action.GetCustomAttribute<HttpGetAttribute>()!.Template).Order());
        Assert.All(actions, action => Assert.Equal(SecurityRateLimitPolicyNames.ExternalApiRead, action.GetCustomAttribute<EnableRateLimitingAttribute>()?.PolicyName));
    }

    [Theory]
    [InlineData(null, StatusCodes.Status401Unauthorized)]
    [InlineData("not-a-token", StatusCodes.Status401Unauthorized)]
    public async Task WithoutAValidToken_NothingShouldBeRead(string? token, int status)
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo);

        var result = Assert.IsType<ObjectResult>(await Controller(rig, token).Battery(PassportPagesTests.Identifier(rig), CancellationToken.None));

        Assert.Equal(status, result.StatusCode);
    }

    [Theory]
    [InlineData("north", true, false)]
    [InlineData("south", false, false)]
    [InlineData("global", true, true)]
    public async Task ABattery_ShouldBeReadCutToTheToken(string token, bool seesStateOfCharge, bool seesOperatorId)
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo).WithClustersAsync();
        var tokens = await TokensAsync(rig);

        var data = Data(await Controller(rig, tokens[token]).Battery(PassportPagesTests.Identifier(rig), CancellationToken.None));

        Assert.Equal(PassportPagesTests.Identifier(rig), data.GetProperty("batteryId").GetString());
        Assert.Equal(1, data.GetProperty("version").GetInt32());
        Assert.Equal("active", data.GetProperty("dppStatus").GetString());
        Assert.True(data.GetProperty("signatureVerified").GetBoolean());
        var passport = data.GetProperty("passport");
        Assert.Equal(seesStateOfCharge, passport.GetProperty("batteryPassportData").GetProperty("performanceAndDurability").TryGetProperty("stateOfChargeSoC", out _));
        Assert.Equal(seesOperatorId, passport.GetProperty("dppHeader").TryGetProperty("economicOperatorId", out _));
    }

    [Fact]
    public async Task AnUnknownIdOrADraft_ShouldBeNotFound()
    {
        var rig = await PublicationTesting.RigAsync(mongo).WithClustersAsync();
        var tokens = await TokensAsync(rig);

        var draft = Assert.IsType<ObjectResult>(await Controller(rig, tokens["global"]).Battery(PassportPagesTests.Identifier(rig), CancellationToken.None));
        var unknown = Assert.IsType<ObjectResult>(await Controller(rig, tokens["global"]).Passports("no-such-battery", CancellationToken.None));

        Assert.Equal(StatusCodes.Status404NotFound, draft.StatusCode);
        Assert.Equal(StatusCodes.Status404NotFound, unknown.StatusCode);
        Assert.Equal(DppApiController.NoPassport, Json(draft).GetProperty("message").GetString());
    }

    [Theory]
    [InlineData("north", new[] { BatteryTesting.North })]
    [InlineData("global", new[] { BatteryTesting.North, BatteryTesting.South })]
    public async Task Clusters_ShouldBeTheTokensOwnOrAllForAGlobalToken(string token, string[] expected)
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo).WithClustersAsync();
        var tokens = await TokensAsync(rig);

        var data = Data(await Controller(rig, tokens[token]).Clusters(CancellationToken.None));

        Assert.Equal(token == "global", data.GetProperty("globalAccess").GetBoolean());
        Assert.Equal(expected.Order(), data.GetProperty("clusters").EnumerateArray().Select(cluster => cluster.GetProperty("clusterId").GetString()).Order());
    }

    [Fact]
    public async Task AClustersBatteries_ShouldBeListedToItsOwnTokensOnly()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo).WithClustersAsync();
        var tokens = await TokensAsync(rig);

        var own = Data(await Controller(rig, tokens["north"]).ClusterBatteries(BatteryTesting.North, CancellationToken.None));
        var other = Assert.IsType<ObjectResult>(await Controller(rig, tokens["south"]).ClusterBatteries(BatteryTesting.North, CancellationToken.None));
        var missing = Assert.IsType<ObjectResult>(await Controller(rig, tokens["global"]).ClusterBatteries("no-such-cluster", CancellationToken.None));

        var battery = Assert.Single(own.GetProperty("batteries").EnumerateArray());
        Assert.Equal(PassportPagesTests.Identifier(rig), battery.GetProperty("batteryId").GetString());
        Assert.Equal("active", battery.GetProperty("dppStatus").GetString());
        Assert.Equal(StatusCodes.Status403Forbidden, other.StatusCode);
        Assert.Equal(StatusCodes.Status404NotFound, missing.StatusCode);
    }

    [Fact]
    public async Task ASection_ShouldBeOneSchemaSectionAndAnUnknownNameRefused()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo).WithClustersAsync();
        var tokens = await TokensAsync(rig);

        var section = Data(await Controller(rig, tokens["north"]).Section(PassportPagesTests.Identifier(rig), "PerformanceAndDurability", CancellationToken.None));
        var unknown = Assert.IsType<ObjectResult>(await Controller(rig, tokens["north"]).Section(PassportPagesTests.Identifier(rig), "performance", CancellationToken.None));

        Assert.Equal("performanceAndDurability", section.GetProperty("section").GetString());
        Assert.True(section.GetProperty("value").TryGetProperty("stateOfChargeSoC", out _));
        Assert.Equal(StatusCodes.Status400BadRequest, unknown.StatusCode);
        Assert.Contains("performanceAndDurability", Json(unknown).GetProperty("message").GetString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Values_ShouldAnswerEachRequestWithItsUnitAndOrigin()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo).WithClustersAsync();
        var tokens = await TokensAsync(rig);

        var values = Data(await Controller(rig, tokens["south"]).Values(PassportPagesTests.Identifier(rig), ["batteryMass", "#63", "ratedEnergy"], CancellationToken.None))
            .GetProperty("values").EnumerateArray().ToArray();
        var none = Assert.IsType<ObjectResult>(await Controller(rig, tokens["south"]).Values(PassportPagesTests.Identifier(rig), [], CancellationToken.None));

        Assert.Equal("#21", values[0].GetProperty("attribute").GetString());
        Assert.Equal("kg", values[0].GetProperty("unit").GetString());
        Assert.True(values[0].GetProperty("exists").GetBoolean());
        Assert.Equal("test data", values[0].GetProperty("source").GetString());
        Assert.Equal("2026-10-28T09:30:00Z", values[0].GetProperty("generatedAt").GetString());
        Assert.False(values[1].GetProperty("exists").GetBoolean());
        Assert.Equal("No value this token may see.", values[1].GetProperty("message").GetString());
        Assert.Equal("Not an attribute of the passport.", values[2].GetProperty("message").GetString());
        Assert.Equal(StatusCodes.Status400BadRequest, none.StatusCode);
    }

    [Fact]
    public async Task Paths_ShouldListWhatTheTokenCanRead()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo).WithClustersAsync();
        var tokens = await TokensAsync(rig);

        var south = Keys(await Controller(rig, tokens["south"]).Paths(PassportPagesTests.Identifier(rig), null, CancellationToken.None));
        var north = Keys(await Controller(rig, tokens["north"]).Paths(PassportPagesTests.Identifier(rig), "performanceAndDurability", CancellationToken.None));
        var unknown = Assert.IsType<ObjectResult>(await Controller(rig, tokens["north"]).Paths(PassportPagesTests.Identifier(rig), "operations", CancellationToken.None));

        Assert.Contains("batterySerialNumber", south);
        Assert.DoesNotContain("stateOfChargeSoC", south);
        Assert.Contains("stateOfChargeSoC", north);
        Assert.DoesNotContain("batterySerialNumber", north);
        Assert.Equal(StatusCodes.Status400BadRequest, unknown.StatusCode);
    }

    [Fact]
    public async Task Passports_ShouldListTheVersions()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo).WithClustersAsync();
        var tokens = await TokensAsync(rig);

        var versions = Data(await Controller(rig, tokens["south"]).Passports(PassportPagesTests.Identifier(rig), CancellationToken.None))
            .GetProperty("passports").EnumerateArray().ToArray();

        var version = Assert.Single(versions);
        Assert.Equal(1, version.GetProperty("version").GetInt32());
        Assert.True(version.GetProperty("signatureVerified").GetBoolean());
    }

    [Fact]
    public void ThePassportsList_ShouldBeTheTeamsWithAPublicSignatureCheck()
    {
        var type = typeof(PassportsApiController);

        Assert.Equal("AdminOnly", type.GetCustomAttribute<AuthorizeAttribute>()?.Policy);
        Assert.Equal(
            ["Detail", "List", "Verify"],
            type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly).Select(method => method.Name).Order());
        Assert.NotNull(type.GetMethod("Verify")!.GetCustomAttribute<AllowAnonymousAttribute>());
        Assert.Null(type.GetMethod("Detail")!.GetCustomAttribute<AllowAnonymousAttribute>());
    }

    [Fact]
    public async Task ThePassportsList_ShouldListEveryBatteryAndShowOnePassportInFull()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo).WithClustersAsync();
        var controller = PassportsController(rig);

        var list = Json(Assert.IsType<OkObjectResult>(await controller.List(null, CancellationToken.None)));
        var detail = Json(Assert.IsType<OkObjectResult>(await controller.Detail(PassportPagesTests.Identifier(rig), CancellationToken.None)));
        var verify = Json(Assert.IsType<OkObjectResult>(await controller.Verify(PassportPagesTests.Identifier(rig), CancellationToken.None)));
        var missing = await controller.Verify("no-such-passport", CancellationToken.None);

        Assert.Single(list.GetProperty("passports").EnumerateArray());
        Assert.True(detail.GetProperty("passport").GetProperty("dppHeader").TryGetProperty("economicOperatorId", out _));
        Assert.True(verify.GetProperty("signatureVerified").GetBoolean());
        Assert.IsType<NotFoundObjectResult>(missing);
    }

    /// <summary>Tokens for the seed pack's cluster (north), another cluster (south) and the team (global).</summary>
    private static async Task<Dictionary<string, string>> TokensAsync(PublicationRig rig)
    {
        var repository = Repository(rig);
        return new Dictionary<string, string>
        {
            ["north"] = (await repository.CreateTokenAsync("north", ExternalTokenAccessMode.Read, [BatteryTesting.North], false, false, "test")).PlainToken,
            ["south"] = (await repository.CreateTokenAsync("south", ExternalTokenAccessMode.Read, [BatteryTesting.South], false, false, "test")).PlainToken,
            ["global"] = (await repository.CreateTokenAsync("global", ExternalTokenAccessMode.Read, [], false, true, "test")).PlainToken,
        };
    }

    internal static ExternalApiRepository Repository(PublicationRig rig) => new(rig.Context, new ExternalApiSecurityService(Key));

    internal static DppApiController Controller(PublicationRig rig, string? token)
    {
        var http = new DefaultHttpContext();
        if (token is not null)
        {
            http.Request.Headers.Authorization = "Basic " + Convert.ToBase64String(Encoding.UTF8.GetBytes(token + ":"));
        }

        return new DppApiController(Repository(rig), new ClusterRepository(rig.Context), PassportApiTests.Api(rig), BatteryApiTests.Api(rig), UploadServiceTests.Service(rig))
        {
            ControllerContext = new ControllerContext { HttpContext = http },
        };
    }

    private static PassportsApiController PassportsController(PublicationRig rig) => new(
        new DppReaders(new ClusterRepository(rig.Context)),
        BatteryTesting.Pages(rig),
        PassportApiTests.Api(rig),
        PassportPagesTests.Pages(rig))
    {
        ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(
                    [new Claim(ClaimTypes.Email, "team@example.com"), new Claim(ClaimTypes.Role, AccessControlService.RoleAdmin)],
                    "test")),
            },
        },
    };

    internal static JsonElement Json(ObjectResult result) => JsonSerializer.SerializeToElement(result.Value);

    private static JsonElement Data(IActionResult result)
    {
        var answer = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status200OK, answer.StatusCode);
        return Json(answer).GetProperty("data");
    }

    private static string[] Keys(IActionResult result) =>
        Data(result).GetProperty("attributes").EnumerateArray().Select(attribute => attribute.GetProperty("key").GetString()!).ToArray();
}
