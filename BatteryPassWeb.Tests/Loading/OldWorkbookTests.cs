using BatteryPassWeb.Dpp.Catalog;
using BatteryPassWeb.Dpp.Loading;
using BatteryPassWeb.Dpp.Store;
using BatteryPassWeb.Tests.Dpp;

namespace BatteryPassWeb.Tests.Loading;

/// <summary>
/// Package R1 (decision 94): the workbook gets rows for the EU's values, and the owner is filling the one sent on
/// 13 November without them. That workbook still loads as before: a row it doesn't have changes nothing.
/// </summary>
[Collection(MongoCollection.Name)]
public sealed class OldWorkbookTests(MongoFixture mongo)
{
    private static readonly string[] EuSiteKeys = ["site|manufacturingCity", "site|manufacturingRegion", "site|manufacturingCountry"];

    [Fact]
    public void TheWorkbookAsSentOn13November_ShouldStillReadWithoutProblems_AndHaveNoneOfTheNewRows()
    {
        var (book, problem) = XlsxReader.Read(FilledWorkbook.SentOn13November().Bytes());
        Assert.Null(problem);

        var workbook = LoadWorkbook.Read(book!, new LoadFields(AttributeFormsTests.Forms));

        Assert.Empty(workbook.Problems.Select(item => item.Text));
        Assert.All(EuSiteKeys, key => Assert.False(workbook.Sites.RowNames.ContainsKey(key), key));
    }

    [Fact]
    public async Task TheNewRows_ShouldFillTheNewValues_AndTheOldWorkbookShouldLeaveThemAsTheyAre()
    {
        var rig = await LoadTesting.EmptyAsync(mongo);

        var first = await rig.LoadAsync(LoadTesting.Core());
        Assert.True(first.Loaded, string.Join(" | ", first.Check.Problems.Select(item => item.Text)));
        var site = Assert.Single(await rig.Store.ListAsync<ManufacturingSite>());
        Assert.Equal(("Malmö", "Skåne", "SWE"), (Text(site, AttributeCatalog.ManufacturingCity), Text(site, AttributeCatalog.ManufacturingRegion), Text(site, AttributeCatalog.ManufacturingCountry)));

        var old = LoadTesting.Core(FilledWorkbook.SentOn13November())
            .Set(LoadTesting.SiteSheet, "site|manufacturingPlace", "Site 1", "Plant South, Sodertalje, Sweden (corrected)");
        var check = await rig.CheckAsync(old);
        Assert.Empty(check.Texts());
        var second = await rig.LoadAsync(old);
        Assert.True(second.Loaded);

        site = Assert.Single(await rig.Store.ListAsync<ManufacturingSite>());
        Assert.Equal("Plant South, Sodertalje, Sweden (corrected)", Text(site, "manufacturingPlace"));
        Assert.Equal(("Malmö", "Skåne", "SWE"), (Text(site, AttributeCatalog.ManufacturingCity), Text(site, AttributeCatalog.ManufacturingRegion), Text(site, AttributeCatalog.ManufacturingCountry)));
    }

    /// <summary>Round two, L10: every one of the EU's new values (E5 to E20) and #42, on every record, before and after.</summary>
    [Fact]
    public async Task EveryNewValueAndTheMaterials_ShouldBeLeftAsTheyAre_ByTheOldWorkbookAndByTheSameNewOneAgain()
    {
        var rig = await LoadTesting.EmptyAsync(mongo);
        Assert.True((await rig.LoadAsync(LoadTesting.Core())).Loaded);
        var filled = await NewValuesAsync(rig);

        // The new workbook filled them all: E5 to E8 and E10 to E20 on every record that holds them (E6 is optional and
        // filled; E9 is the model's answer's, here "Lead" from the example), and #42.
        var holders = AttributeCatalog.EuValues.Select(attribute => attribute.Label).ToHashSet(StringComparer.Ordinal);
        Assert.All(holders, label => Assert.Contains(filled.Keys, key => key.EndsWith($"|{label}", StringComparison.Ordinal)));
        Assert.Equal(3, filled.Keys.Count(key => key.EndsWith("|#42", StringComparison.Ordinal)));

        Assert.True((await rig.LoadAsync(LoadTesting.Core(FilledWorkbook.SentOn13November()))).Loaded);
        Assert.Equal(filled, await NewValuesAsync(rig));

        Assert.True((await rig.LoadAsync(LoadTesting.Core())).Loaded);
        Assert.Equal(filled, await NewValuesAsync(rig));
    }

    /// <summary>"record id|E5" (and "|#42") to the stored JSON, for every record that holds one of these values.</summary>
    private static async Task<Dictionary<string, string>> NewValuesAsync(LoadRig rig)
    {
        var watched = AttributeCatalog.EuValues.Append(AttributeCatalog.ByNumber(42)).ToArray();
        var records = (await rig.Store.ListAsync<ManufacturingSite>()).Select(record => (record.Id, record.Values))
            .Concat((await rig.Store.ListAsync<ProductionBatch>()).Select(record => (record.Id, record.Values)))
            .Concat((await rig.Store.ListAsync<BatteryModel>()).Select(record => (record.Id, record.Values)));
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (id, stored) in records)
        {
            foreach (var attribute in watched.Where(attribute => stored.ContainsKey(attribute.Property)))
            {
                values[$"{id}|{attribute.Label}"] = stored[attribute.Property].Value.ToJsonString();
            }
        }

        return values;
    }

    private static string? Text(ManufacturingSite site, string property) =>
        site.Values.TryGetValue(property, out var value) ? value.Value.GetValue<string>() : null;
}
