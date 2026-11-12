using System.IO.Compression;
using System.Security.Claims;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using BatteryPassWeb.Controllers;
using BatteryPassWeb.Dpp.Access;
using BatteryPassWeb.Dpp.Demo;
using BatteryPassWeb.Dpp.Documents;
using BatteryPassWeb.Dpp.Editing;
using BatteryPassWeb.Dpp.Identifiers;
using BatteryPassWeb.Dpp.Pages;
using BatteryPassWeb.Dpp.Publication;
using BatteryPassWeb.Dpp.Store;
using BatteryPassWeb.Services;
using BatteryPassWeb.Tests.Demo;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Driver;

namespace BatteryPassWeb.Tests.Dpp;

/// <summary>
/// Step 6d-2, battery IDs released in advance (the owner, 6 Nov 2026): the team releases serial numbers on the "Battery
/// IDs" page, each with its #6 fixed at release; a battery is created by picking an available one; a draft may swap it;
/// a voided one is never reused; scanning one before publication says "Not published yet". Releases and picks are
/// all-or-nothing and race-safe.
/// </summary>
[Collection(MongoCollection.Name)]
public sealed class BatteryIdPoolTests(MongoFixture mongo)
{
    private const string Base = "https://www.sib-scania-eubp.eu";
    private const string Who = DemoRig.Who;

    private static ReleaseRequest Range(string reference, string start, string end, int quantity, string? model = null) =>
        new(reference, model, [new IdRange(start, end, quantity.ToString(System.Globalization.CultureInfo.InvariantCulture))], null);

    private static ReleaseRequest List(string reference, params string[] serials) => new(reference, null, [], string.Join('\n', serials));

    private static async Task<(DemoRig Rig, BatteryIdPool Pool, BatteryPack C7)> SeededAsync(MongoFixture mongo)
    {
        var rig = await DemoRig.StartAsync(mongo, dunsIds: true);
        await rig.Demo.SeedAsync(Who);
        var c7 = (await rig.Store.FindPacksBySerialNumberAsync("DEMO-C7M-0001")).Single();
        await RealProductDataAsync(rig, c7);
        return (rig, rig.Services.GetRequiredService<BatteryIdPool>(), c7);
    }

    /// <summary>
    /// These tests make batteries with real released IDs, which may not sit on demo product data (U16), so C7's batch,
    /// model and operator are made real data here.
    /// </summary>
    private static async Task RealProductDataAsync(DemoRig rig, BatteryPack c7)
    {
        var batch = (await rig.Store.GetAsync<ProductionBatch>(c7.BatchId))!;
        foreach (var (collection, id) in new[] { (DppStore.Batches, batch.Id), (DppStore.Models, batch.ModelId), (DppStore.Operators, c7.OperatorId) })
        {
            await rig.Database.GetCollection<MongoDB.Bson.BsonDocument>(collection).UpdateOneAsync(
                Builders<MongoDB.Bson.BsonDocument>.Filter.Eq("_id", id),
                Builders<MongoDB.Bson.BsonDocument>.Update.Set("IsDemo", false));
        }
    }

    // No customer: a battery with a real ID doesn't go into the demo's customers (M1 round two D).
    private static NewPack NewBattery(BatteryPack like, string serial) =>
        new(like.BatchId, like.OperatorId, null, new Dictionary<string, JsonNode?> { ["batterySerialNumber"] = serial }, FromPool: true);

    [Fact]
    public async Task ARange_ShouldBeReleasedWithItsBatteryIds_AvailableAndUnderItsReference()
    {
        var (rig, pool, c7) = await SeededAsync(mongo);
        using var _ = rig;

        var model = (await rig.Store.GetAsync<ProductionBatch>(c7.BatchId))!.ModelId;

        var result = await pool.ReleaseAsync(Who, Range("PO 4512, line 2", "VPC-12221", "VPC-12230", 10, model));

        Assert.Equal(10, result.Released);
        var items = await pool.ListAsync(new ReleasedIdFilter(Reference: result.ReleaseId));
        Assert.Equal(Enumerable.Range(12221, 10).Select(number => $"VPC-{number}"), items.Select(item => item.SerialNumber).Order(StringComparer.Ordinal));
        Assert.All(items, item => Assert.Equal(ReleasedIdStatuses.Available, item.Status));
        Assert.Equal($"{Base}/25S/UN778474481-VPC-12221", items.Single(item => item.SerialNumber == "VPC-12221").Identifier);
        var release = (await pool.ReleasesAsync()).Single(summary => summary.ReleaseId == result.ReleaseId);
        Assert.Equal(("PO 4512, line 2", model, 10, 10), (release.Reference, release.ModelId, release.Total, release.Count(ReleasedIdStatuses.Available)));
    }

