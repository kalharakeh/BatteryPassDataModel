using BatteryPassWeb.Models.Trust;

namespace BatteryPassWeb.Services;

public sealed class SchemaRegistryService
{
    private static readonly SchemaDescriptor[] KnownSchemas =
    [
        new()
        {
            AspectKey = "generalProductInformation",
            Label = "General product information",
            Version = "1.2.0",
            RelativePath = Path.Combine("BatteryPass", "io.BatteryPass.GeneralProductInformation", "1.2.0", "gen", "GeneralProductInformation-schema.json")
        },
        new()
        {
            AspectKey = "carbonFootprintForBatteries",
            Label = "Carbon footprint",
            Version = "1.2.0",
            RelativePath = Path.Combine("BatteryPass", "io.BatteryPass.CarbonFootprint", "1.2.0", "gen", "CarbonFootprintForBatteries-schema.json")
        },
        new()
        {
            AspectKey = "circularity",
            Label = "Circularity",
            Version = "1.2.0",
            RelativePath = Path.Combine("BatteryPass", "io.BatteryPass.Circularity", "1.2.0", "gen", "Circularity-schema.json")
        },
        new()
        {
            AspectKey = "materialComposition",
            Label = "Material composition",
            Version = "1.2.0",
            RelativePath = Path.Combine("BatteryPass", "io.BatteryPass.MaterialComposition", "1.2.0", "gen", "MaterialComposition-schema.json")
        },
        new()
        {
            AspectKey = "performanceAndDurability",
            Label = "Performance and durability",
            Version = "1.2.1",
            RelativePath = Path.Combine("BatteryPass", "io.BatteryPass.Performance", "1.2.1", "gen", "PerformanceAndDurability.schema")
        },
        new()
        {
            AspectKey = "labeling",
            Label = "Labels and certification",
            Version = "1.2.0",
            RelativePath = Path.Combine("BatteryPass", "io.BatteryPass.Labels", "1.2.0", "gen", "Labeling-schema.json")
        },
        new()
        {
            AspectKey = "supplyChainDueDiligence",
            Label = "Supply chain due diligence",
            Version = "1.2.0",
            RelativePath = Path.Combine("BatteryPass", "io.BatteryPass.SupplyChainDueDiligence", "1.2.0", "gen", "SupplyChainDueDiligence-schema.json")
        }
    ];

    private readonly string _repoRoot;

    public SchemaRegistryService()
    {
        _repoRoot = ResolveRepoRoot(AppContext.BaseDirectory);
    }

    public IReadOnlyList<SchemaDescriptor> ListSchemas()
    {
        return KnownSchemas.Select(WithAbsolutePath).ToList();
    }

    public SchemaDescriptor? GetSchema(string aspectKey)
    {
        var descriptor = KnownSchemas.FirstOrDefault(schema => schema.AspectKey.Equals(aspectKey, StringComparison.OrdinalIgnoreCase));
        return descriptor == null ? null : WithAbsolutePath(descriptor);
    }

    private SchemaDescriptor WithAbsolutePath(SchemaDescriptor descriptor)
    {
        return new SchemaDescriptor
        {
            AspectKey = descriptor.AspectKey,
            Label = descriptor.Label,
            Version = descriptor.Version,
            RelativePath = descriptor.RelativePath,
            AbsolutePath = Path.Combine(_repoRoot, descriptor.RelativePath)
        };
    }

    private static string ResolveRepoRoot(string start)
    {
        var directory = new DirectoryInfo(start);
        while (directory != null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "README.md"))
                && Directory.Exists(Path.Combine(directory.FullName, "BatteryPass"))
                && Directory.Exists(Path.Combine(directory.FullName, "web")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        return Directory.GetCurrentDirectory();
    }
}
