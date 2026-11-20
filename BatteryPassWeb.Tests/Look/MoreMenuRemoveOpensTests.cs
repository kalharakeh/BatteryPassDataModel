using BatteryPassWeb.Tests.Dpp;

namespace BatteryPassWeb.Tests.Look;

/// <summary>
/// The More menu's "Remove this draft battery…" scrolled to the Remove card but left it closed. Its link now also asks
/// for the card open (?open=remove), so the Remove button and its confirm window are one click away.
/// </summary>
public sealed class MoreMenuRemoveOpensTests
{
    [Fact]
    public void RemoveThisDraftBattery_ShouldOpenTheRemoveCard()
    {
        var page = File.ReadAllText(DppTestFiles.RepoFile("web", "Views", "Batteries", "Battery.cshtml"));

        Assert.Contains("<a href=\"@href?section=overview&open=remove#remove\" class=\"is-danger\">", page, StringComparison.Ordinal);
        Assert.Contains("id=\"remove\" open=\"@(card == \"remove\" || Context.Request.Query[\"open\"] == \"remove\")\">", page, StringComparison.Ordinal);
    }
}
