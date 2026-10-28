using System.Text.Json.Nodes;
using BatteryPassWeb.Dpp.Assembly;
using BatteryPassWeb.Dpp.Publication;

namespace BatteryPassWeb.Tests.Dpp;

public sealed class PublicationChecksTests
{
    private static readonly PublicationChecks Checks = new(DppStoreTesting.Schemas);

    [Fact]
    public void CompletePassport_ShouldPublishWithoutWarnings()
    {
        var report = Checks.Check(SeedPassport());

        Assert.True(report.CanPublish);
        Assert.Empty(report.Blocking);
        Assert.Empty(report.Warnings);
    }

    [Fact]
    public void MissingAttributeDueByFeb2027_ShouldBlock()
    {
        var passport = SeedPassport();
        Section(passport, "identifiersAndProductData").Remove("thirdPartyServiceProvider");

        var report = Checks.Check(passport);

        Assert.False(report.CanPublish);
        Assert.Contains(report.Blocking, item => item.StartsWith("#13 ", StringComparison.Ordinal));
    }

    [Fact]
    public void MissingAttributeRequiredOnlyLater_ShouldWarn()
    {
        var passport = SeedPassport();
        Section(passport, "performanceAndDurability").Remove("ratedCapacity");

        var report = Checks.Check(passport);

        Assert.True(report.CanPublish);
        Assert.Contains(report.Warnings, item => item.StartsWith("#57 ", StringComparison.Ordinal) && item.Contains("required later", StringComparison.Ordinal));
    }

    [Fact]
    public void MissingConditionalAttribute_ShouldNeitherBlockNorWarn()
    {
        var passport = SeedPassport();
        Section(passport, "identifiersAndProductData").Remove("warrantyPeriod");

        var report = Checks.Check(passport);

        Assert.True(report.CanPublish);
        Assert.Empty(report.Warnings);
    }

    [Fact]
    public void ValueWithAMissingPart_ShouldBlock()
    {
        var passport = SeedPassport();
        Section(passport, "symbolsLabelsAndDocumentationOfConformity")["euDeclarationOfConformity"] = new JsonObject { ["contentType"] = "application/pdf" };

        var report = Checks.Check(passport);

        Assert.False(report.CanPublish);
        Assert.Contains(report.Blocking, item => item.Contains("euDeclarationOfConformity", StringComparison.Ordinal) && item.Contains("url", StringComparison.Ordinal));
    }

    [Fact]
    public void ValueOutOfRange_ShouldBlock()
    {
        var passport = SeedPassport();
        Section(passport, "performanceAndDurability")["stateOfChargeSoC"] = JsonNode.Parse("""{"percentUnit":"%","percentageValue":120}""");

        Assert.False(Checks.Check(passport).CanPublish);
    }

    private static JsonObject SeedPassport()
    {
        var seed = DppTestSeed.Load();
        return PassportAssembler.Assemble(seed.Sources, seed.Header);
    }

    private static JsonObject Section(JsonObject passport, string section) => passport["batteryPassportData"]![section]!.AsObject();
}
