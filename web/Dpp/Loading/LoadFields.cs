using BatteryPassWeb.Dpp.Catalog;
using BatteryPassWeb.Dpp.Editing;
using BatteryPassWeb.Dpp.Forms;
using BatteryPassWeb.Dpp.Pages;

namespace BatteryPassWeb.Dpp.Loading;

/// <summary>The records the workbook describes; a key's first part names one ("model|batteryMass|gramKilogramValue").</summary>
public enum LoadRecord
{
    Family,
    Model,
    Batch,
    Site,
    Operator,
}

/// <summary>What a field of the app's own data (not passport data) is, which decides how it is read and saved.</summary>
public enum OwnFieldKind
{
    /// <summary>The record's name: the record is found by it, so it is never "the same as the first model's".</summary>
    Name,

    /// <summary>A PNG or JPEG sent with the workbook, named by its file name.</summary>
    Picture,

    /// <summary>The model's module description.</summary>
    ModuleDescription,

    /// <summary>One of the model's three questions, answered <c>Yes</c> or <c>No</c>.</summary>
    Answer,

    /// <summary>The batch's production year.</summary>
    Year,

    /// <summary>The batch's manufacturing site, by name.</summary>
    Site,

    /// <summary>A model's serial number format (decision 81): its own, never the first model's.</summary>
    SerialFormat,

    /// <summary>A field the workbook has and the app doesn't load yet; the check says so when the cell is filled.</summary>
    NotLoadedYet,
}

/// <summary>One field of the app's own data in the workbook.</summary>
/// <param name="Label">The field's name, for when the sheet's row has none.</param>
/// <param name="Note">For <see cref="OwnFieldKind.NotLoadedYet"/>: what the check says when the cell is filled.</param>
public sealed record OwnField(string Key, LoadRecord Record, string Label, OwnFieldKind Kind, ModelQuestion? Question = null, string? Note = null);

/// <summary>One cell of a passport value: the attribute, and the input of its form the cell fills.</summary>
public sealed record ValueField(string Key, LoadRecord Record, AttributeDefinition Attribute, AttributeForm Form, FormInput Input);

/// <summary>A cell the workbook shows although the app writes it itself (#20, the one battery category).</summary>
public sealed record FixedField(string Key, LoadRecord Record, AttributeDefinition Attribute, string Constant);

/// <summary>A document of a model or batch: a file sent with the workbook, or a web link.</summary>
public sealed record DocumentField(LoadRecord Record, AttributeDefinition Attribute, AttributeForm Form)
{
    /// <summary>True for the documents uploaded to the app; false for a public web link.</summary>
    public bool IsFile => Form.Kind == FormKind.Upload;
}

/// <summary>
/// What every key of the product data workbook means: the app's own fields (one list, <see cref="Own"/>), and the
/// passport values, which are not listed here but read from the catalogue and the forms, so that the loader knows
/// exactly the fields the team's pages have. A test keeps the committed workbook in step with both.
/// </summary>
public sealed class LoadFields
{
    public const string FamilySheet = "Family";
    public const string ModelsSheet = "Models";
    public const string BatchesSheet = "Batches";
    public const string SiteSheet = "Site";
    public const string OperatorSheet = "Operator";
    public const string HazardSheet = "Hazardous substances";
    public const string DocumentSheet = "Documents and links";

    /// <summary>#44, filled on its own sheet, one line per substance.</summary>
    public const string HazardProperty = "hazardousSubstancesAndTheirImpact";

    /// <summary>The key of the example line on the hazardous substances sheet, which is never read.</summary>
    public const string HazardExampleKey = "hazard|example";

    /// <summary>The key of a line to fill on the hazardous substances sheet.</summary>
    public const string HazardLineKey = "hazard|line";

    /// <summary>The batch's label (E8): each batch's own, never the first batch's.</summary>
    public const string BatchLabelKey = "batch|" + AttributeCatalog.BatchNumber;

