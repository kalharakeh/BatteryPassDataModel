using System.Text.Json.Nodes;
using BatteryPassWeb.Dpp.Access;
using BatteryPassWeb.Dpp.Assembly;
using BatteryPassWeb.Dpp.Catalog;
using BatteryPassWeb.Dpp.Forms;
using BatteryPassWeb.Dpp.Identifiers;
using BatteryPassWeb.Dpp.Publication;
using BatteryPassWeb.Dpp.Store;
using BatteryPassWeb.Dpp.Values;

namespace BatteryPassWeb.Dpp.Pages;

/// <summary>When a stored value was generated (design 4.1) and where it came from: "form", "upload", "feed" or "seed".</summary>
public sealed record ValueOrigin(DateTime GeneratedAtUtc, string Source, bool Measured);

/// <summary>One attribute on a passport page: its value in words and, on the detail page, its origin.</summary>
public sealed record ShownValue(AttributeDefinition Attribute, IReadOnlyList<ValueLine> Lines, ValueOrigin? Origin);

/// <summary>
/// A tab of the passport pages: a schema section of public data, or one audience's data (detail page). <paramref name="Tab"/>
/// is the tab's short name, <paramref name="Title"/> the heading inside it.
/// </summary>
public sealed record PassportSection(string Key, string Title, string Tab, string Note, IReadOnlyList<ShownValue> Values);

/// <summary>One part of a chart on the summary report: a carbon footprint stage or a recycled content share.</summary>
public sealed record ChartPart(string Label, decimal Value, string Unit);

/// <summary>
/// A published passport version as one reader sees it (design 12): the public data by schema section and, on the
/// detail page, one section per audience the reader may see. Everything shown went through <see cref="PassportFilter"/>.
/// </summary>
public sealed record PassportPage(
    PassportVersion Version,
    PassportVersion? Current,
    string Identifier,
    string? FamilyName,
    string? PictureHref,
    bool IsTest,
    bool SignatureHolds,
    bool ReaderSeesMore,
    IReadOnlyList<PassportSection> Sections,
    IReadOnlyList<PassportSection> Audiences,
    IReadOnlyList<ChartPart> Carbon,
    IReadOnlyList<ChartPart> Recycled)
{
    /// <summary>True for the active version; an archived one links to <see cref="Current"/>.</summary>
    public bool IsCurrent => Version.DppStatus == DppStatuses.Active;

    /// <summary>The address of this page: the battery ID for the current version, the version's own passport ID otherwise.</summary>
    public string Address => IsCurrent ? Identifier : Version.DigitalProductPassportId;

    /// <summary>A public value shown on the page by its number, e.g. #11 for the hero's title; null when it has none.</summary>
    public ShownValue? Value(int number) =>
        Sections.SelectMany(section => section.Values).FirstOrDefault(value => value.Attribute.Number == number);

    /// <summary>The first line of a public value in words, or "-".</summary>
    public string Text(int number) => Value(number)?.Lines.FirstOrDefault()?.Text ?? "-";

    /// <summary>
    /// The battery status (#22) in words for the badge (redesign R3); null unless the reader may see it. It is purchaser
    /// data, so it comes only from the detail page's audience sections, never from the public ones.
    /// </summary>
    public string? BatteryStatus => Audiences.SelectMany(section => section.Values).FirstOrDefault(value => value.Attribute.Number == 22)?.Lines.FirstOrDefault()?.Text;

    /// <summary>How many modules the battery has (decision 31): app data, public, not part of the signed passport.</summary>
    public int ModuleCount { get; init; }

    /// <summary>The model's module description (decision 31); null when not typed.</summary>
    public string? ModuleDescription { get; init; }

    /// <summary>The battery's modules with their IDs, on the detail page for the battery's purchaser's readers; null otherwise.</summary>
    public IReadOnlyList<BatteryModule>? Modules { get; init; }

    /// <summary>
    /// The battery's repair history, newest first, on the detail page for those who may see its status (design 7.9);
    /// null otherwise. Not part of the signed passport.
    /// </summary>
    public IReadOnlyList<RepairEntry>? Repairs { get; init; }

    /// <summary>The line about an inactive passport or one that replaces an earlier passport (S7); null otherwise.</summary>
    public PassportNotice? Notice { get; init; }

    /// <summary>The key figures card (redesign R5): public values only, the same on the summary and the detail page.</summary>
    public KeyFigures? KeyFigures { get; init; }

    /// <summary>Why the passport is invalid and who said so, for the team and market surveillance only (decision 34); null otherwise.</summary>
    public string? MarkReason { get; init; }

    /// <summary>The reader may mark this passport invalid here: market surveillance on an active passport's detail page (S6).</summary>
    public bool MayMarkInvalid { get; init; }

    /// <summary>This version's DPP status (#2) in words, for the hero's pill.</summary>
    public string StatusWords => DppStatuses.Words(Version.DppStatus);
}

