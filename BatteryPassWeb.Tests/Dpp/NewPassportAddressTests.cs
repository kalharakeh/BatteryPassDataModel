using BatteryPassWeb.Dpp.Access;
using BatteryPassWeb.Dpp.Editing;
using BatteryPassWeb.Dpp.Pages;
using BatteryPassWeb.Dpp.Store;
using BatteryPassWeb.Dpp.Values;

namespace BatteryPassWeb.Tests.Dpp;

/// <summary>
/// S01: the "new passport's ID or web address" of another operator is typed by a cluster admin and read by the team, so
/// only an ID of this app or an http or https address may be saved, and only an http or https address becomes a link.
/// </summary>
[Collection(MongoCollection.Name)]
public sealed class NewPassportAddressTests(MongoFixture mongo)
{
    private static readonly ChangeSender NorthAdmin = ChangeSender.ClusterAdmin("north-admin@example.com", BatteryTesting.North);

    [Theory]
    [InlineData("javascript:alert(1)")]
    [InlineData("JavaScript:alert(1)")]
    [InlineData(" data:text/html,<script>alert(1)</script>")]
    [InlineData("vbscript:msgbox(1)")]
    [InlineData("ftp://example.com/p/1")]
    [InlineData("//example.com/p/1")]
    [InlineData("not an address")]
    public async Task AnotherOperatorsPassport_ShouldBeRefused_WhenItIsNotAnIdOrAWebAddress(string typed)
    {
        var rig = await new SuccessionTests(mongo).RepurposedAsync();
        var details = new OtherOperator(null, "Example Remanufacturing AB", typed);

        var linked = await SuccessionTests.Service(rig).SaveOtherOperatorAsync(NorthAdmin, rig.PackId, details, link: true);
        var saved = await SuccessionTests.Service(rig).SaveOtherOperatorAsync(NorthAdmin, rig.PackId, details, link: false);

        Assert.Equal(SuccessionService.PassportNotAnAddress, linked);
        Assert.Equal(SuccessionService.PassportNotAnAddress, saved);
        var pack = (await rig.Store.GetAsync<BatteryPack>(rig.PackId))!;
        Assert.Null(pack.Succession!.OtherPassport);
        Assert.Null(pack.Succession.LinkedAtUtc);
        Assert.NotEqual(DppStatuses.Inactive, await rig.Store.PassportStatusAsync(rig.PackId));
    }

    [Theory]
    [InlineData("https://example.com/passports/reman-1")]
    [InlineData("http://example.com/passports/reman-1")]
    [InlineData("UN778474481-RV32-0001")]
    public async Task AnotherOperatorsPassport_ShouldBeLinked_WhenItIsAnIdOrAWebAddress(string typed)
    {
        var rig = await new SuccessionTests(mongo).RepurposedAsync();

        var linked = await SuccessionTests.Service(rig).SaveOtherOperatorAsync(NorthAdmin, rig.PackId, new OtherOperator(null, "Example Remanufacturing AB", typed), link: true);

        Assert.Null(linked);
        Assert.Equal(typed, (await rig.Store.GetAsync<BatteryPack>(rig.PackId))!.Succession!.OtherPassport);
    }

    [Fact]
    public async Task ThePageOfTheTeam_ShouldShowAPlantedAddressAsTextNotAsALink()
    {
        var rig = await new SuccessionTests(mongo).RepurposedAsync();
        var pack = (await rig.Store.GetAsync<BatteryPack>(rig.PackId))!;
        pack.Succession!.OtherOperatorInfo = "Example Remanufacturing AB";
        pack.Succession.OtherPassport = "javascript:alert(document.domain)"; // as stored before the fix, or put into the database directly
        pack.Succession.LinkedAtUtc = rig.Clock.Now.UtcDateTime;
        await rig.Store.SetSuccessionAsync(rig.PackId, pack.Succession, default);

        var page = (await BatteryTesting.Pages(rig).BatteryAsync(rig.PackId))!;

        var succession = page.Status!.Succession!;
        Assert.Null(succession.NewPassportLink);
        Assert.Equal("javascript:alert(document.domain)", succession.NewPassportText);
        Assert.Null(page.Cards.NewPassportHref);
    }

    [Fact]
    public async Task ThePageOfTheTeam_ShouldLinkAnHttpsAddress()
    {
        var rig = await new SuccessionTests(mongo).RepurposedAsync();
        await SuccessionTests.Service(rig).SaveOtherOperatorAsync(PublicationTesting.Team, rig.PackId, new OtherOperator(null, "Example Remanufacturing AB", "https://example.com/p/1"), link: true);

        var page = (await BatteryTesting.Pages(rig).BatteryAsync(rig.PackId))!;

        Assert.Equal("https://example.com/p/1", page.Status!.Succession!.NewPassportLink);
        Assert.Equal("https://example.com/p/1", page.Cards.NewPassportHref);
    }

    [Theory]
    [InlineData("https://example.com/a", "https://example.com/a")]
    [InlineData("HTTP://example.com/a", "HTTP://example.com/a")]
    [InlineData("javascript:alert(1)", null)]
    [InlineData("data:text/html,x", null)]
    [InlineData("mailto:a@example.com", null)]
    [InlineData("/relative/path", null)]
    [InlineData("", null)]
    [InlineData(null, null)]
    public void WebAddress_ShouldKeepOnlyHttpAndHttpsAddresses(string? text, string? expected)
    {
        Assert.Equal(expected, WebAddress.Safe(text));
        Assert.Equal(expected is not null, WebAddress.IsWebAddress(text));
    }
}
