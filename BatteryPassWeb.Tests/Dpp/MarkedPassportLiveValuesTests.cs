using System.Text.Json.Nodes;
using BatteryPassWeb.Dpp.Editing;
using BatteryPassWeb.Dpp.Publication;
using BatteryPassWeb.Dpp.Store;

namespace BatteryPassWeb.Tests.Dpp;

/// <summary>
/// Redesign R6, the owner's item 11, the second cause: a passport marked invalid or for deletion is still the battery's
/// newest passport, but only an active version was given the battery's live values, so a marked passport had none and no
/// Live values tab (DEMO-C7M-0003, DEMO-CORE-0003). It keeps the battery's last live values now, frozen by the mark. An
/// older version of the same passport (archived) or a passport replaced by a newer one (inactive) still has none.
/// </summary>
[Collection(MongoCollection.Name)]
public sealed class MarkedPassportLiveValuesTests(MongoFixture mongo)
{
    private static readonly ChangeSender Team = PublicationTesting.Team;

    private static bool HasStateOfCharge(JsonObject passport) =>
        passport.ToJsonString().Contains("\"stateOfChargeSoC\"", StringComparison.Ordinal);

    [Fact]
    public async Task AnInvalidPassport_ShouldKeepTheBatterysLastLiveValues()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo).WithClustersAsync();
        Assert.True((await rig.Pipeline.SaveAsync(Team, new ChangeSet(TargetKind.Pack, rig.PackId, new Dictionary<string, JsonNode?> { ["stateOfChargeSoC"] = 40 }, "form"))).Saved);
        Assert.Null(await PassportMarkTests.Service(rig).MarkInvalidAsync(Team, rig.PackId, "Wrong carbon footprint figures"));
        var version = (await rig.Store.GetLatestVersionAsync(rig.PackId))!;

        var passport = (await new PassportReader(rig.Store).VersionAsync(version.DigitalProductPassportId))!;

        Assert.Equal(DppStatuses.Invalid, version.DppStatus);
        Assert.True(HasStateOfCharge(passport.Passport));
    }

    [Fact]
    public async Task APassportMarkedForDeletion_ShouldKeepTheBatterysLastLiveValues()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo).WithClustersAsync();
        await StatusChangeTests.Service(rig).ChangeAsync(Team, rig.PackId, new StatusChange(StatusEvent.Waste, await StatusChangeTests.CurrentAsync(rig)));
        Assert.Null(await PassportMarkTests.Service(rig).MarkForDeletionAsync(Team, rig.PackId));
        var versions = await rig.Store.ListVersionsAsync(rig.PackId);
        var latest = (await rig.Store.GetLatestVersionAsync(rig.PackId))!;

        var reader = new PassportReader(rig.Store);

        Assert.Equal(DppStatuses.MarkedForDeletion, latest.DppStatus);
        Assert.True(HasStateOfCharge((await reader.VersionAsync(latest.DigitalProductPassportId))!.Passport));
        Assert.All(versions.Where(version => version.DppStatus == DppStatuses.Archived), archived => Assert.False(HasStateOfCharge(reader.VersionAsync(archived.DigitalProductPassportId).GetAwaiter().GetResult()!.Passport)));
    }

    [Fact]
    public async Task AReplacedPassport_ShouldStillHaveNoLiveValues()
    {
        var rig = await new SuccessionTests(mongo).InactiveAsync();
        var old = (await rig.Store.GetLatestVersionAsync(rig.PackId))!;

        var passport = (await new PassportReader(rig.Store).VersionAsync(old.DigitalProductPassportId))!;

        Assert.Equal(DppStatuses.Inactive, old.DppStatus);
        Assert.False(HasStateOfCharge(passport.Passport));
    }
}