/// <summary>
/// One line at the top of both passport pages when the passport isn't simply active, or replaces an earlier one (mock-up
/// S7, step 4c): what it says, and where it leads (a passport page, or another operator's address).
/// </summary>
public sealed record PassportNotice(string Pill, string Text, string? Href, string? LinkText)
{
    /// <summary>
    /// An earlier or newer passport of the same battery: drawn calmly, as a small chip beside the passport badge and one
    /// plain line (redesign R3, owner's answer to Q4), not as a warning.
    /// </summary>
    public bool Calm { get; init; }
}

/// <summary>Where a published passport is: its version, and the battery's ID (#6) and serial number (#11).</summary>
public sealed record PassportLocation(PassportVersion Version, string Identifier, string SerialNumber)
{
    /// <summary>The pages' address: the battery ID for the current version, the version's own passport ID otherwise.</summary>
    public string Address => Version.DppStatus == DppStatuses.Active ? Identifier : Version.DigitalProductPassportId;
}

/// <summary>
/// The public summary and the detail page (design 12, step 3b). A passport is found by its battery ID (#6, the
/// current version), its passport ID (#5, that version) or the battery's serial number (#11). Only published
/// versions are shown: drafts stay with the team (J-6).
/// </summary>
public sealed class PassportPages(DppStore store, PassportReader reader, AttributeForms forms, IIdentifierScheme identifiers)
{
    /// <summary>The reader-facing title and line of each schema section, in the schema's order.</summary>
    private static readonly (string Key, string Title, string Tab, string Note)[] SectionTexts =
    [
        (AttributeDefinition.HeaderSection, "Passport", "Passport", "This passport and the battery it belongs to."),
        ("identifiersAndProductData", "Identifiers and product data", "Identifiers", "What the battery is and who makes it."),
        ("symbolsLabelsAndDocumentationOfConformity", "Symbols, labels and conformity", "Symbols and conformity", "The battery's symbols and its declaration of conformity."),
        ("batteryCarbonFootprint", "Carbon footprint", "Carbon footprint", "Per functional unit and per life-cycle stage."),
        ("supplyChainDueDiligence", "Due diligence", "Due diligence", "The supply chain due diligence report."),
        ("batteryMaterialsAndComposition", "Materials and composition", "Materials", "Chemistry, critical raw materials and hazardous substances."),
        ("circularityAndResourceEfficiency", "Circularity", "Circularity", "Recycled and renewable content, and the end user's role in collection and waste prevention."),
        ("performanceAndDurability", "Performance and durability", "Performance", "Capacity, voltage, power, efficiency and expected lifetime."),
    ];

    /// <summary>The detail page's audience sections (design 5.1), with who sees each.</summary>
    private static readonly (Audience Audience, string Key, string Title, string Note)[] AudienceTexts =
    [
        (Audience.Purchaser, "purchaser", "Purchaser", "Seen by the battery's cluster and market surveillance."),
        (Audience.Shared, "shared", "Shared", "Seen by the battery's cluster, market surveillance, the Commission and, per value, repairers, recyclers and treatment facilities with a legitimate interest."),
        (Audience.Authorities, "authorities", "Authorities", "Seen by notified bodies, market surveillance and the Commission."),
    ];

