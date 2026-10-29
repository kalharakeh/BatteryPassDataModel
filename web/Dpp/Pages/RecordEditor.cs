using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using BatteryPassWeb.Dpp.Catalog;
using BatteryPassWeb.Dpp.Documents;
using BatteryPassWeb.Dpp.Editing;
using BatteryPassWeb.Dpp.Forms;
using BatteryPassWeb.Dpp.Store;
using BatteryPassWeb.Dpp.Values;

namespace BatteryPassWeb.Dpp.Pages;

/// <summary>What was typed into a record's editor: the values that differ from the stored ones, and every text as typed.</summary>
public sealed record EditorPost(
    IReadOnlyDictionary<string, JsonNode> Changed,
    IReadOnlyDictionary<string, string> Texts,
    IReadOnlyDictionary<string, int> Rows)
{
    private static readonly IReadOnlySet<string> None = new HashSet<string>(StringComparer.Ordinal);

    /// <summary>Stored values whose fields were emptied on the page: the team removes them.</summary>
    public IReadOnlySet<string> Removals { get; init; } = None;

    /// <summary>Fields edited on the page whose stored value someone else changed after the page was opened.</summary>
    public IReadOnlySet<string> Conflicts { get; init; } = None;

    /// <summary>The fields edited on the page; null when the page sent no stamp, so every field counts as edited.</summary>
    public IReadOnlySet<string>? Edited { get; init; }

    /// <summary>When the typed values were measured (live values, design 7.3); the save time when null.</summary>
    public DateTime? MeasuredAtUtc { get; init; }

    public bool WasEdited(string property) => Edited is null || Edited.Contains(property);
}

/// <summary>A published passport that a change reaches, with the version it is on now.</summary>
public sealed record AffectedPassport(string PackId, string SerialNumber, string? ClusterId, int CurrentVersion);

public enum EditorOutcome
{
    Saved,
    NothingChanged,
    Refused,
    NeedsConfirmation,
    NotFound,
}

/// <summary>
/// The result of saving an editor. <see cref="EditorOutcome.Refused"/> means some values were refused and the others
/// saved (J-12); <see cref="EditorOutcome.NeedsConfirmation"/> means nothing was saved yet because the change would
/// publish new versions of <see cref="Affected"/>. After a save, <see cref="Affected"/> lists the passports that got one.
/// </summary>
public sealed record EditorSave(EditorOutcome Outcome, IReadOnlyList<KeyResult> Keys, IReadOnlyList<AffectedPassport> Affected)
{
    public int SavedCount => Keys.Count(key => key.Outcome == KeyOutcome.Saved);

    public int RemovedCount => Keys.Count(key => key.Outcome == KeyOutcome.Removed);

    public int RefusedCount => Keys.Count(key => key.Outcome == KeyOutcome.Rejected);

    /// <summary>The refused values in plain words, with the parts of a grouped value they are about, by attribute property.</summary>
    public IReadOnlyDictionary<string, Refusal> Refusals(AttributeForms forms) => Keys
        .Where(key => key.Outcome == KeyOutcome.Rejected && AttributeCatalog.FindByProperty(key.Key) is not null)
        .ToDictionary(
            key => key.Key,
            key =>
            {
                var form = forms.For(AttributeCatalog.FindByProperty(key.Key)!);
                return new Refusal(RefusalWording.Describe(form, key.Message), RefusalWording.Parts(form, key.Message));
            },
            StringComparer.Ordinal);
}

/// <summary>Why a value was refused, in plain words, and which inputs of a grouped value it is about.</summary>
public sealed record Refusal(string Text, IReadOnlySet<string> Parts);

