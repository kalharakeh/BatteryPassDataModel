using System.Text;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using BatteryPassWeb.Dpp.Editing;
using BatteryPassWeb.Dpp.Store;
using BatteryPassWeb.Dpp.Uploads;

namespace BatteryPassWeb.Tests.Dpp;

/// <summary>
/// R09: a battery needs a state of charge (#63) and a temperature (#87) before it publishes. Created from its battery
/// system's export, it takes the export's readings as its first values; the export has no temperature yet, so that one
/// is typed.
/// </summary>
[Collection(MongoCollection.Name)]
public sealed class NewBatteryFromExportTests(MongoFixture mongo)
{
    private const string Serial = "VPC9-0100";

    [Fact]
    public async Task AnExportForTheBattery_ShouldBeAccepted_AndOneForAnotherShouldNot()
    {
        var rig = await PublicationTesting.RigAsync(mongo);
        var pages = UploadCheckTests.Pages(rig);
        var export = Bytes(BmsExportUploadTests.Export(Serial));

        Assert.Null(pages.NewBatteryFileProblem(export, Serial));
        Assert.Null(pages.NewBatteryFileProblem(export, " vpc9-0100 "));
        Assert.Equal("the file is for battery VPC9-0100; choose that serial number, or create the battery without the file", pages.NewBatteryFileProblem(export, "VPC9-0101"));
        Assert.Equal(UploadService.NotJson, pages.NewBatteryFileProblem(Encoding.UTF8.GetBytes("not json"), Serial));
    }

    [Fact]
    public async Task ABatteryCreatedFromItsExport_ShouldHaveItsStateOfChargeWithoutTyping_AndStillNeedItsTemperature()
    {
        var rig = await PublicationTesting.RigAsync(mongo).WithClustersAsync();
        var created = await BatteryTesting.Creation(rig).CreateAsync(
            PublicationTesting.Team,
            new NewPack(rig.Seed.Sources.Batch.Id, rig.Seed.Sources.Operator.Id, null, new Dictionary<string, JsonNode?> { ["batterySerialNumber"] = Serial }));
        var packId = created.Pack!.Id;

        var (done, problem) = await UploadCheckTests.Pages(rig).FillNewBatteryAsync(PublicationTesting.Team, packId, "export.json", Bytes(BmsExportUploadTests.Export(Serial)));

        Assert.Null(problem);
        Assert.StartsWith("Saved:", done, StringComparison.Ordinal);
        var pack = (await rig.Store.GetAsync<BatteryPack>(packId))!;
        Assert.Equal(27, pack.Values["stateOfChargeSoC"].Value!["percentageValue"]!.GetValue<int>());
        Assert.Equal(12, pack.Values["numberOfFullChargingAndDischargingCycles"].Value!.GetValue<int>());
        Assert.Equal("2026-03", pack.Values["manufacturingDate"].Value!.GetValue<string>());
        Assert.False(pack.Values.ContainsKey("temperatureInformation"));
        var blocking = (await rig.Publication.CheckAsync(packId))!.Blocking;
        Assert.DoesNotContain(blocking, item => item.StartsWith("#63 ", StringComparison.Ordinal));
        Assert.Contains(blocking, item => item.StartsWith("#87 ", StringComparison.Ordinal));
    }

    [Fact]
    public async Task TheNewBatteryPage_ShouldCreateTheBatteryWithItsExportsReadings_AndSayToTypeTheTemperature()
    {
        var rig = await PublicationTesting.RigAsync(mongo).WithClustersAsync();
        var bytes = Bytes(BmsExportUploadTests.Export(Serial));
        var form = new Dictionary<string, string> { [BatteryTesting.Field("batterySerialNumber")] = Serial };
        var controller = BatteriesControllerTests.Controller(rig, form);

        var result = await controller.CreateBattery(rig.Seed.Sources.Batch.Id, rig.Seed.Sources.Operator.Id, null, CancellationToken.None, new FormFile(new MemoryStream(bytes), 0, bytes.Length, "export", "export.json"));

        var url = Assert.IsType<RedirectResult>(result).Url;
        var status = Assert.IsType<string>(controller.TempData[BatteryPassWeb.Controllers.ProductDataController.StatusKey]);
        Assert.StartsWith("Draft battery created with its export's readings.", status, StringComparison.Ordinal);
        Assert.EndsWith("Type its temperature: the export has none yet.", status, StringComparison.Ordinal);
        var pack = (await rig.Store.GetAsync<BatteryPack>(url["/admin/batteries/".Length..]))!;
        Assert.Equal(27, pack.Values["stateOfChargeSoC"].Value!["percentageValue"]!.GetValue<int>());
    }

