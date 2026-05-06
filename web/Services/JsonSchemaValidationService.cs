using System.Text.Json;
using System.Text.RegularExpressions;
using BatteryPassWeb.Models.Trust;
using MongoDB.Bson;

namespace BatteryPassWeb.Services;

public sealed class JsonSchemaValidationService
{
    private const int MaxDepth = 128;

    public IReadOnlyList<TrustValidationIssue> ValidatePayload(BsonDocument payload, SchemaDescriptor schema)
    {
        if (!schema.Exists)
        {
            return
            [
                new TrustValidationIssue(
                    TrustValidationSeverity.Warning,
                    schema.RelativePath,
                    $"{schema.Label} schema file was not found.")
            ];
        }

        var issues = new List<TrustValidationIssue>();
        try
        {
            using var schemaDocument = JsonDocument.Parse(File.ReadAllText(schema.AbsolutePath));
            var rootSchema = schemaDocument.RootElement;
            ValidateSchema(
                rootSchema,
                payload,
                rootSchema,
                $"aspects.{schema.AspectKey}.payload",
                issues,
                new HashSet<string>(StringComparer.Ordinal),
                0);
        }
        catch (JsonException exception)
        {
            issues.Add(new TrustValidationIssue(
                TrustValidationSeverity.Warning,
                schema.RelativePath,
                $"{schema.Label} schema file could not be parsed: {exception.Message}"));
        }
        catch (IOException exception)
        {
            issues.Add(new TrustValidationIssue(
                TrustValidationSeverity.Warning,
                schema.RelativePath,
                $"{schema.Label} schema file could not be read: {exception.Message}"));
        }

        if (issues.All(issue => issue.Severity != TrustValidationSeverity.BlockingError))
        {
            issues.Add(new TrustValidationIssue(
                TrustValidationSeverity.Passed,
                $"aspects.{schema.AspectKey}.payload",
                $"{schema.Label} payload matches schema {schema.Version}."));
        }

        return issues;
    }

    private static void ValidateSchema(
        JsonElement schema,
        BsonValue value,
        JsonElement rootSchema,
        string path,
        ICollection<TrustValidationIssue> issues,
        ISet<string> referenceStack,
        int depth)
    {
        if (depth > MaxDepth)
        {
            AddBlocking(issues, path, "Schema validation exceeded the maximum supported nesting depth.");
            return;
        }

        if (schema.ValueKind != JsonValueKind.Object)
        {
            return;
        }

        if (schema.TryGetProperty("$ref", out var referenceElement)
            && referenceElement.ValueKind == JsonValueKind.String)
        {
            var reference = referenceElement.GetString() ?? string.Empty;
            if (referenceStack.Contains(reference))
            {
                AddBlocking(issues, path, $"Schema reference cycle detected at {reference}.");
                return;
            }

            if (!TryResolveReference(rootSchema, reference, out var referencedSchema))
            {
                AddBlocking(issues, path, $"Schema reference {reference} could not be resolved.");
                return;
            }

            referenceStack.Add(reference);
            ValidateSchema(referencedSchema, value, rootSchema, path, issues, referenceStack, depth + 1);
            referenceStack.Remove(reference);
        }

        if (schema.TryGetProperty("allOf", out var allOfElement)
            && allOfElement.ValueKind == JsonValueKind.Array)
        {
            foreach (var childSchema in allOfElement.EnumerateArray())
            {
                ValidateSchema(childSchema, value, rootSchema, path, issues, referenceStack, depth + 1);
            }
        }

        ValidateType(schema, value, path, issues);
        ValidateEnum(schema, value, path, issues);
        ValidateString(schema, value, path, issues);
        ValidateNumber(schema, value, path, issues);
        ValidateObject(schema, value, rootSchema, path, issues, referenceStack, depth);
        ValidateArray(schema, value, rootSchema, path, issues, referenceStack, depth);
    }

    private static void ValidateType(JsonElement schema, BsonValue value, string path, ICollection<TrustValidationIssue> issues)
    {
        if (!schema.TryGetProperty("type", out var typeElement))
        {
            return;
        }

        var allowedTypes = ReadAllowedTypes(typeElement);
        if (allowedTypes.Count == 0 || allowedTypes.Any(type => MatchesType(value, type)))
        {
            return;
        }

        AddBlocking(issues, path, $"Value must be {string.Join(" or ", allowedTypes)}.");
    }

