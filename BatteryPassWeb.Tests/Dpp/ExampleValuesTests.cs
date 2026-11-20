using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using BatteryPassWeb.Dpp.Demo;
using BatteryPassWeb.Dpp.Documents;
using BatteryPassWeb.Dpp.Editing;
using BatteryPassWeb.Dpp.Identifiers;
using BatteryPassWeb.Dpp.Pages;
using BatteryPassWeb.Dpp.Publication;
using BatteryPassWeb.Dpp.Store;
using BatteryPassWeb.Dpp.Values;
using BatteryPassWeb.Tests.Demo;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Driver;

namespace BatteryPassWeb.Tests.Dpp;

/// <summary>
/// Decision 70: a real battery doesn't publish the demo's example values. Copied documents are example files until
/// replaced, a text with "(demo)", "Demo ", "example.com" or "demo data" is an example value, the pages list them, and the
/// first publication of a battery with a real ID is blocked while its passport holds one. A test ID isn't.
/// </summary>
[Collection(MongoCollection.Name)]
public sealed class ExampleValuesTests(MongoFixture mongo)
{
    private const string Who = DemoRig.Who;

    [Theory]
    [InlineData("Scania Industrial Batteries (demo)", true)]
    [InlineData("Demo Street 1, Södertälje", true)]
    [InlineData("Made at the DEMO plant", true)] // "Demo " as a word, whatever its capitals
    [InlineData("https://passport.example.com/dpp/documents/doc-1", false)] // the app's own document link: judged by its file
    [InlineData("https://passport.example.com/dpp/packs/pack-1/accidents", false)] // the app's own accident page
    [InlineData("https://www.example.com/battery", true)]
    [InlineData("For the demo data.", true)]
    [InlineData("UN778474481-TEST-DEMO-C7M-0001", false)]
    [InlineData("Scania Industrial Batteries AB", false)]
    [InlineData("Demolition waste", false)]
    public void AText_ShouldCountAsAnExample_OnlyWithTheDemoWords(string text, bool example) =>
        Assert.Equal(example, ExampleValues.IsExampleText(JsonValue.Create(text)));

    private sealed record Copied(DemoRig Rig, BatteryModel Model, ProductionBatch Batch, string OperatorId);

    private static async Task<Copied> CopyAsync(MongoFixture mongo)
    {
        var rig = await DemoRig.StartAsync(mongo, dunsIds: true);
        await rig.Demo.SeedAsync(Who);
        var pages = rig.Services.GetRequiredService<ProductDataPages>();
        var compact = (await rig.Store.ListAsync<BatteryFamily>()).Single(family => family.Name == "Compact");
        var (familyId, _) = await pages.CopyFamilyAsRealAsync(Who, compact.Id, "Compact S");
        var demoOperator = (await rig.Store.ListAsync<EconomicOperator>()).Single(item => item.Name == "Scania Industrial Batteries (demo)");
        var (operatorId, _) = await pages.CopyOperatorAsRealAsync(Who, demoOperator.Id, "Scania Industrial Batteries");
        var model = (await rig.Store.ListModelsAsync(familyId!)).Single(item => item.Name == "Compact 7M");
        return new Copied(rig, model, (await rig.Store.ListBatchesAsync(model.Id)).Single(), operatorId!);
    }

    private static async Task<BatteryPack> MakeAsync(Copied copy, string serial, bool test)
    {
        var pool = copy.Rig.Services.GetRequiredService<BatteryIdPool>();
        Assert.True((await pool.ReleaseAsync(Who, new ReleaseRequest(test ? "Try-out" : "PO 1", null, [], serial, IsDemo: test))).Done);
        var values = DemoCatalogue.Load().Packs.First(pack => pack.Publish).Values.ToDictionary(entry => entry.Key, entry => entry.Value?.DeepClone(), StringComparer.Ordinal);
        values["batterySerialNumber"] = serial;
        var made = await copy.Rig.Services.GetRequiredService<PackCreationService>().CreateAsync(ChangeSender.Team(Who), new NewPack(copy.Batch.Id, copy.OperatorId, null, values, FromPool: true));
        Assert.Null(made.Problem);
        return made.Pack!;
    }

