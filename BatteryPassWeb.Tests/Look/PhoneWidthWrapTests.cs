using BatteryPassWeb.Tests.Dpp;

namespace BatteryPassWeb.Tests.Look;

/// <summary>
/// Redesign R6b, the phone-width pass (3 Nov 2026): the battery pages' folded header facts kept one line and showed past
/// their card at 768 px ("Scania Industrial Batteries (demo)" ran 16 px off the screen), and a frozen passport's last
/// live values cut their date line at 375 px. Each wraps instead, with Bootstrap's own `text-wrap` (site.css unchanged).
/// </summary>
public sealed class PhoneWidthWrapTests
{
    private static string View(params string[] path) => File.ReadAllText(DppTestFiles.RepoFile(["web", "Views", .. path]));

    [Fact]
    public void TheFoldedHeaderFacts_ShouldWrap()
    {
        var team = View("Batteries", "Battery.cshtml");
        var cluster = View("ClusterAdmin", "Battery.cshtml");

        Assert.Contains("<div><dt>Batch</dt><dd class=\"text-wrap\" title=\"@Model.BatchLabel\">", team, StringComparison.Ordinal);
        Assert.Contains("<div><dt>Economic operator</dt><dd class=\"text-wrap\" title=\"@sources.Operator.Name\">", team, StringComparison.Ordinal);
        Assert.Contains("<dd class=\"text-wrap@(Model.Pack.ClusterId is null ? \" bp-batteries-muted\" : null)\">@Model.ClusterLabel</dd>", team, StringComparison.Ordinal);
        Assert.Contains("<div><dt>Batch</dt><dd class=\"text-wrap\" title=\"@Model.BatchLabel\">@Model.BatchLabel</dd></div>", cluster, StringComparison.Ordinal);
        Assert.Contains("<div><dt>Cluster</dt><dd class=\"text-wrap\" title=\"@Model.ClusterLabel\">@Model.ClusterLabel</dd></div>", cluster, StringComparison.Ordinal);
    }

    [Fact]
    public void AFrozenPassportsLastLiveValues_ShouldWrap() =>
        Assert.Contains("<dd class=\"text-wrap\">@string.Join(\", \", ValueWords.Lines(field.Form, stored.Value)", View("Batteries", "_LiveValuesCard.cshtml"), StringComparison.Ordinal);
}
