using System.Text.Json.Nodes;
using BatteryPassWeb.Dpp.Access;
using BatteryPassWeb.Dpp.Api;
using BatteryPassWeb.Dpp.Documents;
using BatteryPassWeb.Dpp.Editing;
using BatteryPassWeb.Dpp.Store;
using BatteryPassWeb.Dpp.Uploads;
using BatteryPassWeb.Services;
using MongoDB.Bson;
using MongoDB.Driver;

namespace BatteryPassWeb.Tests.Dpp;

/// <summary>
/// Finding R20 (the owner's decision, 10 Nov 2026): a cluster's admins and tokens change passport data (status,
/// re-stated values, live values, accident reports, hand-overs, uploads) only once the team has recorded the cluster's
/// written authorisation. Without it every change is refused with the same line; the team changes as before.
/// </summary>
[Collection(MongoCollection.Name)]
public sealed class WrittenAuthorisationTests(MongoFixture mongo)
{
    private static readonly ChangeSender NorthAdmin = ChangeSender.ClusterAdmin("north-admin@example.com", BatteryTesting.North);
    private static readonly ChangeSender NorthFeed = ChangeSender.Feed("api-token:north", BatteryTesting.North);
    private static readonly ChangeSender SouthAdmin = ChangeSender.ClusterAdmin("south-admin@example.com", BatteryTesting.South);

    public static TheoryData<string> Writes() => ["live values", "feed live values", "status", "history note", "accident report", "hand-over", "upload", "api"];

