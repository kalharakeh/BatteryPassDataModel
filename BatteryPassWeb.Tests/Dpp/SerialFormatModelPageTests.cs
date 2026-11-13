using System.Reflection;
using BatteryPassWeb.Controllers;
using BatteryPassWeb.Dpp.Documents;
using BatteryPassWeb.Dpp.Editing;
using BatteryPassWeb.Dpp.Identifiers;
using BatteryPassWeb.Dpp.Pages;
using BatteryPassWeb.Dpp.Store;
using BatteryPassWeb.Services;
using BatteryPassWeb.Tests.Demo;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Bson;
using MongoDB.Driver;
using static BatteryPassWeb.Tests.Dpp.SmallLetterSerialTesting;

namespace BatteryPassWeb.Tests.Dpp;

/// <summary>
/// Decision 81 on the model's page: an optional "Serial number format", checked when it is saved; before a format that
/// some of the model's batteries don't fit is saved, the page says how many; two models may share a format, and the page
/// names the other; "Copy as real product data" carries it; the demo models have none.
/// </summary>
[Collection(MongoCollection.Name)]
public sealed class SerialFormatModelPageTests(MongoFixture mongo)
{
    private const string Core800s = "VPC8C05s-#####";

    private static ProductDataController Controller(DemoRig rig)
    {
        var http = new DefaultHttpContext { User = TestAccounts.SignedIn(rig.Services.GetRequiredService<MongoContext>(), Who, AccessControlService.RoleAdmin) };
        return new ProductDataController(rig.Services.GetRequiredService<ProductDataPages>(), rig.Services.GetRequiredService<RecordEditor>(), rig.Store, rig.Services.GetRequiredService<DocumentService>())
        {
            ControllerContext = new ControllerContext { HttpContext = http },
            TempData = new TempDataDictionary(http, new NoTempDataKept()),
        };
    }

    private static Task<IActionResult> SaveAsync(ProductDataController controller, DemoRig rig, string modelId, string? format, bool confirmed = false) =>
        controller.SetSerialFormat(modelId, format, confirmed, rig.Services.GetRequiredService<IIdentifierScheme>(), CancellationToken.None);

    private static async Task<BatteryModel> ModelOfAsync(DemoRig rig, BatteryPack pack) =>
        (await rig.Store.GetAsync<BatteryModel>((await rig.Store.GetAsync<ProductionBatch>(pack.BatchId))!.ModelId))!;

    [Fact]
    public void TheAction_ShouldBeTheTeamsOnly_AndProtectedAgainstForgery()
    {
        var action = typeof(ProductDataController).GetMethod(nameof(ProductDataController.SetSerialFormat))!;

        Assert.Equal("AdminOnly", typeof(ProductDataController).GetCustomAttribute<AuthorizeAttribute>()?.Policy);
        Assert.Equal("models/{modelId}/serial-format", action.GetCustomAttribute<HttpPostAttribute>()?.Template);
        Assert.NotNull(action.GetCustomAttribute<ValidateAntiForgeryTokenAttribute>());
        Assert.Null(action.GetCustomAttribute<AllowAnonymousAttribute>());
    }

