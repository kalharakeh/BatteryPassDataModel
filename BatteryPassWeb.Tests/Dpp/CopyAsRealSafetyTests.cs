using BatteryPassWeb.Dpp.Documents;
using BatteryPassWeb.Dpp.Pages;
using BatteryPassWeb.Dpp.Store;
using BatteryPassWeb.Tests.Demo;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Bson;
using MongoDB.Driver;

namespace BatteryPassWeb.Tests.Dpp;

/// <summary>
/// Decision 66, round two: a copy runs to its end whatever the page does, one at a time, and one that fails part way
/// removes what it made and says so in the audit log, so the same name can be tried again.
/// </summary>
[Collection(MongoCollection.Name)]
public sealed class CopyAsRealSafetyTests(MongoFixture mongo)
{
    private const string Who = DemoRig.Who;

    private static readonly string[] Counted =
        [DppStore.Families, DppStore.Models, DppStore.Batches, DppStore.Sites, DppStore.Operators, DocumentStore.Records, $"{DocumentStore.Bucket}.files", $"{DocumentStore.Bucket}.chunks"];

    private static async Task<(DemoRig Rig, ProductDataPages Pages, string Compact)> SeededAsync(MongoFixture mongo)
    {
        var rig = await DemoRig.StartAsync(mongo, dunsIds: true);
        await rig.Demo.SeedAsync(Who);
        var pages = rig.Services.GetRequiredService<ProductDataPages>();
        return (rig, pages, (await rig.Store.ListAsync<BatteryFamily>()).Single(family => family.Name == "Compact").Id);
    }

    private static async Task<Dictionary<string, long>> CountsAsync(DemoRig rig)
    {
        var counts = new Dictionary<string, long>(StringComparer.Ordinal);
        foreach (var collection in Counted)
        {
            counts[collection] = await rig.Count(collection);
        }

        return counts;
    }

    [Theory]
    [InlineData("site", 1)] // the real site is made, then the copy fails
    [InlineData("model", 2)] // the second model is made, then the copy fails
    [InlineData("batch", 2)] // the second batch, after both models' documents and pictures
    public async Task ACopyThatFailsPartWay_ShouldRemoveWhatItMade_AndSaySoInTheAuditLog(string step, int failAt)
    {
        var (rig, pages, compact) = await SeededAsync(mongo);
        using var _ = rig;
        var before = await CountsAsync(rig);
        var seen = 0;
        pages.CopyStepHook = name => name == step && ++seen == failAt ? throw new MongoException("the database went away") : Task.CompletedTask;

        var (familyId, problem) = await pages.CopyFamilyAsRealAsync(Who, compact, "Compact S");

        pages.CopyStepHook = null;
        Assert.Null(familyId);
        Assert.Equal(ProductDataPages.CopyFailed, problem);
        Assert.Equal(before, await CountsAsync(rig)); // records, document records and stored files: all gone again
        var entry = (await rig.All("applicationAuditEvents", Builders<BsonDocument>.Filter.Eq("eventType", ProductDataPages.CopyUndoneEvent))).Single();
        Assert.Equal($"Copy as real product data of {compact} failed part way and was undone (MongoException)", entry["message"].AsString);

        Assert.Null((await pages.CopyFamilyAsRealAsync(Who, compact, "Compact S")).Problem); // the same name can be tried again
    }

    [Fact]
    public async Task ASecondCopy_ShouldBeRefusedWhileOneRuns_SoADoubleClickMakesOneFamily()
    {
        var (rig, pages, compact) = await SeededAsync(mongo);
        using var _ = rig;
        var running = new TaskCompletionSource();
        var go = new TaskCompletionSource();
        pages.CopyStepHook = async name =>
        {
            if (name == "model")
            {
                running.TrySetResult();
                await go.Task;
            }
        };

        var first = pages.CopyFamilyAsRealAsync(Who, compact, "Compact S");
        await running.Task;
        var second = await pages.CopyFamilyAsRealAsync(Who, compact, "Compact S");
        var operatorCopy = await pages.CopyOperatorAsRealAsync(Who, (await rig.Store.ListAsync<EconomicOperator>()).First(item => item.IsDemo).Id, "Real operator");
        go.SetResult();
        var done = await first;
        pages.CopyStepHook = null;

        Assert.Equal((null, ProductDataPages.CopyRunning), second);
        Assert.Equal((null, ProductDataPages.CopyRunning), operatorCopy);
        Assert.Null(done.Problem);
        Assert.Single(await rig.Store.ListAsync<BatteryFamily>(), family => family.Name == "Compact S");
        Assert.Null((await pages.CopyOperatorAsRealAsync(Who, (await rig.Store.ListAsync<EconomicOperator>()).First(item => item.IsDemo).Id, "Real operator")).Problem); // free again
    }

    [Fact]
    public void TheCopies_ShouldRunOnNoRequestToken_AndTheirButtonsBeSentOnce()
    {
        var controller = File.ReadAllText(DppTestFiles.RepoFile("web", "Controllers", "ProductDataController.cs"));
        Assert.Contains("pages.CopyFamilyAsRealAsync(Who, familyId, name, CancellationToken.None)", controller, StringComparison.Ordinal);
        Assert.Contains("pages.CopySiteAsRealAsync(Who, siteId, name, CancellationToken.None)", controller, StringComparison.Ordinal);
        Assert.Contains("pages.CopyOperatorAsRealAsync(Who, operatorId, name, CancellationToken.None)", controller, StringComparison.Ordinal);
        Assert.Contains("A copy is already running.", controller, StringComparison.Ordinal);
        foreach (var view in new[] { "Family.cshtml", "SitesAndOperators.cshtml" })
        {
            var text = File.ReadAllText(DppTestFiles.RepoFile("web", "Views", "ProductData", view));
            Assert.Contains("data-confirm-busy=\"Copying", text, StringComparison.Ordinal); // disabled once pressed (bp-confirm.js)
        }
    }
}
