using System.Net;
using BatteryPassWeb.Tests.Dpp;

namespace BatteryPassWeb.Tests.Look;

/// <summary>
/// Review 2 of N1, point 6: over the public read limit a person searching gets a short sentence, not bare JSON; the
/// passport's .json addresses and the API keep their JSON answer; the API reference says the search shares the limit.
/// </summary>
[Collection(MongoCollection.Name)]
public sealed class TooManyRequestsTests(MongoFixture mongo)
{
    [Fact]
    public async Task APersonSearching_ShouldGetASentence()
    {
        await using var site = await ShellSite.StartAsync(mongo.NewContext(), readLimit: 1);

        await site.GetAsync("/search?q=NOPE-0000");
        var (status, body, _) = await site.GetAsync("/search?q=NOPE-0000");

        Assert.Equal(HttpStatusCode.TooManyRequests, status);
        Assert.Equal(BatteryPassWeb.Configuration.TooManyRequests.SearchWords, body);
        Assert.Equal("Too many searches from your address. Try again in a minute.", BatteryPassWeb.Configuration.TooManyRequests.SearchWords);
    }

    [Fact]
    public async Task APassportsJsonAddress_ShouldKeepItsJson()
    {
        await using var site = await ShellSite.StartAsync(mongo.NewContext(), readLimit: 1);

        await site.GetAsync("/dpp/NO-SUCH-ID.json");
        var (status, body, _) = await site.GetAsync("/dpp/NO-SUCH-ID.json");

        Assert.Equal(HttpStatusCode.TooManyRequests, status);
        Assert.Equal("{\"error\":\"Too many requests.\"}", body);
    }

    /// <summary>
    /// Point 10 of round three: a passport's page address answers data when the request asks for JSON (PrefersJson), so
    /// over the limit such a request gets the JSON error too.
    /// </summary>
    [Theory]
    [InlineData("application/json")]
    [InlineData("application/json, text/html;q=0.5")]
    public async Task APassportsPageAddressAskedForJson_ShouldGetTheJsonErrorOverTheLimit(string accept)
    {
        await using var site = await ShellSite.StartAsync(mongo.NewContext(), readLimit: 1);

        await site.GetAsync("/dpp/NO-SUCH-ID", accept: accept);
        var (status, body, _) = await site.GetAsync("/dpp/NO-SUCH-ID", accept: accept);

        Assert.Equal(HttpStatusCode.TooManyRequests, status);
        Assert.Equal("{\"error\":\"Too many requests.\"}", body);
    }

    /// <summary>A browser's Accept picks the passport page, which has no read limit, so it never meets the limit's answer.</summary>
    [Fact]
    public async Task APassportsPageAddressAskedByABrowser_ShouldStillGetThePage()
    {
        await using var site = await ShellSite.StartAsync(mongo.NewContext(), readLimit: 1);
        const string browser = "text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8";

        await site.GetAsync("/dpp/NO-SUCH-ID", accept: browser);
        var (status, body, _) = await site.GetAsync("/dpp/NO-SUCH-ID", accept: browser);

        Assert.NotEqual(HttpStatusCode.TooManyRequests, status);
        Assert.Contains("<html", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TheJsonRule_ShouldBeThePassportAddressesOwn() =>
        Assert.Contains("PrefersJsonAttribute.PrefersJson(", File.ReadAllText(DppTestFiles.RepoFile("web", "Configuration", "TooManyRequests.cs")), StringComparison.Ordinal);

    [Fact]
    public void TheAppAndTheApiReference_ShouldSaySo()
    {
        Assert.Contains("options.OnRejected = TooManyRequests.WriteAsync;", File.ReadAllText(DppTestFiles.RepoFile("web", "Program.cs")), StringComparison.Ordinal);
        Assert.Contains("The home page's search shares this limit.", File.ReadAllText(DppTestFiles.RepoFile("web", "Views", "Help", "Index.cshtml")), StringComparison.Ordinal);
    }
}