/// <summary>One attribute on an editor page, with what its inputs show and whether the record has a value for it.</summary>
public sealed record EditorField(
    AttributeForm Form,
    IReadOnlyDictionary<string, string> Texts,
    int Rows,
    string? Refusal,
    DppDocument? Document,
    bool HasValue)
{
    public AttributeDefinition Attribute => Form.Attribute;

    /// <summary>The inputs of a grouped value the refusal is about; empty means the whole field.</summary>
    public IReadOnlySet<string> RefusedParts { get; init; } = new HashSet<string>(StringComparer.Ordinal);

    /// <summary>For a document field without an uploaded file in the app: the link its stored value points to.</summary>
    public string? StoredLink { get; init; }

    /// <summary>
    /// Empty although the passport needs it by February 2027 (the file's M or (M), with the Feb 2027 mark): what the team
    /// still has to fill in. Conditional values and values needed only later are not counted.
    /// </summary>
    public bool IsMissing => !HasValue && Attribute.Requirement != Requirement.Conditional && Attribute.MandatoryFromFeb2027;

    /// <summary>True when this input of a grouped value should be marked as refused.</summary>
    public bool IsRefused(FormInput input) =>
        Refusal is not null && (RefusedParts.Count == 0 || RefusedParts.Contains(input.Path));

    /// <summary>Who may see the value (or download the document), in words: design 5.2, the rule of <c>AudienceRules.MaySee</c>.</summary>
    public string SeenBy => Attribute.Audience switch
    {
        Audience.Authorities => "notified bodies, market surveillance, the Commission and your team",
        Audience.Shared => $"{SharedGroups()}, the battery's cluster, market surveillance, the Commission and your team",
        Audience.Purchaser => "the battery's cluster, market surveillance and your team",
        _ => "everyone",
    };

    private string SharedGroups()
    {
        var groups = new List<string>();
        if (Attribute.SharedWith.HasFlag(LegitimateInterestGroups.Repair))
        {
            groups.Add("repairers and remanufacturers");
        }

        if (Attribute.SharedWith.HasFlag(LegitimateInterestGroups.Recycling))
        {
            groups.Add("recyclers");
        }

        if (Attribute.SharedWith.HasFlag(LegitimateInterestGroups.Treatment))
        {
            groups.Add("treatment facilities");
        }

        return string.Join(", ", groups);
    }

    public string Text(string fieldName) => Texts.GetValueOrDefault(fieldName, string.Empty);

    /// <summary>The requirement in the grey marker: the file's M, (M) or C, and whether it is needed by February 2027.</summary>
    public string Marker => Attribute.Requirement == Requirement.Conditional
        ? "Conditional"
        : Attribute.MandatoryFromFeb2027 ? "Required by Feb 2027" : "Required later";

    /// <summary>Who may see the value, shown only when it isn't public.</summary>
    public string? AudienceLabel => Attribute.Audience switch
    {
        Audience.Purchaser => "Purchaser",
        Audience.Shared => "Shared",
        Audience.Authorities => "Authorities",
        _ => null,
    };
}

/// <summary>A group of fields with its side-menu entry and intro line; one per schema section.</summary>
public sealed record EditorSection(string Id, string Title, string Note, IReadOnlyList<EditorField> Fields)
{
    public int Filled => Fields.Count(item => item.HasValue);

    public int Missing => Fields.Count(item => item.IsMissing);
}

/// <summary>
/// The editor behind the model, batch, site and operator pages (design 6.2, decision 23): it shows a record's attributes
/// grouped by schema section, reads what was typed, keeps only the values that changed, asks before a change publishes
/// new versions, and saves through the save pipeline.
/// </summary>
public sealed class RecordEditor(AttributeForms forms, SavePipeline pipeline, DppStore store, DocumentStore documents)
{
    /// <summary>The title and intro line of each schema section.</summary>
    private static readonly Dictionary<string, (string Title, string Note)> SectionTexts = new(StringComparer.Ordinal)
    {
        [AttributeDefinition.HeaderSection] = ("Identifiers", "The operator's unique identifier, as registered."),
        ["identifiersAndProductData"] = ("Identifiers and product data", "What the battery is and who makes it."),
        ["symbolsLabelsAndDocumentationOfConformity"] = ("Symbols, labels and conformity", "Public documents are links to your website; test reports are uploaded and shown only to authorities."),
        ["batteryCarbonFootprint"] = ("Carbon footprint", "Per functional unit and per life-cycle stage. Not needed until after February 2027."),
        ["supplyChainDueDiligence"] = ("Due diligence", "A link to your supply chain due diligence report. Not needed until after February 2027."),
        ["batteryMaterialsAndComposition"] = ("Materials and composition", "Chemistry, critical raw materials and hazardous substances."),
        ["circularityAndResourceEfficiency"] = ("Circularity", "Recycled and renewable content, and what repairers, recyclers and end users need to know."),
        ["performanceAndDurability"] = ("Performance and durability", "Fades and expected lifetime are starting values; each battery re-states them when its status changes."),
    };