    private static void ValidateEnum(JsonElement schema, BsonValue value, string path, ICollection<TrustValidationIssue> issues)
    {
        if (schema.TryGetProperty("enum", out var enumElement)
            && enumElement.ValueKind == JsonValueKind.Array
            && !enumElement.EnumerateArray().Any(option => BsonEqualsJson(value, option)))
        {
            AddBlocking(issues, path, "Value is not one of the allowed schema values.");
        }

        if (schema.TryGetProperty("const", out var constElement) && !BsonEqualsJson(value, constElement))
        {
            AddBlocking(issues, path, "Value does not match the required schema constant.");
        }
    }

    private static void ValidateString(JsonElement schema, BsonValue value, string path, ICollection<TrustValidationIssue> issues)
    {
        if (!value.IsString)
        {
            return;
        }

        var text = value.AsString;
        if (schema.TryGetProperty("minLength", out var minLengthElement)
            && minLengthElement.TryGetInt32(out var minLength)
            && text.Length < minLength)
        {
            AddBlocking(issues, path, $"Text must be at least {minLength} characters long.");
        }

        if (schema.TryGetProperty("maxLength", out var maxLengthElement)
            && maxLengthElement.TryGetInt32(out var maxLength)
            && text.Length > maxLength)
        {
            AddBlocking(issues, path, $"Text must be no more than {maxLength} characters long.");
        }

        if (schema.TryGetProperty("pattern", out var patternElement)
            && patternElement.ValueKind == JsonValueKind.String)
        {
            var pattern = patternElement.GetString();
            if (!string.IsNullOrWhiteSpace(pattern) && !Regex.IsMatch(text, pattern))
            {
                AddBlocking(issues, path, "Text does not match the required schema pattern.");
            }
        }
    }

    private static void ValidateNumber(JsonElement schema, BsonValue value, string path, ICollection<TrustValidationIssue> issues)
    {
        if (!value.IsNumeric)
        {
            return;
        }

        var number = value.ToDouble();
        if (schema.TryGetProperty("minimum", out var minimumElement)
            && minimumElement.TryGetDouble(out var minimum)
            && number < minimum)
        {
            AddBlocking(issues, path, $"Number must be greater than or equal to {minimum}.");
        }

        if (schema.TryGetProperty("maximum", out var maximumElement)
            && maximumElement.TryGetDouble(out var maximum)
            && number > maximum)
        {
            AddBlocking(issues, path, $"Number must be less than or equal to {maximum}.");
        }

        if (schema.TryGetProperty("exclusiveMinimum", out var exclusiveMinimumElement)
            && IsExclusiveBoundary(exclusiveMinimumElement, out var exclusiveMinimum)
            && number <= exclusiveMinimum)
        {
            AddBlocking(issues, path, $"Number must be greater than {exclusiveMinimum}.");
        }

        if (schema.TryGetProperty("exclusiveMaximum", out var exclusiveMaximumElement)
            && IsExclusiveBoundary(exclusiveMaximumElement, out var exclusiveMaximum)
            && number >= exclusiveMaximum)
        {
            AddBlocking(issues, path, $"Number must be less than {exclusiveMaximum}.");
        }
    }

