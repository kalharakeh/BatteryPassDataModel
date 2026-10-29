using System.Text.Json.Nodes;
using BatteryPassWeb.Dpp.Catalog;
using BatteryPassWeb.Dpp.Schemas;

namespace BatteryPassWeb.Dpp.Publication;

/// <summary>What stops a passport from being published, and what is only worth knowing.</summary>
public sealed record PublicationReport(IReadOnlyList<string> Blocking, IReadOnlyList<string> Warnings)
{
    public bool CanPublish => Blocking.Count == 0;
}

/// <summary>
/// The checks before a version is published (design section 11). The catalogue decides what must be present; the
/// official schemas then check the shape of what is there.
/// </summary>
public sealed class PublicationChecks(OfficialSchemas schemas)
{
    public PublicationReport Check(JsonObject passport)
    {
        ArgumentNullException.ThrowIfNull(passport);
        var blocking = new List<string>();
        var warnings = new List<string>();

        foreach (var attribute in AttributeCatalog.All.Where(a => Find(passport, a) is null))
        {
            // Conditional attributes never block (J-9); attributes required only after Feb 2027 warn.
            if (attribute.Requirement == Requirement.Conditional)
            {
                continue;
            }

            if (attribute.MandatoryFromFeb2027)
            {
                blocking.Add($"{attribute.Label} {attribute.Name} is missing");
            }
            else
            {
                warnings.Add($"{attribute.Label} {attribute.Name} is missing (required later, not by Feb 2027)");
            }
        }

        foreach (var (variant, feb2027) in new[] { ("Feb-2027 schema", true), ("Full schema", false) })
        {
            foreach (var issue in schemas.CheckPassport(passport, feb2027))
            {
                // A missing attribute is the catalogue's call above, and summaries repeat errors reported on their own.
                // Anything else, including a required part missing inside a value (e.g. a document link without its
                // url), is a shape error that blocks.
                if (!IsMissingAttribute(issue) && !IsSummary(issue))
                {
                    blocking.Add($"{variant}: {issue.Location} {issue.Message}".Trim());
                }
            }
        }

        return new PublicationReport(blocking.Distinct().ToArray(), warnings);
    }

    /// <summary>"properties" and "items" errors only say that something inside failed; that error is reported itself.</summary>
    private static bool IsSummary(SchemaIssue issue) =>
        issue.Message.StartsWith("properties:", StringComparison.Ordinal) || issue.Message.StartsWith("items:", StringComparison.Ordinal);

    /// <summary>A "required" error on the passport, the header, the data root or a section is about a missing attribute.</summary>
    private static bool IsMissingAttribute(SchemaIssue issue) =>
        issue.Message.StartsWith("required", StringComparison.Ordinal)
        && issue.Location.Count(c => c == '/') <= 2
        && (issue.Location.Length == 0
            || issue.Location == "/" + AttributeDefinition.HeaderSection
            || issue.Location.StartsWith("/" + AttributeDefinition.DataRoot, StringComparison.Ordinal));

    private static JsonNode? Find(JsonObject passport, AttributeDefinition attribute) =>
        attribute.Section == AttributeDefinition.HeaderSection
            ? passport[AttributeDefinition.HeaderSection]?[attribute.Property]
            : passport[AttributeDefinition.DataRoot]?[attribute.Section]?[attribute.Property];
}
