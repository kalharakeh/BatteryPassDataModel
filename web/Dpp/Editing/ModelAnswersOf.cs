using BatteryPassWeb.Dpp.Catalog;
using BatteryPassWeb.Dpp.Store;

namespace BatteryPassWeb.Dpp.Editing;

/// <summary>Reads a stored model's answers to its three questions (R01).</summary>
public static class ModelAnswersOf
{
    /// <summary>The model's answers; a model saved before the questions existed has none, so it reads "not answered yet".</summary>
    public static ModelAnswers Answers(this BatteryModel model)
    {
        ArgumentNullException.ThrowIfNull(model);
        return new ModelAnswers(model.HasCommercialWarranty, model.MadeForCyclingUse, model.CadmiumOrLeadAboveLimit);
    }
}
