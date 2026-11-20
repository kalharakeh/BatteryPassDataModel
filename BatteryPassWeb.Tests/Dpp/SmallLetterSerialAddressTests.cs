using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using BatteryPassWeb.Configuration;
using BatteryPassWeb.Controllers;
using BatteryPassWeb.Dpp.Access;
using BatteryPassWeb.Dpp.Api;
using BatteryPassWeb.Dpp.Documents;
using BatteryPassWeb.Dpp.Editing;
using BatteryPassWeb.Dpp.Identifiers;
using BatteryPassWeb.Dpp.Pages;
using BatteryPassWeb.Dpp.Store;
using BatteryPassWeb.Dpp.Uploads;
using BatteryPassWeb.Services;
using BatteryPassWeb.Tests.Demo;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;
using static BatteryPassWeb.Tests.Dpp.SmallLetterSerialTesting;

namespace BatteryPassWeb.Tests.Dpp;

/// <summary>
/// Decision 80 on every address: a battery released as <c>VPC8C05s-00001</c> has the small s in its ID too, and each page, data
/// route, API route, search and file finds it with the ID or the serial number typed in capitals, in small letters or
/// mixed. A removed passport's 410, "not published yet" and "not found" answer the same way.
/// </summary>
[Collection(MongoCollection.Name)]
public sealed class SmallLetterSerialAddressTests(MongoFixture mongo)
{
    private const string PassportItem = Item + "/v1";

    private static ClaimsPrincipal Visitor => new(new ClaimsIdentity());

    private static ClaimsPrincipal Team(DemoRig rig) =>
        TestAccounts.SignedIn(rig.Services.GetRequiredService<MongoContext>(), Who, AccessControlService.RoleAdmin);

    private static T On<T>(T controller, DemoRig rig, ClaimsPrincipal user)
        where T : ControllerBase
    {
        var http = new DefaultHttpContext { User = user, RequestServices = rig.Services };
        controller.ControllerContext = new ControllerContext { HttpContext = http };
        if (controller is Controller page)
        {
            page.TempData = new Microsoft.AspNetCore.Mvc.ViewFeatures.TempDataDictionary(http, new NoTempDataKept());
        }

        return controller;
    }

    private static PassportController Pages(DemoRig rig, ClaimsPrincipal user) =>
        On(new PassportController(rig.Services.GetRequiredService<DppReaders>(), rig.Services.GetRequiredService<PassportPages>()), rig, user);

    private static IOptions<BatteryPassOptions> Settings(DemoRig rig) => rig.Services.GetRequiredService<IOptions<BatteryPassOptions>>();

    /// <summary>The passport page a route showed, by its view's name: never a "not found" page.</summary>
    private static PassportPage Shown(IActionResult result, string view)
    {
        var shown = Assert.IsType<ViewResult>(result);
        Assert.Equal((view, null), (shown.ViewName, shown.StatusCode));
        return Assert.IsType<PassportPage>(shown.Model);
    }

    private static JsonNode Json(IActionResult result) => JsonNode.Parse(Assert.IsType<ContentResult>(result).Content!)!;

    private static IEnumerable<string> AllStrings(JsonNode? node) => node switch
    {
        JsonObject fields => fields.SelectMany(field => AllStrings(field.Value)),
        JsonArray items => items.SelectMany(AllStrings),
        JsonValue value when value.TryGetValue<string>(out var text) => [text],
        _ => [],
    };

