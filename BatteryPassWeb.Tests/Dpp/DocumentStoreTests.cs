using BatteryPassWeb.Configuration;
using BatteryPassWeb.Dpp.Documents;
using BatteryPassWeb.Dpp.Editing;
using BatteryPassWeb.Services;
using Microsoft.Extensions.Options;

namespace BatteryPassWeb.Tests.Dpp;

[Collection(MongoCollection.Name)]
public sealed class DocumentStoreTests(MongoFixture mongo)
{
    [Fact]
    public async Task File_ShouldComeBackUnchanged()
    {
        var documents = await StoreAsync();
        var content = DocumentTesting.Pdf("dismantling");
        var document = Record("doc-1", await documents.UploadFileAsync("dismantling.pdf", content), DppStoreTesting.Now.UtcDateTime);

        await documents.InsertAsync(document);
        var stored = await documents.GetAsync("doc-1");

        Assert.NotNull(stored);
        Assert.Equal("application/pdf", stored.ContentType);
        Assert.Equal(TargetKind.Model, stored.OwnerKind);
        Assert.Equal(content, await documents.DownloadAsync(stored));
    }

    [Fact]
    public async Task List_ShouldShowTheNewestFirst()
    {
        var documents = await StoreAsync();
        var fileId = await documents.UploadFileAsync("a.pdf", DocumentTesting.Pdf());
        await documents.InsertAsync(Record("doc-older", fileId, DppStoreTesting.Now.UtcDateTime));
        await documents.InsertAsync(Record("doc-newer", fileId, DppStoreTesting.Now.UtcDateTime.AddHours(1)));

        var listed = await documents.ListAsync(TargetKind.Model, "model-test", "dismantlingInformation");

        Assert.Equal(["doc-newer", "doc-older"], listed.Select(d => d.Id));
        Assert.Empty(await documents.ListAsync(TargetKind.Model, "model-test", "safetyMeasures"));
    }

    [Fact]
    public async Task MissingDocument_ShouldReadAsNothing()
    {
        var documents = await StoreAsync();

        Assert.Null(await documents.GetAsync("doc-none"));
    }

    [Fact]
    public async Task WithoutADatabase_ReadsShouldReturnNothingAndWritesShouldThrow()
    {
        var documents = new DocumentStore(new MongoContext(Options.Create(new BatteryPassOptions())));

        await documents.EnsureIndexesAsync();
        Assert.Null(await documents.GetAsync("doc-1"));
        await Assert.ThrowsAsync<InvalidOperationException>(() => documents.UploadFileAsync("a.pdf", DocumentTesting.Pdf()));
    }

    private async Task<DocumentStore> StoreAsync()
    {
        var documents = new DocumentStore(mongo.NewContext());
        await documents.EnsureIndexesAsync();
        return documents;
    }

    private static DppDocument Record(string id, string fileId, DateTime uploadedAtUtc) => new()
    {
        Id = id,
        OwnerKind = TargetKind.Model,
        OwnerId = "model-test",
        Property = "dismantlingInformation",
        FileName = "dismantling.pdf",
        ContentType = "application/pdf",
        Length = 30,
        Sha256 = new string('0', 64),
        FileId = fileId,
        Title = "Dismantling manual",
        UploadedAtUtc = uploadedAtUtc,
        UploadedBy = "team@example.com",
    };
}
