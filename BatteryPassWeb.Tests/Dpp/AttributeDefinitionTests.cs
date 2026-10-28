using BatteryPassWeb.Dpp.Catalog;

namespace BatteryPassWeb.Tests.Dpp;

public sealed class AttributeDefinitionTests
{
    [Theory]
    [InlineData("dppHeader", "uniqueProductIdentifier", "dppHeader.uniqueProductIdentifier")]
    [InlineData("performanceAndDurability", "stateOfChargeSoC", "batteryPassportData.performanceAndDurability.stateOfChargeSoC")]
    public void Path_ShouldPointIntoTheAssembledPassport(string section, string property, string expected)
    {
        Assert.Equal(expected, Definition(11, section, property, AttributeLevel.Pack).Path);
    }

    [Theory]
    [InlineData(7, AttributeLevel.Pack, StorageLocation.Operator)]
    [InlineData(14, AttributeLevel.Pack, StorageLocation.Operator)]
    [InlineData(11, AttributeLevel.Pack, StorageLocation.Pack)]
    [InlineData(1, AttributeLevel.PassportSystem, StorageLocation.System)]
    [InlineData(10, AttributeLevel.Batch, StorageLocation.Batch)]
    [InlineData(12, AttributeLevel.Site, StorageLocation.Site)]
    [InlineData(21, AttributeLevel.Model, StorageLocation.Model)]
    [InlineData(59, AttributeLevel.ModelRestatedPerPack, StorageLocation.ModelWithPackOverride)]
    public void Storage_ShouldFollowTheLevelExceptForOperatorAttributes(int number, AttributeLevel level, StorageLocation expected)
    {
        Assert.Equal(expected, Definition(number, "identifiersAndProductData", "anyProperty", level).Storage);
    }

    private static AttributeDefinition Definition(int number, string section, string property, AttributeLevel level) =>
        new(number, "Test attribute", section, property, level, Audience.Public, LegitimateInterestGroups.None,
            AttributeEditors.Team, ChangeRule.Fixed, null, Requirement.Mandatory, true);
}