    [Theory]
    [InlineData("VPC-12221", "VPC-12230", "9", "that is 10 IDs, not 9")]
    [InlineData("VPC-12221", "XYZ-12230", "10", "the same letters before the number")]
    [InlineData("VPC-0001", "VPC-00010", "10", "the same letters before the number")]
    [InlineData("VPC-12230", "VPC-12221", "10", "the end comes before the start")]
    [InlineData("VPC-12221", "VPC-12230", "", "a range needs a start, an end and a quantity")]
    [InlineData("VPC-A", "VPC-B", "2", "end in a number")]
    public void ARangeThatDoesntAddUp_ShouldBeRefused(string start, string end, string quantity, string reason)
    {
        var problems = new List<IdProblem>();

        Assert.Empty(BatteryIdPool.Expand(new IdRange(start, end, quantity), problems));
        Assert.Contains(reason, Assert.Single(problems).Reason, StringComparison.Ordinal);
    }

    [Fact]
    public void ARange_ShouldKeepItsLettersAndDigitCount() =>
        Assert.Equal(["SN-0098", "SN-0099", "SN-0100"], BatteryIdPool.Expand(new IdRange("SN-0098", "SN-0100", "3"), new List<IdProblem>()));

    [Fact]
    public async Task ARelease_ShouldBeAllOrNothing_ListingEveryReason()
    {
        var (rig, pool, c7) = await SeededAsync(mongo);
        using var __ = rig;
        await pool.ReleaseAsync(Who, List("PO 4513", "VPC-12301"));
        // A battery made before the pool (6d-1), from a typed serial number.
        await rig.Services.GetRequiredService<PackCreationService>().CreateAsync(ChangeSender.Team(Who), NewBattery(c7, "LEGACY-0001") with { FromPool = false });

        var result = await pool.ReleaseAsync(Who, new ReleaseRequest("PO 4514", null, [new IdRange("VPC-12228", "VPC-12240", "12")],
            "VPC-12401\nVPC-12401\nVPC12301\nTEST-9\n---\nDEMO-C7M-0001\nLEGACY0001\nVPC12402\nVPC-12402\nvpc-12403\nVPC 12404"));

        Assert.Equal(0, result.Released);
        Assert.Null(result.ReleaseId);
        var reasons = result.Problems.ToDictionary(problem => problem.What, problem => problem.Reason);
        Assert.Equal("that is 13 IDs, not 12", reasons["VPC-12228 to VPC-12240"]);
        Assert.Equal("it is in this release twice", reasons["VPC-12401"]);
        Assert.Equal("already released (Available, PO 4513)", reasons["VPC12301"]); // only the dashes differ (6d-5)
        Assert.Equal(DunsIdentifierScheme.StartsWithTest, reasons["TEST-9"]);
        Assert.Equal(IdentifierForms.SerialRule, reasons["---"]);
        Assert.Equal("already released (Published, Demo)", reasons["DEMO-C7M-0001"]); // one reason per serial number
        Assert.Equal("battery LEGACY-0001 already has this serial number or battery ID", reasons["LEGACY0001"]);
        Assert.Equal("it is in this release twice, also as VPC12402", reasons["VPC-12402"]);
        Assert.Equal(IdentifierForms.SerialRule, reasons["vpc-12403"]); // never upper-cased silently
        Assert.Equal(IdentifierForms.SerialRule, reasons["VPC 12404"]);
        Assert.DoesNotContain(await pool.ReleasesAsync(), release => release.Reference == "PO 4514");
    }

    [Fact]
    public async Task TheSerialRule_ShouldHoldForEveryRelease_WhateverTheIdScheme()
    {
        var pool = new BatteryIdPool(new DppStore(mongo.NewContext()), new TestIdentifierScheme(), new ApplicationAuditService(mongo.NewContext()), TimeProvider.System);

        var result = await pool.ReleaseAsync(Who, List("PO rule", "VBC-92921", "vbc-92922", "VBC-92923-"));

        Assert.False(result.Done);
        Assert.Equal(["vbc-92922", "VBC-92923-"], result.Problems.Select(problem => problem.What));
        Assert.All(result.Problems, problem => Assert.Equal(IdentifierForms.SerialRule, problem.Reason));
    }

