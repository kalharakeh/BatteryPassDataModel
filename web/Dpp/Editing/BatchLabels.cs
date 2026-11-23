using System.Text.Json.Nodes;
using BatteryPassWeb.Dpp.Catalog;
using BatteryPassWeb.Dpp.Store;
using BatteryPassWeb.Dpp.Values;

namespace BatteryPassWeb.Dpp.Editing;

/// <summary>
/// A batch's label (E8, the owner's answer O4): the EU's format sends a battery's model identifier only with a batch
/// number. Scania's batches have no lot number of their own, so the app makes one from what a batch is (model × site ×
/// year, decision 3): the model's serial prefix without its dashes and #, the site's country in three letters and the
/// year, such as VPC8C05-SWE-2026. The team may type its own. No two batches of a model share a label.
/// </summary>
public static class BatchLabels
{
    /// <summary>Why a label is refused when another batch of the same model has it.</summary>
    public const string Taken = "another batch of this model already has this label";

    /// <summary>
    /// The label the app gives a batch: null when the model has no serial number format or the site no country yet, so
    /// there is nothing to make it from.
    /// </summary>
    public static string? Default(BatteryModel model, ManufacturingSite site, int year)
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(site);
        var prefix = Prefix(model.SerialFormat);
        var country = Text(site.Values, AttributeCatalog.ManufacturingCountry);
        return prefix.Length == 0 || !Countries.IsCode(country) ? null : $"{prefix}-{country}-{year}";
    }

    /// <summary>The serial number format up to its first digit place (#), without dashes or spaces: "VPC8C05s-#####" gives "VPC8C05s".</summary>
    public static string Prefix(string? serialFormat)
    {
        var format = serialFormat ?? string.Empty;
        var hash = format.IndexOf('#', StringComparison.Ordinal);
        var head = hash < 0 ? format : format[..hash];
        return new string(head.Where(char.IsLetterOrDigit).ToArray());
    }

    /// <summary>A batch's label; null when it has none.</summary>
    public static string? Of(ProductionBatch batch)
    {
        ArgumentNullException.ThrowIfNull(batch);
        return Text(batch.Values, AttributeCatalog.BatchNumber);
    }

    /// <summary>Two labels are the same when they differ only in capitals or the spaces around them.</summary>
    public static bool Same(string? first, string? second) =>
        first is not null && second is not null && string.Equals(first.Trim(), second.Trim(), StringComparison.OrdinalIgnoreCase);

    /// <summary>True when another batch of <paramref name="modelId"/> than <paramref name="batchId"/> has this label.</summary>
    public static async Task<bool> TakenAsync(DppStore store, string modelId, string? batchId, string label, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(store);
        return (await store.ListBatchesAsync(modelId, cancellationToken)).Any(other => other.Id != batchId && Same(Of(other), label));
    }

    /// <summary>
    /// Gives each batch of the site that has no label yet the app's label, when it can be made and no other batch of the
    /// model has it. Called when the site's country is saved: a batch made before the country (or before labels) gets its
    /// label without a step of its own.
    /// </summary>
    public static async Task FillForSiteAsync(DppStore store, string siteId, string who, DateTime nowUtc, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(store);
        if (await store.GetAsync<ManufacturingSite>(siteId, cancellationToken) is not { } site)
        {
            return;
        }

        foreach (var batch in (await store.ListAsync<ProductionBatch>(cancellationToken)).Where(batch => batch.SiteId == siteId && Of(batch) is null))
        {
            if (await store.GetAsync<BatteryModel>(batch.ModelId, cancellationToken) is { } model
                && Default(model, site, batch.Year) is { } label
                && !await TakenAsync(store, model.Id, batch.Id, label, cancellationToken))
            {
                await store.SetValuesAsync<ProductionBatch>(batch.Id, new Dictionary<string, AttributeValue> { [AttributeCatalog.BatchNumber] = Stored(label, who, nowUtc) }, cancellationToken);
            }
        }
    }

    /// <summary>The label as a stored value, entered by the app for <paramref name="who"/>.</summary>
    public static AttributeValue Stored(string label, string who, DateTime nowUtc) =>
        new() { Value = JsonValue.Create(label), GeneratedAtUtc = nowUtc, Source = "form", Who = who };

    private static string? Text(IReadOnlyDictionary<string, AttributeValue> values, string property) =>
        values.GetValueOrDefault(property)?.Value is JsonValue value && value.TryGetValue<string>(out var text) ? text : null;
}