    public AttributeForms Forms => forms;

    // A site's and an operator's few values sit in the model's sections; their intro says what they are about.
    private static string Note(TargetKind kind, string section) => (kind, section) switch
    {
        (TargetKind.Site, "identifiersAndProductData") => "The manufacturer and the place where the batteries are made.",
        (TargetKind.Operator, "identifiersAndProductData") => "The economic operator that places the batteries on the market.",
        (TargetKind.Pack, "identifiersAndProductData") => "This battery's own values. The serial number, manufacturing date and warranty are fixed once its passport is published.",
        (TargetKind.Pack, "performanceAndDurability") => "Measured on the battery. Its battery system sends them; type them here before it does, for example the end-of-line values, or to correct one.",
        _ => SectionTexts[section].Note,
    };

    /// <summary>
    /// The attributes the team edits on a model, batch, site or operator, in catalogue order; for a pack, the values the
    /// team types (#11, #13, #17, #19). A pack's live values have their own form (<see cref="LiveAttributes"/>).
    /// </summary>
    public static IReadOnlyList<AttributeDefinition> AttributesOf(TargetKind kind) => AttributeCatalog.All
        .Where(attribute => kind switch
        {
            TargetKind.Model => attribute.Storage is StorageLocation.Model or StorageLocation.ModelWithPackOverride,
            TargetKind.Batch => attribute.Storage == StorageLocation.Batch,
            TargetKind.Site => attribute.Storage == StorageLocation.Site,
            TargetKind.Operator => attribute.Storage == StorageLocation.Operator,
            TargetKind.Pack => IsTypedPackValue(attribute),
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
        })
        .ToArray();

    /// <summary>
    /// The live values (#63, #83, #87): the battery system feeds them, and the team types them too, for example the
    /// end-of-line values a draft needs before publication (design 7.1). They change in place, without a new version.
    /// </summary>
    public static IReadOnlyList<AttributeDefinition> LiveAttributes { get; } = AttributeCatalog.All
        .Where(attribute => attribute.Change == ChangeRule.Live && attribute.Editors.HasFlag(AttributeEditors.BatterySystemFeed))
        .ToArray();

    // #6 and #92 are the app's, #22 changes only through the status change (step 4), the live values have their own form.
    private static bool IsTypedPackValue(AttributeDefinition attribute) =>
        attribute.Storage == StorageLocation.Pack
        && attribute.Editors.HasFlag(AttributeEditors.Team)
        && attribute.Change is ChangeRule.Fixed or ChangeRule.IfProviderChanges;

    /// <summary>How many of the record's attributes have a value.</summary>
    public static int CountFilled(TargetKind kind, IReadOnlyDictionary<string, AttributeValue> stored) =>
        AttributesOf(kind).Count(attribute => stored.ContainsKey(attribute.Property));

    /// <summary>How many of the record's values the passport needs by February 2027 are still empty (see <see cref="EditorField.IsMissing"/>).</summary>
    public static int CountMissing(TargetKind kind, IReadOnlyDictionary<string, AttributeValue> stored) =>
        AttributesOf(kind).Count(attribute =>
            !stored.ContainsKey(attribute.Property) && attribute.Requirement != Requirement.Conditional && attribute.MandatoryFromFeb2027);

    /// <summary>
    /// The values the schema allows only one way, which have no input (#20 battery category). A new record starts with
    /// them, so they never show as missing and a save counts only what was typed.
    /// </summary>
    public IReadOnlyDictionary<string, JsonNode> OneWayValues(TargetKind kind) =>
        AttributesOf(kind)
            .Select(forms.For)
            .Where(form => form.Kind == FormKind.Fixed)
            .ToDictionary(form => form.Attribute.Property, form => FormValues.Read(form, _ => null)!, StringComparer.Ordinal);

    /// <summary>
    /// What the page shows, as a short fingerprint per field (and one for the name): the page posts it back, so a save
    /// can tell the fields edited on the page from those it only showed. A page left open then never undoes a newer
    /// value saved by someone else, an upload or a feed.
    /// </summary>
    public string Stamp(TargetKind kind, IReadOnlyDictionary<string, AttributeValue> stored, string? name) =>
        Stamp(AttributesOf(kind), stored, name);