    /// <summary>
    /// The app's own fields the workbook has, in one place. A field that another update brings is added here: the
    /// serial number format becomes loadable by giving it a kind of its own instead of <see cref="OwnFieldKind.NotLoadedYet"/>.
    /// </summary>
    public static IReadOnlyList<OwnField> Own { get; } =
    [
        new("family|Name", LoadRecord.Family, "Family name", OwnFieldKind.Name),
        new("family|Picture", LoadRecord.Family, "Picture of the family", OwnFieldKind.Picture),
        new("model|Name", LoadRecord.Model, "Model name", OwnFieldKind.Name),
        new("model|SerialFormat", LoadRecord.Model, "Serial number format", OwnFieldKind.SerialFormat),
        new("model|ModuleDescription", LoadRecord.Model, "Modules: short description", OwnFieldKind.ModuleDescription),
        new("model|Picture", LoadRecord.Model, "Picture of the model", OwnFieldKind.Picture),
        new("model|HasCommercialWarranty", LoadRecord.Model, ModelQuestions.Text(ModelQuestion.CommercialWarranty), OwnFieldKind.Answer, ModelQuestion.CommercialWarranty),
        new("model|MadeForCyclingUse", LoadRecord.Model, ModelQuestions.Text(ModelQuestion.CyclingUse), OwnFieldKind.Answer, ModelQuestion.CyclingUse),
        new("model|CadmiumOrLeadAboveLimit", LoadRecord.Model, ModelQuestions.Text(ModelQuestion.CadmiumOrLeadAboveLimit), OwnFieldKind.Answer, ModelQuestion.CadmiumOrLeadAboveLimit),
        new("batch|Year", LoadRecord.Batch, "Production year", OwnFieldKind.Year),
        new("batch|Site", LoadRecord.Batch, "Manufacturing site", OwnFieldKind.Site),
        new("site|Name", LoadRecord.Site, "Site name", OwnFieldKind.Name),
        new("operator|Name", LoadRecord.Operator, "Operator name", OwnFieldKind.Name),
    ];

    private static readonly Dictionary<string, OwnField> OwnByKey = Own.ToDictionary(field => field.Key, StringComparer.Ordinal);

    private static readonly (LoadRecord Record, TargetKind Kind)[] ValueRecords =
    [
        (LoadRecord.Model, TargetKind.Model),
        (LoadRecord.Batch, TargetKind.Batch),
        (LoadRecord.Site, TargetKind.Site),
        (LoadRecord.Operator, TargetKind.Operator),
    ];

    private readonly Dictionary<string, ValueField> _values = new(StringComparer.Ordinal);
    private readonly Dictionary<string, FixedField> _fixed = new(StringComparer.Ordinal);
    private readonly Dictionary<string, DocumentField> _documents = new(StringComparer.Ordinal);

    public LoadFields(AttributeForms forms)
    {
        ArgumentNullException.ThrowIfNull(forms);
        Forms = forms;
        foreach (var (record, kind) in ValueRecords)
        {
            foreach (var attribute in RecordEditor.AttributesOf(kind))
            {
                var form = forms.For(attribute);
                var owner = $"{Prefix(record)}|{attribute.Property}";
                switch (form.Kind)
                {
                    case FormKind.Upload:
                    case FormKind.Link:
                        _documents[owner] = new DocumentField(record, attribute, form);
                        break;
                    case FormKind.Rows:
                        // One line per row on a sheet of its own; only #44 has this shape.
                        if (attribute.Property != HazardProperty)
                        {
                            throw new InvalidOperationException($"{attribute.Label} {attribute.Property} is a list of rows and the product data workbook has no sheet for it.");
                        }

                        Hazards = form;
                        break;
                    case FormKind.Fixed:
                        foreach (var (name, constant) in form.Constants)
                        {
                            _fixed[$"{owner}|{name}"] = new FixedField($"{owner}|{name}", record, attribute, constant);
                        }

                        break;
                    default:
                        foreach (var input in form.Inputs)
                        {
                            var key = input.Path.Length == 0 ? owner : $"{owner}|{input.Path}";
                            _values[key] = new ValueField(key, record, attribute, form, input);
                        }

                        break;
                }
            }
        }

        if (Hazards is null)
        {
            throw new InvalidOperationException($"The catalogue has no {HazardProperty}; the product data workbook has a sheet for it.");
        }
    }