    [Fact]
    public async Task AReleaseWithoutAReference_ShouldBeRefused() =>
        Assert.Equal("give the release a reference, such as the purchase order", (await new BatteryIdPool(new DppStore(mongo.NewContext()), new TestIdentifierScheme(), new ApplicationAuditService(mongo.NewContext()), TimeProvider.System).ReleaseAsync(Who, List(" ", "VPC-1"))).Problem);

    [Fact]
    public async Task TwoOverlappingReleasesAtOnce_ShouldNeverShareAnId()
    {
        var (rig, pool, _) = await SeededAsync(mongo);
        using var __ = rig;

        var results = await Task.WhenAll(Enumerable.Range(0, 6).Select(index => pool.ReleaseAsync(Who, Range($"PO race {index}", "RACE-0001", "RACE-0020", 20))));

        Assert.Single(results, result => result.Done);
        Assert.All(results.Where(result => !result.Done), result => Assert.True(result.Problem == BatteryIdPool.RaceLost || result.Problems.Count > 0));
        var raced = await pool.ListAsync(new ReleasedIdFilter(Query: "RACE"));
        Assert.Equal(20, raced.Count);
        Assert.Single(raced.Select(item => item.ReleaseId).Distinct());
    }

    [Fact]
    public async Task OneId_ShouldBePickedByOneBatteryOnly_EvenAtOnce()
    {
        var (rig, pool, _) = await SeededAsync(mongo);
        using var __ = rig;
        await pool.ReleaseAsync(Who, List("PO pick", "PICK-0001"));

        var picks = await Task.WhenAll(Enumerable.Range(0, 20).Select(index => pool.PickAsync("PICK-0001", $"pack-{index}", null)));

        Assert.Single(picks, picked => picked is not null);
    }

    [Fact]
    public async Task ABattery_ShouldBeCreatedFromAReleasedId_AndOnlyFromOne()
    {
        var (rig, pool, c7) = await SeededAsync(mongo);
        using var __ = rig;
        await pool.ReleaseAsync(Who, List("PO 4512", "VPC-12221"));
        var creation = rig.Services.GetRequiredService<PackCreationService>();

        var made = await creation.CreateAsync(ChangeSender.Team(Who), NewBattery(c7, "vpc 12221"));
        var again = await creation.CreateAsync(ChangeSender.Team(Who), NewBattery(c7, "VPC-12221"));
        var never = await creation.CreateAsync(ChangeSender.Team(Who), NewBattery(c7, "VPC-99999"));

        var pack = made.Pack!;
        Assert.Equal("VPC-12221", pack.Values["batterySerialNumber"].Value!.GetValue<string>()); // the released spelling
        Assert.Equal($"{Base}/25S/UN778474481-VPC-12221", pack.Values["uniqueProductIdentifier"].Value!.GetValue<string>());
        Assert.Equal((ReleasedIdStatuses.Used, pack.Id), ((await pool.GetAsync("VPC-12221"))!.Status, (await pool.GetAsync("VPC-12221"))!.PackId));
        Assert.Null(again.Pack); // taken (the serial number is another battery's)
        Assert.Equal(PackCreationService.NotAvailable, never.Problem);
    }

    [Fact]
    public async Task AModelsId_ShouldOnlyGoToThatModel()
    {
        var (rig, pool, c7) = await SeededAsync(mongo);
        using var __ = rig;
        var c7Model = (await rig.Store.GetAsync<ProductionBatch>(c7.BatchId))!.ModelId;
        var otherModel = (await rig.Store.ListAsync<BatteryModel>()).First(model => model.Id != c7Model).Id;
        await pool.ReleaseAsync(Who, new ReleaseRequest("PO model", otherModel, [], "MODEL-0001"));

        var made = await rig.Services.GetRequiredService<PackCreationService>().CreateAsync(ChangeSender.Team(Who), NewBattery(c7, "MODEL-0001"));

        Assert.Equal(PackCreationService.NotAvailable, made.Problem);
        Assert.DoesNotContain(await pool.AvailableAsync(c7Model), item => item.SerialNumber == "MODEL-0001");
        Assert.Contains(await pool.AvailableAsync(otherModel), item => item.SerialNumber == "MODEL-0001");
    }

