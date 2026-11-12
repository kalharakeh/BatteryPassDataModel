using BatteryPassWeb.Dpp.Access;
using BatteryPassWeb.Dpp.Catalog;

namespace BatteryPassWeb.Tests.Dpp;

/// <summary>Design 5.2's role matrix, over every attribute.</summary>
public sealed class AudienceRulesTests
{
    internal const string OwnCluster = "cluster-test";

    internal static readonly string[] ReaderKinds =
    [
        "anonymous", "own-cluster-user", "own-cluster-admin", "other-cluster", "team", "notified-body",
        "market-surveillance", "commission", "li-repair", "li-recycling", "li-treatment",
    ];

    public static TheoryData<string, int> EveryReaderAndAttribute()
    {
        var data = new TheoryData<string, int>();
        foreach (var kind in ReaderKinds)
        {
            foreach (var attribute in AttributeCatalog.All)
            {
                data.Add(kind, attribute.Number);
            }
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(EveryReaderAndAttribute))]
    public void MaySee_ShouldFollowTheRoleMatrix(string kind, int number)
    {
        var attribute = AttributeCatalog.ByNumber(number);

        Assert.Equal(Expected(kind, attribute), AudienceRules.MaySee(ReaderFor(kind), attribute, [OwnCluster]));
    }

    [Fact]
    public void WithoutAPublishedPack_NoClusterShouldSeePurchaserOrSharedData()
    {
        var admin = ReaderFor("own-cluster-admin");

        Assert.False(AudienceRules.MaySee(admin, AttributeCatalog.ByNumber(63), []));
        Assert.False(AudienceRules.MaySee(admin, AttributeCatalog.ByNumber(45), []));
        Assert.True(AudienceRules.MaySee(admin, AttributeCatalog.ByNumber(11), []));
        Assert.True(AudienceRules.MaySee(ReaderFor("team"), AttributeCatalog.ByNumber(63), []));
        Assert.True(AudienceRules.MaySee(ReaderFor("market-surveillance"), AttributeCatalog.ByNumber(63), []));
        Assert.True(AudienceRules.MaySee(ReaderFor("commission"), AttributeCatalog.ByNumber(45), []));
    }

    [Fact]
    public void ReaderWithSeveralRoles_ShouldSeeWhatAnyOfThemSees()
    {
        var reader = new Reader(new HashSet<ReaderRole> { ReaderRole.NotifiedBody, ReaderRole.LiRepair }, new HashSet<string>(), "both@example.com");

        Assert.True(AudienceRules.MaySee(reader, AttributeCatalog.ByNumber(29), [OwnCluster]));
        Assert.True(AudienceRules.MaySee(reader, AttributeCatalog.ByNumber(46), [OwnCluster]));
        Assert.False(AudienceRules.MaySee(reader, AttributeCatalog.ByNumber(63), [OwnCluster]));
    }

    // Design 5.2 written out per reader; the shared rows by attribute number, from design 5.1.
    internal static bool Expected(string kind, AttributeDefinition attribute) => kind switch
    {
        "team" or "market-surveillance" => true,
        "anonymous" or "other-cluster" => attribute.Audience == Audience.Public,
        "own-cluster-user" or "own-cluster-admin" => attribute.Audience is Audience.Public or Audience.Purchaser or Audience.Shared,
        "notified-body" => attribute.Audience is Audience.Public or Audience.Authorities,
        "commission" => attribute.Audience is Audience.Public or Audience.Shared or Audience.Authorities,
        "li-repair" => attribute.Audience == Audience.Public || attribute.Number is 43 or 45 or 46 or 47 or 48,
        "li-recycling" => attribute.Audience == Audience.Public || attribute.Number is 43 or 45 or 48,
        "li-treatment" => attribute.Audience == Audience.Public || attribute.Number is 48,
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
    };

    internal static Reader ReaderFor(string kind) => kind switch
    {
        "anonymous" => Reader.Anonymous,
        "own-cluster-user" => Reader.With("user@example.com", ReaderRole.ClusterUser, OwnCluster),
        "own-cluster-admin" => Reader.With("admin@example.com", ReaderRole.ClusterAdmin, OwnCluster),
        "other-cluster" => Reader.With("other@example.com", ReaderRole.ClusterAdmin, "another-cluster"),
        "team" => Reader.With("team@example.com", ReaderRole.Team),
        "notified-body" => Reader.With("nb@example.com", ReaderRole.NotifiedBody),
        "market-surveillance" => Reader.With("msa@example.com", ReaderRole.MarketSurveillance),
        "commission" => Reader.With("ec@example.com", ReaderRole.Commission),
        "li-repair" => Reader.With("repair@example.com", ReaderRole.LiRepair),
        "li-recycling" => Reader.With("recycling@example.com", ReaderRole.LiRecycling),
        "li-treatment" => Reader.With("treatment@example.com", ReaderRole.LiTreatment),
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
    };
}