    [Fact]
    public async Task ThePublicPageAndItsDataRoutes_ShouldOpen_WithTheIdTypedInCapitalsSmallLettersOrMixed()
    {
        var (rig, pool, c7) = await SeededAsync(mongo);
        using var __ = rig;
        var battery = await PublishedAsync(rig, c7);
        PassportController Fresh() => Pages(rig, Visitor);

        foreach (var id in ItemSpellings)
        {
            var page = Shown(await Fresh().Summary(id, CancellationToken.None, pool), "Summary");
            Assert.Equal($"{Base}/25S/{Item}", page.Identifier); // the ID in its one form, however the address was typed
            Assert.Equal($"/dpp/{Item}.json", page.DataHref); // and the page's own links carry that form
            Assert.Equal($"{Base}/dpp/{PassportItem}", page.Version.DigitalProductPassportId);
            Assert.Equal(Serial, page.Text(11)); // #11 as released, small s included
            Assert.Equal(battery.Id, page.Version.PackId);

            // The four public data routes.
            foreach (var data in new[]
                     {
                         await Fresh().SummaryData(id, CancellationToken.None),
                         await Fresh().SummaryJson(id, CancellationToken.None),
                         await Fresh().VersionData(id, 1, Settings(rig), CancellationToken.None),
                         await Fresh().VersionJson(id, 1, Settings(rig), CancellationToken.None),
                     })
            {
                var strings = AllStrings(Json(data)).ToArray();
                Assert.Contains(Serial, strings);
                Assert.Contains($"{Base}/25S/{Item}", strings);
                Assert.Contains($"{Base}/dpp/{PassportItem}", strings);
                Assert.DoesNotContain(strings, text => text.Contains("VPC8C05S", StringComparison.Ordinal) || text.Contains("vpc8c05s", StringComparison.Ordinal)); // one form, never the typed one
            }

            // The version's own page.
            var version = Shown(await Fresh().Version(id, 1, Settings(rig), CancellationToken.None), "Summary");
            Assert.Equal(battery.Id, version.Version.PackId);
        }

        // The full IDs too, however their letters are typed.
        foreach (var full in new[] { $"{Base}/25S/{Item}", $"{Base}/25S/{Item.ToLowerInvariant()}", $"{Base}/dpp/{PassportItem.ToLowerInvariant()}", $"{Base}/dpp/UN778474481-vpc8c05S-00001/v1" })
        {
            Assert.Equal(battery.Id, Shown(await Fresh().Summary(Uri.EscapeDataString(full), CancellationToken.None, pool), "Summary").Version.PackId);
        }
    }

    [Fact]
    public async Task TheSerialNumber_ShouldOpenItsBattery_HoweverItIsTyped()
    {
        var (rig, pool, c7) = await SeededAsync(mongo);
        using var __ = rig;
        var battery = await PublishedAsync(rig, c7);
        PassportController Fresh() => Pages(rig, Visitor);

        foreach (var serial in SerialSpellings.Append("VPC8C05S00001")) // and without its dash: the same serial number
        {
            var page = Shown(await Fresh().Summary(serial, CancellationToken.None, pool), "Summary");
            Assert.Equal((battery.Id, Serial), (page.Version.PackId, page.Text(11)));
            Assert.Single(await rig.Store.FindPacksBySerialNumberAsync(serial));
        }

        Assert.Empty(await rig.Store.FindPacksBySerialNumberAsync("VPC8C05-00001")); // Core800's: another serial number
        Assert.Empty(await rig.Store.FindPacksBySerialNumberAsync("---"));
    }

    [Fact]
    public async Task TheDetailPage_ShouldOpenForTheTeam_WithTheIdTypedAnyWay()
    {
        var (rig, _, c7) = await SeededAsync(mongo);
        using var __ = rig;
        var battery = await PublishedAsync(rig, c7);
        PassportController Fresh() => Pages(rig, Team(rig));

        foreach (var id in ItemSpellings)
        {
            var detail = Shown(await Fresh().Detail(id, CancellationToken.None), "Detail");
            var version = Shown(await Fresh().VersionDetail(id, 1, Settings(rig), CancellationToken.None), "Detail");
            Assert.Equal((battery.Id, Serial), (detail.Version.PackId, detail.Text(11)));
            Assert.Equal(battery.Id, version.Version.PackId);
        }
    }