    [Fact]
    public async Task ADraft_ShouldSwapItsId_GiveItBackWhenRemoved_AndKeepItOncePublished()
    {
        var (rig, pool, c7) = await SeededAsync(mongo);
        using var __ = rig;
        await pool.ReleaseAsync(Who, List("PO swap", "SWAP-0001", "SWAP-0002"));
        var draft = (await rig.Services.GetRequiredService<PackCreationService>().CreateAsync(ChangeSender.Team(Who), NewBattery(c7, "SWAP-0001"))).Pack!;

        Assert.Null(await pool.SwapAsync(Who, draft.Id, "SWAP-0002"));
        var swapped = (await rig.Store.GetAsync<BatteryPack>(draft.Id))!;
        Assert.Equal("SWAP-0002", swapped.Values["batterySerialNumber"].Value!.GetValue<string>());
        Assert.EndsWith("/25S/UN778474481-SWAP-0002", swapped.Values["uniqueProductIdentifier"].Value!.GetValue<string>(), StringComparison.Ordinal);
        Assert.Equal(ReleasedIdStatuses.Available, (await pool.GetAsync("SWAP-0001"))!.Status);

        Assert.Null((await rig.Services.GetRequiredService<BatteryPages>().RemoveDraftAsync(Who, draft.Id)).Problem);
        Assert.Equal((ReleasedIdStatuses.Available, (string?)null), ((await pool.GetAsync("SWAP-0002"))!.Status, (await pool.GetAsync("SWAP-0002"))!.PackId));

        // A published battery's ID is fixed: DEMO-C7M-0001 was published by the demo data.
        Assert.Equal(ReleasedIdStatuses.Published, (await pool.GetAsync("DEMO-C7M-0001"))!.Status);
        Assert.Equal("its passport is published, so its battery ID is fixed", await pool.SwapAsync(Who, c7.Id, "SWAP-0001"));
    }

    [Fact]
    public async Task AVoidedId_ShouldNeverBeUsedOrReleasedAgain()
    {
        var (rig, pool, c7) = await SeededAsync(mongo);
        using var __ = rig;
        var release = await pool.ReleaseAsync(Who, List("PO void", "VOID-0001", "VOID-0002", "VOID-0003"));

        Assert.Equal("give a reason, such as a damaged label", await pool.VoidAsync(Who, "VOID-0001", " "));
        Assert.Null(await pool.VoidAsync(Who, "VOID-0001", "Label damaged"));
        Assert.Equal((2L, (string?)null), await pool.VoidUnusedAsync(Who, release.ReleaseId!, "Order cancelled"));
        Assert.Equal("only an available ID can be voided; this one is voided", await pool.VoidAsync(Who, "VOID-0002", "Again"));

        Assert.Equal(PackCreationService.NotAvailable, (await rig.Services.GetRequiredService<PackCreationService>().CreateAsync(ChangeSender.Team(Who), NewBattery(c7, "VOID-0001"))).Problem);
        Assert.Equal("already released (Voided, PO void)", Assert.Single((await pool.ReleaseAsync(Who, List("PO again", "VOID-0001"))).Problems).Reason);
        Assert.Equal("Label damaged", (await pool.GetAsync("VOID-0001"))!.VoidReason);
    }