    /// <summary>The stamp of a form that shows only these <paramref name="attributes"/> (e.g. a pack's live values).</summary>
    public string Stamp(IReadOnlyList<AttributeDefinition> attributes, IReadOnlyDictionary<string, AttributeValue> stored, string? name)
    {
        ArgumentNullException.ThrowIfNull(attributes);
        ArgumentNullException.ThrowIfNull(stored);
        var parts = attributes
            .Select(forms.For)
            .Where(form => form.Kind is not (FormKind.Upload or FormKind.Fixed))
            .Select(form => $"{form.Attribute.Property}:{Fingerprint(FormValues.Show(form, stored.GetValueOrDefault(form.Attribute.Property)?.Value))}");
        return string.Join(',', name is null ? parts : parts.Prepend($"{NameKey}:{Fingerprint([new(NameKey, name)])}"));
    }

    /// <summary>True when the posted name differs from the one the page showed (always, when the page sent no stamp).</summary>
    public static bool NameEdited(string? stamp, string? postedName) =>
        ParseStamp(stamp)?.GetValueOrDefault(NameKey) is not { } shown || shown != Fingerprint([new(NameKey, postedName ?? string.Empty)]);

    /// <summary>
    /// Reads the posted form. With the page's <paramref name="stamp"/>, only the fields edited on the page count: an
    /// edited field that differs from the stored value is a change, an emptied one a removal, and one whose stored value
    /// someone else changed meanwhile a conflict. Without a stamp, every non-blank value that differs is a change and
    /// nothing is removed.
    /// </summary>
    /// <param name="field">Gives the posted text of a field name, or null when it wasn't posted.</param>
    public EditorPost Read(TargetKind kind, IReadOnlyDictionary<string, AttributeValue> stored, Func<string, string?> field, string? stamp = null) =>
        Read(AttributesOf(kind), stored, field, stamp);

    /// <summary>Reads a form that shows only these <paramref name="attributes"/>, by the rules above.</summary>
    public EditorPost Read(IReadOnlyList<AttributeDefinition> attributes, IReadOnlyDictionary<string, AttributeValue> stored, Func<string, string?> field, string? stamp = null)
    {
        ArgumentNullException.ThrowIfNull(attributes);
        ArgumentNullException.ThrowIfNull(stored);
        ArgumentNullException.ThrowIfNull(field);
        var shown = ParseStamp(stamp);
        var changed = new Dictionary<string, JsonNode>(StringComparer.Ordinal);
        var texts = new Dictionary<string, string>(StringComparer.Ordinal);
        var rows = new Dictionary<string, int>(StringComparer.Ordinal);
        var edited = new HashSet<string>(StringComparer.Ordinal);
        var removals = new HashSet<string>(StringComparer.Ordinal);
        var conflicts = new HashSet<string>(StringComparer.Ordinal);
        foreach (var attribute in attributes)
        {
            var form = forms.For(attribute);
            var count = form.Kind == FormKind.Rows && int.TryParse(field(form.RowCountField), out var posted) ? posted : 0;
            rows[attribute.Property] = count;
            var names = FieldNames(form, count).ToArray();
            foreach (var name in names)
            {
                texts[name] = field(name) ?? string.Empty;
            }

            var current = stored.GetValueOrDefault(attribute.Property)?.Value;
            var wasShown = shown?.GetValueOrDefault(attribute.Property);
            if (wasShown is not null)
            {
                if (Fingerprint(names.Select(name => new KeyValuePair<string, string>(name, texts[name]))) == wasShown)
                {
                    continue; // not edited on the page: whatever is stored now stays
                }

                edited.Add(attribute.Property);
            }

            var changedMeanwhile = wasShown is not null && Fingerprint(FormValues.Show(form, current)) != wasShown;
            var value = FormValues.Read(form, field);
            if (value is null)
            {
                // Emptied on the page: a removal, only when the page says it was edited there.
                if (wasShown is not null && current is not null && names.All(name => string.IsNullOrWhiteSpace(texts[name])))
                {
                    (changedMeanwhile ? conflicts : removals).Add(attribute.Property);
                }

                continue;
            }

            if (FormValues.SameValue(current, value))
            {
                continue;
            }

            if (changedMeanwhile)
            {
                conflicts.Add(attribute.Property);
                continue;
            }

            changed[attribute.Property] = value;
        }

        return new EditorPost(changed, texts, rows)
        {
            Edited = shown is null ? null : edited,
            Removals = removals,
            Conflicts = conflicts,
        };
    }

