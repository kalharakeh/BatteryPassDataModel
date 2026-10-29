using System.Text.Json.Nodes;
using BatteryPassWeb.Dpp.Catalog;

namespace BatteryPassWeb.Dpp.Access;

/// <summary>
/// The one visibility filter (design principle 4): a passport cut down to what a reader may see. Pages, the API and
/// JSON exports show a passport only through here.
/// </summary>
public static class PassportFilter
{
    /// <summary>
    /// A new passport holding only the catalogue's attributes the reader may see, at their own paths. Anything that isn't
    /// a catalogue attribute is left out, so nothing reaches a reader without an audience. The passport itself is not
    /// changed.
    /// </summary>
    /// <param name="batteryClusterIds">
    /// The battery's cluster when its passport is published, as <see cref="AudienceRules.MaySee"/> takes it; empty for a
    /// draft or a battery without a cluster.
    /// </param>
    public static JsonObject Cut(JsonObject passport, Reader reader, IReadOnlyCollection<string> batteryClusterIds)
    {
        ArgumentNullException.ThrowIfNull(passport);
        ArgumentNullException.ThrowIfNull(reader);
        ArgumentNullException.ThrowIfNull(batteryClusterIds);
        var cut = new JsonObject();
        foreach (var attribute in AttributeCatalog.All)
        {
            if (!AudienceRules.MaySee(reader, attribute, batteryClusterIds))
            {
                continue;
            }

            var path = attribute.Path.Split('.');
            if (Find(passport, path[..^1]) is { } source && source.TryGetPropertyValue(path[^1], out var value))
            {
                Ensure(cut, path[..^1])[path[^1]] = value?.DeepClone();
            }
        }

        return cut;
    }

    private static JsonObject? Find(JsonObject root, string[] path)
    {
        var node = root;
        foreach (var name in path)
        {
            node = node[name] as JsonObject;
            if (node is null)
            {
                return null;
            }
        }

        return node;
    }

    private static JsonObject Ensure(JsonObject root, string[] path)
    {
        var node = root;
        foreach (var name in path)
        {
            if (node[name] is not JsonObject next)
            {
                next = new JsonObject();
                node[name] = next;
            }

            node = next;
        }

        return node;
    }
}
