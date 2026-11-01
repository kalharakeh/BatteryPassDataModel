using System.Text;
using BatteryPassWeb.Dpp.Access;
using BatteryPassWeb.Dpp.Catalog;
using BatteryPassWeb.Dpp.Pages;
using BatteryPassWeb.Dpp.Store;

namespace BatteryPassWeb.Tests.Dpp;

/// <summary>Decision 31: each battery's module list, typed or from the battery system's export, and who sees what of it.</summary>
public sealed class ModuleListTests
{
    // The shape of the battery system's export (metadata.identifiers.deviceIdentifiers), as in its sample files.
    internal static byte[] Export(string serial, string children = """
        [{ "serialNumber": "", "connectivityId": "MD-A9ASVW8", "position": 2 },
         { "serialNumber": "MOD-1", "connectivityId": "MD-15PA66R", "position": 1 }]
        """) => Encoding.UTF8.GetBytes($$"""
        { "version": "1", "metadata": { "identifiers": {
            "deviceIdentifiers": { "serialNumber": "{{serial}}", "connectivityId": "ST-ASMV6", "position": 1, "childrenIdentifiers": {{children}} },
            "parentIdentifiers": { "serialNumber": "VPY-00081", "connectivityId": "SY-J6KG" } } }, "values": {} }
        """);

    [Fact]
    public void AnExport_ShouldGiveItsModulesByPosition()
    {
        var (modules, problem) = ModuleList.FromExport(Export("VPC8C05-01036"), "vpc8c05-01036 ");

        Assert.Null(problem);
        Assert.Equal([1, 2], modules.Select(module => module.Position));
        Assert.Equal("MOD-1", modules[0].SerialNumber);
        Assert.Null(modules[1].SerialNumber); // the export's empty serial number is no serial number
        Assert.Equal("MD-A9ASVW8", modules[1].ConnectivityId);
    }

    [Fact]
    public void AnExportWithAByteOrderMark_ShouldBeRead()
    {
        var file = new byte[] { 0xEF, 0xBB, 0xBF }.Concat(Export("B-1")).ToArray();

        Assert.Null(ModuleList.FromExport(file, "B-1").Problem);
    }

    [Theory]
    [InlineData("OTHER-1", null, "the file is for battery OTHER-1, not B-1")]
    [InlineData("B-1", "[]", "the file lists no modules")]
    [InlineData("B-1", "[{ \"connectivityId\": \"MD-1\" }]", "a module in the file has no position")]
    [InlineData("B-1", "[{ \"position\": 1, \"serialNumber\": \"\" }]", "the module at position 1 needs a serial number or a connectivity ID")]
    [InlineData("B-1", "[{ \"position\": 1, \"connectivityId\": \"A\" }, { \"position\": 1, \"connectivityId\": \"B\" }]", "position 1 is listed twice")]
    public void AWrongExport_ShouldSayWhy(string serial, string? children, string expected)
    {
        var file = children is null ? Export(serial) : Export(serial, children);

        var (modules, problem) = ModuleList.FromExport(file, "B-1");

        Assert.Empty(modules);
        Assert.Equal(expected, problem);
    }

    [Fact]
    public void AFileThatIsntAnExport_ShouldSayWhy()
    {
        Assert.Equal("the file isn't a JSON export", ModuleList.FromExport(Encoding.UTF8.GetBytes("not json"), "B-1").Problem);
        Assert.StartsWith("the file has no module identifiers", ModuleList.FromExport(Encoding.UTF8.GetBytes("{}"), "B-1").Problem);
    }

    [Fact]
    public void TypedModules_ShouldReadOnePerLineAndRoundTrip()
    {
        var (modules, problem) = ModuleList.FromText("2; ; MD-2\r\n\r\n1\tMOD-1\tMD-1\n");

        Assert.Null(problem);
        Assert.Equal([1, 2], modules.Select(module => module.Position));
        Assert.Equal("1; MOD-1; MD-1\n2; ; MD-2", ModuleList.ToText(modules));
        Assert.Equal(modules.Count, ModuleList.FromText(ModuleList.ToText(modules)).Modules.Count);
        Assert.Empty(ModuleList.FromText("  ").Modules); // an empty list clears it
    }

    [Theory]
    [InlineData("1; MOD-1", "line 1 needs a position, a serial number and a connectivity ID, separated by semicolons")]
    [InlineData("first; MOD-1; MD-1", "line 1 doesn't start with a position, such as 1")]
    [InlineData("0; MOD-1; MD-1", "positions start at 1")]
    [InlineData("1; ;", "the module at position 1 needs a serial number or a connectivity ID")]
    public void WronglyTypedModules_ShouldSayWhy(string text, string expected)
    {
        Assert.Equal(expected, ModuleList.FromText(text).Problem);
    }