    [Fact]
    public async Task TheTeamsApiAndVerify_ShouldAnswer_WithTheIdTypedAnyWay()
    {
        var (rig, _, c7) = await SeededAsync(mongo);
        using var __ = rig;
        await PublishedAsync(rig, c7);
        var teamApi = On(new PassportsApiController(rig.Services.GetRequiredService<DppReaders>(), rig.Services.GetRequiredService<BatteryPages>(), rig.Services.GetRequiredService<PassportApi>(), rig.Services.GetRequiredService<PassportPages>()), rig, Team(rig));

        foreach (var id in ItemSpellings.Concat([PassportItem.ToLowerInvariant(), "vpc8c05S-00001"]))
        {
            var detail = JsonSerializer.SerializeToElement(Assert.IsType<OkObjectResult>(await teamApi.Detail(Uri.EscapeDataString(id.Contains("/v", StringComparison.Ordinal) ? $"{Base}/dpp/{id}" : id), CancellationToken.None)).Value);
            Assert.Equal($"{Base}/25S/{Item}", detail.GetProperty("batteryId").GetString());
            Assert.Equal($"{Base}/dpp/{PassportItem}", detail.GetProperty("passportId").GetString());

            var verify = JsonSerializer.SerializeToElement(Assert.IsType<OkObjectResult>(await teamApi.Verify(Uri.EscapeDataString(id.Contains("/v", StringComparison.Ordinal) ? $"{Base}/dpp/{id}" : id), CancellationToken.None)).Value);
            Assert.True(verify.GetProperty("signatureVerified").GetBoolean());
            Assert.Equal($"{Base}/dpp/{PassportItem}", verify.GetProperty("passportId").GetString());

            var list = JsonSerializer.SerializeToElement(Assert.IsType<OkObjectResult>(await teamApi.List(id.Contains("/v", StringComparison.Ordinal) ? id[..id.IndexOf('/', StringComparison.Ordinal)] : id, CancellationToken.None)).Value);
            Assert.Equal(Serial, Assert.Single(list.GetProperty("passports").EnumerateArray()).GetProperty("serialNumber").GetString());
        }

        Assert.IsType<NotFoundObjectResult>(await teamApi.Verify("un778474481-vpc8c05s-00002", CancellationToken.None));
    }

    [Fact]
    public async Task APassportSignedWithASmallLetterId_ShouldVerify_FromAnAddressTypedInCapitalsAndInSmallLetters()
    {
        var (rig, _, c7) = await SeededAsync(mongo);
        using var __ = rig;
        var battery = await PublishedAsync(rig, c7);
        var version = (await rig.Store.GetActiveVersionAsync(battery.Id))!;

        // What was signed holds the IDs in their one form, small s included.
        var signed = version.StaticCore.ToJsonString();
        Assert.Contains($"\"{Base}/25S/{Item}\"", signed, StringComparison.Ordinal);
        Assert.Contains($"\"{Base}/dpp/{PassportItem}\"", signed, StringComparison.Ordinal);
        Assert.DoesNotContain("VPC8C05S", signed, StringComparison.Ordinal);
        Assert.True(rig.Services.GetRequiredService<PassportApi>().Signature(version).Holds);

        foreach (var id in ItemSpellings.Concat([$"{Base}/dpp/{PassportItem.ToUpperInvariant().Replace("/V1", "/v1", StringComparison.Ordinal)}", $"{Base}/dpp/{PassportItem.ToLowerInvariant()}"]).Select(Uri.EscapeDataString))
        {
            // /verify, open to anyone: the signature holds, and the answer names the passport in its one form.
            var verifier = On(new PassportsApiController(rig.Services.GetRequiredService<DppReaders>(), rig.Services.GetRequiredService<BatteryPages>(), rig.Services.GetRequiredService<PassportApi>(), rig.Services.GetRequiredService<PassportPages>()), rig, Visitor);
            var verify = JsonSerializer.SerializeToElement(Assert.IsType<OkObjectResult>(await verifier.Verify(id, CancellationToken.None)).Value);
            Assert.True(verify.GetProperty("signatureVerified").GetBoolean());
            Assert.Equal(JsonValueKind.Null, verify.GetProperty("signatureReason").ValueKind);
            Assert.Equal($"{Base}/dpp/{PassportItem}", verify.GetProperty("passportId").GetString());

            // The public data's proof: served only when it matches the data being served, and its hash is the data's own.
            var data = Json(await Pages(rig, Visitor).SummaryJson(id, CancellationToken.None)).AsObject();
            var proof = Assert.IsType<JsonObject>(data[BatteryPassWeb.Dpp.Assembly.PublicPassport.ProofProperty]);
            Assert.Equal(proof["hash"]!.GetValue<string>(), BatteryPassWeb.Dpp.Signing.CanonicalJson.Hash(BatteryPassWeb.Dpp.Assembly.PublicPassport.SignedPart(data)));
            Assert.Contains($"{Base}/25S/{Item}", AllStrings(data));

            // The proof is over that one form: the same data with the ID in capitals is not what was signed.
            var inCapitals = JsonNode.Parse(data.ToJsonString().Replace("VPC8C05s", "VPC8C05S", StringComparison.Ordinal))!.AsObject();
            Assert.NotEqual(proof["hash"]!.GetValue<string>(), BatteryPassWeb.Dpp.Signing.CanonicalJson.Hash(BatteryPassWeb.Dpp.Assembly.PublicPassport.SignedPart(inCapitals)));
        }
    }

