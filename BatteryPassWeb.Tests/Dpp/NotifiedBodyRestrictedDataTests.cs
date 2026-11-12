using System.Text.Json.Nodes;
using BatteryPassWeb.Dpp.Access;
using BatteryPassWeb.Dpp.Assembly;
using BatteryPassWeb.Dpp.Catalog;
using BatteryPassWeb.Dpp.Pages;
using BatteryPassWeb.Dpp.Publication;
using BatteryPassWeb.Dpp.Signing;
using BatteryPassWeb.Dpp.Store;
using BatteryPassWeb.Tests.Demo;
using Microsoft.Extensions.DependencyInjection;

namespace BatteryPassWeb.Tests.Dpp;

/// <summary>
/// Finding R05 (the owner's decision, 11 Nov 2026: "Yes, as the article"): notified bodies see a model's restricted data,
/// #43 and #45 to #48, through the one who-sees-what rule; nothing changes for any other reader or for the public data.
/// </summary>
[Collection(MongoCollection.Name)]
public sealed class NotifiedBodyRestrictedDataTests(MongoFixture mongo)
{
    private static readonly int[] Restricted = [43, 45, 46, 47, 48];

    [Fact]
    public async Task TheDetailPage_ShouldShowANotifiedBodyTheModelsRestrictedData_OnADemoBattery()
    {
        using var rig = await DemoRig.StartAsync(mongo);
        await rig.Demo.SeedAsync(DemoRig.Who);
        var pages = rig.Services.GetRequiredService<PassportPages>();
        var reader = rig.Services.GetRequiredService<PassportReader>();
        var notifiedBody = Reader.With("demo.notified-body@example.test", ReaderRole.NotifiedBody);

        foreach (var version in await rig.Store.ListAllActiveVersionsAsync())
        {
            var passport = (await reader.CurrentAsync(version.PackId))!.Passport;
            var page = (await pages.DetailAsync(notifiedBody, version.DigitalProductPassportId))!;
            var shared = Assert.Single(page.Audiences, section => section.Key == "shared");
            Assert.Equal(
                Restricted.Where(number => PublicPassportDataTests.Find(passport, AttributeCatalog.ByNumber(number)) is not null),
                shared.Values.Select(value => value.Attribute.Number).Order());
            Assert.Contains("notified bodies", shared.Note, StringComparison.Ordinal);
            Assert.DoesNotContain(page.Audiences, section => section.Key == "purchaser"); // still not the purchaser's data
        }
    }

    [Fact]
    public async Task ThePublicData_ShouldStayThePublicPartOnly_WithItsProof_ForEveryPublishedDemoBattery()
    {
        using var rig = await DemoRig.StartAsync(mongo);
        await rig.Demo.SeedAsync(DemoRig.Who);
        var pages = rig.Services.GetRequiredService<PassportPages>();
        var reader = rig.Services.GetRequiredService<PassportReader>();
        var versions = await rig.Store.ListAllActiveVersionsAsync();
        Assert.NotEmpty(versions);

        foreach (var version in versions)
        {
            var data = (await pages.SummaryAsync(Reader.Anonymous, version.DigitalProductPassportId))!.PublicData!;

            // The bytes signed at publication are the bytes served: the public proof still holds for them.
            Assert.NotNull(data[PublicPassport.ProofProperty]);
            var values = data.DeepClone().AsObject();
            values.Remove(PublicPassport.ProofProperty);
            var passport = (await reader.CurrentAsync(version.PackId))!.Passport;
            Assert.Equal(CanonicalJson.Serialize(OnlyPublic(passport)), CanonicalJson.Serialize(values));
            foreach (var number in Restricted)
            {
                Assert.Null(PublicPassportDataTests.Find(data, AttributeCatalog.ByNumber(number)));
            }
        }
    }

    [Fact]
    public void TheEditorsWords_ShouldNameNotifiedBodies_ForTheRestrictedData()
    {
        foreach (var number in Restricted)
        {
            var attribute = AttributeCatalog.ByNumber(number);
            var field = new EditorField(AttributeFormsTests.Forms.For(attribute), new Dictionary<string, string>(), 1, null, null, false);

            Assert.Contains("the battery's cluster, notified bodies, market surveillance, the Commission and your team", field.SeenBy, StringComparison.Ordinal);
        }
    }

    /// <summary>
    /// The public part worked out without <see cref="AudienceRules"/>: every value whose audience isn't public removed by its
    /// path, as the rule did before this change for an anonymous reader. Sections left empty are dropped.
    /// </summary>
    private static JsonObject OnlyPublic(JsonObject passport)
    {
        var copy = passport.DeepClone().AsObject();
        foreach (var attribute in AttributeCatalog.All.Where(a => a.Audience != Audience.Public))
        {
            var parts = attribute.Path.Split('.');
            var parent = copy as JsonNode;
            foreach (var part in parts[..^1])
            {
                parent = parent?[part];
            }

            (parent as JsonObject)?.Remove(parts[^1]);
        }

        return copy;
    }
}
