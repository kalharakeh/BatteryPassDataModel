using System.Text.Json.Nodes;
using BatteryPassWeb.Dpp.Assembly;
using BatteryPassWeb.Dpp.Catalog;
using BatteryPassWeb.Dpp.Editing;
using BatteryPassWeb.Dpp.Pages;
using BatteryPassWeb.Dpp.Publication;
using BatteryPassWeb.Dpp.Store;
using BatteryPassWeb.Dpp.Values;

namespace BatteryPassWeb.Tests.Dpp;

/// <summary>
/// R22: the rated capacity (#57) is needed before publishing, like the values due by February 2027. The official list
/// says "Feb 2027: no"; the owner follows the Regulation here, so #57 is the one named exception to the list.
/// </summary>
[Collection(MongoCollection.Name)]
public sealed class RatedCapacityRequiredTests(MongoFixture mongo)
{
    private const string ModelId = "model-test";

    [Fact]
    public void RatedCapacity_ShouldBeTheOnlyValueNeededBeforePublishingThatTheOfficialListDoesntMark()
    {
        var beyondTheList = AttributeCatalog.All
            .Where(attribute => PassportNeeds.DueByFeb2027(attribute) != attribute.MandatoryFromFeb2027)
            .Select(attribute => attribute.Number);

        Assert.Equal([57], beyondTheList);
        Assert.False(AttributeCatalog.ByNumber(57).MandatoryFromFeb2027); // the catalogue still says what the list says
        Assert.Contains("Regulation", PassportNeeds.RequiredBeyondTheList[57], StringComparison.Ordinal);
    }

    [Fact]
    public void APassportWithoutRatedCapacity_ShouldNotPublish()
    {
        var seed = DppTestSeed.Load();
        var passport = PassportAssembler.Assemble(seed.Sources, seed.Header);
        passport["batteryPassportData"]!["performanceAndDurability"]!.AsObject().Remove("ratedCapacity");

        var report = new PublicationChecks(DppStoreTesting.Schemas).Check(passport);

        Assert.False(report.CanPublish);
        Assert.Contains(report.Blocking, item => item.StartsWith("#57 Rated capacity is missing", StringComparison.Ordinal));
        Assert.DoesNotContain(report.Warnings, item => item.StartsWith("#57 ", StringComparison.Ordinal));
    }

    [Fact]
    public async Task TheModelEditor_ShouldMarkAnEmptyRatedCapacityAsMissing()
    {
        var rig = await PublicationTesting.RigAsync(mongo);
        var stored = new Dictionary<string, AttributeValue>((await rig.Store.GetAsync<BatteryModel>(ModelId))!.Values, StringComparer.Ordinal);
        stored.Remove("ratedCapacity");

        var sections = await PageTesting.Editor(rig).SectionsAsync(TargetKind.Model, ModelId, stored);
        var field = sections.SelectMany(section => section.Fields).Single(field => field.Attribute.Number == 57);

        Assert.True(field.IsMissing);
        Assert.Equal("Required by Feb 2027", field.Marker);
        Assert.Equal(1, RecordEditor.CountMissing(TargetKind.Model, stored));
    }

    [Fact]
    public async Task ThePublishedPassportsRatedCapacity_ShouldNotBeRemoved()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo);

        var result = await rig.Pipeline.SaveAsync(
            PublicationTesting.Team,
            new ChangeSet(TargetKind.Model, ModelId, new Dictionary<string, JsonNode?>(), "form", Removals: ["ratedCapacity"]));

        Assert.Equal(KeyOutcome.Rejected, Assert.Single(result.Keys).Outcome);
        Assert.True((await rig.Store.GetAsync<BatteryModel>(ModelId))!.Values.ContainsKey("ratedCapacity"));
    }
}