    [Fact]
    public async Task AFormat_ShouldBeSavedOnTheModel_Logged_AndShownWithAnExample()
    {
        var (rig, _, c7) = await SeededAsync(mongo);
        using var __ = rig;
        var model = await ModelOfAsync(rig, c7);
        var controller = Controller(rig);

        var result = Assert.IsType<RedirectResult>(await SaveAsync(controller, rig, model.Id, "  VPC8C05s-#####  "));

        Assert.Equal($"{ProductDataPages.FamilyHref(model.FamilyId, model.Id)}#serial-format", result.Url);
        Assert.Equal("Serial number format saved. A serial number that fits: VPC8C05s-00001. The passport didn't change: it isn't passport data.", controller.TempData[ProductDataController.StatusKey]);
        Assert.Equal(Core800s, (await rig.Store.GetAsync<BatteryModel>(model.Id))!.SerialFormat);
        var logged = Assert.Single(await rig.All("applicationAuditEvents", Builders<BsonDocument>.Filter.Eq("eventType", ProductDataPages.SerialFormatEvent)));
        Assert.Equal(($"Serial number format of battery model {model.Name} set to {Core800s}", model.Id), (logged["message"].AsString, logged["entityId"].AsString));
        var page = (await rig.Services.GetRequiredService<ProductDataPages>().FamilyAsync(model.FamilyId, model.Id))!;
        Assert.Equal(Core800s, page.SelectedModel!.SerialFormat);
        Assert.Empty(page.SerialFormatSharedWith);

        // The same again changes nothing and logs nothing.
        await SaveAsync(Controller(rig), rig, model.Id, Core800s);
        Assert.Single(await rig.All("applicationAuditEvents", Builders<BsonDocument>.Filter.Eq("eventType", ProductDataPages.SerialFormatEvent)));
    }

    [Theory]
    [InlineData("VPC8C05s-00001", SerialFormats.NeedsADigitSign)]
    [InlineData("VPC8C05s #####", SerialFormats.OnlyTheseCharacters)]
    [InlineData("A234567890-234567890-234567890-####", "a serial number of this format can't make a battery ID")]
    public async Task AFormatThatIsNotOne_ShouldNotBeSaved_AndThePageSaysWhy(string typed, string why)
    {
        var (rig, _, c7) = await SeededAsync(mongo);
        using var __ = rig;
        var model = await ModelOfAsync(rig, c7);
        await rig.Store.SetModelSerialFormatAsync(model.Id, Core800s);
        var controller = Controller(rig);

        await SaveAsync(controller, rig, model.Id, typed);

        Assert.StartsWith($"The serial number format wasn't saved: {why}", (string)controller.TempData[ProductDataController.ErrorKey]!, StringComparison.Ordinal);
        Assert.Equal(typed, controller.TempData[ProductDataController.TypedSerialFormatKey]); // kept in the field, to correct
        Assert.Equal(Core800s, (await rig.Store.GetAsync<BatteryModel>(model.Id))!.SerialFormat);
    }

    [Fact]
    public async Task AnEmptyField_ShouldTakeTheFormatAway_SoTheModelAcceptsAnySerialAgain()
    {
        var (rig, pool, c7) = await SeededAsync(mongo);
        using var __ = rig;
        var model = await ModelOfAsync(rig, c7);
        await rig.Store.SetModelSerialFormatAsync(model.Id, Core800s);
        var controller = Controller(rig);

        await SaveAsync(controller, rig, model.Id, "   ");

        Assert.Equal("Serial number format removed. This model takes any serial number again.", controller.TempData[ProductDataController.StatusKey]);
        Assert.Null((await rig.Store.GetAsync<BatteryModel>(model.Id))!.SerialFormat);
        Assert.True((await pool.ReleaseAsync(Who, new ReleaseRequest("PO any", model.Id, [], "WHATEVER-1"))).Done);
    }

