using System.Text.Json.Nodes;

namespace BatteryPassWeb.Dpp.Catalog;

/// <summary>Whether a passport needs a value before it is published.</summary>
public enum Need
{
    /// <summary>Needed before publishing: a missing value blocks.</summary>
    Required,

    /// <summary>Needed only after February 2027: a missing value is worth knowing, nothing more.</summary>
    Later,

    /// <summary>A conditional value no question decides (#49 to #52, E1 to E4): never blocks, never warns.</summary>
    Optional,

    /// <summary>The model's answer says it isn't needed: a value filled in anyway is kept and published.</summary>
    NotNeeded,

    /// <summary>The question that decides it isn't answered yet; the unanswered question blocks publishing.</summary>
    Unanswered,
}

/// <summary>
/// The one rule for what a passport needs before it is published. The publication check, the editors' "missing" marks
/// and counts, and the guard against removing a value a published passport needs all read it. A conditional value that
/// one of the model's three questions decides (<see cref="ModelQuestions"/>) is needed when the answer is yes (R01).
/// </summary>
public static class PassportNeeds
{
    /// <summary>
    /// Values needed before publishing although the official list doesn't mark them "Feb 2027", with the reason. The
    /// catalogue keeps saying what the list says; this is the owner's decision on top of it.
    /// </summary>
    public static IReadOnlyDictionary<int, string> RequiredBeyondTheList { get; } = new Dictionary<int, string>
    {
        [57] = "The Regulation (Annex VI Part A, Annex XIII 1(g)) and the Commission's guidance make the rated capacity mandatory; the owner follows them, not the list's \"no\" (R22).",
    };

    /// <summary>A mandatory value due by February 2027: the list's mark, or one of <see cref="RequiredBeyondTheList"/>.</summary>
    public static bool DueByFeb2027(AttributeDefinition attribute)
    {
        ArgumentNullException.ThrowIfNull(attribute);
        return attribute.MandatoryFromFeb2027 || RequiredBeyondTheList.ContainsKey(attribute.Number);
    }

    /// <summary>The publication check's line when a model's questions aren't all answered; it blocks publishing.</summary>
    public const string QuestionsNotAnswered = "The battery model's three questions aren't answered yet";

    /// <summary>Whether a passport of a model with these <paramref name="answers"/> needs this value before it is published.</summary>
    public static Need NeedOf(AttributeDefinition attribute, ModelAnswers answers)
    {
        ArgumentNullException.ThrowIfNull(attribute);
        ArgumentNullException.ThrowIfNull(answers);
        if (ModelQuestions.QuestionOf(attribute) is { } question)
        {
            return answers.Of(question) switch
            {
                true => DueByFeb2027(attribute) ? Need.Required : Need.Later,
                false => Need.NotNeeded,
                null => Need.Unanswered,
            };
        }

        if (attribute.Requirement == Requirement.Conditional)
        {
            return Need.Optional;
        }

        return DueByFeb2027(attribute) ? Need.Required : Need.Later;
    }

    /// <summary>
    /// The voltages (#64 to #66) whose test temperatures the EU's format requires wherever the voltage is given (G20):
    /// the schema of the file leaves them optional.
    /// </summary>
    public static IReadOnlyList<int> VoltagesWithTemperatures { get; } = [64, 65, 66];

    /// <summary>
    /// What a value that is there still lacks before the passport may be published, in words that finish "is missing …"
    /// ("the test temperatures"); null when nothing. Such a value is kept, shown and counted as missing; a save never
    /// refuses it, so a workbook or a file filled before the rule still loads (package R1, decision 94).
    /// </summary>
    public static string? Lacking(AttributeDefinition attribute, JsonNode? value)
    {
        ArgumentNullException.ThrowIfNull(attribute);
        if (value is JsonObject voltage && VoltagesWithTemperatures.Contains(attribute.Number) && !attribute.IsExtension
            && (voltage["temperatureRangeLowerBoundaryValue"] is null || voltage["temperatureRangeUpperBoundaryValue"] is null))
        {
            return "the test temperatures";
        }

        // #42 is chosen from the EU's list (G16). A name kept from before that isn't on it stays, and is missing a choice.
        if (attribute.Property == AttributeCatalog.CriticalRawMaterials && value is JsonArray materials)
        {
            var off = materials.OfType<JsonValue>()
                .Select(item => item.TryGetValue<string>(out var text) ? text : null)
                .Where(text => text is not null && EuCodeLists.FindRawMaterial(text) is null)
                .ToArray();
            if (off.Length > 0)
            {
                return $"a choice from the EU's list: {string.Join(", ", off.Select(text => $"\"{text}\""))} {(off.Length == 1 ? "isn't" : "aren't")} on it";
            }
        }

        return null;
    }

    /// <summary>
    /// Missing for the passport: needed before it is published (<see cref="NeedOf"/>), and empty or still lacking a part
    /// (<see cref="Lacking"/>). The one rule behind the editors' marks and counts and the battery's missing values.
    /// </summary>
    public static bool IsMissing(AttributeDefinition attribute, JsonNode? value, ModelAnswers answers) =>
        NeedOf(attribute, answers) == Need.Required && (value is null || Lacking(attribute, value) is not null);

    /// <summary>
    /// Why a value is needed when only the model's answer makes it so, e.g. "needed because this model is made for cycling
    /// use"; null for a value every passport needs.
    /// </summary>
    public static string? Reason(AttributeDefinition attribute, ModelAnswers answers) =>
        ModelQuestions.QuestionOf(attribute) is { } question && answers.Of(question) == true
            ? $"needed because {ModelQuestions.WhenYes(question)}"
            : null;

    /// <summary>
    /// The short line under a conditional field in the editors: whether this model needs it. Null for a value that isn't
    /// conditional. A conditional value no question decides keeps its condition from the file.
    /// </summary>
    public static string? EditorLine(AttributeDefinition attribute, ModelAnswers answers)
    {
        ArgumentNullException.ThrowIfNull(attribute);
        ArgumentNullException.ThrowIfNull(answers);
        if (ModelQuestions.QuestionOf(attribute) is not { } question)
        {
            return AttributeCatalog.ConditionOf(attribute);
        }

        return answers.Of(question) switch
        {
            true => $"Needed: {ModelQuestions.WhenYes(question)}.",
            false => $"Not needed: {ModelQuestions.WhenNo(question)}.",
            null => $"Needed only if {ModelQuestions.WhenYes(question)}.",
        };
    }
}
