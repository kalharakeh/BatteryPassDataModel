using BatteryPassWeb.Dpp.Editing;
using BatteryPassWeb.Dpp.Pages;

namespace BatteryPassWeb.Tests.Dpp;

/// <summary>
/// Redesign R4 (owner's choice B): the battery pages show one section at a time from a side menu, like the old app. The
/// server picks the open section, so a save comes back to its own section with its message.
/// </summary>
[Collection(MongoCollection.Name)]
public sealed class BatterySectionsTests(MongoFixture mongo)
{
    private static readonly IReadOnlyList<BatterySection> Menu =
    [
        new("overview", "Overview", "overview"), new("values", "Passport values", "values"), new("status", "Status & repairs", "swap"),
        new("live", "Live values", "live"), new("modules", "Modules", "modules"),
    ];

    [Theory]
    [InlineData("live", null, false, "live")] // asked for
    [InlineData("nonsense", null, false, "overview")] // unknown: the overview
    [InlineData(null, "status", false, "status")] // a status change or a note was just sent
    [InlineData(null, "module-list", false, "modules")] // "Type or correct the list"
    [InlineData(null, "publication", false, "overview")] // publishing, the cluster and removing live in the overview
    [InlineData(null, "cluster", false, "overview")]
    [InlineData(null, "passport-status", false, "overview")] // not on this menu
    [InlineData(null, "mark", false, "overview")] // marking says so in the header (R5)
    [InlineData(null, null, true, "values")] // the values editor was just saved
    [InlineData("status", "live", true, "status")] // asking wins
    [InlineData(null, null, false, "overview")]
    public void TheOpenSection_ShouldBeAskedForElseTheOneJustSavedElseTheOverview(string? asked, string? card, bool valuesMessage, string expected)
    {
        Assert.Equal(expected, BatterySections.Chosen(Menu, asked, card, valuesMessage));
    }

    [Fact]
    public async Task TheTeamsMenu_ShouldListEveryCardInTheOrderOfTheWork()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo).WithClustersAsync();
        var draft = await PublicationTesting.RigAsync(mongo);

        var published = (await BatteryTesting.Pages(rig).BatteryAsync(rig.PackId))!.Sections;
        var unpublished = (await BatteryTesting.Pages(draft).BatteryAsync(draft.PackId))!.Sections;

        Assert.Equal(["overview", "values", "status", "live", "accidents", "modules", "versions"], published.Select(section => section.Id)); // no Passport status section (R5)
        Assert.Equal("Nothing needs you", published[0].Hint);
        Assert.Equal("Original", published.Single(section => section.Id == "status").Hint);
        Assert.Equal(1, published.Single(section => section.Id == "versions").Count);
        Assert.Equal(["overview", "values", "status", "live", "accidents", "modules", "versions"], unpublished.Select(section => section.Id));
        Assert.EndsWith("to do", unpublished[0].Hint, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AnInvalidPassport_ShouldSaySoInTheHeader_NotInASection()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo).WithClustersAsync();
        await PassportMarkTests.Service(rig).MarkInvalidAsync(ChangeSender.MarketSurveillance("msa@example.com"), rig.PackId, "Demo: wrong carbon footprint figures");

        var sections = (await BatteryTesting.Pages(rig).BatteryAsync(rig.PackId))!.Sections;

        Assert.DoesNotContain(sections, section => section.Id == "passport-status"); // the header's red line says it (R5)
        Assert.True((await BatteryTesting.Pages(rig).BatteryAsync(rig.PackId))!.Marks.Marked);
    }

    [Fact]
    public async Task TheClusterAdminsMenu_ShouldHaveTheHandOverAndNoTeamSections()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo).WithClustersAsync();

        var page = (await ClusterBatteryPagesTests.Pages(rig).BatteryAsync(ChangeSender.ClusterAdmin("north-admin@example.com", BatteryTesting.North), rig.PackId))!;

        Assert.Equal(["overview", "status", "live", "accidents", "handover", "modules", "versions"], page.Sections.Select(section => section.Id));
    }
}
