using BatteryPassWeb.Dpp.Catalog;
using BatteryPassWeb.Dpp.Schemas;

namespace BatteryPassWeb.Tests.Dpp;

/// <summary>Checks the catalogue against the official schemas (design Appendix B).</summary>
public sealed class SchemaCoverageTests
{
    private static readonly OfficialSchemas Schemas = OfficialSchemas.Load();

    private static readonly string[] Sections =
    [
        AttributeDefinition.HeaderSection,
        "batteryCarbonFootprint",
        "batteryMaterialsAndComposition",
        "circularityAndResourceEfficiency",
        "identifiersAndProductData",
        "performanceAndDurability",
        "supplyChainDueDiligence",
        "symbolsLabelsAndDocumentationOfConformity",
    ];

    // Design Appendix B.4: optional in the schema and not in the file, so the app leaves them out.
    private static readonly string[] LeftOut =
    [
        "dppHeader.contentSpecificationIds",
        "dppHeader.facilityId",
        "batteryCarbonFootprint.absoluteBatteryCarbonFootprint",
        "identifiersAndProductData.dateOfPuttingTheBatteryIntoService",
        "performanceAndDurability.remainingCapacity",
        "performanceAndDurability.remainingPowerCapability",
        "performanceAndDurability.numberOfDeepDischargeEvents",
        "performanceAndDurability.numberOfOverchargeEvents",
        "supplyChainDueDiligence.thirdPartyAssurancesOfRecognisedSchemes",
        "supplyChainDueDiligence.supplyChainIndices",
    ];

    public static TheoryData<string> SectionNames() => new(Sections);

    [Fact]
    public void EveryCatalogueAttribute_ShouldExistInTheFullSchema()
    {
        Assert.All(AttributeCatalog.FileAttributes, a => Assert.NotNull(Schemas.FindProperty(a.Section, a.Property)));
    }

    [Fact]
    public void SchemaProperties_ShouldBeTheCatalogueOrTheTenLeftOut()
    {
        var inSchema = Sections.SelectMany(section =>
            Schemas.FullDocument["$defs"]![section]!["properties"]!.AsObject().Select(p => $"{section}.{p.Key}"));
        var known = AttributeCatalog.FileAttributes.Select(a => $"{a.Section}.{a.Property}").Concat(LeftOut);

        Assert.Equal(known.Order(StringComparer.Ordinal), inSchema.Order(StringComparer.Ordinal));
    }

    [Fact]
    public void LeftOutProperties_ShouldBeOptionalInTheSchema()
    {
        foreach (var path in LeftOut)
        {
            var (section, property) = (path[..path.IndexOf('.')], path[(path.IndexOf('.') + 1)..]);
            Assert.DoesNotContain(property, OfficialSchemas.RequiredProperties(Schemas.FullDocument, section));
        }
    }

    [Theory]
    [MemberData(nameof(SectionNames))]
    public void FullSchemaRequired_ShouldBeTheMandatoryAttributes(string section)
    {
        var mandatory = AttributeCatalog.FileAttributes
            .Where(a => a.Section == section && a.Requirement != Requirement.Conditional)
            .Select(a => a.Property);

        Assert.Equal(
            mandatory.Order(StringComparer.Ordinal),
            OfficialSchemas.RequiredProperties(Schemas.FullDocument, section).Order(StringComparer.Ordinal));
    }

    [Theory]
    [MemberData(nameof(SectionNames))]
    public void Feb2027SchemaRequired_ShouldBeTheMandatoryAttributesDueByFeb2027(string section)
    {
        var dueByFeb2027 = AttributeCatalog.FileAttributes
            .Where(a => a.Section == section && a.Requirement != Requirement.Conditional && a.MandatoryFromFeb2027)
            .Select(a => a.Property);

        Assert.Equal(
            dueByFeb2027.Order(StringComparer.Ordinal),
            OfficialSchemas.RequiredProperties(Schemas.Feb2027Document, section).Order(StringComparer.Ordinal));
    }

    [Fact]
    public void Feb2027Schema_ShouldRequireTheSectionsThatHaveFeb2027Attributes()
    {
        var expected = AttributeCatalog.FileAttributes
            .Where(a => a.Section != AttributeDefinition.HeaderSection && a.MandatoryFromFeb2027)
            .Select(a => a.Section)
            .Distinct();
        var required = OfficialSchemas.RequiredProperties(Schemas.Feb2027Document, "batteryPassportMaster");

        Assert.Equal(expected.Order(StringComparer.Ordinal), required.Order(StringComparer.Ordinal));
    }
}
