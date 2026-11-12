using BatteryPassWeb.Services;

namespace V3Reset;

public sealed record RoleChange(string Email, string From, IReadOnlyList<string> To);

/// <summary>What a reset will do, worked out from the database as it is now and the owner's decisions.</summary>
public sealed record ResetPlan(
    IReadOnlyList<CollectionCount> Emptied,
    IReadOnlyList<CollectionCount> Removed,
    IReadOnlyList<CollectionCount> LeftAlone,
    IReadOnlyList<string> AccountsDeleted,
    IReadOnlyList<RoleChange> RoleChanges,
    IReadOnlyList<string> ClustersDeleted,
    IReadOnlyList<string> TokensDeleted,
    IReadOnlyList<string> Notes);

/// <summary>Checks the decisions against the database and turns them into a plan; any mistake stops the reset.</summary>
public static class ResetPlanner
{
    /// <summary>The roles an account can be given (design 5.2): the team, members and the app-wide reader roles.</summary>
    public static readonly IReadOnlyList<string> SystemRoles =
    [
        AccessControlService.RoleAdmin,
        AccessControlService.RoleNormalUser,
        AccessControlService.RoleNotifiedBody,
        AccessControlService.RoleMarketSurveillanceAuthority,
        AccessControlService.RoleCommission,
        AccessControlService.RoleLiRepair,
        AccessControlService.RoleLiRecycling,
        AccessControlService.RoleLiTreatment,
    ];

    public static (ResetPlan? Plan, IReadOnlyList<string> Errors) Build(InventoryReport inventory, IReadOnlyList<DecisionRow> rows)
    {
        var errors = new List<string>();
        var notes = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var accountsDeleted = new List<string>();
        var roleChanges = new List<RoleChange>();
        var clustersDeleted = new List<string>();
        var tokensDeleted = new List<string>();

        foreach (var row in rows)
        {
            var where = $"Line {row.Line} ({row.Kind} {row.Id})";
            if (row.Decision is not (DecisionsFile.Keep or DecisionsFile.Delete))
            {
                errors.Add($"{where}: \"{row.Decision}\" should be keep or delete.");
                continue;
            }

            if (!seen.Add($"{row.Kind}|{row.Id}"))
            {
                errors.Add($"{where}: this {row.Kind} has more than one row.");
                continue;
            }

            switch (row.Kind)
            {
                case DecisionsFile.Account:
                    var account = inventory.Accounts.FirstOrDefault(found => found.Email.Equals(row.Id, StringComparison.OrdinalIgnoreCase));
                    if (account is null)
                    {
                        errors.Add($"{where}: there is no such account.");
                    }
                    else if (row.Decision == DecisionsFile.Delete)
                    {
                        accountsDeleted.Add(account.Email);
                    }
                    else if (!SameRoles(row.Role, account.Roles))
                    {
                        var roles = Split(row.Role);
                        var unknown = roles.Where(role => !SystemRoles.Contains(role, StringComparer.Ordinal)).ToList();
                        if (roles.Count == 0 || unknown.Count > 0)
                        {
                            errors.Add($"{where}: \"{row.Role}\" isn't a role. Use {string.Join(", ", SystemRoles)}, or several joined with +.");
                        }
                        else
                        {
                            roleChanges.Add(new RoleChange(account.Email, account.Roles, roles));
                        }
                    }

                    break;
                case DecisionsFile.Cluster:
                    if (inventory.Clusters.All(cluster => cluster.ClusterId != row.Id))
                    {
                        errors.Add($"{where}: there is no such cluster.");
                    }
                    else if (row.Decision == DecisionsFile.Delete)
                    {
                        clustersDeleted.Add(row.Id);
                    }

                    break;
                case DecisionsFile.Token:
                    var token = inventory.Tokens.FirstOrDefault(found => found.TokenId == row.Id);
                    if (token is null)
                    {
                        errors.Add($"{where}: there is no such token.");
                    }
                    else if (row.Decision == DecisionsFile.Delete)
                    {
                        tokensDeleted.Add(token.TokenId);
                    }
                    else if (token.IsSample)
                    {
                        notes.Add($"Token {token.TokenId} is a sample token: it is removed anyway.");
                    }

                    break;
                default:
                    errors.Add($"{where}: \"{row.Kind}\" should be account, cluster or token.");
                    break;
            }
        }

        foreach (var account in inventory.Accounts.Where(account => !seen.Contains($"{DecisionsFile.Account}|{account.Email}")))
        {
            notes.Add($"Account {account.Email} has no row in the decisions file: it stays as it is.");
        }

        foreach (var cluster in inventory.Clusters.Where(cluster => !seen.Contains($"{DecisionsFile.Cluster}|{cluster.ClusterId}")))
        {
            notes.Add($"Cluster {cluster.ClusterId} has no row in the decisions file: it stays as it is.");
        }

        var adminsLeft = inventory.Accounts
            .Where(account => !accountsDeleted.Contains(account.Email))
            .Count(account => (roleChanges.FirstOrDefault(change => change.Email == account.Email)?.To ?? Split(account.Roles)).Contains(AccessControlService.RoleAdmin));
        if (adminsLeft == 0)
        {
            errors.Add("No account would have the admin role. Keep one, or give one the role admin in the role column; otherwise nobody can manage the app.");
        }

        tokensDeleted.AddRange(inventory.Tokens
            .Where(token => token.IsSample || clustersDeleted.Any(clusterId => Inventory.BelongsTo(token, clusterId)))
            .Select(token => token.TokenId)
            .Where(tokenId => !tokensDeleted.Contains(tokenId)));

        if (errors.Count > 0)
        {
            return (null, errors);
        }

        return (new ResetPlan(
            inventory.Collections.Where(collection => collection.Fate is CollectionFate.NewTestData or CollectionFate.Cleared).ToList(),
            inventory.Collections.Where(collection => collection.Fate == CollectionFate.OldData).ToList(),
            inventory.Collections.Where(collection => collection.Fate == CollectionFate.Unknown).ToList(),
            accountsDeleted,
            roleChanges,
            clustersDeleted,
            tokensDeleted,
            notes), errors);
    }

    private static List<string> Split(string roles) =>
        roles.Split('+', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).ToList();

    private static bool SameRoles(string a, string b) =>
        Split(a).Order(StringComparer.Ordinal).SequenceEqual(Split(b).Order(StringComparer.Ordinal), StringComparer.Ordinal);
}
