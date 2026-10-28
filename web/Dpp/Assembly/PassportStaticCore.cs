using System.Text.Json.Nodes;
using BatteryPassWeb.Dpp.Catalog;
using BatteryPassWeb.Dpp.Signing;
using BatteryPassWeb.Dpp.Values;

namespace BatteryPassWeb.Dpp.Assembly;

/// <summary>
/// The part of a passport a version signs (design 4.3): everything except #2 (DPP status), #4 (last update) and the
/// live set (#63, #83, #87, #92), which change without a new version.
/// </summary>
public static class PassportStaticCore
{
    public static IReadOnlyList<AttributeDefinition> NotSigned { get; } =
        AttributeCatalog.All.Where(a => a.Number is 2 or 4 || a.Change == ChangeRule.Live).ToArray();

    public static JsonObject Extract(JsonObject passport)
    {
        ArgumentNullException.ThrowIfNull(passport);
        var core = passport.DeepClone().AsObject();
        foreach (var attribute in NotSigned)
        {
            Parent(core, attribute)?.Remove(attribute.Property);
        }

        return core;
    }

    /// <summary>
    /// Identifies a version's content without its own passport ID (#5), which is new for every version: two cores with
    /// the same content hash would publish the same passport.
    /// </summary>
    public static string ContentHash(JsonNode core)
    {
        ArgumentNullException.ThrowIfNull(core);
        var content = core.DeepClone().AsObject();
        (content[AttributeDefinition.HeaderSection] as JsonObject)?.Remove(AttributeCatalog.ByNumber(5).Property);
        return CanonicalJson.Hash(content);
    }

    /// <summary>
    /// The passport readers see: the signed core with the version's #2 and #4 and, for the active version, the pack's
    /// live values. Archived versions get no live values, because live values aren't versioned; so an archived passport
    /// lacks the required live set and doesn't pass the schema's required lists. That is expected.
    /// </summary>
    public static JsonObject Compose(JsonNode core, string dppStatus, DateTime lastUpdatedUtc, IReadOnlyDictionary<string, AttributeValue>? liveValues)
    {
        ArgumentNullException.ThrowIfNull(core);
        var passport = core.DeepClone().AsObject();
        var header = Child(passport, AttributeDefinition.HeaderSection);
        header["dppStatus"] = new JsonObject { ["dppStatusValue"] = dppStatus };
        header["lastUpdated"] = PassportAssembler.FormatTimestamp(lastUpdatedUtc);
        foreach (var attribute in NotSigned.Where(a => a.Change == ChangeRule.Live))
        {
            if (liveValues is not null && liveValues.TryGetValue(attribute.Property, out var value))
            {
                Child(Child(passport, AttributeDefinition.DataRoot), attribute.Section)[attribute.Property] = value.Value.DeepClone();
            }
        }

        return passport;
    }

    private static JsonObject? Parent(JsonObject passport, AttributeDefinition attribute) =>
        attribute.Section == AttributeDefinition.HeaderSection
            ? passport[AttributeDefinition.HeaderSection] as JsonObject
            : passport[AttributeDefinition.DataRoot]?[attribute.Section] as JsonObject;

    private static JsonObject Child(JsonObject parent, string name)
    {
        if (parent[name] is JsonObject existing)
        {
            return existing;
        }

        var created = new JsonObject();
        parent[name] = created;
        return created;
    }
}
