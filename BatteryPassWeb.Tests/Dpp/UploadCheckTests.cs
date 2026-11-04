using System.Text;
using BatteryPassWeb.Dpp.Editing;
using BatteryPassWeb.Dpp.Pages;
using BatteryPassWeb.Dpp.Store;
using BatteryPassWeb.Dpp.Uploads;
using BatteryPassWeb.Dpp.Values;

namespace BatteryPassWeb.Tests.Dpp;

/// <summary>
/// A file uploaded on a battery's page (decision 35, step 5c): for that battery only, checked before saving, kept an
/// hour for its checker's "Save", and the battery's upload history as each reader may see it.
/// </summary>
[Collection(MongoCollection.Name)]
public sealed class UploadCheckTests(MongoFixture mongo)
{
    private static readonly ChangeSender NorthAdmin = ChangeSender.ClusterAdmin("north-admin@example.com", BatteryTesting.North);

    [Fact]
    public async Task ACheckedFile_ShouldSaveNothingUntilItsCheckerSavesItOnce()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo).WithClustersAsync();
        var service = UploadServiceTests.Service(rig);

        var check = await service.CheckBatteryFileAsync(NorthAdmin, rig.PackId, "readings.json", Native(UploadServiceTests.Serial(rig), 41));

        Assert.Null(check.Problem);
        Assert.NotNull(check.CheckId);
        Assert.True(check.Preview);
        Assert.Equal(76, StateOfCharge(await Pack(rig)));
        Assert.Null(await service.SaveCheckedAsync(ChangeSender.ClusterAdmin("other-admin@example.com", BatteryTesting.North), rig.PackId, check.CheckId));
        Assert.Null(await service.SaveCheckedAsync(NorthAdmin, "another-pack", check.CheckId));

        var saved = (await service.SaveCheckedAsync(NorthAdmin, rig.PackId, check.CheckId))!;

        Assert.False(saved.Preview);
        Assert.Equal(UploadService.Saved, Assert.Single(saved.Batteries).Outcome);
        Assert.Equal(41, StateOfCharge(await Pack(rig)));
        var logged = Assert.Single(await rig.Store.ListUploadsAsync(10));
        Assert.Equal(("readings.json", "page", BatteryTesting.North), (logged.FileName, logged.Channel, logged.Batteries[0].ClusterId));
        Assert.Null(await service.SaveCheckedAsync(NorthAdmin, rig.PackId, check.CheckId)); // saved once
    }

    [Fact]
    public async Task APassportNoLongerActive_ShouldRefuseTheFileBeforeTheCheck()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo).WithClustersAsync();
        Assert.Null(await PassportMarkTests.Service(rig).MarkInvalidAsync(PublicationTesting.Team, rig.PackId, "Wrong figures"));

        var report = await UploadServiceTests.Service(rig).CheckBatteryFileAsync(PublicationTesting.Team, rig.PackId, "readings.json", Native(UploadServiceTests.Serial(rig), 41));

        Assert.Equal(SavePipeline.NoLongerChanges(DppStatuses.Invalid), report.Problem);
        Assert.Null(report.CheckId);
    }

    [Fact]
    public async Task ACheck_ShouldExpireAfterAnHour()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo).WithClustersAsync();
        var service = UploadServiceTests.Service(rig);
        var check = await service.CheckBatteryFileAsync(NorthAdmin, rig.PackId, "readings.json", Native(UploadServiceTests.Serial(rig), 41));

        rig.Clock.Now += UploadService.CheckKeptFor;

        Assert.Null(await service.SaveCheckedAsync(NorthAdmin, rig.PackId, check.CheckId));
        Assert.Equal(76, StateOfCharge(await Pack(rig)));
    }

    [Fact]
    public async Task AFileForAnotherBatteryOrForSeveral_ShouldBeRefusedInPlainWords()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo).WithClustersAsync();
        var service = UploadServiceTests.Service(rig);
        var several = Encoding.UTF8.GetBytes(UploadServiceTests.File(
            UploadServiceTests.Entry(UploadServiceTests.Serial(rig), """{ "stateOfChargeSoC": 1 }"""),
            UploadServiceTests.Entry("VPC9-9999", """{ "stateOfChargeSoC": 2 }""")));

        var other = await service.CheckBatteryFileAsync(PublicationTesting.Team, rig.PackId, "a.json", Native("VPC9-9999", 41));
        var export = await service.CheckBatteryFileAsync(PublicationTesting.Team, rig.PackId, "b.json", Encoding.UTF8.GetBytes(BmsExportUploadTests.Export("VPC9-9999").ToJsonString()));
        var two = await service.CheckBatteryFileAsync(PublicationTesting.Team, rig.PackId, "c.json", several);
        var text = await service.CheckBatteryFileAsync(PublicationTesting.Team, rig.PackId, "d.txt", "not json"u8.ToArray());
        var big = await service.CheckBatteryFileAsync(PublicationTesting.Team, rig.PackId, "e.json", new byte[UploadService.MaxBytes + 1]);

        Assert.Equal(UploadService.NotThisBattery("VPC9-9999"), other.Problem);
        Assert.Equal(UploadService.NotThisBattery("VPC9-9999"), export.Problem);
        Assert.Equal(UploadService.OneBatteryOnly(2), two.Problem);
        Assert.Equal(UploadService.NotJson, text.Problem);
        Assert.Equal(UploadService.TooLarge, big.Problem);
        Assert.All(new[] { other, export, two, text, big }, report => Assert.Null(report.CheckId));
    }

    [Fact]
    public async Task AKeptCheck_ShouldBeShownAgainAsItsCheckerWouldSaveIt()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo).WithClustersAsync();
        var service = UploadServiceTests.Service(rig);
        var refused = await service.CheckBatteryFileAsync(NorthAdmin, rig.PackId, "a.json", Native(UploadServiceTests.Serial(rig), 400));
        var check = await service.CheckBatteryFileAsync(NorthAdmin, rig.PackId, "b.json", Native(UploadServiceTests.Serial(rig), 41));
        var pack = (await Pack(rig))!;

        var shown = (await service.ShowCheckAsync(pack, forTeam: false, check.CheckId))!.Value;

        Assert.Equal(UploadService.NothingSaved, Assert.Single(refused.Batteries).Outcome);
        Assert.NotNull(refused.CheckId); // shown with its refusal, without "Save"
        Assert.Equal(("b.json", UploadService.Saved, true), (shown.Check.FileName, Assert.Single(shown.Report.Batteries).Outcome, shown.Report.Preview));
        Assert.Equal("41", Assert.Single(shown.Report.Batteries).Sent!["stateOfChargeSoC"]);
        Assert.Null(await service.ShowCheckAsync(pack, forTeam: true, check.CheckId)); // the cluster admin's, not the team's
        Assert.Equal(76, StateOfCharge(await Pack(rig)));
    }

    [Fact]
    public async Task TheHistory_ShouldShowTheTeamEveryUploadAndAClusterAdminTheirClusters()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo).WithClustersAsync();
        var service = UploadServiceTests.Service(rig);
        await UploadServiceTests.Run(rig, ChangeSender.Feed("north-feed", BatteryTesting.North), UploadServiceTests.File(UploadServiceTests.Entry(UploadServiceTests.Serial(rig), """{ "stateOfChargeSoC": 50 }""")));
        rig.Clock.Now += TimeSpan.FromMinutes(1);
        await UploadServiceTests.Run(rig, PublicationTesting.Team, UploadServiceTests.File(
            UploadServiceTests.Entry(UploadServiceTests.Serial(rig), """{ "stateOfChargeSoC": 51 }"""),
            UploadServiceTests.Entry("VPC9-9999", """{ "stateOfChargeSoC": 52 }""")));

        var pack = (await Pack(rig))!;
        var team = await service.HistoryAsync(pack, forTeam: true);
        var clusterAdmins = await service.HistoryAsync(pack, forTeam: false);

        Assert.Equal(["team@example.com", "north-feed"], team.Select(upload => upload.Who));
        Assert.All(team, upload => Assert.Equal(rig.PackId, Assert.Single(upload.Batteries).PackId)); // only this battery's line
        Assert.Equal("51", team[0].Batteries[0].Values[0].Value);
        Assert.Equal(["north-feed"], clusterAdmins.Select(upload => upload.Who)); // not the team's (Q19)
    }

    internal static UploadPages Pages(PublicationRig rig) =>
        new(UploadServiceTests.Service(rig), AttributeFormsTests.Forms, new AttributeValueNormalizer(DppStoreTesting.Schemas));

    internal static byte[] Native(string serial, int stateOfCharge) =>
        Encoding.UTF8.GetBytes(UploadServiceTests.File(UploadServiceTests.Entry(serial, $$"""{ "stateOfChargeSoC": {{stateOfCharge}} }""")));

    private static Task<BatteryPack?> Pack(PublicationRig rig) => rig.Store.GetAsync<BatteryPack>(rig.PackId);

    private static int StateOfCharge(BatteryPack? pack) => pack!.Values["stateOfChargeSoC"].Value!["percentageValue"]!.GetValue<int>();
}
