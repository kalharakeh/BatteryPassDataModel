using BatteryPassWeb.Dpp.Assembly;
using BatteryPassWeb.Dpp.Identifiers;
using BatteryPassWeb.Dpp.Store;

namespace BatteryPassWeb.Dpp.Editing;

/// <summary>
/// A real battery ID can't meet demo product data (U16): a battery with a real ID on a demo batch, model or operator is
/// refused when it is made, and its passport isn't published. A demo ID on real product data stays allowed.
/// </summary>
public static class DemoDataRule
{
    public const string Message = "This is demo data. Use a demo ID for it, or choose real product data.";

    /// <summary>True when the batch, its model or the operator is demo data.</summary>
    public static bool UsesDemoData(ProductionBatch batch, BatteryModel model, EconomicOperator economicOperator)
    {
        ArgumentNullException.ThrowIfNull(batch);
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(economicOperator);
        return batch.IsDemo || model.IsDemo || economicOperator.IsDemo;
    }

    /// <summary>True for a battery whose ID is a real one: not a demo battery, and not a test or demo ID.</summary>
    public static bool HasRealId(BatteryPack pack)
    {
        ArgumentNullException.ThrowIfNull(pack);
        return !pack.IsDemo && !IdentifierForms.IsTest(pack.Values.GetValueOrDefault("uniqueProductIdentifier")?.Value?.GetValue<string>());
    }

    /// <summary>True for a made battery whose real ID sits on demo product data.</summary>
    public static bool Conflicts(PassportSources sources)
    {
        ArgumentNullException.ThrowIfNull(sources);
        return HasRealId(sources.Pack) && UsesDemoData(sources.Batch, sources.Model, sources.Operator);
    }
}