    private const string NameKey = "@name";

    private static Dictionary<string, string>? ParseStamp(string? stamp)
    {
        if (string.IsNullOrWhiteSpace(stamp))
        {
            return null;
        }

        var parsed = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var part in stamp.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var colon = part.LastIndexOf(':');
            if (colon > 0)
            {
                parsed[part[..colon]] = part[(colon + 1)..];
            }
        }

        return parsed;
    }

    /// <summary>
    /// A short fingerprint of a field's texts: the non-blank ones, trimmed, with browser line endings evened out, so
    /// an untouched field posts back exactly the fingerprint it was shown with.
    /// </summary>
    private static string Fingerprint(IEnumerable<KeyValuePair<string, string>> texts)
    {
        var canonical = new StringBuilder();
        foreach (var (name, text) in texts
            .Select(entry => (entry.Key, Text: entry.Value.Replace("\r\n", "\n", StringComparison.Ordinal).Trim()))
            .Where(entry => entry.Text.Length > 0)
            .OrderBy(entry => entry.Key, StringComparer.Ordinal))
        {
            canonical.Append(name).Append('\u001f').Append(text).Append('\u001e');
        }

        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical.ToString())), 0, 6).ToLower(CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// The editor's sections. Without <paramref name="post"/> the fields show the stored values; with it, what was typed,
    /// so a refused value can be corrected in place.
    /// </summary>
    public Task<IReadOnlyList<EditorSection>> SectionsAsync(
        TargetKind kind,
        string id,
        IReadOnlyDictionary<string, AttributeValue> stored,
        EditorPost? post = null,
        IReadOnlyDictionary<string, Refusal>? refusals = null,
        CancellationToken cancellationToken = default) =>
        SectionsAsync(kind, id, AttributesOf(kind), stored, post, refusals, cancellationToken);

    /// <summary>The sections of a form that shows only these <paramref name="attributes"/> of the record.</summary>
    public async Task<IReadOnlyList<EditorSection>> SectionsAsync(
        TargetKind kind,
        string id,
        IReadOnlyList<AttributeDefinition> attributes,
        IReadOnlyDictionary<string, AttributeValue> stored,
        EditorPost? post = null,
        IReadOnlyDictionary<string, Refusal>? refusals = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(attributes);
        ArgumentNullException.ThrowIfNull(stored);
        var fields = new List<EditorField>();
        foreach (var attribute in attributes)
        {
            var form = forms.For(attribute);
            var value = stored.GetValueOrDefault(attribute.Property)?.Value;

            // After a post, a field edited on the page shows what was typed; the others show what is stored now.
            var typed = post is not null && post.WasEdited(attribute.Property);
            var texts = typed ? post!.Texts : FormValues.Show(form, value);

            // Rows show the stored ones plus one blank row to add another; as typed, exactly the rows that were posted.
            var rows = typed ? post!.Rows.GetValueOrDefault(attribute.Property) : FormValues.RowCount(value) + 1;
            var document = form.Kind == FormKind.Upload
                ? (await documents.ListAsync(kind, id, attribute.Property, cancellationToken)).FirstOrDefault()
                : null;
            var refusal = refusals?.GetValueOrDefault(attribute.Property);
            fields.Add(new EditorField(form, texts, rows, refusal?.Text, document, value is not null)
            {
                RefusedParts = refusal?.Parts ?? new HashSet<string>(StringComparer.Ordinal),
                StoredLink = form.Kind == FormKind.Upload && value?["url"] is JsonValue url && url.TryGetValue<string>(out var link) ? link : null,
            });
        }

        return fields
            .GroupBy(field => field.Attribute.Section)
            .Select(group => new EditorSection($"section-{group.Key}", SectionTexts[group.Key].Title, Note(kind, group.Key), group.ToArray()))
            .ToArray();
    }

    /// <summary>
    /// Saves the changed values. A change that would publish new versions of published passports is only saved once
    /// <paramref name="confirmed"/>; until then the result lists those passports and nothing is stored.
    /// </summary>
    public async Task<EditorSave> SaveAsync(ChangeSender sender, TargetKind kind, string id, EditorPost post, bool confirmed, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(post);
        var conflicts = post.Conflicts.Select(key => new KeyResult(key, KeyOutcome.Rejected, ConflictMessage)).ToArray();
        if (post.Changed.Count == 0 && post.Removals.Count == 0)
        {
            return conflicts.Length > 0
                ? new EditorSave(EditorOutcome.Refused, conflicts, [])
                : new EditorSave(EditorOutcome.NothingChanged, [], []);
        }

        var changes = new ChangeSet(
            kind,
            id,
            post.Changed.ToDictionary(entry => entry.Key, entry => (JsonNode?)entry.Value.DeepClone(), StringComparer.Ordinal),
            "form",
            post.MeasuredAtUtc,
            post.Removals.ToArray());
        var preview = await pipeline.SaveAsync(sender, changes, preview: true, cancellationToken);
        if (!preview.Found)
        {
            return new EditorSave(EditorOutcome.NotFound, [], []);
        }

        var publishes = preview.Keys.Any(key => key.Outcome is KeyOutcome.Saved or KeyOutcome.Removed && AttributeCatalog.FindByProperty(key.Key)?.Change != ChangeRule.Live);
        var affected = publishes ? await AffectedAsync(kind, id, cancellationToken) : [];
        if (affected.Count > 0 && !confirmed)
        {
            return new EditorSave(EditorOutcome.NeedsConfirmation, [.. preview.Keys, .. conflicts], affected);
        }

        // Saved or refused: Affected now lists the passports that got a new version.
        var result = await pipeline.SaveAsync(sender, changes, preview: false, cancellationToken);
        IReadOnlyList<KeyResult> keys = [.. result.Keys, .. conflicts];
        return new EditorSave(keys.Any(key => key.Outcome == KeyOutcome.Rejected) ? EditorOutcome.Refused : EditorOutcome.Saved, keys, affected);
    }

    /// <summary>Why an edit made on a page that was open while someone else saved the same value is refused.</summary>
    public const string ConflictMessage =
        "Someone else saved this value after you opened this page. What you typed is still here: save again to use it, or Cancel edits to see theirs.";

    /// <summary>The published passports built from this record, which a static change gives a new version.</summary>
    public async Task<IReadOnlyList<AffectedPassport>> AffectedAsync(TargetKind kind, string id, CancellationToken cancellationToken = default)
    {
        var packIds = kind switch
        {
            TargetKind.Model => await store.FindPackIdsUsingAsync(modelId: id, cancellationToken: cancellationToken),
            TargetKind.Site => await store.FindPackIdsUsingAsync(siteId: id, cancellationToken: cancellationToken),
            TargetKind.Batch => await store.FindPackIdsUsingAsync(batchId: id, cancellationToken: cancellationToken),
            TargetKind.Operator => await store.FindPackIdsUsingAsync(operatorId: id, cancellationToken: cancellationToken),
            _ => [id],
        };
        var versions = (await store.ListActiveVersionsAsync(packIds, cancellationToken)).ToDictionary(version => version.PackId, StringComparer.Ordinal);
        var packs = await store.GetManyAsync<BatteryPack>(versions.Keys, cancellationToken);
        return packs
            .Select(pack => new AffectedPassport(
                pack.Id,
                pack.Values.GetValueOrDefault("batterySerialNumber")?.Value?.GetValue<string>() ?? pack.Id,
                pack.ClusterId,
                versions[pack.Id].Number))
            .OrderBy(passport => passport.SerialNumber, StringComparer.Ordinal)
            .ToArray();
    }

    private static IEnumerable<string> FieldNames(AttributeForm form, int rows) => form.Kind switch
    {
        FormKind.Upload or FormKind.Fixed => [],
        FormKind.Rows => Enumerable.Range(0, rows).SelectMany(row => form.Inputs.Select(input => form.FieldName(input, row))),
        _ => form.Inputs.Select(input => form.FieldName(input)),
    };
}
