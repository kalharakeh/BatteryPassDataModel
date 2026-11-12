using System.Text.Json.Nodes;
using BatteryPassWeb.Dpp.Assembly;
using BatteryPassWeb.Dpp.Catalog;

namespace BatteryPassWeb.Tests.Dpp;

public sealed class PassportStaticCoreTests
{
    [Fact]
    public void Extract_ShouldLeaveOutTheStatusTheLastUpdateAndTheLiveSet()
    {
        var core = PassportStaticCore.Extract(SeedPassport());

        Assert.Equal(["#2", "#4", "#63", "#83", "#87", "#92", "E1", "E2", "E3", "E4"], PassportStaticCore.NotSigned.Select(a => a.Label)); // E1–E4: decision 28
        Assert.All(PassportStaticCore.NotSigned, attribute => Assert.Null(Find(core, attribute)));
        Assert.All(
            AttributeCatalog.All.Except(PassportStaticCore.NotSigned),
            attribute => Assert.NotNull(Find(core, attribute)));
    }

    [Fact]
    public void ContentHash_ShouldIgnoreThePassportIdButNotTheContent()
    {
        var core = PassportStaticCore.Extract(SeedPassport());
        var otherId = core.DeepClone().AsObject();
        otherId["dppHeader"]!["digitalProductPassportId"] = "urn:test:dpp:VPC8C05-6788:v2";
        var otherContent = core.DeepClone().AsObject();
        otherContent["batteryPassportData"]!["identifiersAndProductData"]!["thirdPartyServiceProvider"] = "Another AB";

        Assert.Equal(PassportStaticCore.ContentHash(core), PassportStaticCore.ContentHash(otherId));
        Assert.NotEqual(PassportStaticCore.ContentHash(core), PassportStaticCore.ContentHash(otherContent));
    }

    [Fact]
    public void Compose_ShouldAddTheHeaderAndTheLiveValues()
    {
        var seed = DppTestSeed.Load();
        var core = PassportStaticCore.Extract(PassportAssembler.Assemble(seed.Sources, seed.Header));
        var updated = new DateTime(2026, 10, 29, 7, 15, 0, DateTimeKind.Utc);

        var passport = PassportStaticCore.Compose(core, "active", updated, seed.Sources.Pack.Values);

        Assert.Equal("active", passport["dppHeader"]!["dppStatus"]!["dppStatusValue"]!.GetValue<string>());
        Assert.Equal("2026-10-29T07:15:00Z", passport["dppHeader"]!["lastUpdated"]!.GetValue<string>());
        Assert.Equal(76, passport["batteryPassportData"]!["performanceAndDurability"]!["stateOfChargeSoC"]!["percentageValue"]!.GetValue<int>());
        Assert.Empty(DppStoreTesting.Schemas.CheckPassport(passport, feb2027Variant: true));
    }

    [Fact]
    public void ComposeWithoutLiveValues_ShouldLeaveThemOut()
    {
        var core = PassportStaticCore.Extract(SeedPassport());

        var passport = PassportStaticCore.Compose(core, "archived", DppStoreTesting.Now.UtcDateTime, liveValues: null);

        Assert.Equal("archived", passport["dppHeader"]!["dppStatus"]!["dppStatusValue"]!.GetValue<string>());
        Assert.Null(passport["batteryPassportData"]!["performanceAndDurability"]!["stateOfChargeSoC"]);
    }

    private static JsonObject SeedPassport()
    {
        var seed = DppTestSeed.Load();
        return PassportAssembler.Assemble(seed.Sources, seed.Header);
    }

    private static JsonNode? Find(JsonObject passport, AttributeDefinition attribute) =>
        attribute.Section == AttributeDefinition.HeaderSection
            ? passport["dppHeader"]?[attribute.Property]
            : passport["batteryPassportData"]?[attribute.Section]?[attribute.Property];
}
