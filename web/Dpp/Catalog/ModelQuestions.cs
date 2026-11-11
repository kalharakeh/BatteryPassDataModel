namespace BatteryPassWeb.Dpp.Catalog;

/// <summary>The three yes/no questions each battery model answers (R01). They decide which conditional values it needs.</summary>
public enum ModelQuestion
{
    CommercialWarranty,
    CyclingUse,
    CadmiumOrLeadAboveLimit,
}

/// <summary>
/// A model's answers to its three questions: yes, no, or null for "not answered yet". They are the app's own data:
/// not part of the passport, never published.
/// </summary>
public sealed record ModelAnswers(bool? CommercialWarranty, bool? CyclingUse, bool? CadmiumOrLeadAboveLimit)
{
    /// <summary>A new model's answers, and those of a model saved before the questions existed.</summary>
    public static ModelAnswers NotAnswered { get; } = new(null, null, null);

    public bool? Of(ModelQuestion question) => question switch
    {
        ModelQuestion.CommercialWarranty => CommercialWarranty,
        ModelQuestion.CyclingUse => CyclingUse,
        ModelQuestion.CadmiumOrLeadAboveLimit => CadmiumOrLeadAboveLimit,
        _ => throw new ArgumentOutOfRangeException(nameof(question), question, null),
    };

    public ModelAnswers With(ModelQuestion question, bool? answer) => question switch
    {
        ModelQuestion.CommercialWarranty => this with { CommercialWarranty = answer },
        ModelQuestion.CyclingUse => this with { CyclingUse = answer },
        ModelQuestion.CadmiumOrLeadAboveLimit => this with { CadmiumOrLeadAboveLimit = answer },
        _ => throw new ArgumentOutOfRangeException(nameof(question), question, null),
    };

    /// <summary>All three are answered, yes or no.</summary>
    public bool AllAnswered => ModelQuestions.All.All(question => Of(question) is not null);
}

/// <summary>The questions' words, and which conditional values each one decides.</summary>
public static class ModelQuestions
{
    public static IReadOnlyList<ModelQuestion> All { get; } = [ModelQuestion.CommercialWarranty, ModelQuestion.CyclingUse, ModelQuestion.CadmiumOrLeadAboveLimit];

    /// <summary>The conditional values each question decides, by attribute number.</summary>
    public static IReadOnlyDictionary<ModelQuestion, IReadOnlyList<int>> Decides { get; } = new Dictionary<ModelQuestion, IReadOnlyList<int>>
    {
        [ModelQuestion.CommercialWarranty] = [19],
        [ModelQuestion.CyclingUse] = [71, 72, 74, 82, 83, 84, 85],
        [ModelQuestion.CadmiumOrLeadAboveLimit] = [24],
    };

    /// <summary>The question as the model's page asks it.</summary>
    public static string Text(ModelQuestion question) => question switch
    {
        ModelQuestion.CommercialWarranty => "Does this battery come with a commercial warranty?",
        ModelQuestion.CyclingUse => "Is this battery made for cycling use?",
        ModelQuestion.CadmiumOrLeadAboveLimit => "Is its cadmium or lead above the limit for the symbols?",
        _ => throw new ArgumentOutOfRangeException(nameof(question), question, null),
    };

    /// <summary>The form field that carries the answer on the model's page.</summary>
    public static string FieldName(ModelQuestion question) => question switch
    {
        ModelQuestion.CommercialWarranty => "warranty",
        ModelQuestion.CyclingUse => "cycling",
        ModelQuestion.CadmiumOrLeadAboveLimit => "cadmiumOrLead",
        _ => throw new ArgumentOutOfRangeException(nameof(question), question, null),
    };

    /// <summary>What a "yes" says about the model, to finish "needed because …" and "Needed: …".</summary>
    public static string WhenYes(ModelQuestion question) => question switch
    {
        ModelQuestion.CommercialWarranty => "this model comes with a commercial warranty",
        ModelQuestion.CyclingUse => "this model is made for cycling use",
        ModelQuestion.CadmiumOrLeadAboveLimit => "this model's cadmium or lead is above the limit for the symbols",
        _ => throw new ArgumentOutOfRangeException(nameof(question), question, null),
    };

    /// <summary>What a "no" says about the model, to finish "Not needed: …".</summary>
    public static string WhenNo(ModelQuestion question) => question switch
    {
        ModelQuestion.CommercialWarranty => "this model has no commercial warranty",
        ModelQuestion.CyclingUse => "this model isn't made for cycling use",
        ModelQuestion.CadmiumOrLeadAboveLimit => "this model's cadmium and lead are below the limit",
        _ => throw new ArgumentOutOfRangeException(nameof(question), question, null),
    };

    /// <summary>The question that decides whether this value is needed; null for a value no question decides.</summary>
    public static ModelQuestion? QuestionOf(AttributeDefinition attribute)
    {
        ArgumentNullException.ThrowIfNull(attribute);
        if (attribute.IsExtension)
        {
            return null;
        }

        foreach (var (question, numbers) in Decides)
        {
            if (numbers.Contains(attribute.Number))
            {
                return question;
            }
        }

        return null;
    }
}
