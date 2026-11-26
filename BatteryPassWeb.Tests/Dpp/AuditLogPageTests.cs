namespace BatteryPassWeb.Tests.Dpp;

/// <summary>The audit log page: one line per event, grouped by day, opened for its details (mock-up approved 29 Oct 2026).</summary>
public sealed class AuditLogPageTests
{
    private static readonly string Page = File.ReadAllText(DppTestFiles.RepoFile("web", "Views", "Admin", "AuditLog.cshtml"));
    private static readonly string Line = File.ReadAllText(DppTestFiles.RepoFile("web", "Views", "Shared", "_AuditLine.cshtml"));
    private static readonly string Controller = File.ReadAllText(DppTestFiles.RepoFile("web", "Controllers", "AdminController.cs"));

    [Fact]
    public void ThePage_ShouldListTheDaysAndOfferMore()
    {
        Assert.Contains("@foreach (var day in Model.Days)", Page, StringComparison.Ordinal);
        Assert.Contains("_AuditLine", Page, StringComparison.Ordinal);
        Assert.Contains("Show 50 more", Page, StringComparison.Ordinal);
        Assert.Contains("bp-product-data.css", Page, StringComparison.Ordinal);
        Assert.Contains("AuditLines.ByDay", Controller, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("q")]
    [InlineData("entityType")]
    [InlineData("actor")]
    [InlineData("from")]
    [InlineData("to")]
    [InlineData("category")]
    [InlineData("source")]
    public void EveryFilter_ShouldStayAvailable(string name)
    {
        Assert.Contains($"name=\"{name}\"", Page, StringComparison.Ordinal);
    }

    [Fact]
    public void ALine_ShouldOpenWithoutScriptAndKeepTheRawEntry()
    {
        Assert.Contains("<details class=\"bp-auditlog-item", Line, StringComparison.Ordinal);
        Assert.Contains("<summary>", Line, StringComparison.Ordinal);
        Assert.Contains("Technical details", Line, StringComparison.Ordinal);
        Assert.DoesNotContain("<script", Line, StringComparison.Ordinal);
    }
}
