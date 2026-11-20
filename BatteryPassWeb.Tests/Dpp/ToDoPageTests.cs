using BatteryPassWeb.Dpp.Editing;
using BatteryPassWeb.Dpp.Identifiers;
using BatteryPassWeb.Dpp.Pages;
using BatteryPassWeb.Dpp.Publication;
using BatteryPassWeb.Dpp.Store;
using BatteryPassWeb.Tests.Demo;
using Microsoft.Extensions.DependencyInjection;

namespace BatteryPassWeb.Tests.Dpp;

/// <summary>
/// Finding U05 (the owner's approved picture B, 13 Nov 2026): the team's "To do" page. Six boxes, each a count and a short
/// list from what the battery list loads or from one query, every line opening the place to do the job.
/// </summary>
[Collection(MongoCollection.Name)]
public sealed class ToDoPageTests(MongoFixture mongo)
{
    private static ToDoPages Pages(DemoRig rig, DateTime? now = null) => new(
        rig.Services.GetRequiredService<BatteryPages>(),
        rig.Services.GetRequiredService<PublicationService>(),
        rig.Store,
        rig.Services.GetRequiredService<BatteryIdPool>(),
        now ?? DateTime.UtcNow);

    private static ToDoBox Box(ToDoPage page, string key) => page.Boxes.Single(box => box.Key == key);

    [Fact]
    public async Task TheBoxes_ShouldBeTheSixApproved_InTheirOrder()
    {
        using var rig = await DemoRig.StartAsync(mongo);
        await rig.Demo.ResetAsync(DemoRig.Who);

        var page = await Pages(rig).BuildAsync();

        Assert.Equal(["drafts", "waiting", "handovers", "ids", "uploads", "registry"], page.Boxes.Select(box => box.Key));
        Assert.Equal(
            ["Drafts with missing values", "Saved changes not yet public", "Hand-overs waiting", "Battery IDs running low", "Uploads with refused values", "Not yet in the EU registry"],
            page.Boxes.Select(box => box.Title));
        Assert.Equal(8, page.Batteries);
    }

