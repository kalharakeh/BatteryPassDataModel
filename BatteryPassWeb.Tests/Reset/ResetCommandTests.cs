using BatteryPassWeb.Tests.Dpp;
using V3Reset;

namespace BatteryPassWeb.Tests.Reset;

/// <summary>Step 3d-2: reset is a dry run unless told to run, and then needs the database name typed out.</summary>
[Collection(MongoCollection.Name)]
public sealed class ResetCommandTests(MongoFixture mongo)
{
    private async Task<(MongoDB.Driver.IMongoDatabase Database, string Uri, string Decisions)> ReviewedAsync(Func<string, string>? edit = null)
    {
        var (database, uri) = await ResetTesting.FreshV3Async(mongo);
        var review = ResetTesting.NewFolder();
        Assert.Equal(App.Done, await App.RunAsync(["inventory", "--out", review], ResetTesting.Environment(uri, "battery_pass_v3"), new StringReader(string.Empty), new StringWriter(), "test"));
        var decisions = Path.Combine(review, "decisions.csv");
        if (edit is not null)
        {
            await File.WriteAllTextAsync(decisions, edit(await File.ReadAllTextAsync(decisions)));
        }

        return (database, uri, decisions);
    }

    [Fact]
    public async Task WithoutRun_ShouldShowThePlanAndChangeNothing()
    {
        var (database, uri, decisions) = await ReviewedAsync();
        var before = await ResetTesting.Snapshot(database);
        var output = new StringWriter();

        var code = await App.RunAsync(["reset", "--decisions", decisions], ResetTesting.Environment(uri, "battery_pass_v3"), new StringReader("battery_pass_v3\n"), output, "test");

        Assert.Equal(App.Done, code);
        Assert.Equal(before, await ResetTesting.Snapshot(database));
        Assert.Contains("empties dpp_packs (3)", output.ToString(), StringComparison.Ordinal);
        Assert.Contains("removes passports (1)", output.ToString(), StringComparison.Ordinal);
        Assert.Contains("deletes 1 token(s): Sample read (sample-read-token)", output.ToString(), StringComparison.Ordinal);
        Assert.Contains("leaves ownerNotes (1) alone", output.ToString(), StringComparison.Ordinal);
        Assert.EndsWith("Dry run: nothing was changed." + Environment.NewLine, output.ToString(), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("")]
    [InlineData("yes")]
    [InlineData("battery_pass_demo")]
    public async Task TheRealRun_ShouldStopUnlessTheDatabaseNameIsTyped(string typed)
    {
        var (database, uri, decisions) = await ReviewedAsync();
        var before = await ResetTesting.Snapshot(database);
        var copy = ResetTesting.NewFolder();
        var output = new StringWriter();

        var code = await App.RunAsync(["reset", "--decisions", decisions, "--run", "--copy", copy], ResetTesting.Environment(uri, "battery_pass_v3"), new StringReader(typed + "\n"), output, "test");

        Assert.Equal(App.Stopped, code);
        Assert.Equal(before, await ResetTesting.Snapshot(database));
        Assert.False(Directory.Exists(copy));
        Assert.Contains("Stopped: nothing was changed.", output.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task TheRealRun_WithTheNameTyped_ShouldResetAndSayWhereTheCopyIs()
    {
        var (database, uri, decisions) = await ReviewedAsync();
        var copy = ResetTesting.NewFolder();
        var output = new StringWriter();

        var code = await App.RunAsync(["reset", "--decisions", decisions, "--run", "--copy", copy], ResetTesting.Environment(uri, "battery_pass_v3"), new StringReader("battery_pass_v3\n"), output, "test");

        Assert.Equal(App.Done, code);
        Assert.Equal(0, await ResetTesting.Count(database, "dpp_packs"));
        Assert.DoesNotContain("passports", await ResetTesting.CollectionNames(database));
        Assert.Equal(4, await ResetTesting.Count(database, "users"));
        Assert.Contains($"Safety copy of everything removed or changed: {copy}", output.ToString(), StringComparison.Ordinal);
        Assert.True(File.Exists(Path.Combine(copy, "dpp_packs.json")));
        Directory.Delete(copy, recursive: true);
    }

    [Fact]
    public async Task AMistakeInTheFile_ShouldStopBeforeAnything()
    {
        var (database, uri, decisions) = await ReviewedAsync(text => text.Replace("keep,account,demo@demo.test", "remove,account,demo@demo.test", StringComparison.Ordinal));
        var before = await ResetTesting.Snapshot(database);
        var output = new StringWriter();

        var code = await App.RunAsync(["reset", "--decisions", decisions, "--run", "--copy", ResetTesting.NewFolder()], ResetTesting.Environment(uri, "battery_pass_v3"), new StringReader("battery_pass_v3\n"), output, "test");

        Assert.Equal(App.Stopped, code);
        Assert.Equal(before, await ResetTesting.Snapshot(database));
        Assert.Contains("The decisions file needs a fix; nothing was changed:", output.ToString(), StringComparison.Ordinal);
        Assert.Contains("\"remove\" should be keep or delete", output.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task TheLiveDatabase_ShouldBeRefusedForAResetToo()
    {
        var output = new StringWriter();

        var code = await App.RunAsync(["reset", "--decisions", "x.csv", "--run", "--copy", "y"], ResetTesting.Environment("mongodb://127.0.0.1:1", "battery_pass_demo"), new StringReader("battery_pass_demo\n"), output, "test");

        Assert.Equal(App.Refused, code);
        Assert.Contains("battery_pass_demo is the live app's database", output.ToString(), StringComparison.Ordinal);
    }
}
