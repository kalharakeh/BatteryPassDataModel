using BatteryPassWeb.Models.Trust;
using BatteryPassWeb.Services;
using MongoDB.Bson;

namespace BatteryPassWeb.Tests;

public sealed class JsonSchemaValidationServiceTests
{
    [Fact]
    public void ValidatePayload_ShouldReturnPassedIssueForMatchingSchema()
    {
        var schema = WriteSchema("""
            {
              "type": "object",
              "required": ["name", "status", "count", "tags", "address"],
              "properties": {
                "name": { "type": "string" },
                "status": { "enum": ["draft", "published"] },
                "count": { "type": "number", "minimum": 0 },
                "tags": { "type": "array", "items": { "type": "string" } },
                "address": { "$ref": "#/components/schemas/Address" }
              },
              "components": {
                "schemas": {
                  "Address": {
                    "type": "object",
                    "required": ["city"],
                    "properties": {
                      "city": { "type": "string" }
                    }
                  }
                }
              }
            }
            """);
        var payload = new BsonDocument
        {
            ["name"] = "Battery pack",
            ["status"] = "draft",
            ["count"] = 1,
            ["tags"] = new BsonArray { "industrial", "demo" },
            ["address"] = new BsonDocument { ["city"] = "Berlin" }
        };

        var issues = new JsonSchemaValidationService().ValidatePayload(payload, schema);

        Assert.DoesNotContain(issues, issue => issue.Severity == TrustValidationSeverity.BlockingError);
        Assert.Contains(issues, issue => issue.Severity == TrustValidationSeverity.Passed);
    }

    [Fact]
    public void ValidatePayload_ShouldReturnBlockingIssuesForSchemaViolations()
    {
        var schema = WriteSchema("""
            {
              "type": "object",
              "required": ["name", "status", "count", "tags", "address"],
              "properties": {
                "name": { "type": "string" },
                "status": { "enum": ["draft", "published"] },
                "count": { "type": "number", "minimum": 0 },
                "tags": { "type": "array", "items": { "type": "string" } },
                "address": { "$ref": "#/components/schemas/Address" }
              },
              "components": {
                "schemas": {
                  "Address": {
                    "type": "object",
                    "required": ["city"],
                    "properties": {
                      "city": { "type": "string" }
                    }
                  }
                }
              }
            }
            """);
        var payload = new BsonDocument
        {
            ["status"] = "retired",
            ["count"] = -1,
            ["tags"] = new BsonArray { 123 },
            ["address"] = new BsonDocument()
        };

        var issues = new JsonSchemaValidationService().ValidatePayload(payload, schema);

        Assert.Contains(issues, issue => issue.Severity == TrustValidationSeverity.BlockingError && issue.Path == "aspects.test.payload.name");
        Assert.Contains(issues, issue => issue.Severity == TrustValidationSeverity.BlockingError && issue.Path == "aspects.test.payload.status");
        Assert.Contains(issues, issue => issue.Severity == TrustValidationSeverity.BlockingError && issue.Path == "aspects.test.payload.count");
        Assert.Contains(issues, issue => issue.Severity == TrustValidationSeverity.BlockingError && issue.Path == "aspects.test.payload.tags[0]");
        Assert.Contains(issues, issue => issue.Severity == TrustValidationSeverity.BlockingError && issue.Path == "aspects.test.payload.address.city");
    }

    [Fact]
    public void ValidatePayload_ShouldReturnWarningWhenSchemaFileIsMissing()
    {
        var schema = new SchemaDescriptor
        {
            AspectKey = "missing",
            Label = "Missing schema",
            Version = "1.0.0",
            RelativePath = "missing-schema.json",
            AbsolutePath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "missing-schema.json")
        };

        var issues = new JsonSchemaValidationService().ValidatePayload(new BsonDocument(), schema);

        Assert.Contains(issues, issue => issue.Severity == TrustValidationSeverity.Warning && issue.Path == schema.RelativePath);
    }

    private static SchemaDescriptor WriteSchema(string json)
    {
        var directory = Directory.CreateTempSubdirectory("battery-pass-schema-test-");
        var absolutePath = Path.Combine(directory.FullName, "schema.json");
        File.WriteAllText(absolutePath, json);
        return new SchemaDescriptor
        {
            AspectKey = "test",
            Label = "Test aspect",
            Version = "1.0.0",
            RelativePath = "schema.json",
            AbsolutePath = absolutePath
        };
    }
}
