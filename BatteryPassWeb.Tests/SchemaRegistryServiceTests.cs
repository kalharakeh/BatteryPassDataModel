using BatteryPassWeb.Services;

namespace BatteryPassWeb.Tests;

public sealed class SchemaRegistryServiceTests
{
    [Fact]
    public void ListSchemas_ShouldResolveKnownBatteryPassSchemas()
    {
        var service = new SchemaRegistryService();

        var schemas = service.ListSchemas();

        Assert.Contains(schemas, schema => schema.AspectKey == "generalProductInformation" && schema.Exists);
        Assert.Contains(schemas, schema => schema.AspectKey == "carbonFootprintForBatteries" && schema.Exists);
        Assert.Contains(schemas, schema => schema.AspectKey == "circularity" && schema.Exists);
        Assert.Contains(schemas, schema => schema.AspectKey == "materialComposition" && schema.Exists);
        Assert.Contains(schemas, schema => schema.AspectKey == "performanceAndDurability" && schema.Exists);
        Assert.Contains(schemas, schema => schema.AspectKey == "labeling" && schema.Exists);
        Assert.Contains(schemas, schema => schema.AspectKey == "supplyChainDueDiligence" && schema.Exists);
    }

    [Fact]
    public void GetSchema_ShouldReturnNullForUnknownAspect()
    {
        var service = new SchemaRegistryService();

        var schema = service.GetSchema("unknown");

        Assert.Null(schema);
    }
}
