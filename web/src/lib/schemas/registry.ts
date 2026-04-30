import path from "node:path";
import type { AspectKey } from "../../types/passport";

export type AspectSchemaInfo = {
  key: AspectKey;
  title: string;
  version: string;
  schemaPath: string;
};

const repoRoot = path.resolve(process.cwd(), "..");

export const aspectSchemas: Record<AspectKey, AspectSchemaInfo> = {
  generalProductInformation: {
    key: "generalProductInformation",
    title: "General Product Information",
    version: "1.2.0",
    schemaPath: path.join(repoRoot, "BatteryPass/io.BatteryPass.GeneralProductInformation/1.2.0/gen/GeneralProductInformation-schema.json"),
  },
  carbonFootprintForBatteries: {
    key: "carbonFootprintForBatteries",
    title: "Carbon Footprint",
    version: "1.2.0",
    schemaPath: path.join(repoRoot, "BatteryPass/io.BatteryPass.CarbonFootprint/1.2.0/gen/CarbonFootprintForBatteries-schema.json"),
  },
  circularity: {
    key: "circularity",
    title: "Circularity",
    version: "1.2.0",
    schemaPath: path.join(repoRoot, "BatteryPass/io.BatteryPass.Circularity/1.2.0/gen/Circularity-schema.json"),
  },
  materialComposition: {
    key: "materialComposition",
    title: "Material Composition",
    version: "1.2.0",
    schemaPath: path.join(repoRoot, "BatteryPass/io.BatteryPass.MaterialComposition/1.2.0/gen/MaterialComposition-schema.json"),
  },
  performanceAndDurability: {
    key: "performanceAndDurability",
    title: "Performance and Durability",
    version: "1.2.1",
    schemaPath: path.join(repoRoot, "BatteryPass/io.BatteryPass.Performance/1.2.1/gen/PerformanceAndDurability.schema"),
  },
  labeling: {
    key: "labeling",
    title: "Labels and Certification",
    version: "1.2.0",
    schemaPath: path.join(repoRoot, "BatteryPass/io.BatteryPass.Labels/1.2.0/gen/Labeling-schema.json"),
  },
  supplyChainDueDiligence: {
    key: "supplyChainDueDiligence",
    title: "Supply Chain Due Diligence",
    version: "1.2.0",
    schemaPath: path.join(repoRoot, "BatteryPass/io.BatteryPass.SupplyChainDueDiligence/1.2.0/gen/SupplyChainDueDiligence-schema.json"),
  },
};

export function getAspectSchemaInfo(key: AspectKey) {
  return aspectSchemas[key];
}
