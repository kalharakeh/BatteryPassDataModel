using BatteryPassWeb.Models.Trust;

namespace BatteryPassWeb.Models.ViewModels;

public sealed class EditPassportViewModel
{
    public required PassportViewModel Passport { get; init; }
    public string Mode { get; init; } = "edit";
    public DataCompletionPolicySnapshot DataRequirements { get; init; } = new();
    public IReadOnlyList<ProductTemplateSummaryViewModel> ProductTemplates { get; init; } = [];
    public IReadOnlyList<ProductTemplateFormCatalogItemViewModel> ProductTemplateCatalog { get; init; } = [];
    public IReadOnlyList<ProductSoftwareVersionViewModel> ProductSoftwareVersions { get; init; } = [];
    public string SelectedProductId { get; init; } = string.Empty;
    public string SelectedSoftwareVersion { get; init; } = string.Empty;
    public IReadOnlyDictionary<string, DataRequirementField> FieldRequirementByKey { get; init; } =
        new Dictionary<string, DataRequirementField>(StringComparer.OrdinalIgnoreCase);
    public string StatusMessage { get; init; } = string.Empty;
    public string ErrorMessage { get; init; } = string.Empty;
}
