using BatteryPassWeb.Dpp.Catalog;

namespace BatteryPassWeb.Tests.Dpp;

/// <summary>Checks the catalogue against the design's own counts and rules (design sections 4–6).</summary>
public sealed class AttributeCatalogTests
{
    [Fact]
    public void Catalogue_ShouldHave73UniqueAttributes()
    {
        Assert.Equal(73, AttributeCatalog.All.Count);
        Assert.Equal(73, AttributeCatalog.All.Select(a => a.Number).Distinct().Count());
        Assert.Equal(73, AttributeCatalog.All.Select(a => a.Property).Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void Audiences_ShouldMatchTheDesign()
    {
        var counts = AttributeCatalog.All.GroupBy(a => a.Audience).ToDictionary(g => g.Key, g => g.Count());

        Assert.Equal(54, counts[Audience.Public]);
        Assert.Equal(10, counts[Audience.Purchaser]);
        Assert.Equal(5, counts[Audience.Shared]);
        Assert.Equal(4, counts[Audience.Authorities]);
    }

    [Fact]
    public void Levels_ShouldMatchTheDesign()
    {
        var counts = AttributeCatalog.All.GroupBy(a => a.Level).ToDictionary(g => g.Key, g => g.Count());

        Assert.Equal(5, counts[AttributeLevel.PassportSystem]);
        Assert.Equal(12, counts[AttributeLevel.Pack]);
        Assert.Equal(16, counts[AttributeLevel.Batch]);
        Assert.Equal(3, counts[AttributeLevel.Site]);
        Assert.Equal(32, counts[AttributeLevel.Model]);
        Assert.Equal(5, counts[AttributeLevel.ModelRestatedPerPack]);
    }

    [Theory]
    [InlineData(43, LegitimateInterestGroups.Repair | LegitimateInterestGroups.Recycling)]
    [InlineData(45, LegitimateInterestGroups.Repair | LegitimateInterestGroups.Recycling)]
    [InlineData(46, LegitimateInterestGroups.Repair)]
    [InlineData(47, LegitimateInterestGroups.Repair)]
    [InlineData(48, LegitimateInterestGroups.Repair | LegitimateInterestGroups.Recycling | LegitimateInterestGroups.Treatment)]
    public void SharedAttributes_ShouldNameTheirLegitimateInterestRoles(int number, LegitimateInterestGroups expected)
    {
        Assert.Equal(expected, AttributeCatalog.ByNumber(number).SharedWith);
    }

    [Fact]
    public void OnlySharedAttributes_ShouldNameLegitimateInterestRoles()
    {
        Assert.All(AttributeCatalog.All.Where(a => a.Audience != Audience.Shared), a => Assert.Equal(LegitimateInterestGroups.None, a.SharedWith));
    }

    [Fact]
    public void ClusterAdmins_ShouldEditExactlyThePurchaserData()
    {
        Assert.All(AttributeCatalog.All, a => Assert.Equal(
            a.Audience == Audience.Purchaser,
            a.Editors.HasFlag(AttributeEditors.BatteryClusterAdmins)));
    }

    [Fact]
    public void SharedAttributes_ShouldBeEditedByTheTeamOnly()
    {
        Assert.All(AttributeCatalog.All.Where(a => a.Audience == Audience.Shared), a => Assert.Equal(AttributeEditors.Team, a.Editors));
    }

    [Fact]
    public void TheBatterySystemFeed_ShouldWriteOnlyStateOfChargeCyclesAndTemperature()
    {
        var fed = AttributeCatalog.All.Where(a => a.Editors.HasFlag(AttributeEditors.BatterySystemFeed)).Select(a => a.Number);

        Assert.Equal([63, 83, 87], fed);
    }

    [Fact]
    public void TheLiveSet_ShouldBeStateOfChargeCyclesTemperatureAndAccidents()
    {
        var live = AttributeCatalog.All.Where(a => a.Change == ChangeRule.Live).Select(a => a.Number);

        Assert.Equal([63, 83, 87, 92], live);
    }

    [Theory]
    [InlineData(7, StorageLocation.Operator)]
    [InlineData(14, StorageLocation.Operator)]
    [InlineData(11, StorageLocation.Pack)]
    [InlineData(59, StorageLocation.ModelWithPackOverride)]
    [InlineData(10, StorageLocation.Batch)]
    [InlineData(1, StorageLocation.System)]
    public void Storage_ShouldFollowTheDataModel(int number, StorageLocation expected)
    {
        Assert.Equal(expected, AttributeCatalog.ByNumber(number).Storage);
    }

    [Fact]
    public void Lookups_ShouldFindByNumberAndByProperty()
    {
        Assert.Equal("stateOfChargeSoC", AttributeCatalog.ByNumber(63).Property);
        Assert.Equal(63, AttributeCatalog.FindByProperty("stateOfChargeSoC")?.Number);
        Assert.Null(AttributeCatalog.FindByProperty("remainingCapacity"));
        Assert.Throws<KeyNotFoundException>(() => AttributeCatalog.ByNumber(8));
    }
}
