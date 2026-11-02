using System.Text.Json;
using System.Text.Json.Nodes;

namespace BatteryPassWeb.Dpp.Demo;

public sealed record DemoCluster(string Id, string Name);

/// <summary>A demo account: one app role, and optionally a cluster with its role there.</summary>
public sealed record DemoAccount(string Email, string Name, string Role, string? Cluster = null, string? ClusterRole = null);

public sealed record DemoFamily(string Key, string Name, string Picture);

/// <summary>
/// A battery model: its values, its module description, the restricted documents the seed uploads for it, and its own
/// picture when it looks different from its family's (redesign R2).
/// </summary>
public sealed record DemoModel(string Key, string Family, string Name, int Modules, string ModuleDescription, Dictionary<string, string> Documents, JsonObject Values, string? Picture = null);

public sealed record DemoNamed(string Key, string Name, JsonObject Values);

public sealed record DemoBatch(string Key, string Model, string Site, int Year, string TestReport, JsonObject Values);

/// <summary>
/// A status change the seed makes on a published demo battery (step 4b): what happened ("Repaired" or "Waste"), the
/// re-stated values that differ from the battery's current ones, and for a repair how many days ago and what.
/// </summary>
public sealed record DemoStatus(string What, JsonObject? Restated = null, int DaysAgo = 0, string? Repair = null, DemoSuccessor? Successor = null);

/// <summary>Scania's new passport of a demo battery placed on the market again (step 4c-1): made, given a warranty, published.</summary>
public sealed record DemoSuccessor(string Warranty);

/// <summary>
/// A demo battery's passport marked invalid, by the demo market surveillance account with this reason, or marked for
/// deletion by the team (step 4c-2).
/// </summary>
public sealed record DemoMark(string? Invalid = null, bool Deletion = false);

/// <summary>
/// A demo battery. <see cref="Publish"/> publishes it; <see cref="HandOverTo"/> then has its cluster admin hand it over to
/// that cluster, waiting for its answer (step 4d); <see cref="NewVersion"/> then changes these values, which makes
/// version 2; <see cref="History"/> writes seven days of live values; <see cref="Modules"/> imports that many modules
/// from an export-shaped file; <see cref="Accident"/> adds an accident report; <see cref="Status"/> then changes its
/// status through the app's own status change.
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
    string? Accident = null,
    DemoStatus? Status = null,
    DemoMark? Mark = null,
    string? HandOverTo = null);

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

    /// <summary>A family or model picture from the old demo (tag app-v1.2-before-dpp-v3).</summary>
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
