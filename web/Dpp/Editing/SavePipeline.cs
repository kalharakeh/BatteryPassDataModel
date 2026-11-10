using System.Text.Json;
using System.Text.Json.Nodes;
using BatteryPassWeb.Dpp.Catalog;
using BatteryPassWeb.Dpp.Documents;
using BatteryPassWeb.Dpp.Store;
using BatteryPassWeb.Dpp.Values;
using BatteryPassWeb.Services;
using MongoDB.Bson;

namespace BatteryPassWeb.Dpp.Editing;

/// <summary>The record kinds that hold attribute values.</summary>
public enum TargetKind
{
    Model,
    Site,
    Batch,
    Operator,
    Pack,
}

public enum KeyOutcome
{
    /// <summary>Accepted (saved, or would be saved in a preview).</summary>
    Saved,

    /// <summary>Empty: the stored value is kept.</summary>
    KeptEmpty,

    /// <summary>Not in the attribute list; never stored.</summary>
    Ignored,

    Rejected,

    /// <summary>A stored value the team removed (or would remove, in a preview).</summary>
    Removed,
}

public sealed record KeyResult(string Key, KeyOutcome Outcome, string Message);

/// <summary>One change set for one record. <paramref name="Source"/> is "form", "upload" or "feed".</summary>
/// <param name="MeasuredAtUtc">When the values were generated (e.g. a feed's measurement time); the save time if null.</param>
/// <param name="Removals">Stored values to remove. Only the team removes values; for everyone else empty never overwrites.</param>
public sealed record ChangeSet(
    TargetKind Target,
    string TargetId,
    IReadOnlyDictionary<string, JsonNode?> Values,
    string Source,
    DateTime? MeasuredAtUtc = null,
    IReadOnlyCollection<string>? Removals = null)
{
    /// <summary>
    /// True only for the app's own write of a value it keeps itself (#92, when an accident report is added). Never set from
    /// anything a sender sent; every other change set naming such a value is refused for every sender (S03).
    /// </summary>
    public bool ByTheApp { get; init; }
}

public sealed record SaveResult(bool Found, bool Saved, IReadOnlyList<KeyResult> Keys);

/// <summary>The keys of a change set, judged one by one, and the accepted values in the schema's own form.</summary>
public sealed record Evaluation(IReadOnlyList<KeyResult> Results, IReadOnlyDictionary<string, JsonNode> Accepted);

