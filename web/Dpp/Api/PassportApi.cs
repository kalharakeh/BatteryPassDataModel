using System.Text.Json.Nodes;
using BatteryPassWeb.Dpp.Access;
using BatteryPassWeb.Dpp.Assembly;
using BatteryPassWeb.Dpp.Catalog;
using BatteryPassWeb.Dpp.Pages;
using BatteryPassWeb.Dpp.Publication;
using BatteryPassWeb.Dpp.Store;

namespace BatteryPassWeb.Dpp.Api;

/// <summary>A published passport as one API reader may see it, with the version it comes from.</summary>
public sealed record ApiPassport(PassportLocation Location, JsonObject Passport, bool SignatureVerified, PassportSources Sources)
{
    public PassportVersion Version => Location.Version;
}

/// <summary>
/// One requested value: the attribute the request names (null when it names none), and the value when the reader may
/// see it and the passport has it, with the stored value's origin while it is still the one shown.
/// </summary>
public sealed record ApiValue(string Request, AttributeDefinition? Attribute, JsonNode? Value, ValueOrigin? Origin);

/// <summary>
/// The external API's reading side (decision 29, step 3c-1): the same passports, finding rules and visibility filter as
/// the pages. Every answer is cut by <see cref="PassportFilter"/>, so a change to who sees what changes the API too.
/// </summary>
public sealed class PassportApi(DppStore store, PassportReader reader, PassportPages pages, BatteryPages batteries)
{
    /// <summary>The passport's sections in the schema's order: the header, then the data sections.</summary>
    public static IReadOnlyList<string> Sections { get; } = AttributeCatalog.All.Select(attribute => attribute.Section).Distinct().ToArray();

    /// <summary>
    /// The published passport a battery ID (#6), passport ID (#5) or serial number names, cut for the reader; null when
    /// there is none. Drafts aren't served, to anyone (J-6): the team sees them on its battery page.
    /// </summary>
    public async Task<ApiPassport?> ReadAsync(Reader who, string? id, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(who);
        var location = await pages.LocateAsync(id, cancellationToken);
        var published = location is null ? null : await reader.VersionAsync(location.Version.DigitalProductPassportId, cancellationToken);
        var sources = location is null ? null : await store.LoadSourcesAsync(location.Version.PackId, cancellationToken);
        if (location is null || published is null || sources is null)
        {
            return null;
        }

        who = DemoScope.On(who, sources.Pack.IsDemo); // a demo token reads a real battery as a visitor (finding R03)
        var clusters = await store.FindPublishedClusterIdsUsingAsync(packId: location.Version.PackId, cancellationToken: cancellationToken);
        return new ApiPassport(location, PassportFilter.Cut(published.Passport, who, clusters), PassportReader.Verify(location.Version), sources);
    }

    /// <summary>The battery's published versions, newest first.</summary>
    public async Task<IReadOnlyList<PassportVersion>> VersionsAsync(ApiPassport passport, CancellationToken cancellationToken = default) =>
        (await store.ListVersionsAsync(passport.Version.PackId, cancellationToken)).OrderByDescending(version => version.Number).ToArray();

    /// <summary>
    /// A cluster's batteries as the reader may list them: every battery for the team; for anyone else only those with a
    /// published passport, since drafts stay with the team (J-6).
    /// </summary>
    public async Task<IReadOnlyList<BatteryRow>> BatteriesAsync(Reader who, string clusterId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(who);
        var team = who.Roles.Contains(ReaderRole.Team);
        return DemoScope.Lists(who, (await batteries.ListAsync(null, cancellationToken)).Rows, row => row.IsDemo) // finding R03
            .Where(row => string.Equals(row.ClusterId, clusterId, StringComparison.OrdinalIgnoreCase) && (team || !row.IsDraft))
            .ToArray();
    }

    /// <summary>The section a name gives, ignoring case (e.g. "performanceAndDurability"); null when it names none.</summary>
    public static string? SectionNamed(string? name) =>
        Sections.FirstOrDefault(section => section.Equals(name?.Trim(), StringComparison.OrdinalIgnoreCase));

    /// <summary>A section of the cut passport: what the reader sees there, which may be nothing.</summary>
    public static JsonObject Section(ApiPassport passport, string section)
    {
        ArgumentNullException.ThrowIfNull(passport);
        var node = section == AttributeDefinition.HeaderSection
            ? passport.Passport[section]
            : passport.Passport[AttributeDefinition.DataRoot]?[section];
        return node is JsonObject found ? (JsonObject)found.DeepClone() : new JsonObject();
    }

    /// <summary>
    /// The attribute a request names: its property name (stateOfChargeSoC), its label (#63, 63, E1) or its path in the
    /// passport (batteryPassportData.performanceAndDurability.stateOfChargeSoC), ignoring case.
    /// </summary>
    public static AttributeDefinition? AttributeNamed(string? request)
    {
        var text = request?.Trim() ?? string.Empty;
        if (text.Length == 0)
        {
            return null;
        }

        var label = text.StartsWith('#') || text.StartsWith("E", StringComparison.OrdinalIgnoreCase) ? text : $"#{text}";
        return AttributeCatalog.All.FirstOrDefault(attribute =>
            attribute.Property.Equals(text, StringComparison.OrdinalIgnoreCase)
            || attribute.Label.Equals(label, StringComparison.OrdinalIgnoreCase)
            || attribute.Path.Equals(text, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>Each requested value, in the order asked.</summary>
    public static IReadOnlyList<ApiValue> Values(ApiPassport passport, IEnumerable<string> requests)
    {
        ArgumentNullException.ThrowIfNull(passport);
        ArgumentNullException.ThrowIfNull(requests);
        return requests
            .Where(request => !string.IsNullOrWhiteSpace(request))
            .Select(request =>
            {
                var attribute = AttributeNamed(request);
                var value = attribute is null ? null : Find(passport.Passport, attribute);
                var origin = attribute is null || value is null ? null : PassportPages.Origin(attribute, passport.Sources, value);
                return new ApiValue(request.Trim(), attribute, value?.DeepClone(), origin);
            })
            .ToArray();
    }

    /// <summary>The attributes this reader sees a value of, in catalogue order; one section only when it is given.</summary>
    public static IReadOnlyList<AttributeDefinition> Readable(ApiPassport passport, string? section = null)
    {
        ArgumentNullException.ThrowIfNull(passport);
        return AttributeCatalog.All
            .Where(attribute => (section is null || attribute.Section == section) && Find(passport.Passport, attribute) is not null)
            .ToArray();
    }

    /// <summary>#4 as the passport states it, for listings.</summary>
    public static string Timestamp(DateTime utc) => PassportAssembler.FormatTimestamp(utc);

    private static JsonNode? Find(JsonObject passport, AttributeDefinition attribute)
    {
        JsonNode? node = passport;
        foreach (var name in attribute.Path.Split('.'))
        {
            node = node?[name];
        }

        return node;
    }
}