    [Fact]
    public async Task ScanningAReleasedIdBeforePublication_ShouldSayNotPublishedYet_AndAVoidedOneNotAvailable()
    {
        var (rig, pool, _) = await SeededAsync(mongo);
        using var __ = rig;
        await pool.ReleaseAsync(Who, List("PO scan", "SCAN-0001", "SCAN-0002"));
        await pool.VoidAsync(Who, "SCAN-0002", "Label damaged");
        await rig.Store.ReleasedIds.InsertOneAsync(new ReleasedBatteryId // released before 6d-5, in the old form
        {
            Id = "OLD0001",
            SerialNumber = "OLD-0001",
            Identifier = $"{Base}/25S/UN778474481OLD0001",
            ReleaseId = "old-release",
            Reference = "PO old",
            Status = ReleasedIdStatuses.Available,
            ReleasedBy = Who,
            ReleasedAtUtc = DateTime.UtcNow,
        });
        var controller = new PassportController(new DppReaders(rig.Services.GetRequiredService<ClusterRepository>()), rig.Services.GetRequiredService<PassportPages>())
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity()) } },
        };

        var available = Assert.IsType<ViewResult>(await controller.Summary("UN778474481-SCAN-0001", CancellationToken.None, pool));
        var voided = Assert.IsType<ViewResult>(await controller.Summary("UN778474481-SCAN-0002", CancellationToken.None, pool));
        var published = Assert.IsType<ViewResult>(await controller.Summary("UN778474481-TEST-DEMO-C7M-0001", CancellationToken.None, pool));
        var old = Assert.IsType<ViewResult>(await controller.Summary("UN778474481OLD0001", CancellationToken.None, pool));

        Assert.Equal(("~/Views/Dpp/NotPublishedYet.cshtml", 404), (available.ViewName, available.StatusCode));
        Assert.Equal(("~/Views/Dpp/NotPublishedYet.cshtml", 404), (old.ViewName, old.StatusCode));
        Assert.Equal(("~/Views/Dpp/NotAvailable.cshtml", 404), (voided.ViewName, voided.StatusCode));
        Assert.Equal("Summary", published.ViewName);
        var view = File.ReadAllText(DppTestFiles.RepoFile("web", "Views", "Dpp", "NotPublishedYet.cshtml"));
        Assert.Contains("This is a Scania Industrial Batteries battery ID, but the battery's passport isn't published yet.", view, StringComparison.Ordinal);
        Assert.DoesNotContain("Model.PackId", view, StringComparison.Ordinal); // no battery data, not even which battery
    }

    [Fact]
    public async Task ADraftsQrCode_ShouldCarryItsReleasedIdsAddress_ForTheTeam()
    {
        var (rig, pool, c7) = await SeededAsync(mongo);
        using var __ = rig;
        await pool.ReleaseAsync(Who, List("PO qr", "QR-0001"));
        await rig.Services.GetRequiredService<PackCreationService>().CreateAsync(ChangeSender.Team(Who), NewBattery(c7, "QR-0001"));
        var qr = new QrController(rig.Services.GetRequiredService<PassportPages>(), rig.Services.GetRequiredService<DppLinks>(), new PassportQrCodeService(), rig.Store)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Email, Who), new Claim(ClaimTypes.Role, AccessControlService.RoleAdmin)], "test")) },
            },
        };

        var svg = Assert.IsType<ContentResult>(await qr.QrCode("UN778474481-QR-0001", CancellationToken.None));

        Assert.Contains("<svg", svg.Content, StringComparison.Ordinal); // labels can be printed before publication (decision 30)
    }

    [Fact]
    public async Task TheDemoData_ShouldComeFromADemoRelease_WithSpares_AndClearOnlyItself()
    {
        var (rig, pool, _) = await SeededAsync(mongo);
        using var __ = rig;
        await pool.ReleaseAsync(Who, List("PO real", "REAL-0001"));

        var demo = (await pool.ReleasesAsync()).Single(release => release.Reference == DemoDataService.DemoReference);
        var spares = await pool.ListAsync(new ReleasedIdFilter(Query: "DEMOSPARE", Demo: true));
        await rig.Demo.ClearAsync(Who);

        Assert.Equal(DemoDataService.SpareSerials.Length, demo.Count(ReleasedIdStatuses.Available));
        Assert.True(demo.Count(ReleasedIdStatuses.Published) > 0);
        Assert.All(spares, spare => Assert.Matches("/25S/UN778474481-TEST-DEMO-SPARE-000[1-4]$", spare.Identifier));
        Assert.Equal(["REAL-0001"], (await pool.ListAsync(new ReleasedIdFilter())).Select(item => item.SerialNumber)); // real IDs stay
    }

    [Fact]
    public async Task TheApi_ShouldCreateABatteryOnlyFromAReleasedSerial()
    {
        var (rig, pool, c7) = await SeededAsync(mongo);
        using var __ = rig;
        await pool.ReleaseAsync(Who, List("PO api", "API-0001"));
        var api = rig.Services.GetRequiredService<BatteryPassWeb.Dpp.Api.BatteryApi>();

        var made = await api.CreateAsync(ChangeSender.Team(Who), c7.BatchId, c7.OperatorId, null, new Dictionary<string, JsonNode?> { ["batterySerialNumber"] = "API-0001" });
        var refused = await api.CreateAsync(ChangeSender.Team(Who), c7.BatchId, c7.OperatorId, null, new Dictionary<string, JsonNode?> { ["batterySerialNumber"] = "API-0002" });

        Assert.EndsWith("/25S/UN778474481-API-0001", made.Pack!.Values["uniqueProductIdentifier"].Value!.GetValue<string>(), StringComparison.Ordinal);
        Assert.Equal(PackCreationService.NotAvailable, refused.Problem);
    }

    [Fact]
    public async Task TheExport_ShouldBeAnExcelFileWithTextCells_ThatKeepLeadingZeros()
    {
        var (rig, pool, _) = await SeededAsync(mongo);
        using var __ = rig;
        await pool.ReleaseAsync(Who, List("=PO Åsa; \"x\"", "000123", "2026-11"));
        var items = await pool.ListAsync(new ReleasedIdFilter(Reference: (await pool.ReleasesAsync()).First().ReleaseId));

        var file = BatteryIdPool.Export(items, new Dictionary<string, string>());

        using var zip = new ZipArchive(new MemoryStream(file));
        var sheet = new StreamReader(zip.GetEntry("xl/worksheets/sheet1.xml")!.Open()).ReadToEnd();
        var rows = Regex.Matches(sheet, "<row r=\"(\\d+)\">(.*?)</row>").Select(row => Regex.Matches(row.Groups[2].Value, "<t xml:space=\"preserve\">(.*?)</t>").Select(cell => System.Net.WebUtility.HtmlDecode(cell.Groups[1].Value)).ToArray()).ToArray();
        Assert.Equal(["Serial number", "Battery ID", "QR code address", "Model", "Reference", "Released by", "Released (UTC)", "Status"], rows[0]);
        Assert.Equal(["000123", "UN778474481-000123", $"{Base}/25S/UN778474481-000123", "Any model", "=PO Åsa; \"x\""], rows[1][..5]);
        Assert.Equal([items[0].ReleasedBy, "Available"], rows[1][5..]); // the date cell isn't text
        Assert.DoesNotContain("<f>", sheet, StringComparison.Ordinal); // text cells: no formula, nothing turned into a number or date
        Assert.Contains("t=\"inlineStr\"", sheet, StringComparison.Ordinal);

        // The release time is a real date cell, formatted as a date, in a column wide enough to show it.
        var date = Regex.Match(sheet, "<c r=\"G2\" s=\"2\"><v>([0-9.]+)</v></c>");
        Assert.True(date.Success);
        Assert.Equal(items[0].ReleasedAtUtc, DateTime.FromOADate(double.Parse(date.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture)), TimeSpan.FromMilliseconds(1));
        Assert.True(int.Parse(Regex.Match(sheet, "<col min=\"7\" max=\"7\" width=\"(\\d+)\" customWidth=\"1\"/>").Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture) >= 18); // "2026-11-08 13:22" shows, not ####
        Assert.Contains($"formatCode=\"{XlsxWriter.DateFormat}\"", new StreamReader(zip.GetEntry("xl/styles.xml")!.Open()).ReadToEnd(), StringComparison.Ordinal);
        Assert.Equal("Battery IDs", Regex.Match(new StreamReader(zip.GetEntry("xl/workbook.xml")!.Open()).ReadToEnd(), "sheet name=\"([^\"]+)\"").Groups[1].Value);
    }

    [Fact]
    public void ThePages_ShouldBeTheTeamsOnly_AndWiredIn()
    {
        Assert.Equal("AdminOnly", typeof(BatteryIdsController).GetCustomAttributes(typeof(AuthorizeAttribute), true).Cast<AuthorizeAttribute>().Single().Policy);
        Assert.Contains("href=\"/admin/battery-ids\">Battery IDs</a>", File.ReadAllText(DppTestFiles.RepoFile("web", "Views", "Shared", "_AdminTabs.cshtml")), StringComparison.Ordinal);
        var create = File.ReadAllText(DppTestFiles.RepoFile("web", "Views", "Batteries", "NewBattery.cshtml"));
        Assert.Contains("<select name=\"v.batterySerialNumber\" required data-id-picker>", create, StringComparison.Ordinal); // picked, never typed
        Assert.Contains("field.Attribute.Property != \"batterySerialNumber\"", create, StringComparison.Ordinal);
        Assert.Contains("data-open-dialog=\"swap-id-dialog\"", File.ReadAllText(DppTestFiles.RepoFile("web", "Views", "Batteries", "Battery.cshtml")), StringComparison.Ordinal);
        Assert.Contains("@Option(\"dpp-battery-ids\", \"Battery IDs\", filter.EntityType)", File.ReadAllText(DppTestFiles.RepoFile("web", "Views", "Admin", "AuditLog.cshtml")), StringComparison.Ordinal);
    }
}