    [Fact]
    public async Task BeforeAFormatIsSaved_ThePageShouldSayHowManyOfTheModelsBatteriesDoNotFit_AndTouchNone()
    {
        var (rig, pool, c7) = await SeededAsync(mongo);
        using var __ = rig;
        var model = await ModelOfAsync(rig, c7);
        var creation = rig.Services.GetRequiredService<PackCreationService>();
        await pool.ReleaseAsync(Who, List("PO mixed", "VPC8C05s-00001", "VPC8C05-00002", "VPC7C05-00003"));
        foreach (var serial in new[] { "VPC8C05s-00001", "VPC8C05-00002", "VPC7C05-00003" })
        {
            Assert.NotNull((await creation.CreateAsync(ChangeSender.Team(Who), NewBattery(c7, serial))).Pack);
        }

        var before = await rig.All(DppStore.Packs);
        var controller = Controller(rig);

        // Asked first: nothing is saved yet.
        var asked = Assert.IsType<RedirectResult>(await SaveAsync(controller, rig, model.Id, Core800s));

        Assert.Equal($"{ProductDataPages.FamilyHref(model.FamilyId, model.Id)}#serial-format", asked.Url);
        Assert.Null((await rig.Store.GetAsync<BatteryModel>(model.Id))!.SerialFormat);
        Assert.Equal($"{model.Id}|{Core800s}", controller.TempData[ProductDataController.PendingSerialFormatKey]);
        Assert.Equal(2, controller.TempData[ProductDataController.PendingSerialFormatMisfitsKey]); // the demo's own batteries have test IDs: not counted
        Assert.Equal("VPC7C05-00003, VPC8C05-00002", controller.TempData[ProductDataController.PendingSerialFormatExamplesKey]);

        // Saved on the team's yes; the batteries are as they were.
        var confirmed = Controller(rig);
        await SaveAsync(confirmed, rig, model.Id, Core800s, confirmed: true);

        Assert.Equal(Core800s, (await rig.Store.GetAsync<BatteryModel>(model.Id))!.SerialFormat);
        Assert.StartsWith("Serial number format saved.", (string)confirmed.TempData[ProductDataController.StatusKey]!, StringComparison.Ordinal);
        Assert.Equal(before.Select(pack => pack.ToJson()), (await rig.All(DppStore.Packs)).Select(pack => pack.ToJson()));
    }

    [Fact]
    public async Task TwoModels_ShouldBeAllowedTheVerySameFormat_AndEachPageNamesTheOther()
    {
        var (rig, _, c7) = await SeededAsync(mongo);
        using var __ = rig;
        var one = await ModelOfAsync(rig, c7);
        var other = (await rig.Store.ListAsync<BatteryModel>()).First(model => model.Id != one.Id);
        var pages = rig.Services.GetRequiredService<ProductDataPages>();
        await SaveAsync(Controller(rig), rig, one.Id, Core800s);
        var controller = Controller(rig);

        await SaveAsync(controller, rig, other.Id, "vpc8c05S-#####"); // the same format, its letters in another case

        Assert.Equal("vpc8c05S-#####", (await rig.Store.GetAsync<BatteryModel>(other.Id))!.SerialFormat);
        Assert.Contains($"Model {one.Name} has the same format.", (string)controller.TempData[ProductDataController.StatusKey]!, StringComparison.Ordinal);
        Assert.Equal([other.Name], (await pages.FamilyAsync(one.FamilyId, one.Id))!.SerialFormatSharedWith);
        Assert.Equal([one.Name], (await pages.FamilyAsync(other.FamilyId, other.Id))!.SerialFormatSharedWith);

        await rig.Store.SetModelSerialFormatAsync(other.Id, "VPC8C05-#####"); // one letter less: another format
        Assert.Empty((await pages.FamilyAsync(one.FamilyId, one.Id))!.SerialFormatSharedWith);
    }

    [Fact]
    public async Task CopyAsRealProductData_ShouldCarryTheFormatWithTheModel_AndTheDemoModelsHaveNone()
    {
        var rig = await DemoRig.StartAsync(mongo, dunsIds: true);
        using var _ = rig;
        await rig.Demo.SeedAsync(Who);
        Assert.All(await rig.Store.ListAsync<BatteryModel>(), model => Assert.Null(model.SerialFormat)); // the demo models get no format
        var compact = (await rig.Store.ListAsync<BatteryFamily>()).Single(family => family.Name == "Compact");
        var source = (await rig.Store.ListModelsAsync(compact.Id))[0];
        await rig.Store.SetModelSerialFormatAsync(source.Id, Core800s);

        var (familyId, problem) = await rig.Services.GetRequiredService<ProductDataPages>().CopyFamilyAsRealAsync(Who, compact.Id, "Compact S");

        Assert.Null(problem);
        var copies = await rig.Store.ListModelsAsync(familyId!);
        Assert.Equal(Core800s, copies.Single(model => model.Name == source.Name).SerialFormat);
        Assert.All(copies.Where(model => model.Name != source.Name), model => Assert.Null(model.SerialFormat));

        // Resetting the demo data makes the demo models again, without a format.
        await rig.Store.SetModelSerialFormatAsync(source.Id, null);
        Assert.All((await rig.Store.ListAsync<BatteryModel>()).Where(model => model.IsDemo), model => Assert.Null(model.SerialFormat));
    }

