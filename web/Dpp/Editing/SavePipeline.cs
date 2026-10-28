using System.Text.Json;
using System.Text.Json.Nodes;
using BatteryPassWeb.Dpp.Catalog;
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
}

public sealed record KeyResult(string Key, KeyOutcome Outcome, string Message);

/// <summary>One change set for one record. <paramref name="Source"/> is "form", "upload" or "feed".</summary>
/// <param name="MeasuredAtUtc">When the values were generated (e.g. a feed's measurement time); the save time if null.</param>
public sealed record ChangeSet(
    TargetKind Target,
    string TargetId,
    IReadOnlyDictionary<string, JsonNode?> Values,
    string Source,
    DateTime? MeasuredAtUtc = null);

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
    TimeProvider clock)
{
    public const string AuditEventType = "dpp.change";

    public async Task<SaveResult> SaveAsync(ChangeSender sender, ChangeSet changes, bool preview = false, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(sender);
        ArgumentNullException.ThrowIfNull(changes);

        var target = await FindTargetAsync(changes.Target, changes.TargetId, cancellationToken);
        if (!target.Found)
        {
            return new SaveResult(false, false, []);
        }

        var evaluation = Evaluate(sender, changes.Target, target.ClusterId, changes.Values);
        if (preview || evaluation.Accepted.Count == 0)
        {
            return new SaveResult(true, false, evaluation.Results);
        }

        var generatedAt = changes.MeasuredAtUtc?.ToUniversalTime() ?? clock.GetUtcNow().UtcDateTime;
        var stored = evaluation.Accepted.ToDictionary(
            entry => entry.Key,
            entry => new AttributeValue { Value = entry.Value, GeneratedAtUtc = generatedAt, Source = changes.Source, Who = sender.Who },
            StringComparer.Ordinal);
        var saved = await SetValuesAsync(changes.Target, changes.TargetId, stored, cancellationToken);
        if (saved)
        {
            await audit.AppendApplicationAuditEventAsync(
                AuditEventType,
                sender.Who,
                sender.Kind.ToString(),
                changes.Source,
                $"{stored.Count} value(s) saved on {changes.Target} {changes.TargetId}",
                AuditMetadata(evaluation.Results),
                entityType: $"dpp-{changes.Target.ToString().ToLowerInvariant()}",
                entityId: changes.TargetId,
                cancellationToken: cancellationToken);
        }

        return new SaveResult(true, saved, evaluation.Results);
    }

    /// <summary>Judges every key without saving anything; also used when a pack is created.</summary>
    /// <param name="packClusterId">The pack's cluster; null for other record kinds.</param>
    public Evaluation Evaluate(ChangeSender sender, TargetKind target, string? packClusterId, IReadOnlyDictionary<string, JsonNode?> values)
    {
        ArgumentNullException.ThrowIfNull(sender);
        ArgumentNullException.ThrowIfNull(values);
        var results = new List<KeyResult>();
        var accepted = new Dictionary<string, JsonNode>(StringComparer.Ordinal);
        foreach (var (key, raw) in values)
        {
            var (outcome, message, value) = EvaluateKey(sender, target, packClusterId, key, raw);
            results.Add(new KeyResult(key, outcome, message));
            if (outcome == KeyOutcome.Saved)
            {
                accepted[key] = value!;
            }
        }

        return new Evaluation(results, accepted);
    }

    private (KeyOutcome Outcome, string Message, JsonNode? Value) EvaluateKey(
        ChangeSender sender, TargetKind target, string? packClusterId, string key, JsonNode? raw)
    {
        if (AttributeCatalog.FindByProperty(key) is not { } attribute)
        {
            return (KeyOutcome.Ignored, "not in the attribute list", null);
        }

        if (attribute.Storage == StorageLocation.System || attribute.Editors == AttributeEditors.System)
        {
            return (KeyOutcome.Rejected, $"#{attribute.Number} is set by the app", null);
        }

        if (!BelongsTo(attribute.Storage, target))
        {
            return (KeyOutcome.Rejected, $"#{attribute.Number} is stored on the {RecordName(attribute.Storage)}, not on the {target.ToString().ToLowerInvariant()}", null);
        }

        if (target == TargetKind.Pack && (attribute.Change is ChangeRule.OnStatusChange or ChangeRule.RestatedOnStatusChange))
        {
            return (KeyOutcome.Rejected, $"#{attribute.Number} changes only through the status change", null);
        }

        if (!EditRights.MayEdit(sender, attribute, packClusterId))
        {
            return (KeyOutcome.Rejected, $"#{attribute.Number} can't be changed by this sender", null);
        }

        if (IsEmpty(raw))
        {
            return (KeyOutcome.KeptEmpty, "empty values never overwrite", null);
        }

        var value = normalizer.Normalize(attribute, raw)!;
        var issues = validator.Validate(attribute, value);
        return issues.Count > 0
            ? (KeyOutcome.Rejected, string.Join("; ", issues.Select(issue => $"{issue.Location} {issue.Message}".Trim())), null)
            : (KeyOutcome.Saved, string.Empty, value);
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

    private async Task<(bool Found, string? ClusterId)> FindTargetAsync(TargetKind target, string id, CancellationToken cancellationToken)
    {
        switch (target)
        {
            case TargetKind.Pack:
                var pack = await store.GetAsync<BatteryPack>(id, cancellationToken);
                return (pack is not null, pack?.ClusterId);
            case TargetKind.Model:
                return (await store.GetAsync<BatteryModel>(id, cancellationToken) is not null, null);
            case TargetKind.Site:
                return (await store.GetAsync<ManufacturingSite>(id, cancellationToken) is not null, null);
            case TargetKind.Batch:
                return (await store.GetAsync<ProductionBatch>(id, cancellationToken) is not null, null);
            case TargetKind.Operator:
                return (await store.GetAsync<EconomicOperator>(id, cancellationToken) is not null, null);
            default:
                throw new ArgumentOutOfRangeException(nameof(target), target, null);
        }
    }

    private Task<bool> SetValuesAsync(TargetKind target, string id, IReadOnlyDictionary<string, AttributeValue> values, CancellationToken cancellationToken) => target switch
    {
        TargetKind.Pack => store.SetValuesAsync<BatteryPack>(id, values, cancellationToken),
        TargetKind.Model => store.SetValuesAsync<BatteryModel>(id, values, cancellationToken),
        TargetKind.Site => store.SetValuesAsync<ManufacturingSite>(id, values, cancellationToken),
        TargetKind.Batch => store.SetValuesAsync<ProductionBatch>(id, values, cancellationToken),
        TargetKind.Operator => store.SetValuesAsync<EconomicOperator>(id, values, cancellationToken),
        _ => throw new ArgumentOutOfRangeException(nameof(target), target, null),
    };

    private static BsonDocument AuditMetadata(IReadOnlyList<KeyResult> results) => new()
    {
        ["keys"] = new BsonArray(results.Select(result => new BsonDocument
        {
            ["key"] = result.Key,
            ["outcome"] = result.Outcome.ToString(),
            ["message"] = result.Message,
        })),
    };
}
