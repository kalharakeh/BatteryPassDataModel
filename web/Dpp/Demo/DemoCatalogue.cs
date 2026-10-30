using System.Text.Json;
using System.Text.Json.Nodes;

namespace BatteryPassWeb.Dpp.Demo;

public sealed record DemoCluster(string Id, string Name);

/// <summary>A demo account: one app role, and optionally a cluster with its role there.</summary>
public sealed record DemoAccount(string Email, string Name, string Role, string? Cluster = null, string? ClusterRole = null);

public sealed record DemoFamily(string Key, string Name, string Picture);

/// <summary>A battery model: its values, its module description and the restricted documents the seed uploads for it.</summary>
public sealed record DemoModel(string Key, string Family, string Name, int Modules, string ModuleDescription, Dictionary<string, string> Documents, JsonObject Values);

public sealed record DemoNamed(string Key, string Name, JsonObject Values);

public sealed record DemoBatch(string Key, string Model, string Site, int Year, string TestReport, JsonObject Values);

/// <summary>
/// A demo battery. <see cref="Publish"/> publishes it; <see cref="NewVersion"/> then changes these values, which makes
/// version 2; <see cref="History"/> writes seven days of live values; <see cref="Modules"/> imports that many modules
/// from an export-shaped file; <see cref="Accident"/> adds an accident report.
/// </summary>
public sealed record DemoPack(
    string Serial,
    string Batch,
    string Operator,
    string Cluster,
    JsonObject Values,
    bool Publish = false,
    JsonObject? NewVersion = null,
    bool History = false,
    int Modules = 0,
    string? Accident = null);

/// <summary>
/// What the demo data seed makes (decision 32): the Compact and Core families of the old demo on the new model, with
/// sites, operators, batches, batteries in several states, clusters and accounts. Read from the embedded
/// demo-catalogue.json.
/// </summary>
public sealed record DemoCatalogue(
    IReadOnlyList<DemoCluster> Clusters,
    IReadOnlyList<DemoAccount> Accounts,
    IReadOnlyList<DemoFamily> Families,
    IReadOnlyList<DemoModel> Models,
    IReadOnlyList<DemoNamed> Sites,
    IReadOnlyList<DemoNamed> Operators,
    IReadOnlyList<DemoBatch> Batches,
    IReadOnlyList<DemoPack> Packs)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public static DemoCatalogue Load()
    {
        using var stream = Resource("demo-catalogue.json");
        return JsonSerializer.Deserialize<DemoCatalogue>(stream, Json) ?? throw new InvalidOperationException("The demo catalogue is empty.");
    }

    /// <summary>A family picture from the old demo (tag app-v1.2-before-dpp-v3).</summary>
    public static byte[] Picture(string fileName)
    {
        using var stream = Resource(fileName);
        using var copy = new MemoryStream();
        stream.CopyTo(copy);
        return copy.ToArray();
    }

    private static Stream Resource(string fileName) =>
        typeof(DemoCatalogue).Assembly.GetManifestResourceStream($"Dpp.Demo.{fileName}")
        ?? throw new InvalidOperationException($"The demo file {fileName} is missing from the app.");
}
