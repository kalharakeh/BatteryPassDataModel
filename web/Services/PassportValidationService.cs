using BatteryPassWeb.Models.Trust;
using MongoDB.Bson;

namespace BatteryPassWeb.Services;

public sealed class PassportValidationService
{
    private readonly SchemaRegistryService _schemaRegistryService;
    private readonly JsonSchemaValidationService _jsonSchemaValidationService;

    public PassportValidationService(
        SchemaRegistryService schemaRegistryService,
        JsonSchemaValidationService jsonSchemaValidationService)
    {
        _schemaRegistryService = schemaRegistryService;
        _jsonSchemaValidationService = jsonSchemaValidationService;
    }

    public TrustValidationSummary Validate(BsonDocument passport, DataCompletionPolicySnapshot? dataCompletionPolicy = null)
    {
        var sections = new List<TrustValidationSectionResult>
        {
            ValidateIdentity(passport)
        };

        foreach (var schema in _schemaRegistryService.ListSchemas())
        {
            sections.Add(ValidateAspect(passport, schema));
        }

        sections.Add(ValidateBusinessRules(passport));
        if (dataCompletionPolicy != null)
        {
            sections.Add(DataCompletionPolicyService.ValidateRequiredFields(passport, dataCompletionPolicy));
        }

        var state = sections.Any(section => section.HasBlockingErrors) ? TrustState.Invalid : TrustState.Valid;

        return new TrustValidationSummary
        {
            PassportId = BsonHelpers.GetString(passport, "passportId"),
            State = state,
            ValidatedAt = DateTime.UtcNow.ToString("O"),
            Sections = sections
        };
    }

    private static TrustValidationSectionResult ValidateIdentity(BsonDocument passport)
    {
        var issues = new List<TrustValidationIssue>();
        AddRequiredString(passport, "passportId", "Passport ID is required.", issues);
        AddRequiredString(passport, "registryInfo.registryId", "Registry ID is required.", issues);
        AddRequiredString(passport, "app.display.serialNumber", "Serial number is required.", issues);
        AddRequiredString(passport, "app.display.manufacturerName", "Manufacturer name is required.", issues);

        if (issues.Count == 0)
        {
            issues.Add(new TrustValidationIssue(TrustValidationSeverity.Passed, "identity", "Required identity fields are present."));
        }

        return new TrustValidationSectionResult
        {
            SectionKey = "identity",
            SectionLabel = "Identity",
            Issues = issues
        };
    }

    private TrustValidationSectionResult ValidateAspect(BsonDocument passport, SchemaDescriptor schema)
    {
        var issues = new List<TrustValidationIssue>();
        var aspect = BsonHelpers.GetValue(passport, "aspects", schema.AspectKey);
        if (aspect is not BsonDocument aspectDocument)
        {
            issues.Add(new TrustValidationIssue(TrustValidationSeverity.Warning, $"aspects.{schema.AspectKey}", $"{schema.Label} aspect is not present."));
        }
        else if (aspectDocument.GetValue("payload", BsonNull.Value) is not BsonDocument payload)
        {
            issues.Add(new TrustValidationIssue(TrustValidationSeverity.BlockingError, $"aspects.{schema.AspectKey}.payload", $"{schema.Label} payload must be an object."));
        }
        else
        {
            issues.Add(new TrustValidationIssue(TrustValidationSeverity.Passed, $"aspects.{schema.AspectKey}.payload", $"{schema.Label} payload is present."));
            issues.AddRange(_jsonSchemaValidationService.ValidatePayload(payload, schema));
        }

        if (schema.Exists)
        {
            issues.Add(new TrustValidationIssue(TrustValidationSeverity.Passed, schema.RelativePath, $"{schema.Label} schema file was found."));
        }
        else if (issues.All(issue => issue.Path != schema.RelativePath))
        {
            issues.Add(new TrustValidationIssue(TrustValidationSeverity.Warning, schema.RelativePath, $"{schema.Label} schema file was not found."));
        }

        return new TrustValidationSectionResult
        {
            SectionKey = schema.AspectKey,
            SectionLabel = schema.Label,
            Issues = issues
        };
    }

    private static TrustValidationSectionResult ValidateBusinessRules(BsonDocument passport)
    {
        var issues = new List<TrustValidationIssue>();
        AddNonNegativeNumber(passport, "aspects.generalProductInformation.payload.batteryMass", issues);
        AddNonNegativeNumber(passport, "aspects.performanceAndDurability.payload.batteryTechicalProperties.ratedEnergy", issues);
        AddNonNegativeNumber(passport, "aspects.performanceAndDurability.payload.batteryTechicalProperties.ratedCapacity", issues);
        AddNonNegativeNumber(passport, "aspects.performanceAndDurability.payload.batteryTechicalProperties.ratedMaximumPower", issues);
        AddNonNegativeNumber(passport, "aspects.performanceAndDurability.payload.batteryTechicalProperties.nominalVoltage", issues);

        var batteryImageUrl = BsonHelpers.GetString(passport, "app", "media", "batteryImageUrl");
        if (string.IsNullOrWhiteSpace(batteryImageUrl))
        {
            issues.Add(new TrustValidationIssue(TrustValidationSeverity.Warning, "app.media.batteryImageUrl", "Battery image is missing or using fallback."));
        }

        if (issues.Count == 0)
        {
            issues.Add(new TrustValidationIssue(TrustValidationSeverity.Passed, "businessRules", "Business rule checks passed."));
        }

        return new TrustValidationSectionResult
        {
            SectionKey = "businessRules",
            SectionLabel = "Business rules",
            Issues = issues
        };
    }

    private static void AddRequiredString(BsonDocument document, string path, string message, ICollection<TrustValidationIssue> issues)
    {
        var value = BsonHelpers.GetValue(document, path.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
        if (value == null || value.IsBsonNull || string.IsNullOrWhiteSpace(value.ToString()))
        {
            issues.Add(new TrustValidationIssue(TrustValidationSeverity.BlockingError, path, message));
        }
    }

    private static void AddNonNegativeNumber(BsonDocument passport, string path, ICollection<TrustValidationIssue> issues)
    {
        var segments = path.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var value = BsonHelpers.GetValue(passport, segments);
        if (value != null && value.IsNumeric && value.ToDouble() < 0)
        {
            issues.Add(new TrustValidationIssue(TrustValidationSeverity.BlockingError, path, $"{segments.Last()} cannot be negative."));
        }
    }
}
