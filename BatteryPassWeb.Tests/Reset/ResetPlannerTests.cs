using V3Reset;

namespace BatteryPassWeb.Tests.Reset;

/// <summary>Step 3d-2: the owner's decisions are checked against the database before anything changes.</summary>
public sealed class ResetPlannerTests
{
    private static readonly InventoryReport Inventory = new(
        [
            new CollectionCount("dpp_packs", 3, CollectionFate.NewTestData),
            new CollectionCount("passwordResetRequests", 1, CollectionFate.Cleared),
            new CollectionCount("passports", 2, CollectionFate.OldData),
            new CollectionCount("users", 3, CollectionFate.Reviewed),
            new CollectionCount("auditEvents", 9, CollectionFate.Kept),
            new CollectionCount("ownerNotes", 1, CollectionFate.Unknown),
        ],
        [
            new AccountRow("admin@team.test", "Team Admin", "admin", string.Empty, string.Empty),
            new AccountRow("north@north.test", "North", "member", "north (clusterAdmin)", string.Empty),
            new AccountRow("old-li@repair.test", "Old LI", "legitimateInterest", string.Empty, string.Empty),
        ],
        [new ClusterRow("north", "Customer North", 1, 1, 2), new ClusterRow("demo-cluster", "Demo", 0, 2, 1)],
        [
            new TokenRow("sample-read-token", "Sample", "read", null, [], false, true, string.Empty),
            new TokenRow("north-read", "North read", "read", "north", [], false, false, string.Empty),
            new TokenRow("demo-read", "Demo read", "read", "demo-cluster", [], false, false, string.Empty),
            new TokenRow("demo-listed", "Demo listed", "readWrite", null, ["demo-cluster"], false, false, string.Empty),
            new TokenRow("team-full", "Team", "readWriteSign", null, [], true, false, string.Empty),
        ]);

    private static List<DecisionRow> Defaults() => DecisionsFile.Defaults(Inventory).Select((row, index) => row with { Line = index + 2 }).ToList();

    private static List<DecisionRow> With(string kind, string id, string decision, string? role = null) =>
        Defaults().Select(row => row.Kind == kind && row.Id == id ? row with { Decision = decision, Role = role ?? row.Role } : row).ToList();

    [Fact]
    public void TheDefaults_ShouldOnlyRemoveDataAndTheSampleTokens()
    {
        var (plan, errors) = ResetPlanner.Build(Inventory, Defaults());

        Assert.Empty(errors);
        Assert.Equal(["dpp_packs", "passwordResetRequests"], plan!.Emptied.Select(collection => collection.Name));
        Assert.Equal(["passports"], plan.Removed.Select(collection => collection.Name));
        Assert.Equal(["ownerNotes"], plan.LeftAlone.Select(collection => collection.Name));
        Assert.Empty(plan.AccountsDeleted);
        Assert.Empty(plan.RoleChanges);
        Assert.Empty(plan.ClustersDeleted);
        Assert.Equal(["sample-read-token"], plan.TokensDeleted);
    }

    [Fact]
    public void DeletingACluster_ShouldTakeTheTokensThatBelongOnlyToIt()
    {
        var (plan, errors) = ResetPlanner.Build(Inventory, With("cluster", "demo-cluster", "delete"));

        Assert.Empty(errors);
        Assert.Equal(["demo-cluster"], plan!.ClustersDeleted);
        Assert.Equal(["demo-listed", "demo-read", "sample-read-token"], plan.TokensDeleted.Order(StringComparer.Ordinal));
    }

    [Fact]
    public void AnAccountMarkedDelete_ShouldBeDeleted()
    {
        var (plan, _) = ResetPlanner.Build(Inventory, With("account", "old-li@repair.test", "delete"));

        Assert.Equal(["old-li@repair.test"], plan!.AccountsDeleted);
    }

    [Fact]
    public void ANewRole_ShouldBeGivenAndAnUnchangedOldRoleLeftAsItIs()
    {
        var (plan, errors) = ResetPlanner.Build(Inventory, With("account", "north@north.test", "keep", "liRepair + member"));

        Assert.Empty(errors);
        var change = Assert.Single(plan!.RoleChanges);
        Assert.Equal(("north@north.test", "member"), (change.Email, change.From));
        Assert.Equal(["liRepair", "member"], change.To);
    }

    [Theory]
    [InlineData("account", "north@north.test", "remove", null, "\"remove\" should be keep or delete")]
    [InlineData("account", "north@north.test", "keep", "legitimateInterest", "isn't a role")]
    [InlineData("account", "north@north.test", "keep", "boss", "isn't a role")]
    [InlineData("account", "admin@team.test", "delete", null, "No account would have the admin role")]
    [InlineData("account", "admin@team.test", "keep", "member", "No account would have the admin role")]
    public void AMistake_ShouldStopTheReset(string kind, string id, string decision, string? role, string message)
    {
        var (plan, errors) = ResetPlanner.Build(Inventory, With(kind, id, decision, role));

        Assert.Null(plan);
        Assert.Contains(errors, error => error.Contains(message, StringComparison.Ordinal));
    }

    [Fact]
    public void ARowForSomethingThatIsntThere_ShouldStopTheReset()
    {
        var rows = Defaults();
        rows.Add(new DecisionRow("delete", "account", "nobody@x.test", string.Empty, string.Empty, string.Empty, 99));
        rows.Add(new DecisionRow("delete", "cluster", "north", string.Empty, string.Empty, string.Empty, 100));

        var (plan, errors) = ResetPlanner.Build(Inventory, rows);

        Assert.Null(plan);
        Assert.Equal(["Line 99 (account nobody@x.test): there is no such account.", "Line 100 (cluster north): this cluster has more than one row."], errors);
    }

    [Fact]
    public void WhatHasNoRow_ShouldStayAsItIs()
    {
        var rows = Defaults().Where(row => row.Id is not ("north@north.test" or "north")).ToList();

        var (plan, errors) = ResetPlanner.Build(Inventory, rows);

        Assert.Empty(errors);
        Assert.Empty(plan!.AccountsDeleted);
        Assert.Contains("Account north@north.test has no row in the decisions file: it stays as it is.", plan.Notes);
        Assert.Contains("Cluster north has no row in the decisions file: it stays as it is.", plan.Notes);
    }

    [Fact]
    public void ASampleTokenMarkedKeep_ShouldStillGo()
    {
        var (plan, _) = ResetPlanner.Build(Inventory, With("token", "sample-read-token", "keep"));

        Assert.Contains("sample-read-token", plan!.TokensDeleted);
        Assert.Contains("Token sample-read-token is a sample token: it is removed anyway.", plan.Notes);
    }
}