    [Fact]
    public async Task TheExternalApisBatteryRoutes_ShouldFindTheBattery_ByItsIdOrSerialTypedAnyWay()
    {
        var (rig, _, c7) = await SeededAsync(mongo);
        using var __ = rig;
        var battery = await PublishedAsync(rig, c7);
        var token = (await rig.Services.GetRequiredService<ExternalApiRepository>().CreateTokenAsync("team", ExternalTokenAccessMode.Lifecycle, [], false, true, "test")).PlainToken;
        DppApiController Api()
        {
            var http = new DefaultHttpContext { RequestServices = rig.Services };
            http.Request.Headers.Authorization = "Basic " + Convert.ToBase64String(Encoding.UTF8.GetBytes(token + ":"));
            return new DppApiController(
                rig.Services.GetRequiredService<ExternalApiRepository>(),
                rig.Services.GetRequiredService<ClusterRepository>(),
                rig.Services.GetRequiredService<PassportApi>(),
                rig.Services.GetRequiredService<BatteryApi>(),
                rig.Services.GetRequiredService<UploadService>(),
                rig.Services.GetRequiredService<DemoBoundary>())
            {
                ControllerContext = new ControllerContext { HttpContext = http },
            };
        }

        foreach (var id in ItemSpellings.Concat(SerialSpellings))
        {
            foreach (var answer in new[]
                     {
                         await Api().Battery(id, CancellationToken.None),
                         await Api().Passports(id, CancellationToken.None),
                         await Api().Paths(id, null, CancellationToken.None),
                         await Api().LiveValueHistory(id, 1, CancellationToken.None),
                     })
            {
                Assert.Equal(StatusCodes.Status200OK, Assert.IsAssignableFrom<ObjectResult>(answer).StatusCode);
            }

            // A write finds the same battery: the writing side's own lookup.
            var target = await rig.Services.GetRequiredService<BatteryApi>().FindAsync(ChangeSender.Team(Who), id);
            Assert.Equal((WriteRefusal.None, battery.Id), (target.Refusal, target.Pack?.Id));
        }

        var body = JsonSerializer.SerializeToElement(Assert.IsAssignableFrom<ObjectResult>(await Api().Battery("un778474481-VPC8C05s-00001", CancellationToken.None)).Value).GetRawText();
        Assert.Contains($"\"{Serial}\"", body, StringComparison.Ordinal);
        Assert.Equal(StatusCodes.Status404NotFound, Assert.IsAssignableFrom<ObjectResult>(await Api().Battery("un778474481-vpc8c05s-00002", CancellationToken.None)).StatusCode);
    }