    private static void ValidateObject(
        JsonElement schema,
        BsonValue value,
        JsonElement rootSchema,
        string path,
        ICollection<TrustValidationIssue> issues,
        ISet<string> referenceStack,
        int depth)
    {
        var hasObjectKeywords = schema.TryGetProperty("required", out _)
            || schema.TryGetProperty("properties", out _);
        if (!hasObjectKeywords)
        {
            return;
        }

        if (value is not BsonDocument document)
        {
            AddBlocking(issues, path, "Value must be an object.");
            return;
        }

        if (schema.TryGetProperty("required", out var requiredElement)
            && requiredElement.ValueKind == JsonValueKind.Array)
        {
            foreach (var requiredProperty in requiredElement.EnumerateArray())
            {
                var propertyName = requiredProperty.GetString();
                if (!string.IsNullOrWhiteSpace(propertyName)
                    && (!document.TryGetValue(propertyName, out var requiredValue) || requiredValue.IsBsonNull))
                {
                    AddBlocking(issues, AppendPath(path, propertyName), $"{propertyName} is required by the schema.");
                }
            }
        }

        if (schema.TryGetProperty("properties", out var propertiesElement)
            && propertiesElement.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in propertiesElement.EnumerateObject())
            {
                if (document.TryGetValue(property.Name, out var propertyValue) && !propertyValue.IsBsonNull)
                {
                    ValidateSchema(
                        property.Value,
                        propertyValue,
                        rootSchema,
                        AppendPath(path, property.Name),
                        issues,
                        referenceStack,
                        depth + 1);
                }
            }
        }
    }

    private static void ValidateArray(
        JsonElement schema,
        BsonValue value,
        JsonElement rootSchema,
        string path,
        ICollection<TrustValidationIssue> issues,
        ISet<string> referenceStack,
        int depth)
    {
        if (!schema.TryGetProperty("items", out var itemsElement)
            && !schema.TryGetProperty("minItems", out _)
            && !schema.TryGetProperty("maxItems", out _))
        {
            return;
        }

        if (value is not BsonArray array)
        {
            AddBlocking(issues, path, "Value must be an array.");
            return;
        }

        if (schema.TryGetProperty("minItems", out var minItemsElement)
            && minItemsElement.TryGetInt32(out var minItems)
            && array.Count < minItems)
        {
            AddBlocking(issues, path, $"Array must contain at least {minItems} items.");
        }

        if (schema.TryGetProperty("maxItems", out var maxItemsElement)
            && maxItemsElement.TryGetInt32(out var maxItems)
            && array.Count > maxItems)
        {
            AddBlocking(issues, path, $"Array must contain no more than {maxItems} items.");
        }

        if (schema.TryGetProperty("items", out itemsElement) && itemsElement.ValueKind == JsonValueKind.Object)
        {
            for (var index = 0; index < array.Count; index++)
            {
                ValidateSchema(
                    itemsElement,
                    array[index],
                    rootSchema,
                    $"{path}[{index}]",
                    issues,
                    referenceStack,
                    depth + 1);
            }
        }
    }

    private static IReadOnlyList<string> ReadAllowedTypes(JsonElement typeElement)
    {
        if (typeElement.ValueKind == JsonValueKind.String)
        {
            return [typeElement.GetString() ?? string.Empty];
        }

        if (typeElement.ValueKind == JsonValueKind.Array)
        {
            return typeElement.EnumerateArray()
                .Where(item => item.ValueKind == JsonValueKind.String)
                .Select(item => item.GetString() ?? string.Empty)
                .Where(type => !string.IsNullOrWhiteSpace(type))
                .ToList();
        }

        return [];
    }

    private static bool MatchesType(BsonValue value, string type)
    {
        return type switch
        {
            "array" => value is BsonArray,
            "boolean" => value.IsBoolean,
            "integer" => value.IsNumeric && Math.Abs(value.ToDouble() % 1) < double.Epsilon,
            "null" => value.IsBsonNull,
            "number" => value.IsNumeric,
            "object" => value is BsonDocument,
            "string" => value.IsString,
            _ => true
        };
    }

    private static bool BsonEqualsJson(BsonValue value, JsonElement expected)
    {
        return expected.ValueKind switch
        {
            JsonValueKind.String => value.IsString && string.Equals(value.AsString, expected.GetString(), StringComparison.Ordinal),
            JsonValueKind.Number => value.IsNumeric && expected.TryGetDouble(out var number) && Math.Abs(value.ToDouble() - number) < 0.0000001,
            JsonValueKind.True => value.IsBoolean && value.AsBoolean,
            JsonValueKind.False => value.IsBoolean && !value.AsBoolean,
            JsonValueKind.Null => value.IsBsonNull,
            _ => false
        };
    }

    private static bool TryResolveReference(JsonElement rootSchema, string reference, out JsonElement referencedSchema)
    {
        referencedSchema = default;
        if (string.IsNullOrWhiteSpace(reference) || !reference.StartsWith("#/", StringComparison.Ordinal))
        {
            return false;
        }

        var current = rootSchema;
        foreach (var rawSegment in reference[2..].Split('/', StringSplitOptions.RemoveEmptyEntries))
        {
            var segment = DecodeJsonPointerSegment(rawSegment);
            if (current.ValueKind != JsonValueKind.Object || !current.TryGetProperty(segment, out current))
            {
                return false;
            }
        }

        referencedSchema = current;
        return true;
    }

    private static bool IsExclusiveBoundary(JsonElement element, out double boundary)
    {
        boundary = 0;
        return element.ValueKind switch
        {
            JsonValueKind.Number => element.TryGetDouble(out boundary),
            JsonValueKind.True => false,
            JsonValueKind.False => false,
            _ => false
        };
    }

    private static string DecodeJsonPointerSegment(string segment)
    {
        return segment.Replace("~1", "/", StringComparison.Ordinal).Replace("~0", "~", StringComparison.Ordinal);
    }

    private static string AppendPath(string path, string property)
    {
        return $"{path}.{property}";
    }

    private static void AddBlocking(ICollection<TrustValidationIssue> issues, string path, string message)
    {
        issues.Add(new TrustValidationIssue(TrustValidationSeverity.BlockingError, path, message));
    }
}
