using System.Reflection;
using System.Text.Json;
using BatteryPassWeb.Controllers;
using BatteryPassWeb.Dpp.Store;
using BatteryPassWeb.Dpp.Uploads;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace BatteryPassWeb.Tests.Dpp;

/// <summary>The external API's upload endpoint (design 8.1, step 5a), called with real tokens of each kind.</summary>
[Collection(MongoCollection.Name)]
public sealed class DppApiUploadTests(MongoFixture mongo)
{
    [Fact]
    public async Task AClusterToken_ShouldUploadItsBatteriesLiveValuesOnly()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo).WithClustersAsync();
        var tokens = await DppApiWritingTests.TokensAsync(rig);

        var data = Data(
            await Upload(rig, tokens["north-write"], UploadServiceTests.File(UploadServiceTests.Entry(UploadServiceTests.Serial(rig), """{ "stateOfChargeSoC": 33, "warrantyPeriod": "P9Y" }"""))),
            StatusCodes.Status200OK);

        Assert.True(data.GetProperty("saved").GetBoolean());
        Assert.Equal(NativeUploadFormat.Format, data.GetProperty("format").GetString());
        var battery = Assert.Single(data.GetProperty("batteries").EnumerateArray());
        Assert.Equal((1, PassportPagesTests.Identifier(rig), "saved"), (battery.GetProperty("entry").GetInt32(), battery.GetProperty("batteryId").GetString(), battery.GetProperty("outcome").GetString()));
        Assert.Equal(["saved", "rejected"], battery.GetProperty("values").EnumerateArray().Select(value => value.GetProperty("outcome").GetString()));
        var logged = Assert.Single(await rig.Store.ListUploadsAsync(10));
        Assert.Equal((data.GetProperty("uploadId").GetString(), "api", "BatterySystemFeed", (string?)null), (logged.Id, logged.Channel, logged.SentAs, logged.FileName));
        Assert.Equal([BatteryTesting.North], logged.ClusterIds);
    }

    [Theory]
    [InlineData("north-read", StatusCodes.Status403Forbidden)]
    [InlineData(null, StatusCodes.Status401Unauthorized)]
    public async Task ATokenWithoutWriteAccess_ShouldUploadNothing(string? token, int status)
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo).WithClustersAsync();
        var tokens = await DppApiWritingTests.TokensAsync(rig);

        var answer = Assert.IsType<ObjectResult>(await Upload(rig, token is null ? null : tokens[token], UploadServiceTests.File(UploadServiceTests.Entry(UploadServiceTests.Serial(rig), """{ "stateOfChargeSoC": 33 }"""))));

        Assert.Equal(status, answer.StatusCode);
        Assert.Empty(await rig.Store.ListUploadsAsync(10));
    }

    [Fact]
    public async Task NothingSaved_ShouldBeUnprocessableAndAnUnreadableFileABadRequest()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo).WithClustersAsync();
        var tokens = await DppApiWritingTests.TokensAsync(rig);

        var refused = Assert.IsType<ObjectResult>(await Upload(rig, tokens["south-write"], UploadServiceTests.File(UploadServiceTests.Entry(UploadServiceTests.Serial(rig), """{ "stateOfChargeSoC": 33 }"""))));
        var unreadable = Assert.IsType<ObjectResult>(await Upload(rig, tokens["global-write"], """{ "format": "dpp-v9" }"""));

        Assert.Equal(StatusCodes.Status422UnprocessableEntity, refused.StatusCode);
        Assert.Equal(UploadService.NotYours, Assert.Single(DppApiControllerTests.Json(refused).GetProperty("data").GetProperty("batteries").EnumerateArray()).GetProperty("problem").GetString());
        Assert.Equal(StatusCodes.Status400BadRequest, unreadable.StatusCode);
        Assert.Equal("The file can't be read: the format \"dpp-v9\" isn't one this app reads; use \"dpp-v3-upload/1\".", DppApiControllerTests.Json(unreadable).GetProperty("message").GetString());
    }

    [Fact]
    public async Task APreview_ShouldSaveNothing()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo).WithClustersAsync();
        var tokens = await DppApiWritingTests.TokensAsync(rig);

        var data = Data(await Upload(rig, tokens["global-write"], UploadServiceTests.File(UploadServiceTests.Entry(UploadServiceTests.Serial(rig), """{ "stateOfChargeSoC": 33 }""")), preview: true), StatusCodes.Status200OK);

        Assert.True(data.GetProperty("preview").GetBoolean());
        Assert.False(data.GetProperty("saved").GetBoolean());
        Assert.Equal(JsonValueKind.Null, data.GetProperty("uploadId").ValueKind);
        Assert.Equal(76, (await rig.Store.GetAsync<BatteryPack>(rig.PackId))!.Values["stateOfChargeSoC"].Value!["percentageValue"]!.GetValue<int>());
    }

    [Fact]
    public void TheEndpoint_ShouldTakeAtMostTwoMegabytes()
    {
        var limit = typeof(DppApiController).GetMethod(nameof(DppApiController.Upload))!.GetCustomAttribute<RequestSizeLimitAttribute>()!;
        var bytes = typeof(RequestSizeLimitAttribute).GetField("_bytes", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(limit);

        Assert.Equal(2L * 1024 * 1024, bytes);
    }

    private static Task<IActionResult> Upload(PublicationRig rig, string? token, string body, bool preview = false) =>
        DppApiControllerTests.Controller(rig, token).Upload(JsonDocument.Parse(body).RootElement.Clone(), preview, CancellationToken.None);

    private static JsonElement Data(IActionResult result, int status)
    {
        var answer = Assert.IsType<ObjectResult>(result);
        Assert.Equal(status, answer.StatusCode);
        return DppApiControllerTests.Json(answer).GetProperty("data");
    }
}
