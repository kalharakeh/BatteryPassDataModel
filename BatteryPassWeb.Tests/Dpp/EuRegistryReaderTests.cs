using BatteryPassWeb.Dpp.Access;
using BatteryPassWeb.Dpp.Catalog;

namespace BatteryPassWeb.Tests.Dpp;

/// <summary>
/// R3 (decision 94, G06): the EU registry, reading with a valid token, is a reader of its own. It sees the four levels,
/// public, shared, authorities and purchaser, whatever the battery's cluster; it is no team, cluster or authority account
/// and changes nothing.
/// </summary>
public sealed class EuRegistryReaderTests
{
    [Fact]
    public void TheEuRegistry_ShouldBeAReaderOfItsOwn_NamedByTheOrganisation()
    {
        var reader = DppReaders.EuRegistry("ORG-TEST-1");

        Assert.Equal([ReaderRole.EuRegistry], reader.Roles);
        Assert.Empty(reader.ClusterIds);
        Assert.Equal("eu-registry:ORG-TEST-1", reader.Who);
        Assert.False(reader.DemoOnly);
    }

    [Theory]
    [InlineData(Audience.Public)]
    [InlineData(Audience.Shared)]
    [InlineData(Audience.Authorities)]
    [InlineData(Audience.Purchaser)]
    public void TheEuRegistry_ShouldSeeEveryLevel_OfAnyBattery(Audience audience)
    {
        var reader = DppReaders.EuRegistry("ORG-TEST-1");

        foreach (var attribute in AttributeCatalog.All.Where(attribute => attribute.Audience == audience))
        {
            Assert.True(AudienceRules.MaySee(reader, attribute, ["any-cluster"]), $"#{attribute.Number}");
            Assert.True(AudienceRules.MaySee(reader, attribute, []), $"#{attribute.Number} without a cluster");
        }
    }

    [Fact]
    public void TheEuRegistry_ShouldNotSeeTheAppsOwnPurchaserData_NorListBatteries()
    {
        var reader = DppReaders.EuRegistry("ORG-TEST-1");

        Assert.False(AudienceRules.MaySeePurchaserData(reader, ["any-cluster"])); // module IDs and repairs are the pages', not the passport's
        Assert.Contains(ReaderRole.EuRegistry, Enum.GetValues<ReaderRole>());
        Assert.DoesNotContain(ReaderRole.EuRegistry, DppReaders.AppRoles.Values); // no account can be given this role
    }
}
