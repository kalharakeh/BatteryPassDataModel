using System.Text.Json.Nodes;
using BatteryPassWeb.Dpp.Access;
using BatteryPassWeb.Dpp.Assembly;
using BatteryPassWeb.Dpp.Catalog;

namespace BatteryPassWeb.Tests.Dpp;

/// <summary>The one visibility filter (design 4 and 5.2): the whole passport, E1–E4 included, cut for every kind of reader.</summary>
public sealed class PassportFilterTests
{
    private const string OwnCluster = AudienceRulesTests.OwnCluster;

    public static TheoryData<string> EveryReader() => new(AudienceRulesTests.ReaderKinds);

    [Theory]
    [MemberData(nameof(EveryReader))]
    public void Cut_ShouldKeepExactlyWhatTheRoleMatrixGivesTheReader(string kind)
    {
        var passport = FullPassport();

        var cut = PassportFilter.Cut(passport, AudienceRulesTests.ReaderFor(kind), [OwnCluster]);

        var expected = AttributeCatalog.All.Where(attribute => AudienceRulesTests.Expected(kind, attribute)).Select(attribute => attribute.Path);
        Assert.Equal(expected.Order(), Paths(cut).Order());
    }

    [Fact]
    public void TheWholePassport_ShouldHoldEveryAttribute()
    {
        Assert.Equal(AttributeCatalog.All.Select(attribute => attribute.Path).Order(), Paths(FullPassport()).Order());
    }

    [Fact]
    public void Cut_ShouldCopyTheValuesAndLeaveThePassportAsItWas()
    {
        var passport = FullPassport();
        var before = passport.DeepClone();

        var team = PassportFilter.Cut(passport, AudienceRulesTests.ReaderFor("team"), [OwnCluster]);
        var anonymous = PassportFilter.Cut(passport, Reader.Anonymous, [OwnCluster]);
        team["dppHeader"]!["uniqueProductIdentifier"] = "changed";

        Assert.True(JsonNode.DeepEquals(before, passport));
        Assert.Equal(passport["dppHeader"]!["digitalProductPassportId"]!.GetValue<string>(), anonymous["dppHeader"]!["digitalProductPassportId"]!.GetValue<string>());
        Assert.True(JsonNode.DeepEquals(passport["batteryPassportData"]!["identifiersAndProductData"]!["manufacturerInformation"], anonymous["batteryPassportData"]!["identifiersAndProductData"]!["manufacturerInformation"]));
    }

    [Fact]
    public void AnythingOutsideTheCatalogue_ShouldBeLeftOutEvenForTheTeam()
    {
        var passport = FullPassport();
        passport["notInTheFile"] = "x";
        passport["batteryPassportData"]!["performanceAndDurability"]!["remainingCapacity"] = 80;

        var cut = PassportFilter.Cut(passport, AudienceRulesTests.ReaderFor("team"), [OwnCluster]);

        Assert.Null(cut["notInTheFile"]);
        Assert.False(cut["batteryPassportData"]!["performanceAndDurability"]!.AsObject().ContainsKey("remainingCapacity"));
    }

    [Theory]
    [InlineData("own-cluster-user")]
    [InlineData("own-cluster-admin")]
    public void ADraftOrAnotherClustersBattery_ShouldGiveAClusterPublicDataOnly(string kind)
    {
        var publicPaths = AttributeCatalog.All.Where(attribute => attribute.Audience == Audience.Public).Select(attribute => attribute.Path).Order();

        Assert.Equal(publicPaths, Paths(PassportFilter.Cut(FullPassport(), AudienceRulesTests.ReaderFor(kind), [])).Order());
        Assert.Equal(publicPaths, Paths(PassportFilter.Cut(FullPassport(), AudienceRulesTests.ReaderFor(kind), ["cluster-south"])).Order());
    }

    [Theory]
    [InlineData("own-cluster-user", true)]
    [InlineData("market-surveillance", true)]
    [InlineData("team", true)]
    [InlineData("other-cluster", false)]
    [InlineData("commission", false)]
    [InlineData("anonymous", false)]
    public void E1ToE4_ShouldFollowTemperature(string kind, bool sees)
    {
        var cut = PassportFilter.Cut(FullPassport(), AudienceRulesTests.ReaderFor(kind), [OwnCluster]);
        var performance = cut["batteryPassportData"]?["performanceAndDurability"]?.AsObject();

        Assert.Equal(sees, performance?.ContainsKey("temperatureInformation") == true);
        Assert.All(AttributeCatalog.Extensions, extension => Assert.Equal(sees, performance?.ContainsKey(extension.Property) == true));
    }

    /// <summary>The seed passport with E1–E4 added, so every attribute of the catalogue is present.</summary>
    private static JsonObject FullPassport()
    {
        var seed = DppTestSeed.Load();
        foreach (var extension in AttributeCatalog.Extensions)
        {
            seed.Sources.Pack.Values[extension.Property] = DppStoreTesting.Value(JsonValue.Create(3600));
        }

        return PassportAssembler.Assemble(seed.Sources, seed.Header);
    }

    /// <summary>The attribute paths in a passport: the header's properties, and each section's properties.</summary>
    private static IEnumerable<string> Paths(JsonObject passport)
    {
        foreach (var (name, node) in passport)
        {
            if (name == AttributeDefinition.HeaderSection && node is JsonObject header)
            {
                foreach (var (property, _) in header)
                {
                    yield return $"{name}.{property}";
                }
            }
            else if (name == AttributeDefinition.DataRoot && node is JsonObject data)
            {
                foreach (var (section, sectionNode) in data)
                {
                    foreach (var (property, _) in sectionNode!.AsObject())
                    {
                        yield return $"{name}.{section}.{property}";
                    }
                }
            }
            else
            {
                yield return name;
            }
        }
    }
}
