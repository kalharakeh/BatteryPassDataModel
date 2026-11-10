using System.Text;
using BatteryPassWeb.Configuration;
using BatteryPassWeb.Dpp.Access;
using BatteryPassWeb.Dpp.Documents;
using BatteryPassWeb.Dpp.Editing;
using BatteryPassWeb.Dpp.Store;
using BatteryPassWeb.Services;
using Microsoft.Extensions.Options;

namespace BatteryPassWeb.Tests.Dpp;

[Collection(MongoCollection.Name)]
public sealed class DocumentServiceTests(MongoFixture mongo)
{
    private const string ModelId = "model-test";

    [Fact]
    public async Task Upload_ShouldStoreTheFileAndLinkTheAttributeToIt()
    {
        var rig = await PublicationTesting.RigAsync(mongo);
        var service = Service(rig);

        var result = await service.UploadAsync(PublicationTesting.Team, TargetKind.Model, ModelId, "dismantlingInformation", new DocumentUpload("C:\\temp\\dismantling.pdf", DocumentTesting.Pdf(), "Dismantling manual", "en-GB"));

        Assert.Null(result.Problem);
        var document = result.Document!;
        Assert.Equal("dismantling.pdf", document.FileName);
        Assert.Equal("application/pdf", document.ContentType);
        Assert.Equal(64, document.Sha256.Length);
        var link = (await rig.Store.GetAsync<BatteryModel>(ModelId))!.Values["dismantlingInformation"].Value;
        Assert.Equal(DocumentTesting.Links.DocumentUrl(document.Id), link["url"]!.GetValue<string>());
        Assert.Equal("application/pdf", link["contentType"]!.GetValue<string>());
        Assert.Equal("Dismantling manual", link["resourceTitle"]!.GetValue<string>());
        Assert.Equal("en-GB", link["language"]!.GetValue<string>());
    }

