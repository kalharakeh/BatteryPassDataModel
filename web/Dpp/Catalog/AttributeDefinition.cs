namespace BatteryPassWeb.Dpp.Catalog;

/// <summary>Where an attribute belongs (design section 4).</summary>
public enum AttributeLevel
{
    PassportSystem,
    Pack,
    Batch,
    Site,
    Model,
    ModelRestatedPerPack,
}

/// <summary>Who may see an attribute, from the file's access column (design section 5.1).</summary>
public enum Audience
{
    Public,
    Purchaser,
    Shared,
    Authorities,
}

/// <summary>The legitimate-interest roles that see a shared attribute (decision 6).</summary>
[Flags]
public enum LegitimateInterestGroups
{
    None = 0,
    Repair = 1,
    Recycling = 2,
    Treatment = 4,
}

/// <summary>Who may change an attribute (design section 6.1).</summary>
[Flags]
public enum AttributeEditors
{
    None = 0,
    System = 1,
    Team = 2,
    BatteryClusterAdmins = 4,
    BatterySystemFeed = 8,
    MarketSurveillance = 16,
}

/// <summary>When an attribute's value changes (design Appendix A, column "Changes").</summary>
public enum ChangeRule
{
    Fixed,
    Lifecycle,
    OnPublishedOrLiveUpdate,
    NewPerVersion,
    OnStatusChange,
    RestatedOnStatusChange,
    Live,
    IfProviderChanges,
}

/// <summary>The file's requirement for Other industrial batteries: M (Battery Regulation), (M) (ESPR and JTC-24) or C (conditional).</summary>
public enum Requirement
{
    Mandatory,
    MandatoryEspr,
    Conditional,
}

/// <summary>Which stored record holds an attribute's value.</summary>
public enum StorageLocation
{
    System,
    Pack,
    Operator,
    Batch,
    Site,
    Model,
    ModelWithPackOverride,
}

/// <summary>One row of the attribute catalogue (design Appendix A).</summary>
/// <param name="Section">"dppHeader", or a section of batteryPassportData in the v2.0.1 schema.</param>
/// <param name="Property">The v2.0.1 property name. Property names are unique across sections.</param>
/// <param name="Unit">The file's unit for this battery category, or null.</param>
public sealed record AttributeDefinition(
    int Number,
    string Name,
    string Section,
    string Property,
    AttributeLevel Level,
    Audience Audience,
    LegitimateInterestGroups SharedWith,
    AttributeEditors Editors,
    ChangeRule Change,
    string? Unit,
    Requirement Requirement,
    bool MandatoryFromFeb2027)
{
    public const string HeaderSection = "dppHeader";
    public const string DataRoot = "batteryPassportData";

    /// <summary>Dotted path in the assembled passport, e.g. batteryPassportData.performanceAndDurability.capacityFade.</summary>
    public string Path => Section == HeaderSection ? $"{HeaderSection}.{Property}" : $"{DataRoot}.{Section}.{Property}";

    /// <summary>The record that stores the value. #7 and #14 are per pack in the file, but entered once per economic operator (design 4.1).</summary>
    public StorageLocation Storage => Number is 7 or 14
        ? StorageLocation.Operator
        : Level switch
        {
            AttributeLevel.PassportSystem => StorageLocation.System,
            AttributeLevel.Pack => StorageLocation.Pack,
            AttributeLevel.Batch => StorageLocation.Batch,
            AttributeLevel.Site => StorageLocation.Site,
            AttributeLevel.Model => StorageLocation.Model,
            AttributeLevel.ModelRestatedPerPack => StorageLocation.ModelWithPackOverride,
            _ => throw new InvalidOperationException($"Unknown level {Level}."),
        };
}
