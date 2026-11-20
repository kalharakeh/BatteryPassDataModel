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

    [Fact]
    public void TheAppAndTheApiReference_ShouldSaySo()
    {
        Assert.Contains("options.OnRejected = TooManyRequests.WriteAsync;", File.ReadAllText(DppTestFiles.RepoFile("web", "Program.cs")), StringComparison.Ordinal);
        Assert.Contains("The home page's search shares this limit.", File.ReadAllText(DppTestFiles.RepoFile("web", "Views", "Help", "Index.cshtml")), StringComparison.Ordinal);
    }
}
