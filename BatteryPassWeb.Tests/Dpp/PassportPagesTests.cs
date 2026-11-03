using System.Text.Json.Nodes;
using BatteryPassWeb.Dpp.Access;
using BatteryPassWeb.Dpp.Catalog;
using BatteryPassWeb.Dpp.Editing;
using BatteryPassWeb.Dpp.Identifiers;
using BatteryPassWeb.Dpp.Pages;
using BatteryPassWeb.Dpp.Publication;
using BatteryPassWeb.Dpp.Store;

namespace BatteryPassWeb.Tests.Dpp;

/// <summary>The public summary and the detail page's data (design 12): found by #6, #5 or serial number, cut per reader.</summary>
[Collection(MongoCollection.Name)]
public sealed class PassportPagesTests(MongoFixture mongo)
{
    private static readonly Reader OwnCluster = Reader.With("user@example.com", ReaderRole.ClusterUser, BatteryTesting.North);
    private static readonly Reader OtherCluster = Reader.With("other@example.com", ReaderRole.ClusterUser, BatteryTesting.South);

    [Fact]
    public async Task APassport_ShouldBeFoundByBatteryIdPassportIdOrSerialNumber()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo);
        var pages = Pages(rig);
        var active = await rig.Store.GetActiveVersionAsync(rig.PackId);

        Assert.Equal(active!.Id, (await pages.FindAsync(Identifier(rig)))?.Id);
        Assert.Equal(active.Id, (await pages.FindAsync(active.DigitalProductPassportId))?.Id);
        Assert.Equal(active.Id, (await pages.FindAsync(" VPC8C05-6788 "))?.Id);
        Assert.Null(await pages.FindAsync("VPC8C05-0000"));
        var located = (await pages.LocateAsync(active.DigitalProductPassportId))!;
        Assert.Equal((Identifier(rig), "VPC8C05-6788", Identifier(rig)), (located.Identifier, located.SerialNumber, located.Address));
        Assert.Null(await pages.FindAsync(""));
    }

    [Fact]
    public async Task ADraft_ShouldBeFoundByNobody()
    {
        var rig = await PublicationTesting.RigAsync(mongo);
        var pages = Pages(rig);

        Assert.Null(await pages.FindAsync(Identifier(rig)));
        Assert.Null(await pages.FindAsync("VPC8C05-6788"));
        Assert.Null(await pages.SummaryAsync(Reader.With("team@example.com", ReaderRole.Team), Identifier(rig)));
        Assert.Null(await pages.DetailAsync(Reader.With("team@example.com", ReaderRole.Team), Identifier(rig)));
    }

    [Fact]
    public async Task TheSummary_ShouldShowEveryPublicValueAndNothingElseWhoeverReads()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo);
        var team = Reader.With("team@example.com", ReaderRole.Team);

        var page = (await Pages(rig).SummaryAsync(team, Identifier(rig)))!;

        var shown = page.Sections.SelectMany(section => section.Values).ToArray();
        Assert.All(shown, value => Assert.Equal(Audience.Public, value.Attribute.Audience));
        Assert.All(shown, value => Assert.Null(value.Origin));
        Assert.Equal(AttributeCatalog.All.Count(a => a.Audience == Audience.Public), shown.Length);
        Assert.Empty(page.Audiences);
        Assert.Equal(
            ["Passport", "Identifiers and product data", "Symbols, labels and conformity", "Carbon footprint", "Due diligence", "Materials and composition", "Circularity", "Performance and durability"],
            page.Sections.Select(section => section.Title));
        Assert.Equal(
            ["Passport", "Identifiers", "Symbols and conformity", "Carbon footprint", "Due diligence", "Materials", "Circularity", "Performance"],
            page.Sections.Select(section => section.Tab));
        Assert.Null(page.Value(22));
        Assert.Equal("VPC8C05-6788", page.Text(11));
        Assert.Equal("Active", page.Text(2)); // the badge's words; the passport keeps "active" (R5)
    }

    [Fact]
    public async Task TheSummary_ShouldSayWhetherTheDetailPageHasMoreForTheReader()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo);
        var pages = Pages(rig);

        Assert.True((await pages.SummaryAsync(OwnCluster, Identifier(rig)))!.ReaderSeesMore);
        Assert.True((await pages.SummaryAsync(Reader.With("nb@example.com", ReaderRole.NotifiedBody), Identifier(rig)))!.ReaderSeesMore);
        Assert.False((await pages.SummaryAsync(OtherCluster, Identifier(rig)))!.ReaderSeesMore);
        Assert.False((await pages.SummaryAsync(Reader.Anonymous, Identifier(rig)))!.ReaderSeesMore);
    }

    [Fact]
    public async Task TheSummary_ShouldCarryTheBannerThePictureAndTheCharts()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo);
        await rig.Store.SetFamilyPictureAsync("family-test", "file-1", "image/png");

        var page = (await Pages(rig).SummaryAsync(Reader.Anonymous, "VPC8C05-6788"))!;

        Assert.True(page.IsTest);
        Assert.True(page.SignatureHolds);
        Assert.True(page.IsCurrent);
        Assert.Null(page.Current);
        Assert.Equal("Test battery family", page.FamilyName);
        Assert.Equal("/dpp/families/family-test/picture", page.PictureHref);
        Assert.Equal(Identifier(rig), page.Address);
        Assert.Equal(["Raw material acquisition", "Main production", "Distribution", "End of life and recycling"], page.Carbon.Select(part => part.Label));
        Assert.Equal([48.1m, 20.3m, 2.2m, 4.8m], page.Carbon.Select(part => part.Value));
        Assert.Equal([("Nickel", 6m), ("Cobalt", 16m), ("Lithium", 6m), ("Lead", 0m)], page.Recycled.Select(part => (part.Label, part.Value)));
    }

    [Theory]
    [InlineData("cluster", new[] { "purchaser", "shared" })]
    [InlineData("other-cluster", new string[0])]
    [InlineData("team", new[] { "purchaser", "shared", "authorities" })]
    [InlineData("market-surveillance", new[] { "purchaser", "shared", "authorities" })]
    [InlineData("notified-body", new[] { "authorities" })]
    [InlineData("commission", new[] { "shared", "authorities" })]
    [InlineData("li-treatment", new[] { "shared" })]
    public async Task TheDetailPage_ShouldHaveOneSectionPerAudienceTheReaderMaySee(string who, string[] expected)
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo);
        var reader = who switch
        {
            "cluster" => OwnCluster,
            "other-cluster" => OtherCluster,
            "team" => Reader.With(who, ReaderRole.Team),
            "market-surveillance" => Reader.With(who, ReaderRole.MarketSurveillance),
            "notified-body" => Reader.With(who, ReaderRole.NotifiedBody),
            "commission" => Reader.With(who, ReaderRole.Commission),
            _ => Reader.With(who, ReaderRole.LiTreatment),
        };

        var page = (await Pages(rig).DetailAsync(reader, Identifier(rig)))!;

        Assert.Equal(expected, page.Audiences.Select(section => section.Key));
        var clusters = await rig.Store.FindPublishedClusterIdsUsingAsync(packId: rig.PackId);
        Assert.All(
            page.Audiences.SelectMany(section => section.Values),
            value => Assert.True(AudienceRules.MaySee(reader, value.Attribute, clusters), value.Attribute.Label));
        Assert.Equal(AttributeCatalog.All.Count(a => a.Audience == Audience.Public), page.Sections.Sum(section => section.Values.Count));
    }

    [Fact]
    public async Task ATreatmentFacility_ShouldSeeOnlyTheSafetyMeasuresOfTheSharedData()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo);

        var page = (await Pages(rig).DetailAsync(Reader.With("weee@example.com", ReaderRole.LiTreatment), Identifier(rig)))!;

        Assert.Equal([48], Assert.Single(page.Audiences).Values.Select(value => value.Attribute.Number));
    }

    [Fact]
    public async Task EveryValueOnTheDetailPage_ShouldSayWhenAndWhereItCameFrom()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo);
        var measured = rig.Clock.Now.UtcDateTime.AddMinutes(-5);
        await rig.Pipeline.SaveAsync(
            ChangeSender.Feed("bms-feed", BatteryTesting.North),
            new ChangeSet(TargetKind.Pack, rig.PackId, new Dictionary<string, JsonNode?> { ["stateOfChargeSoC"] = JsonValue.Create(55) }, "feed", measured));

        var page = (await Pages(rig).DetailAsync(OwnCluster, Identifier(rig)))!;

        var values = page.Sections.Concat(page.Audiences).SelectMany(section => section.Values).ToArray();
        Assert.All(values.Where(value => value.Attribute.Number > 5), value => Assert.NotNull(value.Origin));
        Assert.All(values.Where(value => value.Attribute.Number <= 5), value => Assert.Null(value.Origin));
        var stateOfCharge = values.Single(value => value.Attribute.Number == 63);
        Assert.Equal(new ValueOrigin(measured, "feed", Measured: true), stateOfCharge.Origin);
        Assert.Equal("55 %", Assert.Single(stateOfCharge.Lines).Text);
        Assert.Equal(new ValueOrigin(DppTestSeed.GeneratedAtUtc, "seed", Measured: false), values.Single(value => value.Attribute.Number == 21).Origin);
        Assert.Equal("battery system", PassportPages.SourceWord("feed"));
        var accidents = Assert.Single(values.Single(value => value.Attribute.Number == 92).Lines);
        Assert.Equal("https://passport.example.com/dpp/packs/pack-VPC8C05-6788/accidents", accidents.Href);
    }

    [Fact]
    public async Task AnArchivedVersion_ShouldShowItsOwnDataAndLinkToTheCurrentOne()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo);
        var first = (await rig.Store.GetActiveVersionAsync(rig.PackId))!;
        rig.Clock.Now = rig.Clock.Now.AddHours(1);
        await rig.Pipeline.SaveAsync(
            PublicationTesting.Team,
            new ChangeSet(TargetKind.Model, "model-test", new Dictionary<string, JsonNode?> { ["batteryMass"] = JsonValue.Create(520) }, "form"));
        var second = (await rig.Store.GetActiveVersionAsync(rig.PackId))!;
        var team = Reader.With("team@example.com", ReaderRole.Team);

        var page = (await Pages(rig).DetailAsync(team, first.DigitalProductPassportId))!;

        Assert.False(page.IsCurrent);
        Assert.Equal(second.Id, page.Current?.Id);
        Assert.Equal(first.DigitalProductPassportId, page.Address);
        Assert.Equal(first.DigitalProductPassportId, (await Pages(rig).LocateAsync(first.DigitalProductPassportId))!.Address);
        Assert.Equal("Archived", page.Text(2)); // the badge's words (R5)
        Assert.Equal("512.5 kg", page.Text(21));
        Assert.Null(page.Value(21)!.Origin);
        var purchaser = page.Audiences.Single(section => section.Key == "purchaser").Values.Select(value => value.Attribute.Number).ToArray();
        Assert.Contains(22, purchaser);
        Assert.DoesNotContain(63, purchaser);
        Assert.Equal("520 kg", (await Pages(rig).SummaryAsync(team, Identifier(rig)))!.Text(21));
    }

    [Fact]
    public void TheAddresses_ShouldKeepTheBatteryIdReadable()
    {
        Assert.Equal("/dpp/urn:x-battery-pass:TEST-0f3a", PassportPages.SummaryHref("urn:x-battery-pass:TEST-0f3a"));
        Assert.Equal("/dpp/urn:x-battery-pass:TEST-0f3a/detail", PassportPages.DetailHref("urn:x-battery-pass:TEST-0f3a"));
        Assert.Equal("/dpp/VPC%2F1%20A", PassportPages.SummaryHref("VPC/1 A"));
        Assert.Equal("/qr/urn:x-battery-pass:TEST-0f3a/svg", PassportPages.QrHref("urn:x-battery-pass:TEST-0f3a"));
        Assert.Equal("https://passport.example.com/dpp/urn:x-battery-pass:TEST-0f3a", DocumentTesting.Links.PassportUrl("urn:x-battery-pass:TEST-0f3a"));
    }

    internal static PassportPages Pages(PublicationRig rig) =>
        new(rig.Store, new PassportReader(rig.Store), AttributeFormsTests.Forms, new TestIdentifierScheme());

    internal static string Identifier(PublicationRig rig) => rig.Seed.Sources.Pack.Values["uniqueProductIdentifier"].Value.GetValue<string>();
}
