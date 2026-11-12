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
    /// <summary>The refusal, said with the way out (decision 69): wherever the rule answers (creating, publishing, the API).</summary>
    public const string Message = "The serial number is a real battery ID, but the batch, its model or the economic operator you chose is demo data, and real battery IDs go only on real product data. Choose a test ID instead, or make real product data with \"Copy as real product data\" on the battery family's page (and \"Copy as real\" for a demo operator under Sites and operators).";

    /// <summary>The refusal naming which of the batch, its model and the operator is demo data, and the way out for each.</summary>
    public static string MessageFor(ProductionBatch batch, BatteryModel? model, EconomicOperator economicOperator)
    {
        ArgumentNullException.ThrowIfNull(batch);
        ArgumentNullException.ThrowIfNull(economicOperator);
        var productDemo = batch.IsDemo || model?.IsDemo == true;
        var demo = new List<string>();
        if (batch.IsDemo)
        {
            demo.Add("the batch");
        }

        if (model?.IsDemo == true)
        {
            demo.Add(batch.IsDemo ? "its model" : "the batch's model");
        }

        if (economicOperator.IsDemo)
        {
            demo.Add("the economic operator");
        }

        if (demo.Count == 0)
        {
            return Message;
        }

        var which = demo.Count == 1 ? demo[0] : $"{string.Join(", ", demo.Take(demo.Count - 1))} and {demo[^1]}";
        var wayOut = productDemo
            ? "make real product data with \"Copy as real product data\" on the battery family's page" + (economicOperator.IsDemo ? " and \"Copy as real\" for the operator under Sites and operators" : string.Empty)
            : "copy the operator as real with \"Copy as real\" under Sites and operators";
        return $"The serial number is a real battery ID, but {which} you chose {(demo.Count == 1 ? "is" : "are")} demo data, and real battery IDs go only on real product data. Choose a test ID instead, or {wayOut}.";
    }

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
