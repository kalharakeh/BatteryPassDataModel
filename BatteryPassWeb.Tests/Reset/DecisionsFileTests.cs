using V3Reset;

namespace BatteryPassWeb.Tests.Reset;

/// <summary>Step 3d-2: the decisions file, one row per account, cluster and token, easy to change on a phone.</summary>
public sealed class DecisionsFileTests
{
    private static readonly InventoryReport Inventory = new(
        [],
        [new AccountRow("ann@team.test", "Ann, Team", "admin", "north (clusterAdmin)", "2026-10-01T08:00:00Z")],
        [new ClusterRow("north", "Customer North", 2, 1, 4)],
        [
            new TokenRow("sample-read-token", "Sample read", "read", null, [], false, true, ""),
            new TokenRow("north-read", "North \"read\"", "read", "north", [], false, false, "2026-10-29T10:00:00Z"),
        ]);

    [Fact]
    public void Defaults_ShouldKeepEverythingButTheSampleTokens()
    {
        var rows = DecisionsFile.Defaults(Inventory);

        Assert.Equal(["keep account ann@team.test", "keep cluster north", "delete token sample-read-token", "keep token north-read"], rows.Select(row => $"{row.Decision} {row.Kind} {row.Id}"));
        Assert.Equal("admin", rows[0].Role);
        Assert.Equal("clusters: north (clusterAdmin); created 2026-10-01", rows[0].Details);
        Assert.Equal("2 members, 1 tokens; its 4 batteries go with the reset", rows[1].Details);
        Assert.Equal("sample token: always removed", rows[2].Details);
        Assert.Equal("read, cluster north, last used 2026-10-29", rows[3].Details);
    }

    [Fact]
    public void AnOldLegitimateInterestAccount_ShouldBePointedOut()
    {
        var inventory = Inventory with { Accounts = [new AccountRow("li@repair.test", "Old LI", "legitimateInterest", string.Empty, "2026-09-01T08:00:00Z")] };

        var row = DecisionsFile.Defaults(inventory)[0];

        Assert.Equal(("keep", "legitimateInterest"), (row.Decision, row.Role));
        Assert.Equal("old role legitimateInterest: public data only; give a new role or delete; no clusters; created 2026-09-01", row.Details);
    }

    [Fact]
    public void TheFile_ShouldStartWithTheDecisionColumn()
    {
        var text = DecisionsFile.Write(DecisionsFile.Defaults(Inventory));

        Assert.StartsWith("decision,kind,id,name,role,details\r\nkeep,account,ann@team.test,\"Ann, Team\",admin,", text, StringComparison.Ordinal);
    }

    [Fact]
    public void WritingThenReading_ShouldGiveTheSameRows()
    {
        var rows = DecisionsFile.Defaults(Inventory);

        var read = DecisionsFile.Read(DecisionsFile.Write(rows));

        Assert.Equal(rows.Select(row => row with { Line = 0 }), read.Select(row => row with { Line = 0 }));
        Assert.Equal([2, 3, 4, 5], read.Select(row => row.Line));
    }

    [Fact]
    public void ExcelsSemicolonsAndByteOrderMark_ShouldBeRead()
    {
        var text = "﻿decision;kind;id;name;role;details\r\nDelete ;account;ann@team.test;Ann, Team;admin;x\r\n\r\n";

        var row = Assert.Single(DecisionsFile.Read(text));

        Assert.Equal(("delete", "account", "ann@team.test", "Ann, Team"), (row.Decision, row.Kind, row.Id, row.Name));
    }

    [Fact]
    public void AFileWithoutTheColumns_ShouldBeRefused()
    {
        var error = Assert.Throws<FormatException>(() => DecisionsFile.Read("keep,account,ann@team.test\r\n"));

        Assert.Contains("Start again from the file the inventory wrote", error.Message, StringComparison.Ordinal);
    }
}