    private static readonly (int Number, string Label)[] CarbonStages =
    [
        (31, "Raw material acquisition"),
        (32, "Main production"),
        (33, "Distribution"),
        (34, "End of life and recycling"),
    ];

    private static readonly (int Number, string Label)[] RecycledShares = [(49, "Nickel"), (50, "Cobalt"), (51, "Lithium"), (52, "Lead")];

    /// <summary>The public summary's address. Colons stay as they are, so a battery ID reads as written.</summary>
    public static string SummaryHref(string id) => $"/dpp/{Escape(id)}";

    /// <summary>The detail page's address.</summary>
    public static string DetailHref(string id) => $"/dpp/{Escape(id)}/detail";

    /// <summary>The QR code's picture of a battery, and the same picture as a file to save.</summary>
    public static string QrHref(string id) => $"/qr/{Escape(id)}/svg";

    public static string QrDownloadHref(string id) => $"/qr/{Escape(id)}/download";

    /// <summary>What a reader-facing source is called on the detail page.</summary>
    public static string SourceWord(string source) => source switch
    {
        "form" => "typed in the app",
        "system" => "set by the app",
        "upload" => "upload",
        "feed" => "battery system",
        "api" => "API",
        "seed" => "test data",
        _ => source,
    };

    /// <summary>
    /// The published version a battery ID (#6), passport ID (#5) or serial number (#11) names; null when there is none.
    /// A serial number counts only when exactly one published battery has it: the one with an active passport, else (an
    /// invalid or marked passport, redesign R1) the one whose latest version is published.
    /// </summary>
    public async Task<PassportVersion?> FindAsync(string? id, CancellationToken cancellationToken = default)
    {
        var text = id?.Trim() ?? string.Empty;
        if (text.Length == 0)
        {
            return null;
        }

        if (await store.GetVersionByPassportIdAsync(text, cancellationToken) is { } version)
        {
            return version;
        }

        if (await store.FindPackByUniqueProductIdentifierAsync(text, cancellationToken) is { } pack)
        {
            // A passport that is no longer active stays readable by its battery ID (step 4c).
            return await store.GetActiveVersionAsync(pack.Id, cancellationToken) ?? await store.GetLatestVersionAsync(pack.Id, cancellationToken);
        }

        var candidates = await store.FindPacksBySerialNumberAsync(text, cancellationToken);
        var published = new List<PassportVersion>();
        foreach (var candidate in candidates)
        {
            if (await store.GetActiveVersionAsync(candidate.Id, cancellationToken) is { } active)
            {
                published.Add(active);
            }
        }

        if (published.Count == 0)
        {
            // Marking a passport invalid leaves it no active version; it stays findable and says so (owner's point 10b).
            foreach (var candidate in candidates)
            {
                if (await store.GetLatestVersionAsync(candidate.Id, cancellationToken) is { } latest)
                {
                    published.Add(latest);
                }
            }
        }

        return published.Count == 1 ? published[0] : null;
    }

    /// <summary>The published passport an ID names, with the battery's ID and serial number; null when there is none.</summary>
    public async Task<PassportLocation?> LocateAsync(string? id, CancellationToken cancellationToken = default)
    {
        var version = await FindAsync(id, cancellationToken);
        var pack = version is null ? null : await store.GetAsync<BatteryPack>(version.PackId, cancellationToken);
        return version is null || pack is null
            ? null
            : new PassportLocation(version, PackText(pack, "uniqueProductIdentifier") ?? string.Empty, PackText(pack, "batterySerialNumber") ?? string.Empty);
    }

