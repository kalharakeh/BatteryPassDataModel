using BatteryPassWeb.Dpp.Catalog;
using BatteryPassWeb.Dpp.Store;
using BatteryPassWeb.Dpp.Values;
using BatteryPassWeb.Tests.Dpp;
using static BatteryPassWeb.Tests.Loading.LoadTesting;

namespace BatteryPassWeb.Tests.Loading;

/// <summary>
/// Package R1, round two (review M2): before a load, the check says which published batteries it reaches will keep their
/// version because their passport would still lack a value, naming the values, instead of promising each a new version.
/// The load is still allowed.
/// </summary>
[Collection(MongoCollection.Name)]
public sealed class HeldBackCheckTests(MongoFixture mongo)
{
    /// <summary>The workbook as sent on 13 November, naming the seed's family and model, with one value changed.</summary>
    private static FilledWorkbook OldFormatChangingTheMass() => FilledWorkbook.SentOn13November()
        .Set(FamilySheet, "family|Name", "Core", "Test battery family")
        .Set(ModelSheet, "model|Name", "Core800", "EXB-NMC-100")
        .Clear(ModelSheet, "model|Name", "Core800s")
        .Clear(ModelSheet, "model|Name", "Core700")
        .Clear(ModelSheet, "model|SerialFormat", "Core800")
        .SetNumber(ModelSheet, "model|batteryMass|gramKilogramValue", "Core800", 520);

    /// <summary>The seed battery published, then its model and site as v3.8 left them: without the EU's new values.</summary>
    private static async Task<LoadRig> PublishedFromV38DataAsync(MongoFixture mongo)
    {
        var rig = Around(await PublicationTesting.PublishedRigAsync(mongo));
        var modelValues = AttributeCatalog.EuValues.Where(attribute => attribute.Storage == StorageLocation.Model).Select(attribute => attribute.Property).ToArray();
        await rig.Store.SetValuesAsync<BatteryModel>("model-test", new Dictionary<string, AttributeValue>(), modelValues);
        await rig.Store.SetValuesAsync<ManufacturingSite>("site-test", new Dictionary<string, AttributeValue>(), [AttributeCatalog.ManufacturingCity, AttributeCatalog.ManufacturingRegion, AttributeCatalog.ManufacturingCountry]);
        return rig;
    }

    [Fact]
    public async Task AnOldFormatWorkbookReachingABatteryThatLacksTheNewValues_ShouldSayItKeepsItsVersion_NamingThem()
    {
        var rig = await PublishedFromV38DataAsync(mongo);

        var check = await rig.CheckAsync(OldFormatChangingTheMass(), []);

        Assert.Empty(check.Texts());
        var text = check.Publication?.Text ?? string.Empty;
        Assert.Contains("1 published battery of EXB-NMC-100 keeps its version until these are filled in:", text, StringComparison.Ordinal);
        Assert.Contains("E10 Test of the idle temperature range", text, StringComparison.Ordinal);
        Assert.Contains("E5 Manufacturing city", text, StringComparison.Ordinal);
        Assert.DoesNotContain("It gets one new version", text, StringComparison.Ordinal);
        Assert.True(check.CanLoad); // the load is still allowed
    }

    [Fact]
    public async Task AWorkbookReachingABatteryThatHasEveryValue_ShouldStillSayOneNewVersion()
    {
        var rig = Around(await PublicationTesting.PublishedRigAsync(mongo)); // the seed has every value, the new ones too

        var check = await rig.CheckAsync(OldFormatChangingTheMass(), []);

        var text = check.Publication?.Text ?? string.Empty;
        Assert.StartsWith("1 published battery is built from records this load changes. It gets one new version", text, StringComparison.Ordinal);
        Assert.DoesNotContain("keeps its version", text, StringComparison.Ordinal);
    }
}
