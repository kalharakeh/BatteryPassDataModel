using System.Globalization;
using System.Text.Json.Nodes;
using BatteryPassWeb.Dpp.Assembly;
using BatteryPassWeb.Dpp.Store;
using BatteryPassWeb.Dpp.Values;

namespace BatteryPassWeb.Tests.Dpp;

/// <summary>
/// One fully filled test pack (seed-other-industrial.json): every stored attribute has a value in the schema's own
/// form, and the pack re-states #59 (capacity fade 4 % instead of the model's 0 %). Each call returns fresh records.
/// </summary>
internal static class DppTestSeed
{
    public static readonly DateTime GeneratedAtUtc = new(2026, 10, 28, 9, 30, 0, DateTimeKind.Utc);

    public static SeedPassport Load()
    {
        var root = JsonNode.Parse(File.ReadAllText(DppTestFiles.RepoFile("BatteryPassWeb.Tests", "Dpp", "seed-other-industrial.json")))!;
        var header = root["header"]!;
        var model = root["model"]!;
        var site = root["site"]!;
        var batch = root["batch"]!;
        var economicOperator = root["operator"]!;
        var pack = root["pack"]!;

        var sources = new PassportSources(
            new BatteryPack
            {
                Id = Text(pack, "id"),
                BatchId = Text(pack, "batchId"),
                OperatorId = Text(pack, "operatorId"),
                ClusterId = Text(pack, "clusterId"),
                Values = Values(pack),
            },
            new ProductionBatch
            {
                Id = Text(batch, "id"),
                ModelId = Text(batch, "modelId"),
                SiteId = Text(batch, "siteId"),
                Year = batch["year"]!.GetValue<int>(),
                Values = Values(batch),
            },
            new BatteryModel { Id = Text(model, "id"), FamilyId = Text(model, "familyId"), Name = Text(model, "name"), Values = Values(model) },
            new ManufacturingSite { Id = Text(site, "id"), Name = Text(site, "name"), Values = Values(site) },
            new EconomicOperator { Id = Text(economicOperator, "id"), Name = Text(economicOperator, "name"), Values = Values(economicOperator) });

        var passportHeader = new PassportHeader(
            Text(header, "digitalProductPassportId"),
            Text(header, "dppStatus"),
            DateTime.Parse(Text(header, "lastUpdatedUtc"), CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal));

        return new SeedPassport(sources, passportHeader);
    }

    private static string Text(JsonNode node, string name) => node[name]!.GetValue<string>();

    private static Dictionary<string, AttributeValue> Values(JsonNode record) =>
        record["values"]!.AsObject().ToDictionary(
            entry => entry.Key,
            entry => new AttributeValue { Value = entry.Value!.DeepClone(), GeneratedAtUtc = GeneratedAtUtc, Source = "seed" },
            StringComparer.Ordinal);
}

internal sealed record SeedPassport(PassportSources Sources, PassportHeader Header);