    /// <summary>The public summary: public data only, whoever is reading. <see cref="PassportPage.ReaderSeesMore"/> says whether the detail page has more for this reader.</summary>
    public Task<PassportPage?> SummaryAsync(Reader who, string id, CancellationToken cancellationToken = default) =>
        PageAsync(who, id, detail: false, cancellationToken);

    /// <summary>The detail page: the public data and each audience section the reader may see, every value with its origin.</summary>
    public Task<PassportPage?> DetailAsync(Reader who, string id, CancellationToken cancellationToken = default) =>
        PageAsync(who, id, detail: true, cancellationToken);

    private async Task<PassportPage?> PageAsync(Reader who, string id, bool detail, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(who);
        var version = await FindAsync(id, cancellationToken);
        var published = version is null ? null : await reader.VersionAsync(version.DigitalProductPassportId, cancellationToken);
        var sources = version is null ? null : await store.LoadSourcesAsync(version.PackId, cancellationToken);
        if (version is null || published is null || sources is null)
        {
            return null;
        }

        var clusters = await store.FindPublishedClusterIdsUsingAsync(packId: version.PackId, cancellationToken: cancellationToken);
        var everyone = PassportFilter.Cut(published.Passport, Reader.Anonymous, clusters);
        var seen = detail ? PassportFilter.Cut(published.Passport, who, clusters) : everyone;
        var family = await store.GetAsync<BatteryFamily>(sources.Model.FamilyId, cancellationToken);
        var identifier = Text(everyone, AttributeCatalog.ByNumber(6)) ?? string.Empty;
        var current = version.DppStatus == DppStatuses.Active ? null : await store.GetActiveVersionAsync(version.PackId, cancellationToken);

        var sections = SectionTexts
            .Select(section => new PassportSection(
                section.Key,
                section.Title,
                section.Tab,
                section.Note,
                Shown(seen, sources, detail, AttributeCatalog.All.Where(a => a.Audience == Audience.Public && a.Section == section.Key))))
            .Where(section => section.Values.Count > 0)
            .ToArray();
        var audiences = detail
            ? AudienceTexts
                .Where(audience => AttributeCatalog.All.Any(a => a.Audience == audience.Audience && AudienceRules.MaySee(who, a, clusters)))
                .Select(audience => new PassportSection(
                    audience.Key,
                    audience.Title,
                    audience.Title,
                    audience.Note,
                    Shown(seen, sources, detail, AttributeCatalog.All.Where(a => a.Audience == audience.Audience))))
                .ToArray()
            : [];

        var carbon = Chart(everyone, CarbonStages, "kgCO2EquivalentPerKilowattHourValue", "kgCO2-eq/kWh");
        var recycled = Chart(everyone, RecycledShares, "percentageValue", "%");
        string? Words(int number) => sections.SelectMany(section => section.Values).FirstOrDefault(value => value.Attribute.Number == number)?.Lines.FirstOrDefault()?.Text;
        return new PassportPage(
            version,
            current,
            identifier,
            family?.Name,
            // The model's own picture, else its family's (redesign R2, decision 20).
            sources.Model.PictureFileId is not null
                ? $"/dpp/models/{Uri.EscapeDataString(sources.Model.Id)}/picture"
                : family?.PictureFileId is null ? null : $"/dpp/families/{Uri.EscapeDataString(family.Id)}/picture",
            identifiers.IsTestIdentifier(identifier) || identifiers.IsTestIdentifier(version.DigitalProductPassportId),
            PassportReader.Verify(version),
            AttributeCatalog.All.Any(a => a.Audience != Audience.Public && AudienceRules.MaySee(who, a, clusters)),
            sections,
            audiences,
            carbon,
            recycled)
        {
            KeyFigures = KeyFigures.From(number => Find(everyone, AttributeCatalog.ByNumber(number)), Words, carbon, recycled),
            ModuleCount = sources.Pack.Modules.Count,
            ModuleDescription = sources.Model.ModuleDescription,
            Modules = detail && AudienceRules.MaySeePurchaserData(who, clusters) ? sources.Pack.Modules : null,
            Repairs = detail && AudienceRules.MaySeePurchaserData(who, clusters) ? sources.Pack.Repairs.AsEnumerable().Reverse().ToArray() : null,
            Notice = await NoticeAsync(version, sources.Pack, detail, detail && AudienceRules.MaySee(who, AttributeCatalog.ByNumber(22), clusters), cancellationToken),
            MarkReason = detail && sources.Pack.Mark is { Reason: { } reason } mark && version.DppStatus == mark.Status
                && (who.Roles.Contains(ReaderRole.Team) || who.Roles.Contains(ReaderRole.MarketSurveillance))
                ? $"{reason} (marked by {(mark.ByRole == "MarketSurveillance" ? "market surveillance" : "the team")}, {mark.By})"
                : null,
            MayMarkInvalid = detail && version.DppStatus == DppStatuses.Active && who.Roles.Contains(ReaderRole.MarketSurveillance),
        };
    }

