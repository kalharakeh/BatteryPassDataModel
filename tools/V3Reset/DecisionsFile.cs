using System.Text;

namespace V3Reset;

/// <summary>One row of the decisions file: an account, a cluster or a token, and whether it stays.</summary>
public sealed record DecisionRow(string Decision, string Kind, string Id, string Name, string Role, string Details, int Line = 0);

/// <summary>
/// The owner's decisions file: a CSV with one row per account, cluster and token, the default already filled in.
/// It opens in Excel or on a phone; the owner changes "keep" to "delete", or an account's role. Reading accepts the
/// commas this tool writes and the semicolons Excel writes in some countries.
/// </summary>
public static class DecisionsFile
{
    public const string Keep = "keep";
    public const string Delete = "delete";
    public const string Account = "account";
    public const string Cluster = "cluster";
    public const string Token = "token";

    /// <summary>The v1.2 role the three LI roles replaced; the app gives it public data only (3a).</summary>
    public const string LegacyRole = "legitimateInterest";

    private static readonly string[] Columns = ["decision", "kind", "id", "name", "role", "details"];

    /// <summary>The rows for everything the inventory found, with the defaults filled in.</summary>
    public static IReadOnlyList<DecisionRow> Defaults(InventoryReport inventory)
    {
        var rows = new List<DecisionRow>();
        rows.AddRange(inventory.Accounts.Select(account => new DecisionRow(
            Keep,
            Account,
            account.Email,
            account.Name,
            account.Roles,
            (account.Roles.Contains(LegacyRole, StringComparison.Ordinal) ? $"old role {LegacyRole}: public data only; give a new role or delete; " : string.Empty)
            + $"{(account.Memberships.Length == 0 ? "no clusters" : "clusters: " + account.Memberships)}; created {Day(account.CreatedAt)}")));
        rows.AddRange(inventory.Clusters.Select(cluster => new DecisionRow(
            Keep,
            Cluster,
            cluster.ClusterId,
            cluster.Name,
            string.Empty,
            $"{cluster.Members} members, {cluster.Tokens} tokens; its {cluster.Batteries} batteries go with the reset")));
        rows.AddRange(inventory.Tokens.Select(token => new DecisionRow(
            token.IsSample ? Delete : Keep,
            Token,
            token.TokenId,
            token.Name,
            string.Empty,
            token.IsSample
                ? "sample token: always removed"
                : $"{token.AccessMode}, {Reach(token)}, last used {(token.LastUsedAt.Length == 0 ? "never" : Day(token.LastUsedAt))}")));
        return rows;
    }

    /// <summary>The file's text: a header and one line per row, separated by commas.</summary>
    public static string Write(IEnumerable<DecisionRow> rows)
    {
        var text = new StringBuilder();
        text.Append(string.Join(',', Columns)).Append("\r\n");
        foreach (var row in rows)
        {
            text.Append(string.Join(',', new[] { row.Decision, row.Kind, row.Id, row.Name, row.Role, row.Details }.Select(Quote))).Append("\r\n");
        }

        return text.ToString();
    }

    /// <summary>Reads the file back; a missing column or a line that can't be read is an error.</summary>
    public static IReadOnlyList<DecisionRow> Read(string text)
    {
        var lines = Records(text.TrimStart('﻿')).ToList();
        if (lines.Count > 0 && lines[0].Fields.Count == 1 && lines[0].Fields[0].StartsWith("sep=", StringComparison.OrdinalIgnoreCase))
        {
            lines.RemoveAt(0);
        }

        if (lines.Count == 0)
        {
            throw new FormatException("The decisions file is empty.");
        }

        var header = lines[0].Fields.Select(field => field.Trim().ToLowerInvariant()).ToList();
        var missing = Columns.Where(column => !header.Contains(column)).ToList();
        if (missing.Count > 0)
        {
            throw new FormatException($"The decisions file has no {string.Join(", ", missing)} column. Start again from the file the inventory wrote.");
        }

        string Field(IReadOnlyList<string> fields, string column)
        {
            var index = header.IndexOf(column);
            return index < fields.Count ? fields[index].Trim() : string.Empty;
        }

        return lines.Skip(1)
            .Where(line => line.Fields.Any(field => field.Trim().Length > 0))
            .Select(line => new DecisionRow(
                Field(line.Fields, "decision").ToLowerInvariant(),
                Field(line.Fields, "kind").ToLowerInvariant(),
                Field(line.Fields, "id"),
                Field(line.Fields, "name"),
                Field(line.Fields, "role"),
                Field(line.Fields, "details"),
                line.Number))
            .ToList();
    }

    private static string Reach(TokenRow token) =>
        token.GlobalAccess ? "all clusters (team)"
        : token.AutoClusterId is not null ? $"cluster {token.AutoClusterId}"
        : token.ClusterIds.Count == 0 ? "no clusters"
        : $"clusters {string.Join(" ", token.ClusterIds)}";

    private static string Day(string timestamp) => timestamp.Length >= 10 ? timestamp[..10] : timestamp;

    private static string Quote(string field) =>
        field.IndexOfAny([',', ';', '"', '\r', '\n']) >= 0 ? $"\"{field.Replace("\"", "\"\"")}\"" : field;

    /// <summary>Splits CSV text into records, with quoted fields; the separator is the header line's comma or semicolon.</summary>
    private static IEnumerable<(int Number, IReadOnlyList<string> Fields)> Records(string text)
    {
        var firstLine = text.Split('\n')[0];
        var separator = firstLine.Count(c => c == ';') > firstLine.Count(c => c == ',') ? ';' : ',';
        var fields = new List<string>();
        var field = new StringBuilder();
        var quoted = false;
        var line = 1;
        var start = 1;
        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            if (quoted)
            {
                if (c == '"' && i + 1 < text.Length && text[i + 1] == '"')
                {
                    field.Append('"');
                    i++;
                }
                else if (c == '"')
                {
                    quoted = false;
                }
                else
                {
                    if (c == '\n') line++;
                    field.Append(c);
                }
            }
            else if (c == '"')
            {
                quoted = true;
            }
            else if (c == separator)
            {
                fields.Add(field.ToString());
                field.Clear();
            }
            else if (c == '\n')
            {
                fields.Add(field.ToString().TrimEnd('\r'));
                yield return (start, fields);
                fields = [];
                field.Clear();
                line++;
                start = line;
            }
            else
            {
                field.Append(c);
            }
        }

        if (field.Length > 0 || fields.Count > 0)
        {
            fields.Add(field.ToString().TrimEnd('\r'));
            yield return (start, fields);
        }
    }
}
