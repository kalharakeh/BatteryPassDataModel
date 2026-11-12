using BatteryPassWeb.Controllers;
using BatteryPassWeb.Dpp.Access;
using BatteryPassWeb.Dpp.Pages;
using BatteryPassWeb.Dpp.Store;
using BatteryPassWeb.Tests.Demo;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Bson;
using MongoDB.Driver;

namespace BatteryPassWeb.Tests.Dpp;

/// <summary>
/// Finding R02 (the manager's decision): the team records each published real battery's registration in the EU's DPP
/// Registry on its page; the list counts and lists those not recorded yet and gives them as CSV. The app's own data: not
/// part of the passport, not published, not shown outside the team.
/// </summary>
[Collection(MongoCollection.Name)]
public sealed class EuRegistryRecordTests(MongoFixture mongo)
{
    [Fact]
    public async Task APublishedBattery_ShouldHaveTheSection_ADraftShouldNot()
    {
        var draft = await PublicationTesting.RigAsync(mongo);
        var published = await PublicationTesting.PublishedRigAsync(mongo);

        Assert.Null((await BatteryTesting.Pages(draft).BatteryAsync(draft.PackId))!.Registry);
        var page = (await BatteryTesting.Pages(published).BatteryAsync(published.PackId))!;
        Assert.Equal(($"/admin/batteries/{published.PackId}/registry", null), (page.Registry!.Action, page.Registry.Record));
        Assert.Contains(page.Sections, section => section is { Id: "registry", Title: "EU registry", Hint: "Not recorded yet" });
    }

    [Fact]
    public async Task ADemoBattery_ShouldHaveNoSection_AndNotCount()
    {
        using var rig = await DemoRig.StartAsync(mongo);
        await rig.Demo.SeedAsync(DemoRig.Who);
        var pages = rig.Services.GetRequiredService<BatteryPages>();

        foreach (var version in await rig.Store.ListAllActiveVersionsAsync())
        {
            Assert.Null((await pages.BatteryAsync(version.PackId))!.Registry);
        }

        Assert.Equal(0, (await pages.TeamListAsync(null)).NotInTheRegistry);
        Assert.Equal("Only a battery with an active passport, outside the demo, is recorded in the EU registry.", await pages.RecordRegistryAsync("team@example.com", (await rig.Store.ListAllActiveVersionsAsync())[0].PackId, "2026-11-01", "X"));
    }