    [Fact]
    public async Task UploadForAPublishedPacksModel_ShouldPublishANewVersion()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo);

        await Service(rig).UploadAsync(PublicationTesting.Team, TargetKind.Model, ModelId, "safetyMeasures", new DocumentUpload("safety.pdf", DocumentTesting.Pdf()));

        Assert.Equal(2, (await rig.Store.GetActiveVersionAsync(rig.PackId))?.Number);
    }

    [Theory]
    [InlineData("png", "image/png")]
    [InlineData("jpeg", "image/jpeg")]
    public async Task Images_ShouldBeRecognisedByTheirBytes(string kind, string expected)
    {
        var rig = await PublicationTesting.RigAsync(mongo);
        var content = kind == "png" ? DocumentTesting.Png() : DocumentTesting.Jpeg();

        var result = await Service(rig).UploadAsync(PublicationTesting.Team, TargetKind.Model, ModelId, "partNumbersForComponents", new DocumentUpload("parts.pdf", content));

        Assert.Equal(expected, result.Document?.ContentType);
    }

    [Theory]
    [InlineData("not a pdf at all", 0, "only PDF, PNG and JPEG")]
    [InlineData("", 0, "empty")]
    [InlineData("%PDF-1.7 a little too long", 10, "upload limit")]
    public async Task UnacceptableFile_ShouldBeRefusedAndNothingStored(string text, long limit, string expected)
    {
        var rig = await PublicationTesting.RigAsync(mongo);
        var service = Service(rig, maxUploadBytes: limit);

        var result = await service.UploadAsync(PublicationTesting.Team, TargetKind.Model, ModelId, "dismantlingInformation", new DocumentUpload("manual.pdf", Encoding.ASCII.GetBytes(text)));

        Assert.Contains(expected, result.Problem, StringComparison.Ordinal);
        Assert.Empty(await new DocumentStore(rig.Context).ListAsync(TargetKind.Model, ModelId, "dismantlingInformation"));
    }

    [Theory]
    [InlineData("cluster-admin", TargetKind.Model, ModelId, "dismantlingInformation", "only the team")]
    [InlineData("team", TargetKind.Batch, "batch-test-2026", "euDeclarationOfConformity", "public documents are links")]
    [InlineData("team", TargetKind.Pack, "pack-VPC8C05-6788", "informationOnAccidents", "AddAccidentReportAsync")]
    [InlineData("team", TargetKind.Model, "no-such-model", "dismantlingInformation", "not found")]
    public async Task WrongUpload_ShouldBeRefused(string sender, TargetKind owner, string ownerId, string property, string expected)
    {
        var rig = await PublicationTesting.RigAsync(mongo);
        var who = sender == "team" ? PublicationTesting.Team : ChangeSender.ClusterAdmin("admin@example.com", "cluster-test");

        var result = await Service(rig).UploadAsync(who, owner, ownerId, property, new DocumentUpload("file.pdf", DocumentTesting.Pdf()));

        Assert.Null(result.Document);
        Assert.Contains(expected, result.Problem, StringComparison.Ordinal);
    }

    [Fact]
    public async Task WithoutAnAppBaseUrl_UploadsShouldBeRefused()
    {
        var rig = await PublicationTesting.RigAsync(mongo);

        var result = await Service(rig, DocumentTesting.NoLinks).UploadAsync(PublicationTesting.Team, TargetKind.Model, ModelId, "dismantlingInformation", new DocumentUpload("a.pdf", DocumentTesting.Pdf()));

        Assert.Equal(DppLinks.NotConfigured, result.Problem);
    }

    [Theory]
    [InlineData("team", true, true)]
    [InlineData("market-surveillance", true, true)]
    [InlineData("li-repair", true, true)]
    [InlineData("li-treatment", true, false)] // #45 isn't shared with treatment facilities
    [InlineData("own-cluster-user", true, true)]
    [InlineData("own-cluster-user", false, false)] // a draft pack gives its cluster nothing (J-6)
    [InlineData("team", false, true)]
    [InlineData("market-surveillance", false, false)] // nothing unpublished leaves the team (J-6)
    [InlineData("li-repair", false, false)]
    [InlineData("other-cluster", true, false)]
    [InlineData("anonymous", true, false)]
    public async Task Read_ShouldServeTheFileOnlyToItsAudience(string kind, bool published, bool allowed)
    {
        var rig = published ? await PublicationTesting.PublishedRigAsync(mongo) : await PublicationTesting.RigAsync(mongo);
        var service = Service(rig);
        var content = DocumentTesting.Pdf("dismantling");
        var uploaded = (await service.UploadAsync(PublicationTesting.Team, TargetKind.Model, ModelId, "dismantlingInformation", new DocumentUpload("d.pdf", content))).Document!;

        var file = await service.ReadAsync(ReaderFor(kind), uploaded.Id);

        Assert.Equal(allowed, file is not null);
        if (file is not null)
        {
            Assert.Equal(content, file.Content);
        }
    }

    [Theory]
    [InlineData("notified-body", true)]
    [InlineData("market-surveillance", true)]
    [InlineData("own-cluster-user", false)]
    public async Task APublishedBatteryWithoutACluster_ShouldServeItsBatchsTestReportsToTheAuthorities(string kind, bool allowed)
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo);
        await rig.Store.SetPackClusterAsync(rig.PackId, null);
        var service = Service(rig);
        var uploaded = (await service.UploadAsync(PublicationTesting.Team, TargetKind.Batch, "batch-test-2026", "resultsOfTestReportsProvingCompliance", new DocumentUpload("tests.pdf", DocumentTesting.Pdf()))).Document!;

        Assert.Equal(allowed, await service.ReadAsync(ReaderFor(kind), uploaded.Id) is not null);
    }

    [Fact]
    public async Task RefusedRead_ShouldBeLogged()
    {
        var rig = await PublicationTesting.RigAsync(mongo);
        var service = Service(rig);
        var uploaded = (await service.UploadAsync(PublicationTesting.Team, TargetKind.Batch, "batch-test-2026", "resultsOfTestReportsProvingCompliance", new DocumentUpload("tests.pdf", DocumentTesting.Pdf()))).Document!;

        Assert.Null(await service.ReadAsync(Reader.Anonymous, uploaded.Id));

        var events = await new ApplicationAuditService(rig.Context).ListApplicationAuditEventsAsync(new ApplicationAuditFilter());
        Assert.Contains(events, e => e["eventType"].AsString == DocumentService.DeniedEvent && e["entityId"].AsString == uploaded.Id);
    }

    [Fact]
    public async Task AccidentReport_ShouldBeStoredAndOnlyMoveTheLastUpdate()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo);
        rig.Clock.Now = rig.Clock.Now.AddDays(3);

        var result = await Service(rig).AddAccidentReportAsync(ChangeSender.ClusterAdmin("admin@example.com", "cluster-test"), rig.PackId, new DocumentUpload("incident-2026-11.pdf", DocumentTesting.Pdf()));

        Assert.Null(result.Problem);
        var version = Assert.Single(await rig.Store.ListVersionsAsync(rig.PackId)); // a live update: no new version
        Assert.Equal(rig.Clock.Now.UtcDateTime, version.LastUpdatedUtc);
        var link = (await rig.Store.GetAsync<BatteryPack>(rig.PackId))!.Values["informationOnAccidents"].Value;
        Assert.Equal(DocumentTesting.Links.AccidentPageUrl(rig.PackId), link["url"]!.GetValue<string>());
    }

    [Fact]
    public async Task AccidentReportFromAnotherCluster_ShouldBeRefusedAndNothingStored()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo);

        var result = await Service(rig).AddAccidentReportAsync(ChangeSender.ClusterAdmin("other@example.com", "another-cluster"), rig.PackId, new DocumentUpload("incident.pdf", DocumentTesting.Pdf()));

        Assert.Contains("can't be changed by this sender", result.Problem, StringComparison.Ordinal);
        Assert.Empty(await new DocumentStore(rig.Context).ListAsync(TargetKind.Pack, rig.PackId, "informationOnAccidents"));
    }

    [Fact]
    public async Task AccidentReports_ShouldBeListedNewestFirstForTheirAudience()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo);
        var service = Service(rig);
        await service.AddAccidentReportAsync(PublicationTesting.Team, rig.PackId, new DocumentUpload("first.pdf", DocumentTesting.Pdf("1")));
        rig.Clock.Now = rig.Clock.Now.AddHours(1);
        await service.AddAccidentReportAsync(PublicationTesting.Team, rig.PackId, new DocumentUpload("second.pdf", DocumentTesting.Pdf("2")));

        var reports = await service.AccidentReportsAsync(ReaderFor("own-cluster-user"), rig.PackId);

        Assert.Equal(["second.pdf", "first.pdf"], reports!.Select(r => r.FileName));
        Assert.Null(await service.AccidentReportsAsync(Reader.Anonymous, rig.PackId));
        Assert.Null(await service.AccidentReportsAsync(ReaderFor("team"), "no-such-pack"));
    }

    internal static DocumentService Service(PublicationRig rig, DppLinks? links = null, long maxUploadBytes = BatteryPassOptions.DefaultMaxUploadBytes) => new(
        new DocumentStore(rig.Context),
        rig.Store,
        TestDemo.For(rig),
        rig.Pipeline,
        links ?? DocumentTesting.Links,
        Options.Create(new BatteryPassOptions { AppBaseUrl = DocumentTesting.BaseUrl, MaxUploadBytes = maxUploadBytes }),
        new ApplicationAuditService(rig.Context),
        rig.Clock);

    private static Reader ReaderFor(string kind) => kind switch
    {
        "team" => Reader.With("team@example.com", ReaderRole.Team),
        "market-surveillance" => Reader.With("msa@example.com", ReaderRole.MarketSurveillance),
        "li-repair" => Reader.With("repair@example.com", ReaderRole.LiRepair),
        "li-treatment" => Reader.With("treatment@example.com", ReaderRole.LiTreatment),
        "notified-body" => Reader.With("nb@example.com", ReaderRole.NotifiedBody),
        "own-cluster-user" => Reader.With("user@example.com", ReaderRole.ClusterUser, "cluster-test"),
        "other-cluster" => Reader.With("other@example.com", ReaderRole.ClusterUser, "another-cluster"),
        _ => Reader.Anonymous,
    };
}