    /// <summary>The team's work before Monday: every example text written anew, every example file replaced.</summary>
    internal static async Task ReplaceExamplesAsync(DemoRig rig, string packId)
    {
        var sources = (await rig.Store.LoadSourcesAsync(packId))!;
        await RewriteAsync<BatteryPack>(rig, sources.Pack.Id, sources.Pack.Values);
        await RewriteAsync<ProductionBatch>(rig, sources.Batch.Id, sources.Batch.Values);
        await RewriteAsync<BatteryModel>(rig, sources.Model.Id, sources.Model.Values);
        await RewriteAsync<ManufacturingSite>(rig, sources.Site.Id, sources.Site.Values);
        await RewriteAsync<EconomicOperator>(rig, sources.Operator.Id, sources.Operator.Values);
        var documents = rig.Services.GetRequiredService<DocumentService>();
        foreach (var (kind, id, record) in new (TargetKind, string, (IReadOnlyDictionary<string, AttributeValue> Values, List<string> Examples))[]
                 {
                     (TargetKind.Model, sources.Model.Id, (sources.Model.Values, sources.Model.ExampleDocumentIds)),
                     (TargetKind.Batch, sources.Batch.Id, (sources.Batch.Values, sources.Batch.ExampleDocumentIds)),
                 })
        {
            foreach (var property in record.Values.Keys.Where(DppLinks.RestrictedDocumentProperties.Contains))
            {
                var uploaded = await documents.UploadAsync(ChangeSender.Team(Who), kind, id, property, new DocumentUpload($"{property}.pdf", DemoPdf.Make("Real file", "The real document.")));
                Assert.Null(uploaded.Problem);
            }
        }
    }

    private static Task RewriteAsync<T>(DemoRig rig, string id, IReadOnlyDictionary<string, AttributeValue> values)
        where T : class, IDppRecord => RewriteAsync<T>(rig.Store, id, values);

    private static async Task RewriteAsync<T>(DppStore store, string id, IReadOnlyDictionary<string, AttributeValue> values)
        where T : class, IDppRecord
    {
        var changed = values
            .Where(entry => ExampleValues.IsExampleText(entry.Value.Value))
            .ToDictionary(
                entry => entry.Key,
                entry => new AttributeValue
                {
                    Value = JsonNode.Parse(Regex.Replace(Regex.Replace(entry.Value.Value.ToJsonString(), @"\s?\(demo\)", string.Empty, RegexOptions.IgnoreCase), @"(?i)demo data|example\.com|\bdemo\s", match => match.Value.ToLowerInvariant() switch { "demo data" => "real data", "example.com" => "scania.com", _ => "Real " }))!,
                    GeneratedAtUtc = entry.Value.GeneratedAtUtc,
                    Source = entry.Value.Source,
                    Who = Who,
                });
        if (changed.Count > 0)
        {
            Assert.True(await store.SetValuesAsync<T>(id, changed));
        }
    }

    [Fact]
    public async Task ARealIdOnAFreshCopy_ShouldBeBlocked_NamingTheExamples_UntilTheyAreReplaced()
    {
        var copy = await CopyAsync(mongo);
        using var _ = copy.Rig;
        var pack = await MakeAsync(copy, "VPC-12221", test: false);
        var publication = copy.Rig.Services.GetRequiredService<PublicationService>();

        var blocked = await publication.PublishAsync(ChangeSender.Team(Who), pack.Id);

        Assert.Null(blocked.Version);
        var blocking = blocked.Report!.Blocking;
        Assert.Contains(ExampleValues.WhatToDo, blocking);
        Assert.Contains(blocking, item => item.StartsWith("Still the demo's example file: #4", StringComparison.Ordinal)); // the copied demo PDFs (#45-48)
        Assert.Contains(blocking, item => item.StartsWith("Still the demo's example value: #", StringComparison.Ordinal));
        Assert.All(blocking.Where(item => item != ExampleValues.WhatToDo), item => Assert.Matches(@"^Still the demo's example (value|file): #\d+ .+$", item));
        Assert.DoesNotContain(blocking, item => item.StartsWith('#')); // so the battery page doesn't list them as missing values

        await ReplaceExamplesAsync(copy.Rig, pack.Id);
        var published = await publication.PublishAsync(ChangeSender.Team(Who), pack.Id);

        Assert.True(published.Version is not null, string.Join(" | ", published.Report?.Blocking ?? [published.Problem ?? string.Empty]));
    }