    [Fact]
    public async Task TheDraftsBox_ShouldCountWhatEachBatteryPageCounts()
    {
        using var rig = await DemoRig.StartAsync(mongo);
        await rig.Demo.ResetAsync(DemoRig.Who);
        var batteries = rig.Services.GetRequiredService<BatteryPages>();

        var drafts = Box(await Pages(rig).BuildAsync(), "drafts");

        var expected = new List<(string Serial, int Missing)>();
        foreach (var row in (await batteries.TeamListAsync(null)).Rows.Where(row => row.IsDraft))
        {
            var missing = (await batteries.BatteryAsync(row.PackId))!.Publication.MissingCount;
            if (missing > 0)
            {
                expected.Add((row.SerialNumber, missing));
            }
        }

        Assert.NotEmpty(expected);
        Assert.Equal(expected.Count, drafts.Count);
        foreach (var (serial, missing) in expected)
        {
            var line = Assert.Single(drafts.Lines, line => line.Title == serial);
            Assert.EndsWith($"{Words.Count(missing, "value")} missing", line.Note, StringComparison.Ordinal);
            Assert.StartsWith("/admin/batteries/", line.Href, StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task TheHandOversBox_ShouldFollowTheWaitingHandOvers()
    {
        using var rig = await DemoRig.StartAsync(mongo);
        await rig.Demo.ResetAsync(DemoRig.Who);
        var before = Box(await Pages(rig).BuildAsync(), "handovers");
        var rows = (await rig.Services.GetRequiredService<BatteryPages>().TeamListAsync(null)).Rows;
        var free = rows.First(row => row.Active is not null && row.ClusterId == "demo-north" && before.Lines.All(line => line.Title != row.SerialNumber));

        var asked = await rig.Services.GetRequiredService<HandOverService>().AskAsync(ChangeSender.ClusterAdmin("demo.north.admin@example.test", "demo-north"), free.PackId, "demo-south", confirm: true);
        Assert.Null(asked.Problem);
        var after = Box(await Pages(rig).BuildAsync(), "handovers");

        Assert.Equal(before.Count + 1, after.Count);
        var line = Assert.Single(after.Lines, line => line.Title == free.SerialNumber);
        Assert.Equal(BatteryPages.BatteryHref(free.PackId), line.Href);
        Assert.StartsWith("to ", line.Note, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TheUploadsBox_ShouldCountRefusedValuesOfTheLast14Days()
    {
        using var rig = await DemoRig.StartAsync(mongo);
        await rig.Demo.ResetAsync(DemoRig.Who);
        var now = DateTime.UtcNow;
        var before = Box(await Pages(rig, now).BuildAsync(), "uploads").Count;
        var pack = (await rig.Services.GetRequiredService<BatteryPages>().TeamListAsync(null)).Rows.First(row => row.Active is not null);

        UploadRecord Upload(int daysAgo, string valueOutcome, string batteryOutcome = "saved") => new()
        {
            Id = Guid.NewGuid().ToString("N"),
            AtUtc = now.AddDays(-daysAgo),
            Who = DemoRig.Who,
            SentAs = "Team",
            Channel = "page",
            Format = "dpp-v3-upload/1",
            Batteries =
            [
                new UploadedBattery
                {
                    NamedAs = pack.SerialNumber,
                    PackId = pack.PackId,
                    Outcome = batteryOutcome,
                    Values = [new UploadedValue { Key = "stateOfCharge", Outcome = valueOutcome }],
                },
            ],
        };
        await rig.Store.AddUploadAsync(Upload(3, "Rejected"));
        await rig.Store.AddUploadAsync(Upload(13, "Saved", batteryOutcome: "refused"));
        await rig.Store.AddUploadAsync(Upload(20, "Rejected")); // too old
        await rig.Store.AddUploadAsync(Upload(1, "Saved")); // nothing refused

        var uploads = Box(await Pages(rig, now).BuildAsync(), "uploads");

        Assert.Equal(before + 2, uploads.Count);
        Assert.All(uploads.Lines, line => Assert.Equal($"{BatteryPages.BatteryHref(pack.PackId)}?section=upload", line.Href));
        Assert.Contains(uploads.Lines, line => line.Note.StartsWith("a value refused", StringComparison.Ordinal));
        Assert.Contains(uploads.Lines, line => line.Note.StartsWith("file refused", StringComparison.Ordinal));
    }

    [Fact]
    public async Task TheIdsBox_ShouldCountRealIdsPerRealModel_AndAnyModel()
    {
        using var rig = await DemoRig.StartAsync(mongo);
        await rig.Demo.ResetAsync(DemoRig.Who);

        var ids = Box(await Pages(rig).BuildAsync(), "ids");

        // Demo models and demo or test IDs aren't counted, as on the Battery IDs page; "Any model" is always weighed.
        var line = Assert.Single(ids.Lines);
        Assert.Equal("Any model", line.Title);
        Assert.Equal("no IDs left", line.Note);
        Assert.Equal("/admin/battery-ids", line.Href);
        Assert.Equal(1, ids.Count);
    }

    [Fact]
    public async Task ABoxWithNothingToDo_ShouldSaySo_AndListNothing()
    {
        using var rig = await DemoRig.StartAsync(mongo);
        await rig.Demo.ResetAsync(DemoRig.Who);

        var page = await Pages(rig).BuildAsync();

        // The demo has no held-back change, and demo batteries are outside the EU registry's scope.
        foreach (var key in new[] { "waiting", "registry" })
        {
            var box = Box(page, key);
            Assert.Equal(0, box.Count);
            Assert.Empty(box.Lines);
            Assert.False(string.IsNullOrWhiteSpace(box.Clear));
        }
    }

    [Fact]
    public void ThePage_ShouldMakeNoCallPerBattery()
    {
        var code = File.ReadAllText(DppTestFiles.RepoFile("web", "Dpp", "Pages", "ToDoPages.cs"));

        Assert.DoesNotContain("BatteryAsync(", code, StringComparison.Ordinal);
        Assert.DoesNotContain("CheckAsync(", code, StringComparison.Ordinal);
        Assert.DoesNotContain("LoadSourcesAsync(", code, StringComparison.Ordinal);
        Assert.Contains("CheckDraftsAsync(", code, StringComparison.Ordinal);
        Assert.Contains("ListWaitingHandOversAsync(", code, StringComparison.Ordinal);
        Assert.Contains("ListRefusedUploadsSinceAsync(", code, StringComparison.Ordinal); // filtered and capped in the query (review 1, point 5)
    }
    /// <summary>Review 1, point 5: the box's query keeps only refused uploads, reads only what the box shows, and is capped.</summary>
    [Fact]
    public async Task TheUploadsQuery_ShouldFilterInTheDatabase_ReadOnlyWhatTheBoxShows_AndStopAtTheCap()
    {
        using var rig = await DemoRig.StartAsync(mongo);
        var now = DateTime.UtcNow;
        UploadRecord Upload(int minutesAgo, string valueOutcome, string batteryOutcome = "saved") => new()
        {
            Id = Guid.NewGuid().ToString("N"),
            AtUtc = now.AddMinutes(-minutesAgo),
            Who = DemoRig.Who,
            SentAs = "Team",
            Channel = "api",
            Format = "dpp-v3-upload/1",
            Batteries =
            [
                new UploadedBattery
                {
                    NamedAs = "SN-1",
                    PackId = "pack-1",
                    Outcome = batteryOutcome,
                    Problem = "a problem the box doesn't show",
                    Values = [new UploadedValue { Key = "stateOfCharge", Value = "\"secret\"", Outcome = valueOutcome, Message = "a message" }],
                },
            ],
        };
        for (var i = 0; i < DppStore.RefusedUploadsRead + 5; i++)
        {
            await rig.Store.AddUploadAsync(Upload(i + 1, "Rejected"));
        }

        await rig.Store.AddUploadAsync(Upload(1, "Saved")); // nothing refused
        await rig.Store.AddUploadAsync(Upload(0, "Saved", batteryOutcome: "refused"));

        var read = await rig.Store.ListRefusedUploadsSinceAsync(now.AddDays(-14));

        Assert.Equal(DppStore.RefusedUploadsRead, read.Count);
        Assert.Equal("refused", read[0].Batteries.Single().Outcome); // newest first
        Assert.All(read, upload => Assert.All(upload.Batteries, battery =>
        {
            Assert.Null(battery.Problem);
            Assert.All(battery.Values, value => Assert.Null(value.Value));
        }));
    }
}
