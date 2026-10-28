using System.Text.Json.Nodes;
using BatteryPassWeb.Dpp.Editing;
using BatteryPassWeb.Dpp.Values;
using BatteryPassWeb.Services;

namespace BatteryPassWeb.Tests.Dpp;

/// <summary>Restricted documents may only link to the app's own addresses, so they stay behind its access checks.</summary>
[Collection(MongoCollection.Name)]
public sealed class RestrictedDocumentLinkTests(MongoFixture mongo)
{
    private static readonly ChangeSender Team = ChangeSender.Team("team@example.com");

    [Theory]
    [InlineData("https://www.example.com/dismantling.pdf", KeyOutcome.Rejected)]
    [InlineData("https://passport.example.com/dpp/documents/doc-new", KeyOutcome.Saved)]
    public async Task ModelDocument_ShouldOnlyLinkToTheApp(string url, KeyOutcome expected)
    {
        var (store, context, seed) = await DppStoreTesting.SeededStoreAsync(mongo);

        var result = await SavePipelineTesting.Pipeline(store, context).SaveAsync(Team, Change(TargetKind.Model, seed.Sources.Model.Id, "dismantlingInformation", url));

        var key = Assert.Single(result.Keys);
        Assert.Equal(expected, key.Outcome);
        if (expected == KeyOutcome.Rejected)
        {
            Assert.Contains("must link to a file uploaded to the app", key.Message, StringComparison.Ordinal);
        }
    }

    [Theory]
    [InlineData("https://www.example.com/accidents", KeyOutcome.Rejected)]
    [InlineData("https://passport.example.com/dpp/packs/pack-VPC8C05-6788/accidents", KeyOutcome.Saved)]
    public async Task ClusterAdmin_ShouldNotPointAccidentsElsewhere(string url, KeyOutcome expected)
    {
        var (store, context, _) = await DppStoreTesting.SeededStoreAsync(mongo);

        var result = await SavePipelineTesting.Pipeline(store, context).SaveAsync(
            ChangeSender.ClusterAdmin("admin@example.com", "cluster-test"),
            Change(TargetKind.Pack, "pack-VPC8C05-6788", "informationOnAccidents", url, "text/html"));

        Assert.Equal(expected, Assert.Single(result.Keys).Outcome);
    }

    [Fact]
    public async Task PublicDocument_ShouldStillLinkAnywhere()
    {
        var (store, context, seed) = await DppStoreTesting.SeededStoreAsync(mongo);

        var result = await SavePipelineTesting.Pipeline(store, context).SaveAsync(Team, Change(TargetKind.Batch, seed.Sources.Batch.Id, "euDeclarationOfConformity", "https://www.example.com/doc-2026.pdf"));

        Assert.True(result.Saved);
    }

    [Fact]
    public async Task WithoutAnAppBaseUrl_RestrictedDocumentsShouldBeRejected()
    {
        var (store, context, seed) = await DppStoreTesting.SeededStoreAsync(mongo);
        var pipeline = new SavePipeline(
            store,
            new AttributeValueNormalizer(DppStoreTesting.Schemas),
            new AttributeValueValidator(DppStoreTesting.Schemas),
            new ApplicationAuditService(context),
            new FixedTimeProvider(DppStoreTesting.Now),
            DocumentTesting.NoLinks,
            []);

        var result = await pipeline.SaveAsync(Team, Change(TargetKind.Model, seed.Sources.Model.Id, "safetyMeasures", "https://passport.example.com/dpp/documents/doc-new"));

        Assert.Equal(KeyOutcome.Rejected, Assert.Single(result.Keys).Outcome);
    }

    private static ChangeSet Change(TargetKind target, string id, string property, string url, string contentType = "application/pdf") =>
        new(target, id, new Dictionary<string, JsonNode?> { [property] = new JsonObject { ["contentType"] = contentType, ["url"] = url } }, "form");
}