    [Fact]
    public void ThePage_ShouldOfferTheField_SayWhatTheSignMeans_AndAskBeforeSavingAFormatBatteriesDoNotFit()
    {
        var family = File.ReadAllText(DppTestFiles.RepoFile("web", "Views", "ProductData", "Family.cshtml"));

        Assert.Contains("action=\"/admin/models/@Uri.EscapeDataString(selected.Id)/serial-format\"", family, StringComparison.Ordinal);
        Assert.Contains("Serial number format of @selected.Name <em class=\"bp-field-marker\">Not passport data</em>", family, StringComparison.Ordinal);
        Assert.Contains("@SerialFormats.Hint", family, StringComparison.Ordinal);
        Assert.Contains("# for one digit", SerialFormats.Hint, StringComparison.Ordinal);
        Assert.Contains("A serial number that fits: @SerialFormats.Example(format).", family, StringComparison.Ordinal);
        Assert.Contains("Without a format, this model takes any serial number.", family, StringComparison.Ordinal);
        Assert.Contains("Save the format anyway", family, StringComparison.Ordinal);
        Assert.Contains("They stay as they are. Nothing is saved yet.", family, StringComparison.Ordinal);
        Assert.Contains("the same format", family, StringComparison.Ordinal);
        Assert.Contains("Test IDs aren't checked.", family, StringComparison.Ordinal);

        // The Help and the release form say it too.
        var guide = File.ReadAllText(DppTestFiles.RepoFile("web", "Views", "Help", "Guide.cshtml"));
        var release = File.ReadAllText(DppTestFiles.RepoFile("web", "Views", "BatteryIds", "Index.cshtml"));
        Assert.Contains("A model can have a serial number format on its page, such as VPC8C05s-##### with # for one digit", guide, StringComparison.Ordinal);
        Assert.Contains("A model without a format takes any serial number, and test IDs aren't checked.", guide, StringComparison.Ordinal);
        Assert.Contains("If the model has a serial number format, every serial number must fit it.", release, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TheNewBatteryForm_ShouldOfferForABatchOnlyTheSerialsThatFitItsModelsFormat()
    {
        var (rig, pool, c7) = await SeededAsync(mongo);
        using var __ = rig;
        var model = await ModelOfAsync(rig, c7);
        await rig.Store.SetModelSerialFormatAsync(model.Id, Core800s);
        await pool.ReleaseAsync(Who, List("PO any", "VPC8C05s-00001", "VPC7C05-00001"));

        var page = await rig.Services.GetRequiredService<BatteryPages>().NewAsync();

        // The page knows each model's format; the form's script hides the serials that don't fit the chosen batch's.
        Assert.Equal(Core800s, page.ModelFormats[model.Id]);
        Assert.DoesNotContain(page.ModelFormats, entry => entry.Key != model.Id);
        var view = File.ReadAllText(DppTestFiles.RepoFile("web", "Views", "Batteries", "NewBattery.cshtml"));
        var script = File.ReadAllText(DppTestFiles.RepoFile("web", "wwwroot", "js", "bp-battery-ids.js"));
        Assert.Contains("data-format=\"@Model.ModelFormats.GetValueOrDefault(choice.ModelId)\"", view, StringComparison.Ordinal);
        Assert.Contains("data-test-ids", view, StringComparison.Ordinal); // the test IDs' group is free of the format
        Assert.Contains("const fits = (format, serial) =>", script, StringComparison.Ordinal);
        Assert.Contains("group.dataset.testIds !== undefined || fits(format, option.value)", script, StringComparison.Ordinal);
        Assert.Contains("fits its model’s serial number format", script, StringComparison.Ordinal);
    }
}
