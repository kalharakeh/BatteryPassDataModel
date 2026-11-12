using System.Text.Json.Nodes;
using BatteryPassWeb.Dpp.Editing;
using BatteryPassWeb.Dpp.Pages;
using BatteryPassWeb.Dpp.Store;

namespace BatteryPassWeb.Tests.Dpp;

/// <summary>
/// Step 5's review (4 Nov 2026). An API token's ID ("api-token:…") is the app's own key, never words for a reader: the live
/// values section says "from the battery system" without it, and a battery's earlier uploads name a team token's upload
/// "API token". A long e-mail or file name in "Upload a file" wraps instead of being cut at 375 px.
/// </summary>
[Collection(MongoCollection.Name)]
public sealed class Step5ReviewTests(MongoFixture mongo)
{
    [Theory]
    [InlineData("api-token:6aeaebcdc82c4212842972d9", "")]
    [InlineData("demo.north.admin@example.test", " by demo.north.admin@example.test")]
    [InlineData(null, "")]
    [InlineData("", "")]
    public void WhoSavedAValue_ShouldNeverShowATokensId(string? who, string words)
    {
        Assert.Equal(words, BatteryCards.By(who));
        var card = File.ReadAllText(DppTestFiles.RepoFile("web", "Views", "Batteries", "_LiveValuesCard.cshtml"));
        Assert.Contains("@BatteryCards.By(stored.Who)", card, StringComparison.Ordinal);
        Assert.DoesNotContain("$\" by {stored.Who}\"", card, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ATeamTokensUpload_ShouldBeNamedAnApiTokenInEarlierUploads()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo).WithClustersAsync();
        await UploadServiceTests.Service(rig).RunAsync(ChangeSender.Team("api-token:team-1"), JsonNode.Parse(UploadServiceTests.File(
            $$"""{ "batterySerialNumber": "{{UploadServiceTests.Serial(rig)}}", "values": { "stateOfChargeSoC": 40 } }""")), "api", null, preview: false);
        var pack = (await rig.Store.GetAsync<BatteryPack>(rig.PackId))!;

        var history = (await UploadCheckTests.Pages(rig).CardAsync(pack, "/admin/batteries/x", forTeam: true, null, null)).History;

        Assert.Equal("API token · upload file", Assert.Single(history).WhoAndWhat);
    }

    [Fact]
    public void ALongSenderOrFileName_ShouldWrapInsteadOfBeingCut()
    {
        // At 375 px the row's "demo.north.admin@example.test" (one word) ran past the card, which cuts it (step 5's review).
        var css = File.ReadAllText(DppTestFiles.RepoFile("web", "wwwroot", "css", "bp-product-data.css")).Replace("\r\n", "\n");

        Assert.Contains(".bp-upload-head > span,\n[data-upload-row] > summary > span {\n  min-width: 0;\n  overflow-wrap: anywhere;\n}", css, StringComparison.Ordinal);
    }
}
