using System.Reflection;
using BatteryPassWeb.Configuration;
using BatteryPassWeb.Controllers;
using BatteryPassWeb.Dpp.Access;
using BatteryPassWeb.Dpp.Documents;
using BatteryPassWeb.Dpp.Editing;
using BatteryPassWeb.Dpp.Store;
using BatteryPassWeb.Services;
using Microsoft.AspNetCore.RateLimiting;

namespace BatteryPassWeb.Tests.Dpp;

/// <summary>
/// S07: the team withdraws an accident report (with a reason, kept in the audit log): it leaves the accident page and the
/// passport's count, nobody can open its file, and the stored file is deleted. The actions that take a file are rate limited.
/// </summary>
[Collection(MongoCollection.Name)]
public sealed class WithdrawAccidentReportTests(MongoFixture mongo)
{
    private static readonly ChangeSender NorthAdmin = ChangeSender.ClusterAdmin("north-admin@example.com", BatteryTesting.North);

    private async Task<(PublicationRig Rig, DppDocument Report, DppDocument Other)> TwoReportsAsync()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo).WithClustersAsync();
        var documents = DocumentServiceTests.Service(rig);
        var first = (await documents.AddAccidentReportAsync(NorthAdmin, rig.PackId, new DocumentUpload("collision.pdf", DocumentTesting.Pdf("collision"), "Collision"))).Document!;
        rig.Clock.Now = rig.Clock.Now.AddMinutes(5);
        var second = (await documents.AddAccidentReportAsync(NorthAdmin, rig.PackId, new DocumentUpload("fire.pdf", DocumentTesting.Pdf("fire"), "Fire"))).Document!;
        return (rig, first, second);
    }

    [Fact]
    public async Task TheTeam_ShouldWithdrawAReportSoNoReaderSeesOrOpensIt()
    {
        var (rig, report, other) = await TwoReportsAsync();
        var documents = DocumentServiceTests.Service(rig);
        var store = new DocumentStore(rig.Context);

        var problem = await documents.WithdrawAccidentReportAsync(PublicationTesting.Team, rig.PackId, report.Id, "Added to the wrong battery");

        Assert.Null(problem);
        var team = Reader.With("team@example.com", ReaderRole.Team);
        var surveillance = Reader.With("msa@example.com", ReaderRole.MarketSurveillance);
        Assert.Equal([other.Id], (await documents.AccidentReportsAsync(team, rig.PackId))!.Select(each => each.Id));
        Assert.Equal([other.Id], (await documents.AccidentReportsAsync(surveillance, rig.PackId))!.Select(each => each.Id));
        Assert.Null(await documents.ReadAsync(team, report.Id)); // the old address no longer opens it
        Assert.Null(await store.GetAsync(report.Id));
        await Assert.ThrowsAnyAsync<Exception>(() => store.DownloadFileAsync(report.FileId)); // the stored file is gone
        Assert.NotNull(await documents.ReadAsync(team, other.Id)); // the other report stays
    }

    [Fact]
    public async Task AWithdrawal_ShouldBeInTheAuditLogWithWhoWhenWhyAndTheFilesName()
    {
        var (rig, report, _) = await TwoReportsAsync();
        rig.Clock.Now = rig.Clock.Now.AddDays(1);

        await DocumentServiceTests.Service(rig).WithdrawAccidentReportAsync(PublicationTesting.Team, rig.PackId, report.Id, "Added to the wrong battery");

        var events = await new ApplicationAuditService(rig.Context).ListApplicationAuditEventsAsync(new ApplicationAuditFilter());
        var line = Assert.Single(events, entry => entry["eventType"].AsString == DocumentService.WithdrawnEvent);
        Assert.Equal(PublicationTesting.Team.Who, line["actor"].AsString);
        Assert.Equal(rig.PackId, line["entityId"].AsString);
        Assert.Contains("collision.pdf", line["message"].AsString, StringComparison.Ordinal);
        Assert.Contains("Added to the wrong battery", line["message"].AsString, StringComparison.Ordinal);
        var details = line["metadata"].AsBsonDocument;
        Assert.Equal("collision.pdf", details["fileName"].AsString);
        Assert.Equal("Added to the wrong battery", details["reason"].AsString);
        Assert.Equal(report.Id, details["documentId"].AsString);
        Assert.True(line.Contains("createdAt"));
    }

    [Theory]
    [InlineData("cluster-admin", "Added to the wrong battery", "only the team")]
    [InlineData("team", "", "reason")]
    [InlineData("team", "   ", "reason")]
    [InlineData("team", null, "reason")]
    public async Task AWithdrawal_ShouldBeRefusedWithoutTheTeamOrAReason(string who, string? reason, string expected)
    {
        var (rig, report, _) = await TwoReportsAsync();

        var problem = await DocumentServiceTests.Service(rig).WithdrawAccidentReportAsync(who == "team" ? PublicationTesting.Team : NorthAdmin, rig.PackId, report.Id, reason);

        Assert.Contains(expected, problem, StringComparison.Ordinal);
        Assert.NotNull(await new DocumentStore(rig.Context).GetAsync(report.Id));
    }

    [Fact]
    public async Task AWithdrawal_ShouldTouchOnlyAnAccidentReportOfThatBattery()
    {
        var (rig, report, _) = await TwoReportsAsync();
        var documents = DocumentServiceTests.Service(rig);
        var batchReport = (await documents.UploadAsync(PublicationTesting.Team, TargetKind.Batch, "batch-test-2026", "resultsOfTestReportsProvingCompliance", new DocumentUpload("tests.pdf", DocumentTesting.Pdf()))).Document!;

        var otherBattery = await documents.WithdrawAccidentReportAsync(PublicationTesting.Team, "pack-someone-else", report.Id, "Wrong battery");
        var notAReport = await documents.WithdrawAccidentReportAsync(PublicationTesting.Team, rig.PackId, batchReport.Id, "Wrong file");
        var missing = await documents.WithdrawAccidentReportAsync(PublicationTesting.Team, rig.PackId, "doc-missing", "Wrong file");

        Assert.All(new[] { otherBattery, notAReport, missing }, problem => Assert.Contains("no such accident report", problem, StringComparison.Ordinal));
        var store = new DocumentStore(rig.Context);
        Assert.NotNull(await store.GetAsync(report.Id));
        Assert.NotNull(await store.GetAsync(batchReport.Id));
    }

    [Fact]
    public async Task TheTeamsAction_ShouldWithdrawAndSayWhatHappened()
    {
        var (rig, report, _) = await TwoReportsAsync();
        var form = new Dictionary<string, string>();

        var withdrawn = BatteriesControllerTests.Controller(rig, form);
        await withdrawn.WithdrawAccidentReport(rig.PackId, report.Id, "Added to the wrong battery", CancellationToken.None);
        var refused = BatteriesControllerTests.Controller(rig, form);
        await refused.WithdrawAccidentReport(rig.PackId, "doc-missing", "", CancellationToken.None);

        Assert.Equal("accidents", withdrawn.TempData[BatteriesController.CardKey]);
        Assert.Equal("collision.pdf withdrawn.", withdrawn.TempData[BatteriesController.CardStatusKey]);
        Assert.Equal("The report wasn't withdrawn: give the reason first.", refused.TempData[BatteriesController.CardErrorKey]);
        Assert.Null(await new DocumentStore(rig.Context).GetAsync(report.Id));
    }

    [Theory]
    [InlineData(typeof(BatteriesController), nameof(BatteriesController.AddAccidentReport))]
    [InlineData(typeof(BatteriesController), nameof(BatteriesController.CheckUpload))]
    [InlineData(typeof(ClusterBatteriesController), nameof(ClusterBatteriesController.AddAccidentReport))]
    [InlineData(typeof(ClusterBatteriesController), nameof(ClusterBatteriesController.CheckUpload))]
    public void TheActionsThatTakeAFile_ShouldUseTheUploadRateLimit(Type controller, string action)
    {
        var method = controller.GetMethod(action)!;

        Assert.Equal(SecurityRateLimitPolicyNames.FileUpload, method.GetCustomAttribute<EnableRateLimitingAttribute>()?.PolicyName);
    }
}