    [Theory]
    [MemberData(nameof(Writes))]
    public async Task AWrite_ShouldBeRefusedWithOneLine_UntilTheClusterIsAuthorised(string write)
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo).WithClustersAsync();
        var clusters = new ClusterRepository(rig.Context);
        await clusters.RemoveWrittenAuthorisationAsync(BatteryTesting.North, DateTime.UtcNow);
        var before = await SnapshotAsync(rig);

        Assert.Equal(WrittenAuthorisation.Refusal, await WriteAsync(rig, write));
        Assert.Equal(before, await SnapshotAsync(rig)); // nothing changed, nothing marked, no new version

        await clusters.SetWrittenAuthorisationAsync(BatteryTesting.North, new DateOnly(2026, 11, 12), "team@example.com", DateTime.UtcNow);
        Assert.Null(await WriteAsync(rig, write));
    }

    [Fact]
    public async Task TheTeam_ShouldChangeABatteryOfAnUnauthorisedCluster_AsBefore()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo).WithClustersAsync();
        await new ClusterRepository(rig.Context).RemoveWrittenAuthorisationAsync(BatteryTesting.North, DateTime.UtcNow);

        var saved = await rig.Pipeline.SaveAsync(PublicationTesting.Team, new ChangeSet(TargetKind.Pack, rig.PackId, new Dictionary<string, JsonNode?> { ["stateOfChargeSoC"] = Soc(40) }, "form"));

        Assert.True(saved.Saved);
    }

    [Fact]
    public async Task AcceptingAHandOver_ShouldNeedTheReceivingClustersAuthorisation_DecliningShouldNot()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo).WithClustersAsync();
        var clusters = new ClusterRepository(rig.Context);
        var handOvers = HandOverTests.Service(rig);
        var asked = await handOvers.AskAsync(NorthAdmin, rig.PackId, BatteryTesting.South, confirm: true);
        await clusters.RemoveWrittenAuthorisationAsync(BatteryTesting.South, DateTime.UtcNow);

        Assert.Equal(WrittenAuthorisation.Refusal, await handOvers.AnswerAsync(SouthAdmin, asked.HandOver!.Id, accept: true));
        Assert.Equal(BatteryTesting.North, (await rig.Store.GetAsync<BatteryPack>(rig.PackId))!.ClusterId);
        Assert.Null(await handOvers.AnswerAsync(SouthAdmin, asked.HandOver.Id, accept: false)); // declining leaves it where it is
    }

    [Fact]
    public async Task AClusterRecordWithoutTheField_ShouldNotBeAuthorised()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo);
        await rig.Context.Database!.GetCollection<BsonDocument>("clusters").InsertOneAsync(new BsonDocument { ["clusterId"] = BatteryTesting.North, ["name"] = "From before" });
        var demo = TestDemo.For(rig);

        Assert.Equal(WrittenAuthorisation.Refusal, await demo.WriteProblemAsync(NorthAdmin, BatteryTesting.North));
        Assert.Equal(WrittenAuthorisation.Refusal, await demo.WriteProblemAsync(NorthFeed, "no-such-cluster"));
        Assert.Null(await demo.WriteProblemAsync(PublicationTesting.Team, BatteryTesting.North));
        Assert.Null(await demo.WriteProblemAsync(ChangeSender.MarketSurveillance("msa@example.com"), BatteryTesting.North));
    }

    [Fact]
    public async Task TheRecord_ShouldKeepTheDateAndWho_AndARenameShouldKeepIt()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo).WithClustersAsync();
        var clusters = new ClusterRepository(rig.Context);
        var at = new DateTime(2026, 11, 12, 9, 30, 0, DateTimeKind.Utc);

        Assert.True(await clusters.SetWrittenAuthorisationAsync(BatteryTesting.North, new DateOnly(2026, 11, 3), "team@example.com", at));
        await clusters.UpsertClusterAsync(BatteryTesting.North, "Customer North, renamed");
        await clusters.UpdateClusterNameAsync(BatteryTesting.North, "Customer North");

        Assert.Equal(new ClusterAuthorisation(new DateOnly(2026, 11, 3), "team@example.com", at), WrittenAuthorisation.Of(await clusters.GetClusterByIdAsync(BatteryTesting.North)));
        Assert.True(await clusters.RemoveWrittenAuthorisationAsync(BatteryTesting.North, at));
        Assert.False(await clusters.RemoveWrittenAuthorisationAsync(BatteryTesting.North, at)); // it had none any more
        Assert.Null(WrittenAuthorisation.Of(await clusters.GetClusterByIdAsync(BatteryTesting.North)));
        Assert.False(await clusters.SetWrittenAuthorisationAsync("no-such-cluster", new DateOnly(2026, 11, 3), "team@example.com", at));
    }

    /// <summary>Does one write as North's admin or token; null when it was done, else the problem it gave.</summary>
    private static async Task<string?> WriteAsync(PublicationRig rig, string write)
    {
        switch (write)
        {
            case "live values":
            case "feed live values":
            {
                var sender = write == "live values" ? NorthAdmin : NorthFeed;
                var saved = await rig.Pipeline.SaveAsync(sender, new ChangeSet(TargetKind.Pack, rig.PackId, new Dictionary<string, JsonNode?> { ["stateOfChargeSoC"] = Soc(40) }, "form"));
                return saved.Saved ? null : Assert.Single(saved.Keys.Select(key => key.Message).Distinct());
            }

            case "status":
            {
                var result = await StatusChangeTests.Service(rig).ChangeAsync(NorthAdmin, rig.PackId, new StatusChange(StatusEvent.Waste, await StatusChangeTests.CurrentAsync(rig)));
                return result.Problem;
            }

            case "history note":
                return await StatusChangeTests.Service(rig).AddNoteAsync(NorthAdmin, rig.PackId, "Checked the cables.");

            case "accident report":
                return (await DocumentServiceTests.Service(rig).AddAccidentReportAsync(NorthAdmin, rig.PackId, new DocumentUpload("accident.pdf", DocumentTesting.Pdf()))).Problem;

            case "hand-over":
                return (await HandOverTests.Service(rig).AskAsync(NorthAdmin, rig.PackId, BatteryTesting.South, confirm: true)).Problem;

            case "upload":
            {
                var report = await UploadServiceTests.Run(rig, NorthAdmin, UploadServiceTests.File(UploadServiceTests.Entry(UploadServiceTests.Serial(rig), """{ "stateOfChargeSoC": 41 }""")));
                var battery = Assert.Single(report.Batteries);
                return battery.Outcome == UploadService.Saved ? null : battery.Problem;
            }

            case "api":
            {
                var target = await BatteryApiTests.Api(rig).FindAsync(NorthFeed, UploadServiceTests.Serial(rig));
                if (target.Refusal == WriteRefusal.NotAuthorised)
                {
                    return WrittenAuthorisation.Refusal;
                }

                var saved = await BatteryApiTests.Api(rig).WriteLiveValuesAsync(NorthFeed, target.Pack!, new Dictionary<string, JsonNode?> { ["stateOfChargeSoC"] = JsonValue.Create(40) }, null, preview: false);
                return saved.Saved ? null : "not saved";
            }

            default:
                throw new ArgumentOutOfRangeException(nameof(write), write, null);
        }
    }

    /// <summary>What a refused write must leave as it was: the battery's record (values, marker), its versions, documents, files and hand-overs.</summary>
    private static async Task<string> SnapshotAsync(PublicationRig rig)
    {
        var database = rig.Context.Database!;
        var parts = new List<string>();
        foreach (var name in new[] { DppStore.Packs, DppStore.Versions, DocumentStore.Records, $"{DocumentStore.Bucket}.files", DppStore.HandOvers })
        {
            var documents = await database.GetCollection<BsonDocument>(name).Find(Builders<BsonDocument>.Filter.Empty).ToListAsync();
            parts.Add($"{name}:{string.Join("|", documents.Select(document => document.ToJson()))}");
        }

        return string.Join("\n", parts);
    }

    private static JsonNode Soc(int percent) => JsonValue.Create(percent);
}
