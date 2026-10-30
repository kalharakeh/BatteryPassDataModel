using System.Text.RegularExpressions;
using BatteryPassWeb.Dpp.Documents;
using BatteryPassWeb.Dpp.Store;
using BatteryPassWeb.Tests.Dpp;
using V3Reset;

namespace BatteryPassWeb.Tests.Reset;

/// <summary>Step 3d-2: the reset knows what to do with every collection the app uses (design 14).</summary>
public sealed class ResetCollectionsTests
{
    [Fact]
    public void EveryCollectionTheAppUses_ShouldHaveAKnownFate()
    {
        var web = Path.GetDirectoryName(DppTestFiles.RepoFile("web", "Program.cs"))!;
        var named = Directory.GetFiles(web, "*.cs", SearchOption.AllDirectories)
            .Where(file => !file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .SelectMany(file => Regex.Matches(File.ReadAllText(file), "GetCollection<[^>]*>\\(\"([^\"]+)\"\\)").Select(match => match.Groups[1].Value));
        var dpp = new[] { DppStore.Families, DppStore.Models, DppStore.Sites, DppStore.Batches, DppStore.Operators, DppStore.Packs, DppStore.Versions, DocumentStore.Records, $"{DocumentStore.Bucket}.files", $"{DocumentStore.Bucket}.chunks" };

        foreach (var collection in named.Concat(dpp).Distinct())
        {
            Assert.True(ResetCollections.FateOf(collection) != CollectionFate.Unknown, $"The reset doesn't know {collection}");
        }
    }

    [Fact]
    public void TheNewModelsCollections_ShouldBeEmptiedAsTestData()
    {
        foreach (var collection in new[] { DppStore.Families, DppStore.Models, DppStore.Sites, DppStore.Batches, DppStore.Operators, DppStore.Packs, DppStore.Versions, DocumentStore.Records, $"{DocumentStore.Bucket}.files" })
        {
            Assert.Equal(CollectionFate.NewTestData, ResetCollections.FateOf(collection));
        }
    }

    [Theory]
    [InlineData("passports", CollectionFate.OldData)]
    [InlineData("batteryProductTemplates", CollectionFate.OldData)]
    [InlineData("passportFiles.chunks", CollectionFate.OldData)]
    [InlineData("passwordResetRequests", CollectionFate.Cleared)]
    [InlineData("users", CollectionFate.Reviewed)]
    [InlineData("apiTokens", CollectionFate.Reviewed)]
    [InlineData("applicationAuditEvents", CollectionFate.Kept)]
    [InlineData("auditEvents", CollectionFate.Kept)]
    [InlineData("batteryAuditEvents", CollectionFate.Kept)]
    [InlineData("applicationSettings", CollectionFate.Kept)]
    [InlineData("ownerNotes", CollectionFate.Unknown)]
    public void EachCollection_ShouldHaveItsFate(string collection, CollectionFate fate)
    {
        Assert.Equal(fate, ResetCollections.FateOf(collection));
    }
}
