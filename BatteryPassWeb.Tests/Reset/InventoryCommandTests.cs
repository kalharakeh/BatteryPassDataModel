using System.Text;
using BatteryPassWeb.Tests.Dpp;
using V3Reset;

namespace BatteryPassWeb.Tests.Reset;

/// <summary>Step 3d-2: the tool works only on battery_pass_v3, and its inventory writes the review folder without changing anything.</summary>
[Collection(MongoCollection.Name)]
public sealed class InventoryCommandTests(MongoFixture mongo)
{
    [Theory]
    [InlineData("battery_pass_demo", "battery_pass_demo is the live app's database")]
    [InlineData("BATTERY_PASS_DEMO", "battery_pass_demo is the live app's database")]
    [InlineData("battery_pass_v3_copy", "works only on battery_pass_v3")]
    [InlineData("", "works only on battery_pass_v3")]
    public async Task AnyDatabaseButV3_ShouldBeRefused(string database, string message)
    {
        var output = new StringWriter();

        var code = await App.RunAsync(["inventory", "--out", ResetTesting.NewFolder()], ResetTesting.Environment("mongodb://127.0.0.1:1", database), new StringReader(string.Empty), output, "test");

        Assert.Equal(App.Refused, code);
        Assert.Contains(message, output.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task AnUnknownCommand_ShouldShowHowToUseTheTool()
    {
        var output = new StringWriter();

        var code = await App.RunAsync(["wipe"], ResetTesting.Environment("mongodb://127.0.0.1:1", "battery_pass_v3"), new StringReader(string.Empty), output, "test");

        Assert.Equal(App.Refused, code);
        Assert.Contains("Usage: V3Reset inventory --out <new folder>", output.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Inventory_ShouldWriteTheReviewFolderAndChangeNothing()
    {
        var (database, uri) = await ResetTesting.FreshV3Async(mongo);
        var before = await ResetTesting.Snapshot(database);
        var folder = ResetTesting.NewFolder();
        var output = new StringWriter();

        var code = await App.RunAsync(["inventory", "--out", folder], ResetTesting.Environment(uri, "battery_pass_v3"), new StringReader(string.Empty), output, "test");

        Assert.Equal(App.Done, code);
        Assert.Equal(before, await ResetTesting.Snapshot(database));
        var bytes = await File.ReadAllBytesAsync(Path.Combine(folder, "decisions.csv"));
        Assert.Equal([0xEF, 0xBB, 0xBF], bytes.Take(3));
        var rows = DecisionsFile.Read(Encoding.UTF8.GetString(bytes));
        Assert.Equal(4 + 2 + 5, rows.Count);
        Assert.Contains("ownerNotes", await File.ReadAllTextAsync(Path.Combine(folder, "inventory.txt")), StringComparison.Ordinal);
        Assert.Contains("left alone (not known to the app)", output.ToString(), StringComparison.Ordinal);
        Assert.Contains("Nothing was changed.", output.ToString(), StringComparison.Ordinal);
        Directory.Delete(folder, recursive: true);
    }

    [Fact]
    public async Task Inventory_ShouldNotWriteIntoAFolderThatIsInUse()
    {
        var (_, uri) = await ResetTesting.FreshV3Async(mongo);
        var folder = ResetTesting.NewFolder();
        Directory.CreateDirectory(folder);
        await File.WriteAllTextAsync(Path.Combine(folder, "decisions.csv"), "the owner's edits");

        var code = await App.RunAsync(["inventory", "--out", folder], ResetTesting.Environment(uri, "battery_pass_v3"), new StringReader(string.Empty), new StringWriter(), "test");

        Assert.Equal(App.Refused, code);
        Assert.Equal("the owner's edits", await File.ReadAllTextAsync(Path.Combine(folder, "decisions.csv")));
        Directory.Delete(folder, recursive: true);
    }
}