/// <summary>
/// The one save path for passport data (design 6.2). For each key: is it a catalogue attribute of this record kind,
/// may the sender change it, is it empty, is it valid. Accepted keys are saved together and logged; rejected keys are
/// reported and don't block the rest (J-12). Every pack is a draft until publication arrives (plan 2b).
/// </summary>
public sealed class SavePipeline(
    DppStore store,
    AttributeValueNormalizer normalizer,
    AttributeValueValidator validator,
    ApplicationAuditService audit,
    TimeProvider clock,
    DppLinks links,
    IEnumerable<IPackChangeListener> listeners)
{
    public const string AuditEventType = "dpp.change";

    /// <summary>#92: the passport's accident link, which the app keeps itself.</summary>
    private const string AppOwnedAccidentLink = "informationOnAccidents";

    private readonly IPackChangeListener[] _listeners = listeners.ToArray();

    public Task<SaveResult> SaveAsync(ChangeSender sender, ChangeSet changes, bool preview = false, CancellationToken cancellationToken = default) =>
        SaveCoreAsync(sender, changes, preview, statusChange: false, cancellationToken);

    /// <summary>
    /// The status change's own way in (design 6.2 step 6 and 7.4, step 4b): the battery status (#22) and its re-stated
    /// values, which every other change set is refused. Rights and values are checked as for any change, and it is all
    /// or nothing: one refused or empty key saves none. Only <see cref="StatusChangeService"/> calls it, after its own
    /// checks of the transition.
    /// </summary>
    /// <param name="preview">True for an upload's check (step 5d): every key is judged, nothing is saved.</param>
    /// <param name="source">"form", or "upload" for a status change in an uploaded file (step 5d).</param>
    public Task<SaveResult> SaveStatusChangeAsync(ChangeSender sender, string packId, IReadOnlyDictionary<string, JsonNode?> values, CancellationToken cancellationToken = default, bool preview = false, string source = "form") =>
        SaveCoreAsync(sender, new ChangeSet(TargetKind.Pack, packId, values, source), preview, statusChange: true, cancellationToken);

    private async Task<SaveResult> SaveCoreAsync(ChangeSender sender, ChangeSet changes, bool preview, bool statusChange, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(sender);
        ArgumentNullException.ThrowIfNull(changes);

        var target = await FindTargetAsync(changes.Target, changes.TargetId, cancellationToken);
        if (!target.Found)
        {
            return new SaveResult(false, false, []);
        }

        // An inactive, invalid or marked-for-deletion passport takes no more changes (step 4c, decision 34).
        if (changes.Target == TargetKind.Pack && await store.PassportStatusAsync(changes.TargetId, cancellationToken) is { } status && DppStatuses.IsNoLongerActive(status))
        {
            var frozen = changes.Values.Keys.Concat(changes.Removals ?? []).Distinct(StringComparer.Ordinal)
                .Select(key => new KeyResult(key, KeyOutcome.Rejected, NoLongerChanges(status)))
                .ToArray();
            return new SaveResult(true, false, frozen);
        }

        var evaluation = EvaluateCore(sender, changes.Target, target.ClusterId, changes.Values, statusChange, changes.ByTheApp);
        if (changes.Target == TargetKind.Pack)
        {
            evaluation = await CheckPackRulesAsync(changes.TargetId, evaluation, cancellationToken);
        }

        var removals = await EvaluateRemovalsAsync(sender, changes, target, cancellationToken);
        var results = evaluation.Results.Concat(removals).ToArray();
        var removed = removals.Where(result => result.Outcome == KeyOutcome.Removed).Select(result => result.Key).ToArray();
        if (preview || (evaluation.Accepted.Count == 0 && removed.Length == 0)
            || (statusChange && results.Any(result => result.Outcome != KeyOutcome.Saved)))
        {
            return new SaveResult(true, false, results);
        }

        var generatedAt = changes.MeasuredAtUtc?.ToUniversalTime() ?? clock.GetUtcNow().UtcDateTime;
        var stored = evaluation.Accepted.ToDictionary(
            entry => entry.Key,
            entry => new AttributeValue { Value = entry.Value, GeneratedAtUtc = generatedAt, Source = changes.Source, Who = sender.Who },
            StringComparer.Ordinal);
        var saved = await ApplyAsync(changes.Target, changes.TargetId, stored, removed, cancellationToken);
        if (saved)
        {
            await audit.AppendApplicationAuditEventAsync(
                AuditEventType,
                sender.Who,
                sender.Kind.ToString(),
                changes.Source,
                removed.Length == 0
                    ? $"{stored.Count} value(s) saved on {changes.Target} {changes.TargetId}"
                    : $"{stored.Count} value(s) saved and {removed.Length} removed on {changes.Target} {changes.TargetId}",
                AuditMetadata(results, target.Values, stored),
                entityType: $"dpp-{changes.Target.ToString().ToLowerInvariant()}",
                entityId: changes.TargetId,
                cancellationToken: cancellationToken);
            foreach (var listener in _listeners)
            {
                await listener.ValuesSavedAsync(changes.Target, changes.TargetId, stored.Keys.Concat(removed).ToArray(), sender, cancellationToken);
            }
        }

        return new SaveResult(true, saved, results);
    }

    /// <summary>
    /// Judges each value to remove: a catalogue attribute of this record that the sender may change, stored now, and
    /// not one a published passport built from this record needs by February 2027 (it would block the new version and
    /// leave the published passport out of date). Only the team removes values.
    /// </summary>
    private async Task<IReadOnlyList<KeyResult>> EvaluateRemovalsAsync(ChangeSender sender, ChangeSet changes, Target target, CancellationToken cancellationToken)
    {
        if (changes.Removals is not { Count: > 0 } removals)
        {
            return [];
        }

        bool? published = null;
        var results = new List<KeyResult>();
        foreach (var key in removals.Distinct(StringComparer.Ordinal))
        {
            var (outcome, message, _) = EvaluateKey(sender, changes.Target, target.ClusterId, key, JsonValue.Create("remove"), statusChange: false, byTheApp: false, checkValue: false);
            if (outcome != KeyOutcome.Saved)
            {
                results.Add(new KeyResult(key, outcome, message));
                continue;
            }

            var attribute = AttributeCatalog.FindByProperty(key)!;
            if (sender.Kind != SenderKind.Team)
            {
                results.Add(new KeyResult(key, KeyOutcome.Rejected, $"{attribute.Label} is removed only by the team; an empty value never overwrites"));
            }
            else if (changes.Values.ContainsKey(key))
            {
                results.Add(new KeyResult(key, KeyOutcome.Ignored, "set and removed at once; the new value is kept"));
            }
            else if (!target.Values.ContainsKey(key))
            {
                results.Add(new KeyResult(key, KeyOutcome.Ignored, "no value to remove"));
            }
            else if (changes.Target == TargetKind.Pack && IsFixedPackValue(attribute) && await store.GetLatestVersionNumberAsync(changes.TargetId, cancellationToken) > 0)
            {
                results.Add(new KeyResult(key, KeyOutcome.Rejected, FixedOncePublished));
            }
            else if (attribute.Requirement != Requirement.Conditional && attribute.MandatoryFromFeb2027
                && (published ??= await UsedByPublishedPassportAsync(changes.Target, changes.TargetId, cancellationToken)))
            {
                results.Add(new KeyResult(key, KeyOutcome.Rejected, "The published passports need this value, so it can't be removed. Change it instead."));
            }
            else
            {
                results.Add(new KeyResult(key, KeyOutcome.Removed, string.Empty));
            }
        }

        return results;
    }

    /// <summary>Why a value of a passport that is no longer active is refused.</summary>
    public static string NoLongerChanges(string status) => $"the passport is {DppStatuses.Words(status).ToLowerInvariant()}; nothing on it changes";

    /// <summary>Why a pack's fixed value (#11, #17, #19) is refused after publication.</summary>
    public const string FixedOncePublished = "fixed once the passport is published";

    /// <summary>Why a serial number (#11) another battery already has is refused.</summary>
    public const string SerialNumberTaken = "another battery already has this serial number";

    /// <summary>
    /// A pack's own fixed values (#11, #17, #19): the team corrects them while the passport is a draft; once a version
    /// is published they stay as they are (parity check, design 14). #6 is the app's and never changes.
    /// </summary>
    public static bool IsFixedPackValue(AttributeDefinition attribute)
    {
        ArgumentNullException.ThrowIfNull(attribute);
        return attribute.Storage == StorageLocation.Pack && attribute.Change == ChangeRule.Fixed && attribute.Editors.HasFlag(AttributeEditors.Team);
    }

    /// <summary>
    /// The rules for a pack's own values: its fixed values can't change once a version has been published, and no two
    /// batteries share a serial number (ignoring case). A refused key doesn't block the others (J-12).
    /// </summary>
    private async Task<Evaluation> CheckPackRulesAsync(string packId, Evaluation evaluation, CancellationToken cancellationToken)
    {
        var results = evaluation.Results.ToList();
        var accepted = new Dictionary<string, JsonNode>(evaluation.Accepted, StringComparer.Ordinal);
        bool? published = null;
        for (var index = 0; index < results.Count; index++)
        {
            var result = results[index];
            if (result.Outcome != KeyOutcome.Saved || AttributeCatalog.FindByProperty(result.Key) is not { } attribute)
            {
                continue;
            }

            string? refusal = null;
            if (IsFixedPackValue(attribute) && (published ??= await store.GetLatestVersionNumberAsync(packId, cancellationToken) > 0))
            {
                refusal = FixedOncePublished;
            }
            else if (attribute.Number == 11
                && accepted[result.Key] is JsonValue serial
                && serial.TryGetValue<string>(out var text)
                && await store.SerialNumberTakenAsync(text, await LinkedPacksAsync(packId, cancellationToken), cancellationToken))
            {
                refusal = SerialNumberTaken;
            }

            if (refusal is not null)
            {
                results[index] = result with { Outcome = KeyOutcome.Rejected, Message = refusal };
                accepted.Remove(result.Key);
            }
        }

        return new Evaluation(results, accepted);
    }

    /// <summary>The pack and every earlier and later passport of the same battery, which share its serial number (R6).</summary>
    private Task<IReadOnlyCollection<string>> LinkedPacksAsync(string packId, CancellationToken cancellationToken) =>
        store.ChainPackIdsAsync(packId, cancellationToken);

    private async Task<bool> UsedByPublishedPassportAsync(TargetKind target, string id, CancellationToken cancellationToken)
    {
        var packIds = target switch
        {
            TargetKind.Pack => [id],
            TargetKind.Model => await store.FindPackIdsUsingAsync(modelId: id, cancellationToken: cancellationToken),
            TargetKind.Site => await store.FindPackIdsUsingAsync(siteId: id, cancellationToken: cancellationToken),
            TargetKind.Batch => await store.FindPackIdsUsingAsync(batchId: id, cancellationToken: cancellationToken),
            TargetKind.Operator => await store.FindPackIdsUsingAsync(operatorId: id, cancellationToken: cancellationToken),
            _ => [],
        };
        return packIds.Count > 0 && (await store.ListActiveVersionsAsync(packIds, cancellationToken)).Count > 0;
    }

    /// <summary>Judges every key without saving anything; also used when a pack is created.</summary>
    /// <param name="packClusterId">The pack's cluster; null for other record kinds.</param>
    public Evaluation Evaluate(ChangeSender sender, TargetKind target, string? packClusterId, IReadOnlyDictionary<string, JsonNode?> values) =>
        EvaluateCore(sender, target, packClusterId, values, statusChange: false, byTheApp: false);

    private Evaluation EvaluateCore(ChangeSender sender, TargetKind target, string? packClusterId, IReadOnlyDictionary<string, JsonNode?> values, bool statusChange, bool byTheApp)
    {
        ArgumentNullException.ThrowIfNull(sender);
        ArgumentNullException.ThrowIfNull(values);
        var results = new List<KeyResult>();
        var accepted = new Dictionary<string, JsonNode>(StringComparer.Ordinal);
        foreach (var (key, raw) in values)
        {
            var (outcome, message, value) = EvaluateKey(sender, target, packClusterId, key, raw, statusChange, byTheApp);
            results.Add(new KeyResult(key, outcome, message));
            if (outcome == KeyOutcome.Saved)
            {
                accepted[key] = value!;
            }
        }

        return new Evaluation(results, accepted);
    }

    /// <param name="statusChange">True for the status change's own keys (#22 and the re-stated values), which only it may send.</param>
    /// <param name="checkValue">False when only the attribute and the sender's rights are judged (a removal).</param>
    private (KeyOutcome Outcome, string Message, JsonNode? Value) EvaluateKey(
        ChangeSender sender, TargetKind target, string? packClusterId, string key, JsonNode? raw, bool statusChange, bool byTheApp, bool checkValue = true)
    {
        if (AttributeCatalog.FindByProperty(key) is not { } attribute)
        {
            return (KeyOutcome.Ignored, "not in the attribute list", null);
        }

        if (attribute.Storage == StorageLocation.System || attribute.Editors == AttributeEditors.System)
        {
            return (KeyOutcome.Rejected, $"{attribute.Label} is set by the app", null);
        }

        // #92 is the app's own value (S03): it points at the battery's accident page and only the app moves it, when a report is added.
        if (!byTheApp && attribute.Property == AppOwnedAccidentLink)
        {
            return (KeyOutcome.Rejected, $"{attribute.Label} can't be changed by this sender", null);
        }

        if (!BelongsTo(attribute.Storage, target))
        {
            return (KeyOutcome.Rejected, $"{attribute.Label} is stored on the {RecordName(attribute.Storage)}, not on the {target.ToString().ToLowerInvariant()}", null);
        }

        var throughStatusChange = target == TargetKind.Pack && attribute.Change is ChangeRule.OnStatusChange or ChangeRule.RestatedOnStatusChange;
        if (throughStatusChange != statusChange)
        {
            return (KeyOutcome.Rejected, statusChange ? $"{attribute.Label} isn't part of the status change" : $"{attribute.Label} changes only through the status change", null);
        }

        if (!EditRights.MayEdit(sender, attribute, packClusterId))
        {
            return (KeyOutcome.Rejected, $"{attribute.Label} can't be changed by this sender", null);
        }

        if (!checkValue)
        {
            return (KeyOutcome.Saved, string.Empty, null);
        }

        if (IsEmpty(raw))
        {
            return (KeyOutcome.KeptEmpty, "empty values never overwrite", null);
        }

        var value = normalizer.Normalize(attribute, raw)!;
        var issues = validator.Validate(attribute, value);
        if (issues.Count > 0)
        {
            return (KeyOutcome.Rejected, string.Join("; ", issues.Select(issue => $"{issue.Location} {issue.Message}".Trim())), null);
        }

        // Restricted documents are served by the app behind access checks; a link elsewhere would publish them.
        if (DppLinks.RestrictedDocumentProperties.Contains(attribute.Property) && !links.IsAppUrl(value["url"]?.GetValue<string>() ?? string.Empty))
        {
            return (KeyOutcome.Rejected, $"{attribute.Label} must link to a file uploaded to the app", null);
        }

        return (KeyOutcome.Saved, string.Empty, value);
    }

    /// <summary>Re-stated attributes start on the model; a pack only gets its own value through the status change.</summary>
    private static bool BelongsTo(StorageLocation storage, TargetKind target) => (storage, target) switch
    {
        (StorageLocation.Pack, TargetKind.Pack) => true,
        (StorageLocation.ModelWithPackOverride, TargetKind.Pack) => true,
        (StorageLocation.ModelWithPackOverride, TargetKind.Model) => true,
        (StorageLocation.Model, TargetKind.Model) => true,
        (StorageLocation.Batch, TargetKind.Batch) => true,
        (StorageLocation.Site, TargetKind.Site) => true,
        (StorageLocation.Operator, TargetKind.Operator) => true,
        _ => false,
    };

    private static string RecordName(StorageLocation storage) => storage switch
    {
        StorageLocation.ModelWithPackOverride => "model",
        StorageLocation.Site => "manufacturing site",
        StorageLocation.Operator => "economic operator",
        _ => storage.ToString().ToLowerInvariant(),
    };

    /// <summary>Missing, null, blank text, [] and {} never overwrite: no attribute accepts an empty list or object.</summary>
    private static bool IsEmpty(JsonNode? value) => value switch
    {
        null => true,
        JsonArray array => array.Count == 0,
        JsonObject obj => obj.Count == 0,
        _ => value.GetValueKind() == JsonValueKind.String && string.IsNullOrWhiteSpace(value.GetValue<string>()),
    };

    /// <summary>The record a change set is for: whether it exists, its cluster (packs only) and its stored values.</summary>
    private sealed record Target(bool Found, string? ClusterId, IReadOnlyDictionary<string, AttributeValue> Values);

    private static readonly Target NotFound = new(false, null, new Dictionary<string, AttributeValue>());

    private async Task<Target> FindTargetAsync(TargetKind target, string id, CancellationToken cancellationToken)
    {
        return target switch
        {
            TargetKind.Pack => await store.GetAsync<BatteryPack>(id, cancellationToken) is { } pack ? new Target(true, pack.ClusterId, pack.Values) : NotFound,
            TargetKind.Model => await store.GetAsync<BatteryModel>(id, cancellationToken) is { } model ? new Target(true, null, model.Values) : NotFound,
            TargetKind.Site => await store.GetAsync<ManufacturingSite>(id, cancellationToken) is { } site ? new Target(true, null, site.Values) : NotFound,
            TargetKind.Batch => await store.GetAsync<ProductionBatch>(id, cancellationToken) is { } batch ? new Target(true, null, batch.Values) : NotFound,
            TargetKind.Operator => await store.GetAsync<EconomicOperator>(id, cancellationToken) is { } economicOperator ? new Target(true, null, economicOperator.Values) : NotFound,
            _ => throw new ArgumentOutOfRangeException(nameof(target), target, null),
        };
    }

    private Task<bool> ApplyAsync(TargetKind target, string id, IReadOnlyDictionary<string, AttributeValue> values, IReadOnlyCollection<string> removals, CancellationToken cancellationToken) => target switch
    {
        TargetKind.Pack => store.SetValuesAsync<BatteryPack>(id, values, removals, cancellationToken),
        TargetKind.Model => store.SetValuesAsync<BatteryModel>(id, values, removals, cancellationToken),
        TargetKind.Site => store.SetValuesAsync<ManufacturingSite>(id, values, removals, cancellationToken),
        TargetKind.Batch => store.SetValuesAsync<ProductionBatch>(id, values, removals, cancellationToken),
        TargetKind.Operator => store.SetValuesAsync<EconomicOperator>(id, values, removals, cancellationToken),
        _ => throw new ArgumentOutOfRangeException(nameof(target), target, null),
    };

    /// <summary>
    /// Each key's outcome, with the value it replaced and, when saved, the new value (both as JSON text) and when it was
    /// generated, e.g. measured by the battery system: the live values' history is read from here (step 3c-2).
    /// </summary>
    private static BsonDocument AuditMetadata(
        IReadOnlyList<KeyResult> results,
        IReadOnlyDictionary<string, AttributeValue> before,
        IReadOnlyDictionary<string, AttributeValue> after) => new()
    {
        ["keys"] = new BsonArray(results.Select(result =>
        {
            var entry = new BsonDocument
            {
                ["key"] = result.Key,
                ["outcome"] = result.Outcome.ToString(),
                ["message"] = result.Message,
            };
            if (result.Outcome is KeyOutcome.Saved or KeyOutcome.Removed)
            {
                entry["previous"] = before.TryGetValue(result.Key, out var previous) && previous.Value is not null
                    ? previous.Value.ToJsonString()
                    : BsonNull.Value;
            }

            if (result.Outcome == KeyOutcome.Saved && after.TryGetValue(result.Key, out var value) && value.Value is not null)
            {
                entry["value"] = value.Value.ToJsonString();
                entry["generatedAt"] = value.GeneratedAtUtc.ToString("O", System.Globalization.CultureInfo.InvariantCulture);
            }

            return entry;
        })),
    };
}