    [Theory]
    [InlineData(ReaderRole.Team, new string[0], true)]
    [InlineData(ReaderRole.MarketSurveillance, new string[0], true)]
    [InlineData(ReaderRole.ClusterUser, new[] { "north" }, true)]
    [InlineData(ReaderRole.ClusterAdmin, new[] { "north" }, true)]
    [InlineData(ReaderRole.ClusterUser, new[] { "south" }, false)]
    [InlineData(ReaderRole.NotifiedBody, new string[0], false)]
    [InlineData(ReaderRole.LiRecycling, new string[0], false)]
    public void TheModuleIds_ShouldReachThePurchaserAttributesReadersOnly(ReaderRole role, string[] clusters, bool expected)
    {
        var reader = Reader.With("x", role, clusters);

        Assert.Equal(expected, AudienceRules.MaySeePurchaserData(reader, ["north"]));
        Assert.Equal(AudienceRules.MaySee(reader, AttributeCatalog.ByNumber(92), ["north"]), AudienceRules.MaySeePurchaserData(reader, ["north"]));
        Assert.False(AudienceRules.MaySeePurchaserData(Reader.Anonymous, ["north"]));
    }
}

[Collection(MongoCollection.Name)]
public sealed class ModuleDataTests(MongoFixture mongo)
{
    private static string Serial(PublicationRig rig) => rig.Seed.Sources.Pack.Values["batterySerialNumber"].Value!.GetValue<string>();

    [Fact]
    public async Task TheTeam_ShouldFillABatterysModulesFromItsExportWithoutANewVersion()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo);
        var pages = BatteryTesting.Pages(rig);
        var versions = await rig.Store.GetLatestVersionNumberAsync(rig.PackId);

        Assert.Equal("the file is for battery OTHER-1, not " + Serial(rig), await pages.ImportModulesAsync("team@example.com", rig.PackId, ModuleListTests.Export("OTHER-1")));
        Assert.Null(await pages.ImportModulesAsync("team@example.com", rig.PackId, ModuleListTests.Export(Serial(rig))));

        var pack = (await rig.Store.GetAsync<BatteryPack>(rig.PackId))!;
        Assert.Equal(["MD-15PA66R", "MD-A9ASVW8"], pack.Modules.Select(module => module.ConnectivityId));
        Assert.Equal(versions, await rig.Store.GetLatestVersionNumberAsync(rig.PackId)); // not passport data: no new version

        Assert.Null(await pages.SetModulesAsync("team@example.com", rig.PackId, "1; MOD-1; MD-1"));
        Assert.Single((await rig.Store.GetAsync<BatteryPack>(rig.PackId))!.Modules);
        Assert.NotNull(await pages.SetModulesAsync("team@example.com", rig.PackId, "1; MOD-1"));
        Assert.Single((await rig.Store.GetAsync<BatteryPack>(rig.PackId))!.Modules); // a refused list changes nothing
    }

    [Fact]
    public async Task ThePassportPages_ShouldShowTheCountToEveryoneAndTheIdsToTheTeam()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo);
        await BatteryTesting.Pages(rig).ImportModulesAsync("team@example.com", rig.PackId, ModuleListTests.Export(Serial(rig)));
        await rig.Store.SetModelModuleDescriptionAsync(rig.Seed.Sources.Model.Id, "2 lithium-ion modules");
        var pages = PassportPagesTests.Pages(rig);
        var id = PassportPagesTests.Identifier(rig);

        var summary = (await pages.SummaryAsync(Reader.With("team", ReaderRole.Team), id))!;
        var outsider = (await pages.DetailAsync(Reader.With("x", ReaderRole.LiRepair), id))!;
        var team = (await pages.DetailAsync(Reader.With("team", ReaderRole.Team), id))!;

        Assert.Equal(2, summary.ModuleCount);
        Assert.Equal("2 lithium-ion modules", summary.ModuleDescription);
        Assert.Null(summary.Modules); // the summary is public data only
        Assert.Null(outsider.Modules);
        Assert.Equal(2, team.Modules!.Count);
    }

    [Fact]
    public void ThePages_ShouldOfferTheModuleForms()
    {
        var battery = DppTestFiles.BatteryPageSource();
        var family = File.ReadAllText(DppTestFiles.RepoFile("web", "Views", "ProductData", "Family.cshtml"));
        var hero = File.ReadAllText(DppTestFiles.RepoFile("web", "Views", "Passport", "_PassportHero.cshtml"));

        Assert.Contains("action=\"@Model.Href/modules/from-export\"", battery);
        Assert.Contains("action=\"@Model.Href/modules\"", battery);
        Assert.Contains("action=\"/admin/models/@selected.Id/module-description\"", family);
        Assert.Contains("Model.ModuleCount", hero);
    }
}
