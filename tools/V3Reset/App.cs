using System.Text;
using BatteryPassWeb.Configuration;
using BatteryPassWeb.Services;
using Microsoft.Extensions.Options;

namespace V3Reset;

/// <summary>
/// The tool's commands. <c>inventory</c> only reads and writes the review folder. <c>reset</c> is a dry run unless
/// given <c>--run</c>, and then asks for the database name typed out before it changes anything. Both work only on
/// battery_pass_v3 (design 14, 3d-2).
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

        if (command is not ("inventory" or "reset"))
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
            return command == "inventory"
                ? await InventoryAsync(context, Option("--out"), output)
                : await ResetAsync(context, args, Option("--decisions"), Option("--copy"), input, output, actor);
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

    private static async Task<int> ResetAsync(MongoContext context, string[] args, string? decisionsPath, string? copy, TextReader input, TextWriter output, string actor)
    {
        if (context.Database is null)
        {
            await output.WriteLineAsync($"Can't connect: {context.ConnectionError?.Message}");
            return Refused;
        }

        if (decisionsPath is null || !File.Exists(decisionsPath))
        {
            await output.WriteLineAsync("Give the decisions file from the inventory: --decisions <decisions.csv>.");
            return Refused;
        }

        IReadOnlyList<DecisionRow> rows;
        try
        {
            rows = DecisionsFile.Read(await File.ReadAllTextAsync(decisionsPath));
        }
        catch (FormatException exception)
        {
            await output.WriteLineAsync(exception.Message);
            return Stopped;
        }

        var inventory = await Inventory.ReadAsync(context.Database);
        var (plan, errors) = ResetPlanner.Build(inventory, rows);
        if (plan is null)
        {
            await output.WriteLineAsync("The decisions file needs a fix; nothing was changed:");
            foreach (var error in errors)
            {
                await output.WriteLineAsync($"  - {error}");
            }

            return Stopped;
        }

        await output.WriteAsync(DescribePlan(plan, inventory));
        if (!args.Contains("--run"))
        {
            await output.WriteLineAsync("Dry run: nothing was changed.");
            return Done;
        }

        if (copy is null)
        {
            await output.WriteLineAsync("Give a new folder for the safety copy: --copy <folder>.");
            return Refused;
        }

        await output.WriteLineAsync($"Type {ResetCollections.Database} to make these changes, or anything else to stop:");
        if ((await input.ReadLineAsync())?.Trim() != ResetCollections.Database)
        {
            await output.WriteLineAsync("Stopped: nothing was changed.");
            return Stopped;
        }

        await ResetRunner.RunAsync(context, plan, copy, actor);
        await output.WriteLineAsync($"Done. The audit log has a \"{ResetRunner.AuditEventType}\" entry with the counts.");
        await output.WriteLineAsync($"Safety copy of everything removed or changed: {copy}");
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

    private static string DescribePlan(ResetPlan plan, InventoryReport inventory)
    {
        string TokenName(string tokenId) =>
            inventory.Tokens.FirstOrDefault(token => token.TokenId == tokenId) is { } token && token.Name != tokenId ? $"{token.Name} ({tokenId})" : tokenId;
        var text = new StringBuilder();
        text.AppendLine($"What the reset does to {ResetCollections.Database}:");
        foreach (var collection in plan.Emptied)
        {
            text.AppendLine($"  empties {collection.Name} ({collection.Count})");
        }

        foreach (var collection in plan.Removed)
        {
            text.AppendLine($"  removes {collection.Name} ({collection.Count})");
        }

        text.AppendLine($"  deletes {plan.AccountsDeleted.Count} account(s){List(plan.AccountsDeleted)}");
        foreach (var change in plan.RoleChanges)
        {
            text.AppendLine($"  changes {change.Email}'s role from {(change.From.Length == 0 ? "none" : change.From)} to {string.Join(" + ", change.To)}");
        }

        text.AppendLine($"  deletes {plan.ClustersDeleted.Count} cluster(s){List(plan.ClustersDeleted)}, with their memberships and tokens");
        text.AppendLine($"  deletes {plan.TokensDeleted.Count} token(s){List(plan.TokensDeleted.Select(TokenName).ToList())}");
        foreach (var collection in plan.LeftAlone)
        {
            text.AppendLine($"  leaves {collection.Name} ({collection.Count}) alone: the app doesn't know it");
        }

        text.AppendLine("  keeps everything else: accounts, clusters and tokens not marked delete, the settings and the audit log");
        foreach (var note in plan.Notes)
        {
            text.AppendLine($"  note: {note}");
        }

        return text.ToString();
    }

    private static string List(IReadOnlyList<string> items) => items.Count == 0 ? string.Empty : $": {string.Join(", ", items)}";
}
