namespace BatteryPassWeb.Models.Trust;

public sealed class DataCompletionPolicySnapshot
{
    public string PolicyKey { get; init; } = string.Empty;
    public string UpdatedAt { get; init; } = string.Empty;
    public string UpdatedBy { get; init; } = string.Empty;
    public IReadOnlyList<DataRequirementSection> Sections { get; init; } = [];
}

public sealed class DataRequirementSection
{
    public string SectionKey { get; init; } = string.Empty;
    public string Label { get; init; } = string.Empty;
    public int SortOrder { get; init; }
    public IReadOnlyList<DataRequirementField> Fields { get; init; } = [];
    public int RequiredCount => Fields.Count(requirement => requirement.IsRequired);
    public int OptionalCount => Fields.Count(requirement => !requirement.IsRequired);
}

public sealed class DataRequirementField
{
    public string FieldKey { get; init; } = string.Empty;
    public string SectionKey { get; init; } = string.Empty;
    public string Label { get; init; } = string.Empty;
    public string DataPath { get; init; } = string.Empty;
    public string Guidance { get; init; } = string.Empty;
    public bool DefaultRequired { get; init; }
    public bool IsRequired { get; init; }
    public int SortOrder { get; init; }
}
