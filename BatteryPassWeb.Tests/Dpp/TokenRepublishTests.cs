using System.Text.Json.Nodes;
using BatteryPassWeb.Dpp.Editing;
using BatteryPassWeb.Dpp.Access;
using BatteryPassWeb.Services;

namespace BatteryPassWeb.Tests.Dpp;

/// <summary>
/// Finding W11, decided from the code: a published battery gets a new version whenever a value that belongs in the passport
/// is saved, by whoever saves it (the team's form, an upload, a token for all batteries); "sign" gates only the explicit
/// publish call. So the token kinds are described as they behave: Read + write also sends other values for all batteries,
/// and a published battery then gets a new version by itself. The publish call itself still needs "sign".
/// </summary>
[Collection(MongoCollection.Name)]
public sealed class TokenRepublishTests(MongoFixture mongo)
{
    [Fact]
    public async Task AGlobalReadWriteToken_ShouldMakeANewVersion_WhenItSavesAStaticValueOfAPublishedBattery()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo);
        var sender = DppReaders.SenderFor(new ExternalApiTokenContext { TokenId = "t-rw", GlobalAccess = true, AccessMode = ExternalTokenAccessMode.ReadWrite });
        Assert.Equal(1, await rig.Store.GetLatestVersionNumberAsync(rig.PackId));

        var result = await rig.Pipeline.SaveAsync(sender,
            new ChangeSet(TargetKind.Pack, rig.PackId, new Dictionary<string, JsonNode?> { ["thirdPartyServiceProvider"] = "Other Hosting AB" }, "upload"));

        Assert.True(result.Saved);
        Assert.Equal(2, await rig.Store.GetLatestVersionNumberAsync(rig.PackId));
    }

    [Fact]
    public void TheTokenWindow_ShouldSayThatAPublishedBatteryGetsANewVersionByItself()
    {
        var window = File.ReadAllText(DppTestFiles.RepoFile("web", "Views", "Admin", "Clusters.cshtml"));

        Assert.Contains("a published battery then gets a new version by itself, as when the team saves", window, StringComparison.Ordinal);
        Assert.Contains("<strong>Passport lifecycle</strong><small>Reads, and for all batteries publishes passports.</small>", window, StringComparison.Ordinal);
    }

    [Fact]
    public void TheExplicitPublishCall_ShouldStillNeedSignAccess()
    {
        var controller = File.ReadAllText(DppTestFiles.RepoFile("web", "Controllers", "DppApiController.cs"));

        Assert.Contains("TeamTokenAsync(ExternalTokenRequirement.Sign, TeamOnlyPublish", controller, StringComparison.Ordinal);
    }
}
