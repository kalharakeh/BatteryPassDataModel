using BatteryPassWeb.Dpp.Audit;
using BatteryPassWeb.Dpp.Documents;
using BatteryPassWeb.Dpp.Editing;
using BatteryPassWeb.Dpp.Store;
using BatteryPassWeb.Services;

namespace BatteryPassWeb.Tests.Dpp;

/// <summary>
/// S07, the trace of a withdrawal: the audit entry is written before anything is deleted, so a failure never leaves a
/// deleted file without a trace; and the battery's cluster admin reads one plain line in the History: the team withdrew an
/// accident report, its file name and the day, not the team member's name and not the reason.
/// </summary>
[Collection(MongoCollection.Name)]
public sealed class WithdrawalTraceTests(MongoFixture mongo)
{
    private static readonly ChangeSender NorthAdmin = ChangeSender.ClusterAdmin("north-admin@example.com", BatteryTesting.North);

    [Fact]
    public async Task TheAuditEntry_ShouldBeWrittenBeforeTheFileIsDeleted()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo).WithClustersAsync();
        var documents = new DocumentStore(rig.Context);
        // A report whose stored file can't be deleted (its file id is not an id), so the deletion fails after the entry.
        var broken = new DppDocument
        {
            Id = "doc-broken",
            OwnerKind = TargetKind.Pack,
            OwnerId = rig.PackId,
            Property = DocumentService.Accidents,
            FileName = "broken.pdf",
            ContentType = "application/pdf",
            Length = 10,
            Sha256 = "00",
            FileId = "not-an-object-id",
            Title = "Broken",
            UploadedAtUtc = rig.Clock.Now.UtcDateTime,
            UploadedBy = NorthAdmin.Who,
        };
        await documents.InsertAsync(broken);

        await Assert.ThrowsAnyAsync<Exception>(() => DocumentServiceTests.Service(rig).WithdrawAccidentReportAsync(PublicationTesting.Team, rig.PackId, broken.Id, "Wrong battery"));

        var events = await new ApplicationAuditService(rig.Context).ListApplicationAuditEventsAsync(new ApplicationAuditFilter());
        Assert.Contains(events, entry => entry["eventType"].AsString == DocumentService.WithdrawnEvent && entry["metadata"]["fileName"].AsString == "broken.pdf");
    }

    [Fact]
    public async Task TheClusterAdminsHistory_ShouldShowAPlainLineForAWithdrawal()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo).WithClustersAsync();
        var documents = DocumentServiceTests.Service(rig);
        var report = (await documents.AddAccidentReportAsync(NorthAdmin, rig.PackId, new DocumentUpload("collision.pdf", DocumentTesting.Pdf("collision")))).Document!;
        rig.Clock.Now = rig.Clock.Now.AddDays(1);
        Assert.Null(await documents.WithdrawAccidentReportAsync(PublicationTesting.Team, rig.PackId, report.Id, "Secret reason: wrong battery"));
        var history = new ClusterHistory(new ApplicationAuditService(rig.Context), new AuditLines(rig.Store, new DocumentStore(rig.Context), AttributeFormsTests.Forms, rig.Context));
        var pack = (await rig.Store.GetAsync<BatteryPack>(rig.PackId))!;

        var lines = await history.ForAsync(pack, new HashSet<string> { BatteryTesting.North });

        var line = Assert.Single(lines, entry => entry.EventType == DocumentService.WithdrawnEvent);
        var text = string.Concat(line.Sentence.Select(word => word.Text));
        Assert.Equal("The manufacturer's team withdrew an accident report", text);
        Assert.Equal("collision.pdf", line.Change);
        Assert.NotNull(line.When);
        var everything = text + line.Change + string.Concat(line.Facts.Select(fact => fact.Term + fact.Detail)) + line.Technical;
        Assert.DoesNotContain(PublicationTesting.Team.Who, everything, StringComparison.Ordinal); // not the team member's name
        Assert.DoesNotContain("Secret reason", everything, StringComparison.Ordinal); // not the reason
    }
}