    [Fact]
    public async Task TheSearches_ShouldFindTheBattery_HoweverItsIdOrSerialIsTyped()
    {
        var (rig, _, c7) = await SeededAsync(mongo);
        using var __ = rig;
        var battery = await PublishedAsync(rig, c7);
        var home = On(new HomeController(rig.Services.GetRequiredService<PassportPages>()), rig, Visitor);
        var lists = rig.Services.GetRequiredService<BatteryPages>();

        foreach (var typed in ItemSpellings.Concat(SerialSpellings).Concat([$"https://www.sib-scania-eubp.eu/25S/{Item.ToLowerInvariant()}", $"HTTPS://WWW.SIB-SCANIA-EUBP.EU/25S/{Item}", $"{Base}/dpp/{PassportItem.ToLowerInvariant()}"]))
        {
            // Home: straight to the battery's page, at its stored address.
            Assert.Equal($"/dpp/{Item}", Assert.IsType<RedirectResult>(await home.Search(typed, CancellationToken.None)).Url);
        }

        foreach (var typed in ItemSpellings.Concat(SerialSpellings).Concat(["vpc8c05s", "8C05S-0000", "un778474481-vpc8"]))
        {
            // The team's list and the Registry (one row per battery) search by part of an ID or serial number.
            Assert.Equal([battery.Id], (await lists.ListAsync(typed)).Rows.Select(row => row.PackId));
            var registry = await lists.ListOnePerBatteryAsync(typed, _ => true, true);
            Assert.Equal([Serial], registry.Rows.Select(row => row.SerialNumber)); // shown as released
        }

        Assert.Empty((await lists.ListAsync("VPC8C05-0")).Rows); // Core800's serial numbers aren't Core800s's
    }

    [Fact]
    public async Task ARemovedPassportsAddresses_ShouldAnswer410_HoweverTheyAreTyped_AndItsSerialNeverComesBack()
    {
        var (rig, pool, c7) = await SeededAsync(mongo);
        using var __ = rig;
        var battery = await PublishedAsync(rig, c7);
        await rig.Database.GetCollection<BsonDocument>(DppStore.Packs).UpdateOneAsync(
            Builders<BsonDocument>.Filter.Eq("_id", battery.Id),
            Builders<BsonDocument>.Update.Set("Values.batteryStatus.Value.batteryStatusValues", "waste")); // recycled
        Assert.Null(await rig.Services.GetRequiredService<PassportMarks>().MarkForDeletionAsync(ChangeSender.Team(Who), battery.Id, "recycled"));
        Assert.Null(await rig.Services.GetRequiredService<PassportRemoval>().RemoveAsync(ChangeSender.Team(Who), battery.Id, Item.ToLowerInvariant())); // typed in small letters
        Assert.Null(await rig.Store.GetAsync<BatteryPack>(battery.Id));
        PassportController Fresh() => Pages(rig, Visitor);
        var api = On(new PassportsApiController(rig.Services.GetRequiredService<DppReaders>(), rig.Services.GetRequiredService<BatteryPages>(), rig.Services.GetRequiredService<PassportApi>(), rig.Services.GetRequiredService<PassportPages>()), rig, Visitor);

        foreach (var id in ItemSpellings.Concat(SerialSpellings))
        {
            var page = Assert.IsType<ViewResult>(await Fresh().Summary(id, CancellationToken.None, pool));
            Assert.Equal(("~/Views/Dpp/Removed.cshtml", StatusCodes.Status410Gone), (page.ViewName, page.StatusCode));
            Assert.Equal(StatusCodes.Status410Gone, Assert.IsType<ObjectResult>(await Fresh().SummaryJson(id, CancellationToken.None)).StatusCode);
            Assert.Equal(StatusCodes.Status410Gone, Assert.IsType<ObjectResult>(await api.Verify(id, CancellationToken.None)).StatusCode);
            Assert.NotNull(await rig.Services.GetRequiredService<PassportRemoval>().FindAsync(id));
        }

        foreach (var id in ItemSpellings)
        {
            Assert.Equal(StatusCodes.Status410Gone, Assert.IsType<ObjectResult>(await Fresh().VersionJson(id, 1, Settings(rig), CancellationToken.None)).StatusCode);
        }

        // Its serial number never comes back, in any letters.
        foreach (var serial in SerialSpellings)
        {
            Assert.Equal(BatteryIdPool.RemovedId, Assert.Single((await pool.ReleaseAsync(Who, List("PO again", serial))).Problems).Reason);
        }

        // Another battery's addresses are untouched.
        Assert.Null(await rig.Services.GetRequiredService<PassportRemoval>().FindAsync("un778474481-vpc8c05s-00002"));
        Assert.Null(await rig.Services.GetRequiredService<PassportRemoval>().FindAsync("VPC8C05-00001"));
    }