    public AttributeForms Forms { get; }

    /// <summary>The form of #44: its inputs are the columns of the hazardous substances sheet, by their labels.</summary>
    public AttributeForm Hazards { get; } = null!;

    /// <summary>The key of the row on the Models sheet that points to the hazardous substances sheet; its cells aren't read.</summary>
    public static string HazardRowKey => $"{Prefix(LoadRecord.Model)}|{HazardProperty}";

    public IReadOnlyDictionary<string, ValueField> Values => _values;

    public IReadOnlyDictionary<string, FixedField> Fixed => _fixed;

    /// <summary>The documents, by "record|property" ("model|safetyMeasures").</summary>
    public IReadOnlyDictionary<string, DocumentField> Documents => _documents;

    public static OwnField? OwnField(string key) => OwnByKey.GetValueOrDefault(key);

    public DocumentField? Document(LoadRecord record, string property) => _documents.GetValueOrDefault($"{Prefix(record)}|{property}");

    /// <summary>True for a key of a value sheet the loader knows, whatever it does with it.</summary>
    public bool Knows(string key) => OwnByKey.ContainsKey(key) || _values.ContainsKey(key) || _fixed.ContainsKey(key) || key == HazardRowKey;

    /// <summary>The record a key names by its first part; null when that is no record.</summary>
    public static LoadRecord? RecordOf(string key)
    {
        var bar = key.IndexOf('|', StringComparison.Ordinal);
        return (bar < 0 ? key : key[..bar]) switch
        {
            "family" => LoadRecord.Family,
            "model" => LoadRecord.Model,
            "batch" => LoadRecord.Batch,
            "site" => LoadRecord.Site,
            "operator" => LoadRecord.Operator,
            _ => null,
        };
    }

    public static string Prefix(LoadRecord record) => record switch
    {
        LoadRecord.Family => "family",
        LoadRecord.Model => "model",
        LoadRecord.Batch => "batch",
        LoadRecord.Site => "site",
        _ => "operator",
    };

    /// <summary>The sheet a record's keys are on.</summary>
    public static string SheetOf(LoadRecord record) => record switch
    {
        LoadRecord.Family => FamilySheet,
        LoadRecord.Model => ModelsSheet,
        LoadRecord.Batch => BatchesSheet,
        LoadRecord.Site => SiteSheet,
        _ => OperatorSheet,
    };

    /// <summary>The record kind the save pipeline knows; a family holds no passport values.</summary>
    public static TargetKind KindOf(LoadRecord record) => record switch
    {
        LoadRecord.Model => TargetKind.Model,
        LoadRecord.Batch => TargetKind.Batch,
        LoadRecord.Site => TargetKind.Site,
        LoadRecord.Operator => TargetKind.Operator,
        _ => throw new ArgumentOutOfRangeException(nameof(record), record, "A battery family holds no passport values."),
    };

    /// <summary>The record in the team's words: "Battery model", "Manufacturing site".</summary>
    public static string Words(LoadRecord record) => record switch
    {
        LoadRecord.Family => ProductDataPages.KindName(RecordKind.Family),
        LoadRecord.Model => ProductDataPages.KindName(RecordKind.Model),
        LoadRecord.Batch => ProductDataPages.KindName(RecordKind.Batch),
        LoadRecord.Site => ProductDataPages.KindName(RecordKind.Site),
        _ => ProductDataPages.KindName(RecordKind.Operator),
    };
}
