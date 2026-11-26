using System.Text.Json.Nodes;
using BatteryPassWeb.Dpp.Catalog;
using BatteryPassWeb.Dpp.Editing;
using BatteryPassWeb.Dpp.Pages;
using BatteryPassWeb.Dpp.Publication;
using BatteryPassWeb.Dpp.Store;

namespace BatteryPassWeb.Tests.Dpp;

/// <summary>
/// Redesign R6 (the review of the built R5): a code from the schema's lists shows in words wherever a page shows it, as
/// the badges say it: the battery status (#22, "Re-used", not "re-used"), the category (#20) and, as since R5, the
/// passport status (#2); a sentence naming a passport status says "marked for deletion", not "marked-for-deletion".
/// </summary>
public sealed class StoredCodesInWordsTests
{
    private static string Shown(int number, JsonNode value) =>
        Assert.Single(ValueWords.Lines(AttributeFormsTests.Forms.For(AttributeCatalog.ByNumber(number)), value)).Text;

    [Theory]
    [InlineData("original", "Original")]
    [InlineData("re-used", "Re-used")]
    [InlineData("waste", "Waste")]
    public void TheBatteryStatus_ShouldShowInTheBadgesWords(string code, string words) =>
        Assert.Equal(words, Shown(22, new JsonObject { ["batteryStatusValues"] = code })); // the schema's shape

    [Fact]
    public void TheCategory_ShouldReadAsWords() =>
        Assert.Equal("Industrial, non-stationary battery", Shown(20, new JsonObject { ["batteryCategoryValue"] = "industrial/non-stationary battery" }));

    [Fact]
    public void ThePassportStatus_ShouldStillShowInTheBadgesWords() =>
        Assert.Equal("Marked for deletion", Shown(2, new JsonObject { ["dppStatusValue"] = DppStatuses.MarkedForDeletion }));

    [Fact]
    public void ASentenceNamingAPassportStatus_ShouldSayItInWords()
    {
        Assert.Equal("the passport is marked for deletion; nothing on it changes", SavePipeline.NoLongerChanges(DppStatuses.MarkedForDeletion));
        Assert.Equal("the passport is marked for deletion, so it gets no new version", PublicationService.NoNewVersion(DppStatuses.MarkedForDeletion));
        Assert.Contains("The passport is {DppStatuses.Words(passportStatus!).ToLowerInvariant()}: nothing on it changes any more.",
            File.ReadAllText(DppTestFiles.RepoFile("web", "Dpp", "Pages", "StatusPages.cs")), StringComparison.Ordinal);
    }
}
