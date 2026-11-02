using BatteryPassWeb.Controllers;
using BatteryPassWeb.Dpp.Access;
using BatteryPassWeb.Dpp.Documents;
using BatteryPassWeb.Dpp.Pages;
using BatteryPassWeb.Dpp.Store;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace BatteryPassWeb.Tests.Dpp;

/// <summary>
/// Redesign R2: a picture per battery model, shown instead of its family's (owner's point 8: the Compact 13M showed the
/// 7M's picture). A model without one shows its family's picture, as before (decision 20).
/// </summary>
[Collection(MongoCollection.Name)]
public sealed class ModelPictureTests(MongoFixture mongo)
{
    private const string Team = "team@example.com";

    private static IFormFile Upload(byte[] content, string name) => new FormFile(new MemoryStream(content), 0, content.Length, "picture", name);

    [Fact]
    public async Task AModelsPicture_ShouldBeShownInsteadOfItsFamilysAndServedToAnyone()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo);
        await rig.Store.SetFamilyPictureAsync("family-test", "file-1", "image/png");

        var problem = await PageTesting.Pages(rig).SetModelPictureAsync(Team, "model-test", "model.png", DocumentTesting.Png());
        var summary = (await PassportPagesTests.Pages(rig).SummaryAsync(Reader.Anonymous, "VPC8C05-6788"))!;
        var file = Assert.IsType<FileContentResult>(await PageTesting.Controller(rig).ModelPicture("model-test", CancellationToken.None));

        Assert.Null(problem);
        Assert.Equal("/dpp/models/model-test/picture", summary.PictureHref);
        Assert.Equal(FileTypes.Png, file.ContentType);
        Assert.Equal(DocumentTesting.Png(), file.FileContents);
        Assert.IsType<NotFoundResult>(await PageTesting.Controller(rig).ModelPicture("no-such-model", CancellationToken.None));
    }

    [Fact]
    public async Task RemovingAModelsPicture_ShouldShowTheFamilysAgain()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo);
        await rig.Store.SetFamilyPictureAsync("family-test", "file-1", "image/png");
        await PageTesting.Pages(rig).SetModelPictureAsync(Team, "model-test", "model.png", DocumentTesting.Png());

        Assert.True(await PageTesting.Pages(rig).RemoveModelPictureAsync(Team, "model-test"));
        var summary = (await PassportPagesTests.Pages(rig).SummaryAsync(Reader.Anonymous, "VPC8C05-6788"))!;

        Assert.Equal("/dpp/families/family-test/picture", summary.PictureHref);
        Assert.Null((await rig.Store.GetAsync<BatteryModel>("model-test"))!.PictureFileId);
        Assert.IsType<NotFoundResult>(await PageTesting.Controller(rig).ModelPicture("model-test", CancellationToken.None));
        Assert.False(await PageTesting.Pages(rig).RemoveModelPictureAsync(Team, "model-test")); // nothing left to remove
    }

    [Fact]
    public async Task AModelsPicture_ShouldBeAPngOrJpegOfAKnownModel()
    {
        var rig = await PublicationTesting.RigAsync(mongo);

        Assert.Equal("the picture must be a PNG or JPEG file", await PageTesting.Pages(rig).SetModelPictureAsync(Team, "model-test", "model.pdf", DocumentTesting.Pdf()));
        Assert.Equal("the battery model was not found", await PageTesting.Pages(rig).SetModelPictureAsync(Team, "no-such-model", "model.png", DocumentTesting.Png()));
        Assert.Null((await rig.Store.GetAsync<BatteryModel>("model-test"))!.PictureFileId);
    }

    [Fact]
    public async Task TheFamilyPage_ShouldSaveAndRemoveAModelsPictureInItsSection()
    {
        var rig = await PublicationTesting.RigAsync(mongo);
        var controller = PageTesting.Controller(rig);
        var page = File.ReadAllText(DppTestFiles.RepoFile("web", "Views", "ProductData", "Family.cshtml"));

        var saved = Assert.IsType<RedirectResult>(await controller.UploadModelPicture("model-test", Upload(DocumentTesting.Png(), "model.png"), CancellationToken.None));
        var savedMessage = controller.TempData[ProductDataController.StatusKey];
        var removed = Assert.IsType<RedirectResult>(await controller.RemoveModelPicture("model-test", CancellationToken.None));

        Assert.Equal($"{ProductDataPages.FamilyHref("family-test", "model-test")}#modules", saved.Url);
        Assert.Equal("Picture saved. Its batteries' public summary shows it instead of the family's.", savedMessage);
        Assert.Equal($"{ProductDataPages.FamilyHref("family-test", "model-test")}#modules", removed.Url);
        Assert.Equal("Picture removed. Its batteries show the family's picture again.", controller.TempData[ProductDataController.StatusKey]);
        Assert.Contains("action=\"/admin/models/@selected.Id/picture\"", page, StringComparison.Ordinal);
        Assert.Contains("action=\"/admin/models/@selected.Id/picture/remove\"", page, StringComparison.Ordinal);
        Assert.Contains("<img src=\"/dpp/models/@selected.Id/picture\"", page, StringComparison.Ordinal);
    }
}
