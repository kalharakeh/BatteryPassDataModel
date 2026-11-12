using BatteryPassWeb.Tests.Dpp;

namespace BatteryPassWeb.Tests.Look;

/// <summary>
/// Redesign R6, the owner's choice 8 (3 Nov 2026): the audit log's filter still offered "Old passports", "Old batteries"
/// and "Old passport files", leftovers of the old model whose data was reset (decision 1); they're gone.
/// </summary>
public sealed class AuditFilterOldEntriesTests
{
    [Fact]
    public void TheFilter_ShouldNotOfferTheOldModelsEntries()
    {
        var view = File.ReadAllText(DppTestFiles.RepoFile("web", "Views", "Admin", "AuditLog.cshtml"));

        Assert.DoesNotContain("\"Old passports\"", view, StringComparison.Ordinal);
        Assert.DoesNotContain("\"Old batteries\"", view, StringComparison.Ordinal);
        Assert.DoesNotContain("\"Old passport files\"", view, StringComparison.Ordinal);
        Assert.Contains("@Option(\"dpp-pack\", \"Batteries\", filter.EntityType)", view, StringComparison.Ordinal);
    }
}
