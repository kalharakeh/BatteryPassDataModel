using System.Reflection;
using BatteryPassWeb.Controllers;
using BatteryPassWeb.Dpp.Documents;
using BatteryPassWeb.Dpp.Editing;
using BatteryPassWeb.Dpp.Pages;
using BatteryPassWeb.Dpp.Store;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace BatteryPassWeb.Tests.Dpp;

[Collection(MongoCollection.Name)]
public sealed class ProductDataControllerTests(MongoFixture mongo)
{
    [Fact]
    public void Pages_ShouldBeForTheTeamAndEveryPostProtectedAgainstForgery()
    {
        var type = typeof(ProductDataController);
        Assert.Equal("AdminOnly", type.GetCustomAttribute<AuthorizeAttribute>()?.Policy);
        var actions = type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
        Assert.All(actions.Where(action => action.GetCustomAttribute<HttpPostAttribute>() is not null), action =>
            Assert.NotNull(action.GetCustomAttribute<ValidateAntiForgeryTokenAttribute>()));
        Assert.Equal(["FamilyPicture"], actions.Where(action => action.GetCustomAttribute<AllowAnonymousAttribute>() is not null).Select(action => action.Name));
    }

    [Fact]
    public async Task SavingAChange_ShouldReturnToTheEditorWithAMessage()
    {
        var rig = await PublicationTesting.RigAsync(mongo);
        var form = PageTesting.ShownForm(TargetKind.Model, (await rig.Store.GetAsync<BatteryModel>("model-test"))!.Values);
        form["v.batteryMass"] = "520";
        form["name"] = "EXB-NMC-100";
        var controller = PageTesting.Controller(rig, form);

        var result = Assert.IsType<RedirectResult>(await controller.SaveModel("model-test", confirmed: false, CancellationToken.None));

        Assert.Equal("/admin/families/family-test?model=model-test#editor", result.Url);
        Assert.Equal("1 value saved.", controller.TempData[ProductDataController.EditorStatusKey]);
    }

    [Fact]
    public async Task ARefusedValue_ShouldShowThePageAgainWithWhatWasTyped()
    {
        var rig = await PublicationTesting.RigAsync(mongo);
        var form = PageTesting.ShownForm(TargetKind.Model, (await rig.Store.GetAsync<BatteryModel>("model-test"))!.Values);
        form["v.initialRoundTripEnergyEfficiency"] = "120";
        var controller = PageTesting.Controller(rig, form);

        var view = Assert.IsType<ViewResult>(await controller.SaveModel("model-test", confirmed: false, CancellationToken.None));

        Assert.Equal("Family", view.ViewName);
        var field = Assert.IsType<FamilyPage>(view.Model).ModelEditor!.Sections.SelectMany(section => section.Fields).Single(f => f.Attribute.Number == 71);
        Assert.NotNull(field.Refusal);
        Assert.Equal("120", field.Text("v.initialRoundTripEnergyEfficiency"));
        Assert.Contains("1 value not saved", (string)controller.ViewData[ProductDataController.EditorErrorKey]!);
    }