    /// <summary>
    /// The S7 line: an inactive passport leads to the battery's new one (Scania's page, or another operator's address);
    /// a passport that replaces an earlier one leads back to it (design 7.5, step 4c-1), both calmly and dated (redesign
    /// R3). Only a reader who may see the battery status (#22, purchaser data) reads what happened ("Repurposed on …");
    /// everyone else reads the facts without it. Invalid and marked passports keep a warning. Null for any other passport.
    /// </summary>
    private async Task<PassportNotice?> NoticeAsync(PassportVersion version, BatteryPack pack, bool detail, bool showsStatus, CancellationToken cancellationToken)
    {
        string Href(string identifier) => detail ? DetailHref(identifier) : SummaryHref(identifier);
        if (version.DppStatus == DppStatuses.Invalid)
        {
            return new PassportNotice("Invalid", $"This passport was marked invalid on {ValueWords.Day(version.LastUpdatedUtc.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture))}. Its data can't be relied on.", null, null);
        }

        if (version.DppStatus == DppStatuses.MarkedForDeletion)
        {
            return new PassportNotice("Marked for deletion", "The battery was recycled or exported. This passport stays readable until it is deleted.", null, null);
        }

        if (version.DppStatus == DppStatuses.Inactive && pack.Succession is { } succession)
        {
            var what = Happened(succession);
            var since = Day(succession.StartedAtUtc);
            if (succession.SuccessorPackId is { } successorId && await store.GetAsync<BatteryPack>(successorId, cancellationToken) is { } successor
                && PackText(successor, "uniqueProductIdentifier") is { } newIdentifier)
            {
                var text = showsStatus ? $"{what}; the battery continues on its new passport." : $"This battery continues on a new passport since {since}.";
                return new PassportNotice("Inactive", text, Href(newIdentifier), "New passport") { Calm = true };
            }

            if (succession.OtherPassport is { } other)
            {
                var link = Uri.TryCreate(other, UriKind.Absolute, out var address) && address.Scheme is "https" or "http" ? other : null;
                var issuer = succession.OtherOperatorInfo ?? succession.OtherOperatorId;
                var tail = link is null ? $": {other}" : string.Empty;
                var text = showsStatus
                    ? $"{what} by {issuer}, which issued its new passport{tail}."
                    : $"This battery continues on a new passport issued by {issuer} since {since}{tail}.";
                return new PassportNotice("Inactive", text, link, link is null ? null : "New passport") { Calm = true };
            }

            return new PassportNotice("Inactive", showsStatus ? $"{what}; the battery has a new passport." : $"This battery has a new passport since {since}.", null, null) { Calm = true };
        }

        if (version.DppStatus == DppStatuses.Active && pack.SuccessorOf is { } predecessorId
            && await store.GetAsync<BatteryPack>(predecessorId, cancellationToken) is { } predecessor
            && PackText(predecessor, "uniqueProductIdentifier") is { } oldIdentifier)
        {
            var text = predecessor.Succession is not { } earlier
                ? "This passport replaces an earlier one of the same battery."
                : showsStatus
                    ? $"{Happened(earlier)}. Its earlier passport is inactive and stays readable."
                    : $"Replaces an earlier passport of this battery (since {Day(earlier.StartedAtUtc)}).";
            return new PassportNotice("Active", text, Href(oldIdentifier), "Earlier passport") { Calm = true };
        }

        return null;
    }

