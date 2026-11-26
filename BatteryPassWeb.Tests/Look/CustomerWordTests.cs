using System.Net;
using System.Text.RegularExpressions;
using BatteryPassWeb.Dpp.Demo;
using BatteryPassWeb.Dpp.Pages;
using BatteryPassWeb.Tests.Demo;
using BatteryPassWeb.Tests.Dpp;
using Microsoft.Extensions.DependencyInjection;

namespace BatteryPassWeb.Tests.Look;

/// <summary>
/// Finding W07, decision 55: "customer" in place of "cluster" on the pages customers and readers see; "cluster" stays on
/// the team's pages and in the API (its reference included). Every page each reader reaches is read as they get it: its
/// text, closed windows, titles, labels and placeholders.
/// </summary>
[Collection(MongoCollection.Name)]
public sealed partial class CustomerWordTests(MongoFixture mongo)
{
    [GeneratedRegex(@"<(script|style)\b.*?</\1>", RegexOptions.Singleline | RegexOptions.IgnoreCase)]
    private static partial Regex Code();

    [GeneratedRegex("\\b(?:title|aria-label|placeholder|alt|data-tooltip)=\"([^\"]*)\"", RegexOptions.IgnoreCase)]
    private static partial Regex Readable();

    [GeneratedRegex("<[^>]+>")]
    private static partial Regex Tag();

    [GeneratedRegex(@"\bclusters?\b", RegexOptions.IgnoreCase)]
    private static partial Regex Cluster();

    /// <summary>What a reader can read on the page: its text, and the attributes a browser shows or reads out.</summary>
    internal static string ReadableText(string html)
    {
        var body = Code().Replace(html, " ");
        var attributes = string.Join(" | ", Readable().Matches(body).Select(match => match.Groups[1].Value));
        return WebUtility.HtmlDecode(Tag().Replace(body, " ") + " | " + attributes);
    }

    private static IEnumerable<string> Hits(string text) =>
        Cluster().Matches(text).Select(match => text.Substring(Math.Max(0, match.Index - 60), Math.Min(text.Length - Math.Max(0, match.Index - 60), 130)).Trim());

    [Theory]
    [InlineData("demo.north.admin@example.test")]
    [InlineData("demo.north.user@example.test")]
    [InlineData("demo.msa@example.test")]
    [InlineData("demo.recycling@example.test")]
    [InlineData(null)]
    public async Task NoPageAReaderReaches_ShouldSayCluster(string? email)
    {
        await using var site = await ShellSite.StartAsync(mongo.NewContext());
        await site.Services.GetRequiredService<DemoDataService>().ResetAsync(DemoRig.Who);
        var rows = (await site.Services.GetRequiredService<BatteryPages>().TeamListAsync(null)).Rows;
        var north = rows.Where(row => row.ClusterId == "demo-north" && row.Active is not null).ToArray();
        var other = rows.First(row => row.ClusterId != "demo-north" && row.Active is not null);
        Assert.NotEmpty(north);

        var paths = new List<string> { "/", "/?q=DEMO&notFound=1", "/help", "/account", "/registry", "/no-such-page", "/not-allowed" };
        foreach (var row in north.Append(other))
        {
            paths.Add(PassportPages.SummaryHref(row.Identifier));
            paths.Add(PassportPages.DetailHref(row.Identifier));
        }

        if (email == "demo.north.admin@example.test")
        {
            paths.AddRange(["/cluster-admin/batteries", "/cluster-admin/users", "/cluster-admin/api-tokens"]);
            foreach (var row in north)
            {
                foreach (var section in new[] { "overview", "status", "live", "upload", "accidents", "modules", "versions", "handover", "history" })
                {
                    paths.Add($"{ClusterBatteryPages.BatteryHref(row.PackId)}?section={section}");
                }
            }
        }

        var found = new List<string>();
        foreach (var path in paths)
        {
            var (status, body, _) = await site.GetAsync(path, email);
            if (status is HttpStatusCode.OK or HttpStatusCode.NotFound or HttpStatusCode.Forbidden)
            {
                found.AddRange(Hits(ReadableText(body)).Select(hit => $"{path}: {hit}"));
            }
        }

        Assert.True(found.Count == 0, string.Join("\n", found.Distinct().Take(40)));
    }

    [Fact]
    public async Task TheTeamsPages_ShouldKeepCluster()
    {
        await using var site = await ShellSite.StartAsync(mongo.NewContext());
        await site.Services.GetRequiredService<DemoDataService>().ResetAsync(DemoRig.Who);
        await ToDoPageViewTests.AddTeamAsync(site);

        var (_, batteries, _) = await site.GetAsync("/admin/batteries", ToDoPageViewTests.Team);
        var (_, registry, _) = await site.GetAsync("/registry", ToDoPageViewTests.Team);

        Assert.Contains("<th>Cluster</th>", batteries, StringComparison.Ordinal);
        Assert.Contains("<th>Cluster</th>", registry, StringComparison.Ordinal);
    }
}
