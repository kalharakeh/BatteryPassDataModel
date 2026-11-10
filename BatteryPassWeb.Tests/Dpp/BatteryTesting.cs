using BatteryPassWeb.Dpp.Catalog;
using BatteryPassWeb.Dpp.Editing;
using BatteryPassWeb.Dpp.Documents;
using BatteryPassWeb.Dpp.Forms;
using BatteryPassWeb.Dpp.Identifiers;
using BatteryPassWeb.Dpp.Pages;
using BatteryPassWeb.Dpp.Values;
using BatteryPassWeb.Services;

namespace BatteryPassWeb.Tests.Dpp;

/// <summary>The batteries pages around a publication rig, with two registered clusters, and their forms as the page shows them.</summary>
internal static class BatteryTesting
{
    public const string North = "cluster-test";
    public const string South = "cluster-south";

    /// <summary>Registers the seed pack's cluster ("Customer North") and a second one ("Customer South").</summary>
    public static async Task<PublicationRig> WithClustersAsync(this Task<PublicationRig> rigTask)
    {
        var rig = await rigTask;
        var clusters = new ClusterRepository(rig.Context);
        await clusters.UpsertClusterAsync(North, "Customer North");
        await clusters.UpsertClusterAsync(South, "Customer South");
        return rig;
    }

    public static PackCreationService Creation(PublicationRig rig) =>
        new(rig.Store, TestDemo.For(rig), rig.Pipeline, new TestIdentifierScheme(), DocumentTesting.Links, new ApplicationAuditService(rig.Context), rig.Clock);

    public static BatteryPages Pages(PublicationRig rig) => new(
        rig.Store,
        PageTesting.Editor(rig),
        Creation(rig),
        rig.Publication,
        new DocumentStore(rig.Context),
        DocumentTesting.Links,
        new ClusterRepository(rig.Context),
        new ApplicationAuditService(rig.Context),
        StatusChangeTests.Pages(rig));

    /// <summary>The fields of a form that shows these attributes, as the page shows them.</summary>
    public static Dictionary<string, string> ShownForm(IEnumerable<AttributeDefinition> attributes, IReadOnlyDictionary<string, AttributeValue> stored)
    {
        var texts = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var attribute in attributes)
        {
            foreach (var (name, text) in FormValues.Show(AttributeFormsTests.Forms.For(attribute), stored.GetValueOrDefault(attribute.Property)?.Value))
            {
                texts[name] = text;
            }
        }

        return texts;
    }

    public static string Field(string property) => $"{AttributeForm.Prefix}{property}";
}