    [Fact]
    public async Task AChangeToAPublishedPassport_ShouldAskThenPublish()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo);
        var form = PageTesting.ShownForm(TargetKind.Batch, (await rig.Store.GetAsync<ProductionBatch>("batch-test-2026"))!.Values);
        form["v.carbonFootprintPerformanceClass"] = "Class A";

        var asked = Assert.IsType<ViewResult>(await PageTesting.Controller(rig, form).SaveBatch("batch-test-2026", confirmed: false, CancellationToken.None));
        Assert.Single(Assert.IsType<BatchPage>(asked.Model).Editor.Confirm);
        Assert.Equal(1, (await rig.Store.GetActiveVersionAsync(rig.PackId))?.Number);

        var controller = PageTesting.Controller(rig, form);
        Assert.IsType<RedirectResult>(await controller.SaveBatch("batch-test-2026", confirmed: true, CancellationToken.None));

        Assert.Equal(2, (await rig.Store.GetActiveVersionAsync(rig.PackId))?.Number);
        Assert.Equal("1 value saved. 1 passport got a new version.", controller.TempData[ProductDataController.EditorStatusKey]);
    }

    [Fact]
    public async Task UploadingATestReport_ShouldLinkTheBatchToTheFile()
    {
        var rig = await PublicationTesting.RigAsync(mongo);
        var controller = PageTesting.Controller(rig);
        using var content = new MemoryStream(DocumentTesting.Pdf("reports"));
        var file = new FormFile(content, 0, content.Length, "file", "test-reports.pdf");

        var result = Assert.IsType<RedirectResult>(await controller.UploadBatchDocument("batch-test-2026", "resultsOfTestReportsProvingCompliance", file, "Test reports", "en-GB", confirmed: false, CancellationToken.None));

        Assert.Equal("/admin/batches/batch-test-2026#editor", result.Url);
        Assert.Equal("test-reports.pdf uploaded.", controller.TempData[ProductDataController.EditorStatusKey]);
        var document = Assert.Single(await new DocumentStore(rig.Context).ListAsync(TargetKind.Batch, "batch-test-2026", "resultsOfTestReportsProvingCompliance"));
        Assert.Equal("Test reports", document.Title);
    }

    [Fact]
    public async Task AnUploadThatPublishesNewVersions_ShouldWaitForTheTick()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo);
        var controller = PageTesting.Controller(rig);
        FormFile File(MemoryStream content) => new(content, 0, content.Length, "file", "test-reports.pdf");
        using var first = new MemoryStream(DocumentTesting.Pdf("reports"));
        using var second = new MemoryStream(DocumentTesting.Pdf("reports"));

        await controller.UploadBatchDocument("batch-test-2026", "resultsOfTestReportsProvingCompliance", File(first), null, null, confirmed: false, CancellationToken.None);

        Assert.Contains("would publish new versions of 1 passport", (string)controller.TempData[ProductDataController.EditorErrorKey]!);
        Assert.Empty(await new DocumentStore(rig.Context).ListAsync(TargetKind.Batch, "batch-test-2026", "resultsOfTestReportsProvingCompliance"));

        await controller.UploadBatchDocument("batch-test-2026", "resultsOfTestReportsProvingCompliance", File(second), null, null, confirmed: true, CancellationToken.None);

        Assert.Single(await new DocumentStore(rig.Context).ListAsync(TargetKind.Batch, "batch-test-2026", "resultsOfTestReportsProvingCompliance"));
        Assert.Equal(2, (await rig.Store.GetActiveVersionAsync(rig.PackId))?.Number);
    }

    [Fact]
    public async Task AnUploadWithoutAFile_ShouldSaySo()
    {
        var rig = await PublicationTesting.RigAsync(mongo);
        var controller = PageTesting.Controller(rig);

        await controller.UploadModelDocument("model-test", "dismantlingInformation", null, null, null, confirmed: false, CancellationToken.None);

        Assert.Equal("Choose a file first.", controller.TempData[ProductDataController.EditorErrorKey]);
    }

    [Fact]
    public async Task ADuplicateBatch_ShouldBeRefusedWithAReason()
    {
        var rig = await PublicationTesting.RigAsync(mongo);
        var controller = PageTesting.Controller(rig);

        var result = Assert.IsType<RedirectResult>(await controller.CreateBatch("model-test", 2026, "site-test", CancellationToken.None));

        Assert.Equal("/admin/families/family-test?model=model-test", result.Url);
        Assert.Contains("already has a 2026 batch", (string)controller.TempData[ProductDataController.ErrorKey]!);
    }

    [Fact]
    public async Task ADuplicateModelName_ShouldBeRefused()
    {
        var rig = await PublicationTesting.RigAsync(mongo);
        var model = (await rig.Store.GetAsync<BatteryModel>("model-test"))!;
        var controller = PageTesting.Controller(rig);

        await controller.CreateModel("family-test", model.Name.ToUpperInvariant(), null, CancellationToken.None);

        Assert.Contains("already has a model called", (string)controller.TempData[ProductDataController.ErrorKey]!);
        Assert.Single(await rig.Store.ListModelsAsync("family-test"));
    }

    [Fact]
    public async Task ThePicture_ShouldBeServedWithItsType()
    {
        var rig = await PublicationTesting.RigAsync(mongo);
        await PageTesting.Pages(rig).SetFamilyPictureAsync("team@example.com", "family-test", "family.png", DocumentTesting.Png());
        var controller = PageTesting.Controller(rig);

        var file = Assert.IsType<FileContentResult>(await controller.FamilyPicture("family-test", CancellationToken.None));

        Assert.Equal(FileTypes.Png, file.ContentType);
        Assert.Equal(DocumentTesting.Png(), file.FileContents);
        Assert.IsType<NotFoundResult>(await controller.FamilyPicture("no-such-family", CancellationToken.None));
    }

    [Fact]
    public async Task UnknownRecords_ShouldBeNotFound()
    {
        var rig = await PublicationTesting.RigAsync(mongo);
        var controller = PageTesting.Controller(rig);

        Assert.IsType<NotFoundResult>(await controller.Family("no-such-family", null, CancellationToken.None));
        Assert.IsType<NotFoundResult>(await controller.Batch("no-such-batch", CancellationToken.None));
        Assert.IsType<NotFoundResult>(await controller.SaveModel("no-such-model", false, CancellationToken.None));
        Assert.IsType<NotFoundResult>(await controller.SaveSite("no-such-site", false, CancellationToken.None));
    }
}