    [Fact]
    public async Task Recording_ShouldKeepTheDayTheIdAndWho_AndACorrectionShouldBeLoggedWithWhatItWas()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo);
        var pages = BatteryTesting.Pages(rig);

        Assert.Null(await pages.RecordRegistryAsync("team@example.com", rig.PackId, "2026-11-02", " EU-REG-0001 "));
        Assert.Null(await pages.RecordRegistryAsync("other@example.com", rig.PackId, "2026-11-03", "EU-REG-0002"));

        var record = (await rig.Store.GetAsync<BatteryPack>(rig.PackId))!.Registry!;
        Assert.Equal(("2026-11-03", "EU-REG-0002", "other@example.com"), (record.RegisteredOn, record.RegistrationId, record.RecordedBy));
        var page = (await pages.BatteryAsync(rig.PackId))!;
        Assert.Equal("EU-REG-0002", page.Registry!.Record!.RegistrationId);
        Assert.Contains(page.Sections, section => section is { Id: "registry", Hint: "Registered" });
        var entries = await rig.Context.Database!.GetCollection<BsonDocument>("applicationAuditEvents")
            .Find(Builders<BsonDocument>.Filter.Eq("eventType", BatteryPages.RegistryRecordedEvent)).SortBy(entry => entry["_id"]).ToListAsync();
        Assert.Equal(2, entries.Count);
        Assert.True(entries[0]["metadata"]["before"].IsBsonNull);
        Assert.Equal("EU-REG-0001", entries[1]["metadata"]["before"]["registrationId"].AsString);
        Assert.Contains("was 2026-11-02, ID EU-REG-0001", entries[1]["message"].AsString, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("", "EU-1", "Give the day it was registered: today or a day before.")]
    [InlineData("2 Nov 2026", "EU-1", "Give the day it was registered: today or a day before.")]
    [InlineData("2999-01-01", "EU-1", "Give the day it was registered: today or a day before.")]
    [InlineData("2026-11-02", "  ", "Type the registration ID the Registry gave, up to 200 characters.")]
    public async Task Recording_ShouldAskForADayAndAnId(string on, string id, string problem)
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo);

        Assert.Equal(problem, await BatteryTesting.Pages(rig).RecordRegistryAsync("team@example.com", rig.PackId, on, id));
        Assert.Null((await rig.Store.GetAsync<BatteryPack>(rig.PackId))!.Registry);
    }

    [Fact]
    public async Task TheRecord_ShouldNotBePassportData()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo).WithClustersAsync();
        var before = (await SigningTesting.Passports(rig.Store).CurrentAsync(rig.PackId))!.Passport.ToJsonString();

        await BatteryTesting.Pages(rig).RecordRegistryAsync("team@example.com", rig.PackId, "2026-11-02", "EU-REG-SECRET-7");

        var pack = (await rig.Store.GetAsync<BatteryPack>(rig.PackId))!;
        Assert.Null(pack.ChangesWaitingSinceUtc); // nothing waits to be published
        Assert.Equal(1, await rig.Store.GetLatestVersionNumberAsync(rig.PackId));
        Assert.Equal(before, (await SigningTesting.Passports(rig.Store).CurrentAsync(rig.PackId))!.Passport.ToJsonString());
        Assert.Equal(0, (await rig.Publication.ReconcileAsync("check-test")).Marked); // the hourly check sees no difference
        var detail = await PassportPagesTests.Pages(rig).DetailAsync(Reader.With("msa@example.com", ReaderRole.MarketSurveillance), PassportPagesTests.Identifier(rig));
        Assert.DoesNotContain("EU-REG-SECRET-7", System.Text.Json.JsonSerializer.Serialize(detail!.Audiences.SelectMany(section => section.Values).Select(value => value.Lines)), StringComparison.Ordinal);
        Assert.DoesNotContain("EU-REG-SECRET-7", detail.PublicData!.ToJsonString(), StringComparison.Ordinal);
        var clusterPage = await ClusterBatteryPagesTests.Pages(rig).BatteryAsync(BatteryPassWeb.Dpp.Editing.ChangeSender.ClusterAdmin("north@example.com", BatteryTesting.North), rig.PackId);
        Assert.NotNull(clusterPage);
        Assert.DoesNotContain("Model.Pack.Registry", File.ReadAllText(DppTestFiles.RepoFile("web", "Views", "ClusterAdmin", "Battery.cshtml")), StringComparison.Ordinal); // the cluster admin's page never shows it
    }

    [Fact]
    public async Task TheList_ShouldCountAndNarrowToTheUnrecorded_AndTheCsvShouldHaveThem()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo);
        var pages = BatteryTesting.Pages(rig);

        var all = await pages.TeamListAsync(null);
        var missing = await pages.TeamListAsync(null, onlyNotInTheRegistry: true);
        Assert.Equal(1, all.NotInTheRegistry);
        Assert.Equal([rig.PackId], missing.Rows.Select(row => row.PackId));
        var line = Assert.Single(await pages.NotInTheRegistryAsync());
        Assert.Equal(PassportPagesTests.Identifier(rig), line.BatteryId);
        Assert.EndsWith(Uri.EscapeDataString(PassportPagesTests.Identifier(rig)).Replace("%3A", ":", StringComparison.Ordinal).Split('/')[^1], line.PassportAddress.Split('/')[^1], StringComparison.OrdinalIgnoreCase);
        Assert.NotEqual(string.Empty, line.ModelIdentifier);
        Assert.NotEqual(string.Empty, line.OperatorIdentifier);
        Assert.NotEqual(string.Empty, line.FacilityIdentifier);
        Assert.Equal(DateTime.UtcNow.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture)[..4], line.PublicationDate[..4]);

        var csv = BatteryPages.RegistryCsv([line]).Split("\r\n");
        Assert.Equal("Battery ID,Passport address,Model identifier (#10),Operator identifier (#7),Facility identifier (#12),Publication date", csv[0]);
        Assert.StartsWith($"\"{line.BatteryId}\",", csv[1], StringComparison.Ordinal);

        await pages.RecordRegistryAsync("team@example.com", rig.PackId, "2026-11-02", "EU-1");
        Assert.Equal(0, (await pages.TeamListAsync(null)).NotInTheRegistry);
        Assert.Empty((await pages.TeamListAsync(null, onlyNotInTheRegistry: true)).Rows);
        Assert.Empty(await pages.NotInTheRegistryAsync());
    }

    [Fact]
    public void TheCsv_ShouldQuoteFields_AndKeepAFormulaAsText()
    {
        var csv = BatteryPages.RegistryCsv([new RegistryLine("=HYPERLINK(\"x\")", "https://a.example/dpp/1", "M,1", "-7", "+12", "2026-11-02")]);

        Assert.Equal("\"'=HYPERLINK(\"\"x\"\")\",\"https://a.example/dpp/1\",\"M,1\",\"'-7\",\"'+12\",\"2026-11-02\"", csv.Split("\r\n")[1]);
    }

    [Fact]
    public async Task TheController_ShouldSaveAndSayIt_AndTheCsvShouldBeTheTeamsDownload()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo).WithClustersAsync();
        var controller = BatteriesControllerTests.Controller(rig, new Dictionary<string, string>());

        var saved = Assert.IsType<RedirectResult>(await controller.RecordRegistry(rig.PackId, "2026-11-02", "EU-9", CancellationToken.None));
        var file = Assert.IsType<FileContentResult>(await BatteriesControllerTests.Controller(rig, new Dictionary<string, string>()).RegistryCsv(TimeProvider.System, CancellationToken.None));

        Assert.Equal($"/admin/batteries/{rig.PackId}?section=registry", saved.Url);
        Assert.Equal("text/csv; charset=utf-8", file.ContentType);
        Assert.Equal(2, System.Text.Encoding.UTF8.GetString(file.FileContents).Split("\r\n").Length); // the header and an empty last line: none left
        Assert.Equal("AdminOnly", typeof(BatteriesController).GetCustomAttributes(typeof(Microsoft.AspNetCore.Authorization.AuthorizeAttribute), false).Cast<Microsoft.AspNetCore.Authorization.AuthorizeAttribute>().Single().Policy);
    }
}