    [Fact]
    public async Task AnIdThatIsReleasedButNotPublished_ShouldSayNotPublishedYet_AndAnUnknownOne404_InAnyLetters()
    {
        var (rig, pool, c7) = await SeededAsync(mongo);
        using var __ = rig;
        await pool.ReleaseAsync(Who, List("PO Core800s", Serial, "VPC8C05s-00002"));
        await pool.VoidAsync(Who, "VPC8C05s-00002", "Label damaged");
        await rig.Services.GetRequiredService<PackCreationService>().CreateAsync(ChangeSender.Team(Who), NewBattery(c7, Serial)); // a draft: not shown to anyone here
        PassportController Fresh() => Pages(rig, Visitor);

        foreach (var id in ItemSpellings.Concat([$"{Base}/25S/{Item.ToLowerInvariant()}", "vpc8c05S-00001"]))
        {
            var waiting = Assert.IsType<ViewResult>(await Fresh().Summary(Uri.EscapeDataString(id), CancellationToken.None, pool));
            Assert.Equal(("~/Views/Dpp/NotPublishedYet.cshtml", StatusCodes.Status404NotFound), (waiting.ViewName, waiting.StatusCode));
            Assert.Equal(Serial, Assert.IsType<ReleasedBatteryId>(waiting.Model).SerialNumber);
            Assert.IsType<NotFoundObjectResult>(await Fresh().SummaryJson(Uri.EscapeDataString(id), CancellationToken.None));
        }

        foreach (var id in new[] { "UN778474481-VPC8C05s-00002", "UN778474481-VPC8C05S-00002", "un778474481-vpc8c05s-00002", "UN778474481-VPC8C05s-00009", "un778474481-vpc8c05s-00009", "Un778474481-Vpc8c05-00001" })
        {
            var missing = Assert.IsType<ViewResult>(await Fresh().Summary(id, CancellationToken.None, pool)); // voided, or never released
            Assert.Equal(("~/Views/Dpp/NotAvailable.cshtml", StatusCodes.Status404NotFound), (missing.ViewName, missing.StatusCode));
            Assert.IsType<NotFoundObjectResult>(await Fresh().SummaryJson(id, CancellationToken.None));
        }
    }

