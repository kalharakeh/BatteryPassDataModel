namespace BatteryPassWeb.Tests.Dpp;

/// <summary>
/// What the team pages say about themselves, after the product owner's first look (29 Oct 2026): a family is named in a
/// dialog, a batch says what it is, and every editor says whose values it holds.
/// </summary>
public sealed class TeamPageWordingTests
{
    private static string View(string name) => File.ReadAllText(DppTestFiles.RepoFile("web", "Views", "ProductData", name));

    [Fact]
    public void AddingAFamily_ShouldAskForItsNameInADialog()
    {
        var page = View("Families.cshtml");
        var dialog = page[page.IndexOf("<dialog", StringComparison.Ordinal)..page.IndexOf("</dialog>", StringComparison.Ordinal)];

        Assert.Contains("data-open-dialog=\"add-family-dialog\"", page, StringComparison.Ordinal);
        Assert.Contains("id=\"add-family-dialog\"", dialog, StringComparison.Ordinal);
        Assert.Contains("name=\"name\"", dialog, StringComparison.Ordinal);
        Assert.Single(page.Split("name=\"name\"")[1..]); // the name is asked only in the dialog
        Assert.Contains("formmethod=\"dialog\"", dialog, StringComparison.Ordinal); // Cancel closes it without sending
    }

    [Fact]
    public void Batches_ShouldSayWhatABatchIs()
    {
        var page = View("Family.cshtml");

        Assert.Contains("Production batches of model @selected.Name", page, StringComparison.Ordinal);
        Assert.Contains("A batch is every battery of this model made in one calendar year at one manufacturing site.", page, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("Every battery of this model shares these values.")]
    [InlineData("Every battery made at this site shares these values.")]
    [InlineData("Every battery this operator places on the market shares these values.")]
    public void EachEditor_ShouldSayWhoseValuesItHolds(string sentence)
    {
        Assert.Contains(sentence, View("_RecordEditor.cshtml"), StringComparison.Ordinal);
    }

    [Fact]
    public void TheBatchPage_ShouldLinkTheModelAndSiteValuesWithWhatTheyMiss()
    {
        var page = View("Batch.cshtml");

        Assert.Contains("bp-value-sources", page, StringComparison.Ordinal);
        Assert.Contains("ProductDataPages.FamilyHref(Model.Family.Id, Model.Model.Id)", page, StringComparison.Ordinal);
        Assert.Contains("ProductDataPages.SitesHref(\"site\", Model.Site.Id)", page, StringComparison.Ordinal);
        Assert.Contains("RecordEditor.CountMissing(TargetKind.Site", page, StringComparison.Ordinal);
    }

    [Fact]
    public void ChoosingASiteForABatch_ShouldShowWhatTheSiteMisses()
    {
        Assert.Contains("RecordEditor.CountMissing(TargetKind.Site, site.Values)", View("Family.cshtml"), StringComparison.Ordinal);
    }
}
