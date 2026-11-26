using BatteryPassWeb.Dpp.Access;
using BatteryPassWeb.Dpp.Documents;
using BatteryPassWeb.Dpp.Editing;
using BatteryPassWeb.Dpp.Store;
using MongoDB.Bson;
using MongoDB.Driver;

namespace BatteryPassWeb.Tests.Dpp;

/// <summary>
/// Finding R03 in the documents: a demo account downloads a real model's restricted document as a visitor would (not at
/// all), and adds no accident report to a real battery; on demo records it keeps its role.
/// </summary>
[Collection(MongoCollection.Name)]
public sealed class DemoDocumentAccessTests(MongoFixture mongo)
{
    [Fact]
    public async Task ADemoReader_ShouldNotDownloadARealModelsRestrictedDocument_ButADemoOnes()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo);
        var service = DocumentServiceTests.Service(rig);
        var uploaded = (await service.UploadAsync(PublicationTesting.Team, TargetKind.Model, "model-test", "dismantlingInformation", new DocumentUpload("d.pdf", DocumentTesting.Pdf("dismantling")))).Document!;
        var demoRepair = Reader.With("demo.repair@example.test", ReaderRole.LiRepair) with { DemoOnly = true };

        Assert.NotNull(await service.ReadAsync(Reader.With("repair@example.test", ReaderRole.LiRepair), uploaded.Id));
        Assert.Null(await service.ReadAsync(demoRepair, uploaded.Id));

        await rig.Context.Database!.GetCollection<BsonDocument>(DppStore.Models).UpdateOneAsync(
            Builders<BsonDocument>.Filter.Eq("_id", "model-test"), Builders<BsonDocument>.Update.Set("IsDemo", true));
        Assert.NotNull(await service.ReadAsync(demoRepair, uploaded.Id));
    }

    [Fact]
    public async Task ADemoClusterAdmin_ShouldAddNoAccidentReportToARealBattery()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo);
        var demoAdmin = ChangeSender.ClusterAdmin("demo.admin@example.test", "cluster-test") with { DemoOnly = true };

        var result = await DocumentServiceTests.Service(rig).AddAccidentReportAsync(demoAdmin, rig.PackId, new DocumentUpload("incident.pdf", DocumentTesting.Pdf()));

        Assert.Equal(DemoScope.RealBattery, result.Problem);
        Assert.Empty(await new DocumentStore(rig.Context).ListAsync(TargetKind.Pack, rig.PackId, "informationOnAccidents"));
    }
}
