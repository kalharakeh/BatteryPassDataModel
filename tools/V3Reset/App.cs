using System.Text;
using BatteryPassWeb.Configuration;
using BatteryPassWeb.Services;
using Microsoft.Extensions.Options;

namespace V3Reset;

/// <summary>
/// The tool's command <c>inventory</c>: it only reads, and writes the review folder. It works only on battery_pass_v3
/// (design 14, 3d-2).
/// </summary>
public static class App
{
    public const int Done = 0;
    public const int Stopped = 1;
    public const int Refused = 2;

    private const string Usage =
        "Usage: V3Reset inventory --out <new folder>\n" +
        "       V3Reset reset --decisions <decisions.csv> [--run --copy <new folder>]";

    public static async Task<int> RunAsync(string[] args, IReadOnlyDictionary<string, string?> environment, TextReader input, TextWriter output, string actor)
    {
        var command = args.FirstOrDefault() ?? string.Empty;
        string? Option(string name)
        {
            var index = Array.IndexOf(args, name);
            return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
        }

        if (command != "inventory")
        {
            await output.WriteLineAsync(Usage);
            return Refused;
        }

        var databaseName = environment.GetValueOrDefault("MONGODB_DB")?.Trim() ?? string.Empty;
        var uri = environment.GetValueOrDefault("MONGODB_URI") ?? string.Empty;
        if (databaseName.Equals(ResetCollections.LiveDatabase, StringComparison.OrdinalIgnoreCase))
        {
            await output.WriteLineAsync($"Refused: {ResetCollections.LiveDatabase} is the live app's database. This tool works only on {ResetCollections.Database}.");
            return Refused;
        }

        if (databaseName != ResetCollections.Database)
        {
            await output.WriteLineAsync($"Refused: MONGODB_DB is \"{databaseName}\". This tool works only on {ResetCollections.Database}.");
            return Refused;
        }

        if (string.IsNullOrWhiteSpace(uri))
        {
            await output.WriteLineAsync("Refused: MONGODB_URI is not set.");
            return Refused;
        }

        var context = new MongoContext(Options.Create(new BatteryPassOptions { MongoDbUri = uri, MongoDbName = databaseName }));
        try
        {
            return await InventoryAsync(context, Option("--out"), output);
        }
        catch (Exception exception) when (exception is TimeoutException or MongoDB.Driver.MongoException)
        {
            await output.WriteLineAsync($"Can't reach {ResetCollections.Database}: {exception.Message.Split('\n')[0]}");
            await output.WriteLineAsync("If this PC's internet address changed, add it in Atlas under Security > Network Access, then try again.");
            return Refused;
        }
    }

    private static async Task<int> InventoryAsync(MongoContext context, string? folder, TextWriter output)
    {
        if (context.Database is null)
        {
            await output.WriteLineAsync($"Can't connect: {context.ConnectionError?.Message}");
            return Refused;
        }

        if (folder is null || (Directory.Exists(folder) && Directory.EnumerateFileSystemEntries(folder).Any()))
        {
            await output.WriteLineAsync("Give a new, empty folder for the review: --out <folder>.");
            return Refused;
        }

        var inventory = await Inventory.ReadAsync(context.Database);
        Directory.CreateDirectory(folder);
        var decisions = Path.Combine(folder, "decisions.csv");
        await File.WriteAllTextAsync(decisions, DecisionsFile.Write(DecisionsFile.Defaults(inventory)), new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
        var listing = DescribeInventory(inventory);
        await File.WriteAllTextAsync(Path.Combine(folder, "inventory.txt"), listing);
        await output.WriteAsync(listing);
        await output.WriteLineAsync($"Nothing was changed. Your decisions file: {decisions}");
        await output.WriteLineAsync("In it, change \"keep\" to \"delete\" for what should go, or an account's role; then run reset with that file for a dry run.");
        return Done;
    }

    private static string DescribeInventory(InventoryReport inventory)
    {
        var text = new StringBuilder();
        text.AppendLine($"{ResetCollections.Database}: what is in it and what a reset would do");
        text.AppendLine();
        foreach (var collection in inventory.Collections)
        {
            text.AppendLine($"  {collection.Name,-34} {collection.Count,7}  {ResetCollections.Describe(collection.Fate)}");
        }

        text.AppendLine();
        text.AppendLine($"Accounts ({inventory.Accounts.Count}):");
        foreach (var account in inventory.Accounts)
        {
            text.AppendLine($"  {account.Email}  {account.Name}  role: {account.Roles}  {(account.Memberships.Length == 0 ? "no clusters" : account.Memberships)}");
        }

        text.AppendLine($"Clusters ({inventory.Clusters.Count}):");
        foreach (var cluster in inventory.Clusters)
        {
            text.AppendLine($"  {cluster.ClusterId}  {cluster.Name}  {cluster.Members} members, {cluster.Tokens} tokens, {cluster.Batteries} batteries");
        }

        text.AppendLine($"API tokens ({inventory.Tokens.Count}; values and hashes are never shown):");
        foreach (var token in inventory.Tokens)
        {
            text.AppendLine($"  {token.TokenId}  {token.Name}  {token.AccessMode}{(token.IsSample ? "  sample: always removed" : string.Empty)}");
        }

        text.AppendLine();
        return text.ToString();
    }
}
