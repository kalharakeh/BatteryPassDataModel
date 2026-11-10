using BatteryPassWeb.Dpp.Access;
using BatteryPassWeb.Dpp.Documents;
using BatteryPassWeb.Dpp.Editing;
using BatteryPassWeb.Dpp.Store;

namespace BatteryPassWeb.Tests.Dpp;

/// <summary>
/// S03: #92, the passport's accident link, is the app's own value. An upload, a feed or an API write that names it is
/// refused for every sender, the team too; the app keeps setting it itself when a report is added.
/// </summary>
[Collection(MongoCollection.Name)]
public sealed class AccidentLinkIsTheAppsTests(MongoFixture mongo)
{
    // An address of the app itself, which is the one kind the restricted-document rule lets through.
    private static readonly string Planted = $$"""{"contentType":"text/html","url":"{{DocumentTesting.BaseUrl}}/dpp/packs/another-pack/accidents","resourceTitle":"Nothing was ever reported"}""";

    public static TheoryData<string> Senders => new() { "team", "cluster-admin", "feed" };

    private static ChangeSender Sender(string who) => who switch
    {
        "team" => PublicationTesting.Team,
        "cluster-admin" => ChangeSender.ClusterAdmin("north-admin@example.com", BatteryTesting.North),
        _ => ChangeSender.Feed("bms-feed", BatteryTesting.North),
    };

    [Theory]
    [MemberData(nameof(Senders))]
    public async Task AnUploadThatNamesTheAccidentLink_ShouldBeRefusedForEverySender(string who)
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo).WithClustersAsync();
        var before = (await rig.Store.GetAsync<BatteryPack>(rig.PackId))!.Values["informationOnAccidents"].Value!.ToJsonString();
        var file = UploadServiceTests.File(UploadServiceTests.Entry(UploadServiceTests.Serial(rig), $$"""{ "informationOnAccidents": {{Planted}} }"""));

        var checkedFile = await UploadServiceTests.Run(rig, Sender(who), file, preview: true);
        var saved = await UploadServiceTests.Run(rig, Sender(who), file);

        foreach (var report in new[] { checkedFile, saved })
        {
            var key = Assert.Single(Assert.Single(report.Batteries).Values);
            Assert.Equal(KeyOutcome.Rejected, key.Outcome);
            Assert.Equal("#92 can't be changed by this sender", key.Message);
        }

        Assert.Equal(before, (await rig.Store.GetAsync<BatteryPack>(rig.PackId))!.Values["informationOnAccidents"].Value!.ToJsonString());
    }

    [Theory]
    [MemberData(nameof(Senders))]
    public async Task APassportChange_ShouldRefuseTheAccidentLinkForEverySender(string who)
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo).WithClustersAsync();
        var change = new ChangeSet(TargetKind.Pack, rig.PackId, new Dictionary<string, System.Text.Json.Nodes.JsonNode?> { ["informationOnAccidents"] = System.Text.Json.Nodes.JsonNode.Parse(Planted) }, "api");

        var result = await rig.Pipeline.SaveAsync(Sender(who), change);

        Assert.False(result.Saved);
        Assert.Equal(KeyOutcome.Rejected, Assert.Single(result.Keys).Outcome);
    }

    [Fact]
    public async Task AddingAReport_ShouldStillSetTheAccidentLinkItself()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo).WithClustersAsync();

        var added = await DocumentServiceTests.Service(rig).AddAccidentReportAsync(Sender("cluster-admin"), rig.PackId, new DocumentUpload("incident.pdf", DocumentTesting.Pdf()));

        Assert.Null(added.Problem);
        var link = (await rig.Store.GetAsync<BatteryPack>(rig.PackId))!.Values["informationOnAccidents"].Value!;
        Assert.Equal(DocumentTesting.Links.AccidentPageUrl(rig.PackId), link["url"]!.GetValue<string>());
    }
}