    [Fact]
    public async Task AnExportForAnotherBattery_ShouldCreateNothing()
    {
        var rig = await PublicationTesting.RigAsync(mongo).WithClustersAsync();
        var bytes = Bytes(BmsExportUploadTests.Export("VPC9-0999"));
        var form = new Dictionary<string, string> { [BatteryTesting.Field("batterySerialNumber")] = Serial };
        var controller = BatteriesControllerTests.Controller(rig, form);

        var result = await controller.CreateBattery(rig.Seed.Sources.Batch.Id, rig.Seed.Sources.Operator.Id, null, CancellationToken.None, new FormFile(new MemoryStream(bytes), 0, bytes.Length, "export", "export.json"));

        Assert.Equal("NewBattery", Assert.IsType<ViewResult>(result).ViewName);
        Assert.Equal("The battery wasn't created: the file is for battery VPC9-0999; choose that serial number, or create the battery without the file.", controller.ViewData[BatteryPassWeb.Controllers.ProductDataController.ErrorKey]);
        Assert.Empty(await rig.Store.FindPacksBySerialNumberAsync(Serial));
    }

    [Fact]
    public async Task TheTemperatureField_ShouldSayThatTheExportHasNone_OnlyWhileItIsEmpty()
    {
        var rig = await PublicationTesting.RigAsync(mongo).WithClustersAsync();
        string? Note(BatteryPassWeb.Dpp.Pages.BatteryPage page) => page.Live.Sections.SelectMany(section => section.Fields).Single(field => field.Attribute.Number == 87).ExportNote;

        Assert.Null(Note((await BatteryTesting.Pages(rig).BatteryAsync(rig.PackId))!)); // the seed battery has a temperature
        await rig.Store.SetValuesAsync<BatteryPack>(rig.PackId, new Dictionary<string, BatteryPassWeb.Dpp.Values.AttributeValue>(), ["temperatureInformation"]);
        Assert.Equal("The battery system's export has no temperature yet: type it here.", Note((await BatteryTesting.Pages(rig).BatteryAsync(rig.PackId))!));
        Assert.Contains("field.ExportNote", File.ReadAllText(DppTestFiles.RepoFile("web", "Views", "ProductData", "_EditorField.cshtml")), StringComparison.Ordinal); // the page shows it
    }

    [Fact]
    public async Task AnExportWhoseReadingsAreAllRefused_ShouldSaySo()
    {
        var rig = await PublicationTesting.RigAsync(mongo).WithClustersAsync();
        var export = BmsExportUploadTests.Export(Serial);
        export["values"] = new JsonObject { ["stateOfCharge"] = new JsonObject { ["raw"] = 1, ["formatted"] = "999 %" } };
        export["metadata"]!["identifiers"]!["deviceIdentifiers"]!.AsObject().Remove("childrenIdentifiers");
        var bytes = Bytes(export);
        var form = new Dictionary<string, string> { [BatteryTesting.Field("batterySerialNumber")] = Serial };
        var controller = BatteriesControllerTests.Controller(rig, form);

        await controller.CreateBattery(rig.Seed.Sources.Batch.Id, rig.Seed.Sources.Operator.Id, null, CancellationToken.None, new FormFile(new MemoryStream(bytes), 0, bytes.Length, "export", "export.json"));

        var status = Assert.IsType<string>(controller.TempData[BatteryPassWeb.Controllers.ProductDataController.StatusKey]);
        Assert.StartsWith("Draft battery created, but none of its export's readings were saved", status, StringComparison.Ordinal);
        Assert.DoesNotContain("with its export's readings", status, StringComparison.Ordinal);
    }

    private static byte[] Bytes(JsonObject export) => Encoding.UTF8.GetBytes(export.ToJsonString());
}
