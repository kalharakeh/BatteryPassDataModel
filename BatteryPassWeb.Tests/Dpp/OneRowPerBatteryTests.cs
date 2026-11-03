using System.Text.Json;
using BatteryPassWeb.Dpp.Access;
using BatteryPassWeb.Dpp.Pages;
using BatteryPassWeb.Dpp.Store;
using Microsoft.AspNetCore.Mvc;

namespace BatteryPassWeb.Tests.Dpp;

/// <summary>
/// Redesign R5, owner's item 3 ("One row, newest"): a battery placed on the market again has a pack per passport with the
/// same serial number. The team's list, the Registry and their search show one row per battery, the newest passport its
/// reader may see; the API's lists keep a row per battery ID.
/// </summary>
[Collection(MongoCollection.Name)]
public sealed class OneRowPerBatteryTests(MongoFixture mongo)
{
    private static string View(params string[] parts) => File.ReadAllText(DppTestFiles.RepoFile(["web", ..parts]));

    [Fact]
    public async Task TheTeamsList_ShouldShowTheNewPassportOnly_AndFindItByTheEarlierOnesIds()
    {
        var rig = await new SuccessionTests(mongo).InactiveAsync();
        var old = (await rig.Store.GetAsync<BatteryPack>(rig.PackId))!;
        var successorId = old.Succession!.SuccessorPackId!;
        var pages = BatteryTesting.Pages(rig);
        var every = await pages.ListAsync(null);
        var oldRow = every.Rows.Single(row => row.PackId == rig.PackId);

        var list = await pages.ListOnePerBatteryAsync(null, _ => true, matchClusters: true);
        var byOldId = await pages.ListOnePerBatteryAsync(oldRow.Identifier, _ => true, matchClusters: true);
        var byOldPassport = await pages.ListOnePerBatteryAsync(oldRow.Latest!.DigitalProductPassportId, _ => true, matchClusters: true);

        Assert.Equal(2, every.Rows.Count(row => row.SerialNumber == oldRow.SerialNumber)); // the API's list: a row per battery ID
        Assert.Equal(successorId, Assert.Single(list.Rows, row => row.SerialNumber == oldRow.SerialNumber).PackId);
        Assert.Equal(every.Total - 1, list.Total); // counts batteries, not packs
        Assert.Equal(successorId, Assert.Single(byOldId.Rows).PackId);
        Assert.Equal(successorId, Assert.Single(byOldPassport.Rows).PackId);
    }

    [Fact]
    public async Task WhileTheNewPassportIsADraft_TheRegistryShouldKeepTheActiveOne_AndTheTeamSeeTheDraft()
    {
        var rig = await new SuccessionTests(mongo).RepurposedAsync();
        var (successorId, problem) = await SuccessionTests.Service(rig).CreateSuccessorAsync(PublicationTesting.Team, rig.PackId, (await rig.Store.GetAsync<BatteryPack>(rig.PackId))!.OperatorId);
        Assert.Null(problem);
        var pages = BatteryTesting.Pages(rig);
        var surveillance = Reader.With("msa@example.com", ReaderRole.MarketSurveillance);

        var team = await pages.ListOnePerBatteryAsync(null, _ => true, matchClusters: true);
        var registry = await pages.ListOnePerBatteryAsync(null, row => RegistryList.MayList(surveillance, row), matchClusters: false);

        Assert.Contains(team.Rows, row => row.PackId == successorId);
        Assert.DoesNotContain(team.Rows, row => row.PackId == rig.PackId);
        Assert.Equal(rig.PackId, Assert.Single(registry.Rows).PackId); // the draft isn't the Registry's, so the active one stays
    }

    [Fact]
    public async Task TheApisLists_ShouldStillReturnEveryBatteryId()
    {
        // The manager's condition for item 3: GET /api/passports and a cluster's batteries keep a row per battery ID.
        var rig = await new SuccessionTests(mongo).InactiveAsync();
        var old = (await rig.Store.GetAsync<BatteryPack>(rig.PackId))!;
        var serial = (await BatteryTesting.Pages(rig).ListAsync(null)).Rows.Single(row => row.PackId == rig.PackId).SerialNumber;

        var listed = Assert.IsType<OkObjectResult>(await DppApiControllerTests.PassportsController(rig).List(null, CancellationToken.None));
        var passports = JsonSerializer.SerializeToElement(listed.Value).GetProperty("passports").EnumerateArray();
        var cluster = await PassportApiTests.Api(rig).BatteriesAsync(Reader.With("team@example.com", ReaderRole.Team), old.ClusterId!);

        Assert.Equal(2, passports.Count(passport => passport.GetProperty("serialNumber").GetString() == serial));
        Assert.Equal(2, cluster.Count(row => row.SerialNumber == serial));
    }

    [Fact]
    public void OnePerBattery_ShouldDropEveryRowAnotherRowReplaces_AlongAChain()
    {
        BatteryRow Row(string pack, string? successorOf) =>
            new(pack, "SN-1", $"urn:{pack}", "Compact", "Compact 7M", "2026", null, "No cluster yet", null) { SuccessorOf = successorOf };

        var rows = BatteryPages.OnePerBattery([Row("a", null), Row("b", "a"), Row("c", "b"), Row("other", null)]);

        Assert.Equal(["c", "other"], rows.Select(row => row.PackId));
    }

    [Fact]
    public void TheTeamsListAndTheRegistry_ShouldListOneRowPerBattery_AndTheApiEveryPack()
    {
        Assert.Contains("pages.ListOnePerBatteryAsync(q, _ => true, matchClusters: true, cancellationToken)", View("Controllers", "BatteriesController.cs"), StringComparison.Ordinal);
        Assert.Contains("batteries.ListOnePerBatteryAsync(q, row => RegistryList.MayList(reader, row), RegistryList.ShowsClusters(reader), cancellationToken)", View("Controllers", "RegistryController.cs"), StringComparison.Ordinal);
        Assert.Contains("await batteries.ListAsync(q, cancellationToken)", View("Controllers", "PassportsApiController.cs"), StringComparison.Ordinal);
        Assert.Contains("await batteries.ListAsync(null, cancellationToken)", View("Dpp", "Api", "PassportApi.cs"), StringComparison.Ordinal);
    }
}
