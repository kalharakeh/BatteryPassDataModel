using BatteryPassWeb.Dpp.Store;

namespace BatteryPassWeb.Tests.Dpp;

/// <summary>
/// Redesign R5, item 6: the accident reports section is the place to see and add them: the list and the upload first,
/// the readers' accident page as a small "How readers see it" link last; a frozen passport says why adding is off and
/// points to the new passport.
/// </summary>
[Collection(MongoCollection.Name)]
public sealed class AccidentSectionTests(MongoFixture mongo)
{
    private static string Card() => File.ReadAllText(DppTestFiles.RepoFile("web", "Views", "Batteries", "_AccidentsCard.cshtml")).Replace("\r\n", "\n", StringComparison.Ordinal);

    [Fact]
    public async Task AnInactivePassport_ShouldPointToTheNewPassport()
    {
        var rig = await new SuccessionTests(mongo).InactiveAsync();
        var successorId = (await rig.Store.GetAsync<BatteryPack>(rig.PackId))!.Succession!.SuccessorPackId!;

        var old = (await BatteryTesting.Pages(rig).BatteryAsync(rig.PackId))!.Cards;
        var current = (await BatteryTesting.Pages(rig).BatteryAsync(successorId))!.Cards;

        Assert.True(old.Frozen);
        Assert.Equal(BatteryPassWeb.Dpp.Pages.BatteryPages.BatteryHref(successorId), old.NewPassportHref);
        Assert.False(current.Frozen);
        Assert.Null(current.NewPassportHref);
    }

    [Fact]
    public void TheSection_ShouldPutTheReportsAndTheUploadFirst_AndTheReadersPageLast()
    {
        var card = Card();
        var list = card.IndexOf("<ul class=\"bp-file-checks\">", StringComparison.Ordinal);
        var upload = card.IndexOf("<h3 class=\"bp-subheading\">Add a report</h3>", StringComparison.Ordinal);
        var readers = card.IndexOf("How readers see it</a>", StringComparison.Ordinal);

        Assert.Contains("See and add this battery's accident reports here.", card, StringComparison.Ordinal);
        Assert.True(list > 0 && upload > list && readers > upload);
        Assert.Contains("<p class=\"bp-accident-readers\"><a href=\"@ReaderPages.AccidentsHref(Model.Pack.Id)\">", card, StringComparison.Ordinal);
    }

    [Fact]
    public void AFrozenPassport_ShouldSayWhyAddingIsOff_AndLinkTheNewPassport()
    {
        var card = Card();

        Assert.Contains("<strong>Adding reports is off on this passport.</strong> @frozen", card, StringComparison.Ordinal);
        Assert.Contains("@if (Model.NewPassportHref is { } next)", card, StringComparison.Ordinal);
        Assert.Contains("<a href=\"@next\">Add them on the new passport</a>", card, StringComparison.Ordinal);
    }
}
