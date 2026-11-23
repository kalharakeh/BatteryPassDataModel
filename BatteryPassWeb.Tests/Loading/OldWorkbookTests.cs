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

    private static string? Text(ManufacturingSite site, string property) =>
        site.Values.TryGetValue(property, out var value) ? value.Value.GetValue<string>() : null;
}
