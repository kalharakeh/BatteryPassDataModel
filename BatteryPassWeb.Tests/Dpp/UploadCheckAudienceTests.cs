using System.Text.Json.Nodes;
using BatteryPassWeb.Dpp.Editing;
using BatteryPassWeb.Dpp.Pages;
using BatteryPassWeb.Dpp.Schemas;
using BatteryPassWeb.Dpp.Store;
using BatteryPassWeb.Dpp.Values;

namespace BatteryPassWeb.Tests.Dpp;

/// <summary>
/// S05: "Check the file" shows a battery's current value ("Now") only for values the sender may see. A refused value
/// that is the authorities' (#13) shows the refusal without the value.
/// </summary>
[Collection(MongoCollection.Name)]
public sealed class UploadCheckAudienceTests(MongoFixture mongo)
{
    private const string Provider = "Demo Passport Hosting AB";

    private static readonly ChangeSender NorthAdmin = ChangeSender.ClusterAdmin("north-admin@example.com", BatteryTesting.North);

    private static UploadPages Pages(PublicationRig rig) =>
        new(UploadServiceTests.Service(rig), AttributeFormsTests.Forms, new AttributeValueNormalizer(OfficialSchemas.Load()));

    private static async Task<PublicationRig> RigWithProviderAsync(MongoFixture mongo)
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo).WithClustersAsync();
        await rig.Store.SetValuesAsync<BatteryPack>(
            rig.PackId,
            new Dictionary<string, AttributeValue> { ["thirdPartyServiceProvider"] = new() { Value = JsonValue.Create(Provider)!, GeneratedAtUtc = rig.Clock.Now.UtcDateTime, Source = "form", Who = "team" } });
        return rig;
    }

    [Theory]
    [InlineData(false, "")]
    [InlineData(true, Provider)]
    public async Task TheCheck_ShouldShowTheCurrentAuthoritiesValueOnlyToTheTeam(bool forTeam, string now)
    {
        var rig = await RigWithProviderAsync(mongo);
        var sender = forTeam ? PublicationTesting.Team : NorthAdmin;
        var file = UploadServiceTests.File(UploadServiceTests.Entry(UploadServiceTests.Serial(rig), """{ "thirdPartyServiceProvider": "x" }"""));
        var check = await UploadServiceTests.Service(rig).CheckBatteryFileAsync(sender, rig.PackId, "a.json", System.Text.Encoding.UTF8.GetBytes(file));
        var pack = (await rig.Store.GetAsync<BatteryPack>(rig.PackId))!;

        var card = await Pages(rig).CardAsync(pack, "/x", forTeam, check.CheckId, null, viewer: sender.Who);

        var row = Assert.Single(card.Check!.Values);
        Assert.Equal(now, row.Now);
        if (!forTeam)
        {
            Assert.Equal("Not saved: #13 can't be changed by this sender", row.Result);
            Assert.DoesNotContain(Provider, row.Result + row.InFile + row.Label, StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task TheCheck_ShouldStillShowTheCurrentValueOfAValueTheSenderMaySee()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo).WithClustersAsync();
        var file = UploadServiceTests.File(UploadServiceTests.Entry(UploadServiceTests.Serial(rig), """{ "stateOfChargeSoC": 41 }"""));
        var check = await UploadServiceTests.Service(rig).CheckBatteryFileAsync(NorthAdmin, rig.PackId, "a.json", System.Text.Encoding.UTF8.GetBytes(file));
        var pack = (await rig.Store.GetAsync<BatteryPack>(rig.PackId))!;

        var card = await Pages(rig).CardAsync(pack, "/x", forTeam: false, check.CheckId, null, viewer: NorthAdmin.Who);

        Assert.NotEqual(string.Empty, Assert.Single(card.Check!.Values).Now);
    }
}