    [Fact]
    public async Task ATestIdOnTheSameCopy_ShouldPublishAtOnce()
    {
        var copy = await CopyAsync(mongo);
        using var _ = copy.Rig;
        var pack = await MakeAsync(copy, "TRY-0001", test: true);

        var published = await copy.Rig.Services.GetRequiredService<PublicationService>().PublishAsync(ChangeSender.Team(Who), pack.Id);

        Assert.True(published.Version is not null, string.Join(" | ", published.Report?.Blocking ?? []));
    }

    [Fact]
    public async Task TheCopiedFamily_ShouldListItsExamples_AndADemoFamilyNone()
    {
        var copy = await CopyAsync(mongo);
        using var _ = copy.Rig;
        var pages = copy.Rig.Services.GetRequiredService<ProductDataPages>();

        var family = (await pages.FamilyAsync(copy.Model.FamilyId, copy.Model.Id))!;
        var batch = (await pages.BatchAsync(copy.Batch.Id))!;
        var demo = (await pages.FamilyAsync((await copy.Rig.Store.ListAsync<BatteryFamily>()).Single(item => item.Name == "Compact").Id, null))!;
        var operatorPage = await pages.SitesAndOperatorsAsync(null, copy.OperatorId);

        Assert.Contains(family.Examples, group => group.Where == "Model Compact 7M" && group.Items.Count(item => item.IsFile) == 4);
        Assert.Contains(family.Examples, group => group.Where.StartsWith("Model Compact 7M, batch 2026", StringComparison.Ordinal));
        Assert.Single(batch.Examples);
        Assert.Empty(demo.Examples);
        Assert.Equal("Economic operator Scania Industrial Batteries", Assert.Single(operatorPage.Examples).Where);
        Assert.True((await copy.Rig.All(DocumentStore.Records, Builders<MongoDB.Bson.BsonDocument>.Filter.Eq("OwnerId", copy.Model.Id))).All(document => document["IsExample"].AsBoolean));
    }

    [Fact]
    public async Task RealDataTypedIn_ShouldNotBeTouched_UnlessItsTextMatches()
    {
        var rig = await PublicationTesting.RigAsync(mongo); // the test seed: product data typed in, never copied
        await rig.Store.SetValuesAsync<BatteryPack>(
            rig.PackId,
            new Dictionary<string, AttributeValue>
            {
                ["uniqueProductIdentifier"] = new() { Value = JsonValue.Create("https://www.sib-scania-eubp.eu/25S/UN778474481-VPC-0001")!, GeneratedAtUtc = rig.Clock.Now.UtcDateTime, Source = "system" },
            });
        Assert.Empty(ExampleValues.InPassport((await rig.Store.LoadSourcesAsync(rig.PackId))!));
        Assert.True((await rig.Publication.CheckAsync(rig.PackId))!.CanPublish);

        var model = (await rig.Store.LoadSourcesAsync(rig.PackId))!.Model;
        var text = model.Values.First(entry => entry.Value.Value is JsonValue value && value.TryGetValue<string>(out _));
        await rig.Store.SetValuesAsync<BatteryModel>(model.Id, new Dictionary<string, AttributeValue> { [text.Key] = new() { Value = JsonValue.Create("See www.example.com")!, GeneratedAtUtc = rig.Clock.Now.UtcDateTime, Source = "form" } });

        var check = await rig.Publication.CheckAsync(rig.PackId);
        Assert.Contains(check!.Blocking, item => item.StartsWith("Still the demo's example value: #", StringComparison.Ordinal));
    }
}
