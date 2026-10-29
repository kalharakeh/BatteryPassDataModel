using System.Text.Json.Nodes;
using BatteryPassWeb.Dpp.Assembly;
using BatteryPassWeb.Dpp.Catalog;
using BatteryPassWeb.Dpp.Schemas;
using BatteryPassWeb.Dpp.Store;
using BatteryPassWeb.Dpp.Values;

namespace BatteryPassWeb.Tests.Dpp;

public sealed class PassportAssemblerTests
{
    private static readonly OfficialSchemas Schemas = OfficialSchemas.Load();

    [Fact]
    public void Seed_ShouldHoldEveryStoredAttributeOnTheRightRecord()
    {
        var sources = DppTestSeed.Load().Sources;

        foreach (var attribute in AttributeCatalog.FileAttributes.Where(a => a.Storage != StorageLocation.System))
        {
            var record = attribute.Storage switch
            {
                StorageLocation.Pack => sources.Pack.Values,
                StorageLocation.Operator => sources.Operator.Values,
                StorageLocation.Batch => sources.Batch.Values,
                StorageLocation.Site => sources.Site.Values,
                _ => sources.Model.Values,
            };
            Assert.True(record.ContainsKey(attribute.Property), $"#{attribute.Number} {attribute.Property} is missing from the seed");
        }
    }

    [Fact]
    public void EverySeedValue_ShouldPassTheValueChecks()
    {
        var validator = new AttributeValueValidator(Schemas);
        var sources = DppTestSeed.Load().Sources;
        var stored = new[] { sources.Pack.Values, sources.Operator.Values, sources.Batch.Values, sources.Site.Values, sources.Model.Values }
            .SelectMany(values => values);

        foreach (var (property, value) in stored)
        {
            var attribute = AttributeCatalog.FindByProperty(property);
            Assert.True(attribute is not null, $"{property} is not in the catalogue");
            Assert.Empty(validator.Validate(attribute, value.Value));
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SeedPassport_ShouldPassTheOfficialSchema(bool feb2027Variant)
    {
        var seed = DppTestSeed.Load();

        var passport = PassportAssembler.Assemble(seed.Sources, seed.Header);

        Assert.Empty(Schemas.CheckPassport(passport, feb2027Variant));
    }

    [Fact]
    public void Assemble_ShouldWriteTheSystemHeaderValues()
    {
        var seed = DppTestSeed.Load();

        var header = PassportAssembler.Assemble(seed.Sources, seed.Header)["dppHeader"]!;

        Assert.Equal(PassportAssembler.DppSchemaVersion, header["dppSchemaVersion"]!.GetValue<string>());
        Assert.Equal("active", header["dppStatus"]!["dppStatusValue"]!.GetValue<string>());
        Assert.Equal("item", header["granularity"]!["dppGranularityValue"]!.GetValue<string>());
        Assert.Equal("2026-10-28T09:30:00Z", header["lastUpdated"]!.GetValue<string>());
        Assert.Equal("urn:test:dpp:VPC8C05-6788:v1", header["digitalProductPassportId"]!.GetValue<string>());
    }

    [Fact]
    public void Assemble_ShouldTakeTheOperatorValuesFromTheOperator()
    {
        var seed = DppTestSeed.Load();

        var passport = PassportAssembler.Assemble(seed.Sources, seed.Header);

        Assert.Equal("urn:test:operator:example-battery-systems", passport["dppHeader"]!["economicOperatorId"]!.GetValue<string>());
        Assert.Equal("Example Battery Systems AB", Section(passport, "identifiersAndProductData")["economicOperatorInformation"]!["name"]!.GetValue<string>());
    }

    [Fact]
    public void Assemble_ShouldPreferThePacksRestatedValue()
    {
        var seed = DppTestSeed.Load();

        var performance = Section(PassportAssembler.Assemble(seed.Sources, seed.Header), "performanceAndDurability");

        Assert.Equal(4, performance["capacityFade"]!["percentageValue"]!.GetValue<int>());
        Assert.Equal(0, performance["powerFade"]!["percentageValue"]!.GetValue<int>());
    }

    [Fact]
    public void Assemble_ShouldIgnoreAValueStoredOnTheWrongRecord()
    {
        var seed = DppTestSeed.Load();
        seed.Sources.Pack.Values["ratedCapacity"] = new AttributeValue
        {
            Value = JsonNode.Parse("""{"amperehourMiliamperehourValue":999,"ampereHourMiliamperehourUnit":"Ah"}""")!,
            GeneratedAtUtc = DppTestSeed.GeneratedAtUtc,
            Source = "seed",
        };

        var performance = Section(PassportAssembler.Assemble(seed.Sources, seed.Header), "performanceAndDurability");

        Assert.Equal(200, performance["ratedCapacity"]!["amperehourMiliamperehourValue"]!.GetValue<int>());
    }

    [Fact]
    public void Assemble_ShouldLeaveOutMissingValuesAndEmptySections()
    {
        var seed = DppTestSeed.Load();
        seed.Sources.Pack.Values.Remove("informationOnAccidents");
        seed.Sources.Model.Values.Remove("informationOfDueDiligenceReport");

        var passport = PassportAssembler.Assemble(seed.Sources, seed.Header);

        Assert.Null(Section(passport, "performanceAndDurability")["informationOnAccidents"]);
        Assert.Null(passport["batteryPassportData"]!["supplyChainDueDiligence"]);
        Assert.Contains(Schemas.CheckPassport(passport, feb2027Variant: true), issue => issue.Message.Contains("informationOnAccidents", StringComparison.Ordinal));
    }

    [Fact]
    public void Assemble_ShouldNotShareNodesWithTheStoredValues()
    {
        var seed = DppTestSeed.Load();

        var passport = PassportAssembler.Assemble(seed.Sources, seed.Header);
        Section(passport, "performanceAndDurability")["stateOfChargeSoC"]!["percentageValue"] = 1;

        Assert.Equal(76, seed.Sources.Pack.Values["stateOfChargeSoC"].Value["percentageValue"]!.GetValue<int>());
    }

    private static JsonNode Section(JsonObject passport, string section) => passport["batteryPassportData"]![section]!;
}