    /// <summary>What happened to the battery and when, in words: "Repurposed on 2 Nov 2026". Only for readers of #22.</summary>
    private static string Happened(Succession succession) =>
        $"{StatusBadge.Battery(succession.Status)?.Words ?? succession.Status} on {Day(succession.StartedAtUtc)}";

    private static string Day(DateTime utc) => ValueWords.Day(utc.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture));

    /// <summary>The attributes' values in the cut passport, in catalogue order; the detail page adds each value's origin.</summary>
    private IReadOnlyList<ShownValue> Shown(JsonObject passport, PassportSources sources, bool detail, IEnumerable<AttributeDefinition> attributes)
    {
        var shown = new List<ShownValue>();
        foreach (var attribute in attributes)
        {
            var value = Find(passport, attribute);
            var lines = ValueWords.Lines(forms.For(attribute), value);
            if (lines.Count == 0)
            {
                continue;
            }

            shown.Add(new ShownValue(attribute, lines, detail ? Origin(attribute, sources, value) : null));
        }

        return shown;
    }

    /// <summary>
    /// The stored value's time and source, when the stored value is still the one shown. An archived version may show a
    /// value that has changed since; its origin isn't known any more, so none is given. #1–#5 are set by the app. The
    /// detail page and the API (3c) both show it.
    /// </summary>
    public static ValueOrigin? Origin(AttributeDefinition attribute, PassportSources sources, JsonNode? shown)
    {
        var stored = attribute.Storage switch
        {
            StorageLocation.Pack => sources.Pack.Values.GetValueOrDefault(attribute.Property),
            StorageLocation.Operator => sources.Operator.Values.GetValueOrDefault(attribute.Property),
            StorageLocation.Batch => sources.Batch.Values.GetValueOrDefault(attribute.Property),
            StorageLocation.Site => sources.Site.Values.GetValueOrDefault(attribute.Property),
            StorageLocation.Model => sources.Model.Values.GetValueOrDefault(attribute.Property),
            StorageLocation.ModelWithPackOverride => sources.Pack.Values.GetValueOrDefault(attribute.Property) ?? sources.Model.Values.GetValueOrDefault(attribute.Property),
            _ => null,
        };
        return stored is null || !FormValues.SameValue(stored.Value, shown)
            ? null
            : new ValueOrigin(stored.GeneratedAtUtc, stored.Source, attribute.Change == ChangeRule.Live);
    }

    private static IReadOnlyList<ChartPart> Chart(JsonObject passport, (int Number, string Label)[] parts, string valueName, string unit) =>
        parts
            .Select(part => (part.Label, Value: ValueWords.Decimal(Find(passport, AttributeCatalog.ByNumber(part.Number))?[valueName])))
            .Where(part => part.Value is not null)
            .Select(part => new ChartPart(part.Label, part.Value!.Value, unit))
            .ToArray();

    private static JsonNode? Find(JsonObject passport, AttributeDefinition attribute)
    {
        JsonNode? node = passport;
        foreach (var name in attribute.Path.Split('.'))
        {
            node = node?[name];
        }

        return node;
    }

    private static string? Text(JsonObject passport, AttributeDefinition attribute) =>
        Find(passport, attribute) is JsonValue value && value.TryGetValue<string>(out var text) ? text : null;

    private static string? PackText(BatteryPack pack, string property) =>
        pack.Values.GetValueOrDefault(property)?.Value is JsonValue value && value.TryGetValue<string>(out var text) ? text : null;

    private static string Escape(string id) => Uri.EscapeDataString(id).Replace("%3A", ":", StringComparison.Ordinal);
}
