using BatteryPassWeb.Models.Demo;

namespace BatteryPassWeb.Tests;

public sealed class DemoScenarioResetServiceTests
{
    [Fact]
    public void ScenarioCatalog_ShouldExposeEveryPhase6ADemoState()
    {
        var scenarios = DemoScenarioCatalog.All;

        Assert.Contains(scenarios, scenario => scenario.Key == DemoScenarioKey.PublishedTrusted);
        Assert.Contains(scenarios, scenario => scenario.Key == DemoScenarioKey.DraftIncomplete);
        Assert.Contains(scenarios, scenario => scenario.Key == DemoScenarioKey.ReadyToSign);
        Assert.Contains(scenarios, scenario => scenario.Key == DemoScenarioKey.SignedUnpublished);
        Assert.Contains(scenarios, scenario => scenario.Key == DemoScenarioKey.DirtyAfterEdit);
        Assert.Contains(scenarios, scenario => scenario.Key == DemoScenarioKey.InvalidSignature);
        Assert.Contains(scenarios, scenario => scenario.Key == DemoScenarioKey.RestrictedDocument);
        Assert.Equal(7, scenarios.Select(scenario => scenario.PassportId).Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }

    [Fact]
    public void ScenarioCatalog_ShouldRefuseUnknownPassportIds()
    {
        Assert.True(DemoScenarioCatalog.IsKnownPassportId("did:web:acme.battery.pass:demo-published-trusted-001"));
        Assert.False(DemoScenarioCatalog.IsKnownPassportId("did:web:acme.battery.pass:user-created-production-id"));
    }

    [Fact]
    public void ScenarioCatalog_ShouldUseNorthClusterForAuthorizedDemoAccess()
    {
        Assert.All(DemoScenarioCatalog.All, scenario =>
            Assert.Equal(DemoScenarioCatalog.DefaultClusterId, scenario.ClusterId));
    }

    [Fact]
    public void AuditRevisionService_ShouldExposeTargetedDemoLedgerCleanupOnly()
    {
        var source = File.ReadAllText(RepoFile("web", "Services", "AuditRevisionService.cs"));

        Assert.Contains("DeleteDemoLedgerAsync", source);
        Assert.Contains("Builders<BsonDocument>.Filter.In(\"passportId\"", source);
        Assert.DoesNotContain("DeleteManyAsync(Builders<BsonDocument>.Filter.Empty", source);
    }

    private static string RepoFile(params string[] parts)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null)
        {
            var candidate = Path.Combine(new[] { directory.FullName }.Concat(parts).ToArray());
            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException($"Could not find repository file: {Path.Combine(parts)}");
    }
}
