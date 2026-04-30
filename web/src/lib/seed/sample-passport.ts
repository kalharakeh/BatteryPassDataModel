import type { BatteryPassport } from "../../types/passport";

const now = "2024-09-05T08:03:42.000Z";

export const samplePassport: BatteryPassport = {
  passportId: "did:web:local.battery.pass:0226151e-949c-d067-8ef3-162431e28976",
  schemaVersions: {
    generalProductInformation: "1.2.0",
    carbonFootprintForBatteries: "1.2.0",
    circularity: "1.2.0",
    materialComposition: "1.2.0",
    performanceAndDurability: "1.2.1",
    labeling: "1.2.0",
    supplyChainDueDiligence: "1.2.0",
  },
  registryInfo: {
    registryId: "886a9b6b-1fa2-434a-ade0-b724e8dd7656",
    status: "published",
    createdAt: now,
    updatedAt: now,
    revisionPassportId: "",
    latestPassportRevision: "",
  },
  aspects: {
    generalProductInformation: {
      visibility: "public",
      verification: { state: "verified", issuer: "did:web:local.battery.pass:issuer", signedAt: now },
      payload: {
        productIdentifier: "EV-BAT095",
        batteryPassportIdentifier: "urn:local:0226151e949cd0678ef3162431e28976",
        batteryCategory: "ev",
        manufacturerInformation: {
          contactName: "Exide Batteries Auditor",
          identifier: "exide-batteries",
          postalAddress: { addressCountry: "Germany", postalCode: "10724", streetAddress: "ACME Street 1" },
          webAddress: "https://exide-batteries.example",
        },
        manufacturingDate: "2023-09-05T18:58:41.000Z",
        batteryStatus: "Original",
        batteryMass: 499,
        manufacturingPlace: { addressCountry: "Germany", postalCode: "10724", streetAddress: "ACME Street 1" },
        operatorInformation: {
          contactName: "ACME Batteries Auditor",
          identifier: "acme-batteries",
          postalAddress: { addressCountry: "Germany", postalCode: "10724", streetAddress: "ACME Street 1" },
          webAddress: "https://acme-batteries.example",
        },
        puttingIntoService: "2024-01-10T00:00:00.000Z",
        warrentyPeriod: "P8Y",
      },
    },
    carbonFootprintForBatteries: {
      visibility: "public",
      verification: { state: "verified", issuer: "did:web:local.battery.pass:issuer", signedAt: now },
      payload: {
        batteryCarbonFootprint: 137,
        carbonFootprintPerLifecycleStage: [
          { lifecycleStage: "rawMaterialExtraction", carbonFootprint: 89 },
          { lifecycleStage: "mainProduction", carbonFootprint: 30 },
          { lifecycleStage: "distribution", carbonFootprint: 10 },
          { lifecycleStage: "recycling", carbonFootprint: 8 },
        ],
        carbonFootprintPerformanceClass: "B",
        carbonFootprintStudy: "https://exide-batteries.example/studies/90288",
      },
    },
    supplyChainDueDiligence: {
      visibility: "privileged",
      verification: { state: "partial" },
      payload: {
        supplyChainDueDiligenceReport: "https://exide-batteries.example/sdd-report.pdf",
        thirdPartyAussurances: "https://exide-batteries.example/audit-certificate.vc",
        supplyChainIndicies: 82,
      },
    },
  },
  hiddenProperties: ["supplyChainDueDiligence"],
  instanceOnlyFields: ["generalProductInformation.payload.batteryStatus"],
  validation: {
    hash: "",
    signature: "",
    proof: {},
    isValid: false,
    signedAt: null,
  },
  dataSource: {
    name: "MongoDB Atlas",
    instanceUrl: "/api/passport-instances/did:web:local.battery.pass:0226151e-949c-d067-8ef3-162431e28976",
  },
};