    [Fact]
    public async Task TheQrCode_ShouldCarryTheStoredAddress_HoweverItsOwnAddressIsTyped()
    {
        var (rig, pool, c7) = await SeededAsync(mongo);
        using var __ = rig;
        await pool.ReleaseAsync(Who, List("PO Core800s", "VPC8C05s-00002"));
        await rig.Services.GetRequiredService<PackCreationService>().CreateAsync(ChangeSender.Team(Who), NewBattery(c7, "VPC8C05s-00002")); // a draft
        await PublishedAsync(rig, c7);
        var qr = On(new QrController(rig.Services.GetRequiredService<PassportPages>(), rig.Services.GetRequiredService<DppLinks>(), new PassportQrCodeService(), rig.Store), rig, Team(rig));

        foreach (var item in new[] { Item, "UN778474481-VPC8C05s-00002" }) // published, and a draft's for the team
        {
            var stored = Assert.IsType<ContentResult>(await qr.QrCode(item, CancellationToken.None)).Content;
            Assert.Contains("<svg", stored, StringComparison.Ordinal);
            Assert.Equal(new PassportQrCodeService().GenerateSvg($"{DocumentTesting.BaseUrl.TrimEnd('/')}/25S/{item}"), stored); // what the code carries: the one form, small s included
            Assert.Equal(stored, Assert.IsType<ContentResult>(await qr.QrCode(item.ToLowerInvariant(), CancellationToken.None)).Content); // the same code, however its own address is typed
            Assert.Equal(stored, Assert.IsType<ContentResult>(await qr.QrCode(item.ToUpperInvariant(), CancellationToken.None)).Content);
        }

        var file = Assert.IsType<FileContentResult>(await qr.Download(Item.ToLowerInvariant(), CancellationToken.None));
        Assert.Contains("VPC8C05s-00001", file.FileDownloadName, StringComparison.Ordinal); // named by the serial as released
    }

    [Fact]
    public async Task AnUploadFileAndTheBatterySystemsExport_ShouldFindTheBattery_NamedInEitherCase()
    {
        var (rig, _, c7) = await SeededAsync(mongo);
        using var __ = rig;
        var battery = await PublishedAsync(rig, c7);
        var uploads = rig.Services.GetRequiredService<UploadService>();
        var team = ChangeSender.Team(Who);
        var at = DateTime.UtcNow.AddMinutes(-5);

        foreach (var named in new[] { Serial, "VPC8C05S-00001", "vpc8c05s-00001", Item.ToLowerInvariant(), $"{Base}/25S/{Item}" })
        {
            // The battery system's export of a Core800s says VPC8C05s-…; another system may say it in capitals.
            var export = Encoding.UTF8.GetBytes(UploadSamples.Export(named, at, battery.Modules.Count).ToJsonString());
            var checkedExport = await uploads.CheckBatteryFileAsync(team, battery.Id, "export.json", export);
            Assert.True(checkedExport.Problem is null, checkedExport.Problem);
            Assert.Equal((battery.Id, named), (Assert.Single(checkedExport.Batteries).PackId, checkedExport.Batteries[0].NamedAs));
            Assert.NotEqual(UploadService.Refused, checkedExport.Batteries[0].Outcome);

            // The app's own upload file, through the upload page and the API alike.
            var file = JsonNode.Parse(UploadSamples.LiveValuesFile(named, at, 61, 23));
            var report = await uploads.RunAsync(team, file, "page", "upload.json", preview: true);
            Assert.True(report.Problem is null, report.Problem);
            Assert.Equal(battery.Id, Assert.Single(report.Batteries).PackId);
            Assert.NotEqual(UploadService.Refused, report.Batteries[0].Outcome);
        }

        // A file that names the battery twice, once in each case, is refused both times: it is one battery.
        var twice = JsonNode.Parse(UploadSamples.LiveValuesFile(Serial, at, 61, 23))!;
        twice["batteries"]!.AsArray().Add(JsonNode.Parse(UploadSamples.LiveValuesFile("VPC8C05S-00001", at, 62, 23))!["batteries"]![0]!.DeepClone());
        var both = await uploads.RunAsync(team, twice, "page", "upload.json", preview: true);
        Assert.All(both.Batteries, line => Assert.Equal((UploadService.Refused, UploadService.NamedTwice), (line.Outcome, line.Problem)));

        // The export of another battery is still refused for this one.
        var other = await uploads.CheckBatteryFileAsync(team, battery.Id, "export.json", Encoding.UTF8.GetBytes(UploadSamples.Export("VPC8C05-00001", at, battery.Modules.Count).ToJsonString()));
        Assert.Equal(UploadService.NotThisBattery("VPC8C05-00001"), other.Problem);
    }
}
