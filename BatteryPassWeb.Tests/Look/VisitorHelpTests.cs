using System.Net;
using BatteryPassWeb.Tests.Dpp;

namespace BatteryPassWeb.Tests.Look;

/// <summary>Finding U25: the visitors' Help explained the Battery tag, which visitors never see; it now leaves it out.</summary>
[Collection(MongoCollection.Name)]
public sealed class VisitorHelpTests(MongoFixture mongo)
{
    [Fact]
    public async Task AVisitorsHelp_ShouldLeaveTheBatteryTagOut()
    {
        await using var site = await ShellSite.StartAsync(mongo.NewContext());

        var (status, body, _) = await site.GetAsync("/help");
        var statuses = body[body.IndexOf("id=\"statuses\"", StringComparison.Ordinal)..body.IndexOf("</section>", body.IndexOf("id=\"statuses\"", StringComparison.Ordinal), StringComparison.Ordinal)];

        Assert.Equal(HttpStatusCode.OK, status);
        Assert.DoesNotContain("square tag", statuses, StringComparison.Ordinal);
        Assert.DoesNotContain("bp-badge is-battery", statuses, StringComparison.Ordinal);
        Assert.Contains("round pill", statuses, StringComparison.Ordinal);
        Assert.Contains("The passport that counts now.", statuses, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ASignedInReadersHelp_ShouldKeepBothBadges()
    {
        await using var site = await ShellSite.StartAsync(mongo.NewContext());

        var (_, body, _) = await site.GetAsync("/help", claims: "pat@example.test|Pat Person|member");
        var statuses = body[body.IndexOf("id=\"statuses\"", StringComparison.Ordinal)..body.IndexOf("</section>", body.IndexOf("id=\"statuses\"", StringComparison.Ordinal), StringComparison.Ordinal)];

        Assert.Contains("square tag", statuses, StringComparison.Ordinal);
        Assert.Contains("As made; never repaired or reused.", statuses, StringComparison.Ordinal);
    }
}
