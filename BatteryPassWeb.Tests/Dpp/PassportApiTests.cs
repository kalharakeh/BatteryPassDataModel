using System.Text.Json.Nodes;
using BatteryPassWeb.Dpp.Access;
using BatteryPassWeb.Dpp.Api;
using BatteryPassWeb.Dpp.Catalog;
using BatteryPassWeb.Dpp.Editing;
using BatteryPassWeb.Dpp.Identifiers;
using BatteryPassWeb.Dpp.Pages;
using BatteryPassWeb.Dpp.Publication;
using BatteryPassWeb.Dpp.Store;
using BatteryPassWeb.Services;

namespace BatteryPassWeb.Tests.Dpp;

/// <summary>The API's reading side (step 3c-1): the pages' passports and finding rules, cut by the one visibility rule.</summary>
[Collection(MongoCollection.Name)]
public sealed class PassportApiTests(MongoFixture mongo)
{
    private static readonly Reader Team = Reader.With("team@example.com", ReaderRole.Team);
    private static readonly Reader OwnCluster = Reader.With("token-north", ReaderRole.ClusterUser, BatteryTesting.North);
    private static readonly Reader OtherCluster = Reader.With("token-south", ReaderRole.ClusterUser, BatteryTesting.South);

    [Fact]
    public async Task APassport_ShouldBeFoundByBatteryIdPassportIdOrSerialNumber()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo);
        var version = (await rig.Store.GetActiveVersionAsync(rig.PackId))!;
        var api = Api(rig);

        foreach (var id in new[] { PassportPagesTests.Identifier(rig), version.DigitalProductPassportId, Serial(rig) })
        {
            var passport = await api.ReadAsync(OwnCluster, id);
            Assert.Equal(version.Id, passport?.Version.Id);
            Assert.True(passport!.SignatureVerified);
        }

        Assert.Null(await api.ReadAsync(Team, "no-such-battery"));
    }

    [Fact]
    public async Task ADraft_ShouldNotBeServedEvenToTheTeam()
    {
        var rig = await PublicationTesting.RigAsync(mongo);

        Assert.Null(await Api(rig).ReadAsync(Team, PassportPagesTests.Identifier(rig)));
    }

    [Theory]
    [InlineData("team", true, true)]
    [InlineData("own-cluster", true, false)]
    [InlineData("other-cluster", false, false)]
    public async Task ThePassport_ShouldBeCutForTheReader(string who, bool seesPurchaserData, bool seesAuthoritiesData)
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo);

        var passport = (await Api(rig).ReadAsync(ReaderFor(who), PassportPagesTests.Identifier(rig)))!;

        Assert.Equal(seesPurchaserData, Has(passport, 63));
        Assert.Equal(seesAuthoritiesData, Has(passport, 7));
        Assert.True(Has(passport, 11));
    }

    [Fact]
    public void TheSections_ShouldBeTheSchemasInItsOrder()
    {
        Assert.Equal(
            ["dppHeader", "identifiersAndProductData", "symbolsLabelsAndDocumentationOfConformity", "batteryCarbonFootprint", "supplyChainDueDiligence", "batteryMaterialsAndComposition", "circularityAndResourceEfficiency", "performanceAndDurability"],
            PassportApi.Sections);
        Assert.Equal("performanceAndDurability", PassportApi.SectionNamed(" PerformanceAndDurability "));
        Assert.Null(PassportApi.SectionNamed("performance"));
    }

    [Fact]
    public async Task ASection_ShouldHoldOnlyWhatTheReaderSees()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo);
        var api = Api(rig);

        var own = PassportApi.Section((await api.ReadAsync(OwnCluster, Serial(rig)))!, "performanceAndDurability");
        var other = PassportApi.Section((await api.ReadAsync(OtherCluster, Serial(rig)))!, "performanceAndDurability");
        var header = PassportApi.Section((await api.ReadAsync(OtherCluster, Serial(rig)))!, "dppHeader");

        Assert.True(own.ContainsKey("stateOfChargeSoC"));
        Assert.False(other.ContainsKey("stateOfChargeSoC"));
        Assert.True(other.ContainsKey("ratedCapacity"));
        Assert.False(header.ContainsKey("economicOperatorId"));
        Assert.True(header.ContainsKey("uniqueProductIdentifier"));
    }

    [Theory]
    [InlineData("stateOfChargeSoC", 63)]
    [InlineData("STATEOFCHARGESOC", 63)]
    [InlineData("#63", 63)]
    [InlineData("63", 63)]
    [InlineData("batteryPassportData.performanceAndDurability.stateOfChargeSoC", 63)]
    [InlineData("dppHeader.uniqueProductIdentifier", 6)]
    [InlineData("E1", 1001)]
    [InlineData("e4", 1004)]
    public void AValue_ShouldBeNamedByKeyNumberCodeOrPath(string request, int number)
    {
        Assert.Equal(number, PassportApi.AttributeNamed(request)?.Number);
    }

    [Theory]
    [InlineData("ratedEnergy")]
    [InlineData("#18")]
    [InlineData("E5")]
    [InlineData("aspects.performanceAndDurability.payload")]
    public void ARequestNamingNoAttribute_ShouldNameNone(string request)
    {
        Assert.Null(PassportApi.AttributeNamed(request));
    }

    [Fact]
    public async Task Values_ShouldComeWithTheirOriginOrNotAtAllForThisReader()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo);
        var api = Api(rig);

        var own = PassportApi.Values((await api.ReadAsync(OwnCluster, Serial(rig)))!, ["#63", "batteryMass", "nothing", " "]);
        var other = PassportApi.Values((await api.ReadAsync(OtherCluster, Serial(rig)))!, ["#63"]);

        Assert.Equal(["#63", "batteryMass", "nothing"], own.Select(value => value.Request));
        Assert.Equal(76, own[0].Value!["percentageValue"]!.GetValue<int>());
        Assert.Equal(new ValueOrigin(DppTestSeed.GeneratedAtUtc, "seed", Measured: true), own[0].Origin);
        Assert.Equal(21, own[1].Attribute!.Number);
        Assert.Null(own[2].Attribute);
        Assert.Null(own[2].Value);
        Assert.Equal(63, other[0].Attribute!.Number);
        Assert.Null(other[0].Value);
        Assert.Null(other[0].Origin);
    }

    [Fact]
    public async Task TheReadableValues_ShouldBeThoseTheReaderSeesAValueOf()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo);
        var api = Api(rig);

        var other = PassportApi.Readable((await api.ReadAsync(OtherCluster, Serial(rig)))!);
        var team = PassportApi.Readable((await api.ReadAsync(Team, Serial(rig)))!);
        var performance = PassportApi.Readable((await api.ReadAsync(Team, Serial(rig)))!, "performanceAndDurability");

        Assert.All(other, attribute => Assert.Equal(Audience.Public, attribute.Audience));
        Assert.Contains(team, attribute => attribute.Number == 7);
        Assert.Contains(team, attribute => attribute.Number == 63);
        Assert.All(performance, attribute => Assert.Equal("performanceAndDurability", attribute.Section));
        Assert.True(other.Count < team.Count);
    }

    [Fact]
    public async Task TheUnitsOfE1ToE4_ShouldComeFromTheCatalogue()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo);
        await rig.Pipeline.SaveAsync(
            PublicationTesting.Team,
            new ChangeSet(TargetKind.Pack, rig.PackId, new Dictionary<string, JsonNode?> { ["timeSpentInExtremeTemperaturesAboveBoundary"] = JsonValue.Create(7200) }, "form"));

        var value = PassportApi.Values((await Api(rig).ReadAsync(OwnCluster, Serial(rig)))!, ["E1"]).Single();

        Assert.NotNull(value.Value);
        Assert.Equal(AttributeCatalog.Extensions[0].Unit, value.Attribute!.Unit);
    }

    [Fact]
    public async Task Versions_ShouldBeListedNewestFirst()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo);
        rig.Clock.Now = rig.Clock.Now.AddHours(1);
        await rig.Pipeline.SaveAsync(
            PublicationTesting.Team,
            new ChangeSet(TargetKind.Model, "model-test", new Dictionary<string, JsonNode?> { ["batteryMass"] = JsonValue.Create(520) }, "form"));

        var versions = await Api(rig).VersionsAsync((await Api(rig).ReadAsync(OtherCluster, Serial(rig)))!);

        Assert.Equal([2, 1], versions.Select(version => version.Number));
        Assert.Equal([DppStatuses.Active, DppStatuses.Archived], versions.Select(version => version.DppStatus));
        Assert.All(versions, version => Assert.True(PassportReader.Verify(version)));
    }

    [Fact]
    public async Task AClustersBatteries_ShouldListDraftsForTheTeamOnly()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo).WithClustersAsync();
        var creation = new PackCreationService(rig.Store, TestDemo.For(rig), rig.Pipeline, new TestIdentifierScheme(), DocumentTesting.Links, new ApplicationAuditService(rig.Context), rig.Clock);
        var created = await creation.CreateAsync(PublicationTesting.Team, new NewPack(
            rig.Seed.Sources.Batch.Id,
            rig.Seed.Sources.Operator.Id,
            BatteryTesting.North,
            new Dictionary<string, JsonNode?> { ["batterySerialNumber"] = JsonValue.Create("VPC9-3001") }));
        Assert.NotNull(created.Pack);
        var api = Api(rig);

        var team = await api.BatteriesAsync(Team, BatteryTesting.North);
        var own = await api.BatteriesAsync(OwnCluster, BatteryTesting.North);
        var south = await api.BatteriesAsync(Team, BatteryTesting.South);

        Assert.Equal(2, team.Count);
        Assert.Equal([rig.PackId], own.Select(row => row.PackId));
        Assert.Empty(south);
    }

    internal static PassportApi Api(PublicationRig rig) =>
        new(rig.Store, TestDemo.For(rig), new PassportReader(rig.Store), PassportPagesTests.Pages(rig), BatteryTesting.Pages(rig));

    private static string Serial(PublicationRig rig) => rig.Seed.Sources.Pack.Values["batterySerialNumber"].Value.GetValue<string>();

    private static bool Has(ApiPassport passport, int number)
    {
        JsonNode? node = passport.Passport;
        foreach (var name in AttributeCatalog.ByNumber(number).Path.Split('.'))
        {
            node = node?[name];
        }

        return node is not null;
    }

    private static Reader ReaderFor(string who) => who switch
    {
        "team" => Team,
        "own-cluster" => OwnCluster,
        _ => OtherCluster,
    };
}
