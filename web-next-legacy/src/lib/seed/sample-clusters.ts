import { createHash } from "node:crypto";
import type { BatteryPassport, Cluster, ClusterMembership } from "@/types/passport";
import { samplePassport } from "./sample-passport";

const now = "2026-05-04T00:00:00.000Z";
const sampleBatteryManufacturerName = "Scania Industrial Batteries";
const sampleBatteryImageOptions = [
  { url: "/images/compact7.png", category: "Compact 7M" },
  { url: "/images/compact13.png", category: "Compact 13M" },
  { url: "/images/core.png", category: "Core" },
];

export const sampleClusterPassword = "Password123!";

export const legacySampleClusterIds = [
  "sample-cluster-default",
  "sample-cluster-customer-north",
  "sample-cluster-customer-south",
  "sample-cluster-end-user-fleet",
  "sample-cluster-end-user-storage",
];

export const defaultSampleCluster: Cluster = {
  clusterId: "cluster-default-demonstrator",
  name: "Default Demonstrator Cluster",
  createdAt: now,
  updatedAt: now,
};

type SampleUser = {
  email: string;
  name: string;
  roles: string[];
};

type CarbonStages = {
  rawMaterials: number;
  production: number;
  distribution: number;
  recycling: number;
};

type AdditionalClusterSeedInput = {
  cluster: Cluster;
  passportId: string;
  registryId: string;
  name: string;
  modelNumber: string;
  serialNumber: string;
  facilityId: string;
  batteryCategory: string;
  batteryStatus: string;
  batteryMass: number;
  manufacturingDate: string;
  puttingIntoService: string;
  chemistry: {
    shortName: string;
    clearName: string;
  };
  materialScale: number;
  carbonFootprint: number;
  carbonStages: CarbonStages;
  performanceClass: string;
  ratedEnergy: number;
  ratedCapacity: number;
  ratedMaximumPower: number;
  nominalVoltage: number;
  expectedLifetime: number;
  expectedNumberOfCycles: number;
  stateOfCharge: number;
  remainingCapacity: number;
  remainingEnergy: number;
  fullCycles: number;
  normalUser: SampleUser;
  adminUser: SampleUser;
};

function cloneSamplePassport() {
  return JSON.parse(JSON.stringify(samplePassport)) as BatteryPassport;
}

function slug(value: string) {
  return value.toLowerCase().replace(/[^a-z0-9]+/g, "-").replace(/^-|-$/g, "");
}

function rounded(value: number) {
  return Math.round(value * 10) / 10;
}

export function getSampleBatteryImageOption(passportId: string) {
  const normalized = passportId.trim().toLowerCase();
  const hash = createHash("sha256").update(normalized).digest();
  return sampleBatteryImageOptions[hash[0] % sampleBatteryImageOptions.length];
}

function carbonStagePayload(input: CarbonStages) {
  return [
    { lifecycleStage: "RawMaterialExtraction", carbonFootprint: input.rawMaterials },
    { lifecycleStage: "MainProduction", carbonFootprint: input.production },
    { lifecycleStage: "Distribution", carbonFootprint: input.distribution },
    { lifecycleStage: "Recycling", carbonFootprint: input.recycling },
  ];
}

function carbonStageCharts(input: CarbonStages) {
  return [
    { label: "raw material extraction", value: input.rawMaterials, unit: "gCO2e/kWh", color: "#08a348" },
    { label: "main production", value: input.production, unit: "gCO2e/kWh", color: "#df6b3b" },
    { label: "distribution", value: input.distribution, unit: "gCO2e/kWh", color: "#ead9a4" },
    { label: "recycling", value: input.recycling, unit: "gCO2e/kWh", color: "#4f6f7d" },
  ];
}

function buildPassport(input: AdditionalClusterSeedInput): BatteryPassport {
  const passport = cloneSamplePassport();
  const imageOption = getSampleBatteryImageOption(input.passportId);
  const general = passport.aspects.generalProductInformation?.payload ?? {};
  const materialComposition = passport.aspects.materialComposition?.payload ?? {};
  const carbon = passport.aspects.carbonFootprintForBatteries?.payload ?? {};
  const circularity = passport.aspects.circularity?.payload ?? {};
  const performance = passport.aspects.performanceAndDurability?.payload ?? {};
  const technical = (performance.batteryTechicalProperties ?? {}) as Record<string, unknown>;
  const batteryCondition = (performance.batteryCondition ?? {}) as Record<string, Record<string, unknown>>;

  passport.passportId = input.passportId;
  passport.clusterId = input.cluster.clusterId;
  passport.registryInfo.registryId = input.registryId;
  passport.registryInfo.status = "published";
  passport.registryInfo.createdAt = now;
  passport.registryInfo.updatedAt = now;
  passport.dataSource.instanceUrl = `/api/passport-instances/${input.passportId}`;
  passport.validation.signedAt = now;

  passport.app.display = {
    name: input.name,
    modelNumber: input.modelNumber,
    serialNumber: input.serialNumber,
    facilityId: input.facilityId,
    manufacturerName: sampleBatteryManufacturerName,
  };
  passport.app.media = {
    ...passport.app.media,
    batteryImageUrl: imageOption.url,
    batteryImageAlt: `Industrial battery pack for passport ${input.modelNumber}`,
  };

  general.productIdentifier = input.modelNumber;
  general.batteryPassportIdentifier = `urn:acme:${input.serialNumber.toLowerCase().replace(/[^a-z0-9]/g, "")}`;
  general.batteryCategory = imageOption.category;
  general.batteryStatus = input.batteryStatus;
  general.batteryMass = input.batteryMass;
  general.manufacturingDate = input.manufacturingDate;
  general.puttingIntoService = input.puttingIntoService;
  general.manufacturerInformation = {
    ...((general.manufacturerInformation ?? {}) as Record<string, unknown>),
    contactName: sampleBatteryManufacturerName,
    identifier: slug(sampleBatteryManufacturerName),
    postalAddress: { addressCountry: "Germany", postalCode: "10115", streetAddress: input.facilityId },
  };
  general.manufacturingPlace = { addressCountry: "Germany", postalCode: "10115", streetAddress: input.facilityId };
  passport.aspects.generalProductInformation = {
    ...(passport.aspects.generalProductInformation ?? { visibility: "public", verification: { state: "verified" as const } }),
    payload: general,
  };

  materialComposition.batteryChemistry = input.chemistry;
  if (Array.isArray(materialComposition.batteryMaterials)) {
    materialComposition.batteryMaterials = materialComposition.batteryMaterials.map((material) => ({
      ...(material as Record<string, unknown>),
      batteryMaterialMass: rounded(Number((material as Record<string, unknown>).batteryMaterialMass ?? 0) * input.materialScale),
    }));
  }
  passport.aspects.materialComposition = {
    ...(passport.aspects.materialComposition ?? { visibility: "public", verification: { state: "verified" as const } }),
    payload: materialComposition,
  };
  passport.app.charts.materialComposition = passport.app.charts.materialComposition.map((segment) => ({
    ...segment,
    value: rounded(segment.value * input.materialScale),
  }));

  carbon.batteryCarbonFootprint = input.carbonFootprint;
  carbon.carbonFootprintPerLifecycleStage = carbonStagePayload(input.carbonStages);
  carbon.carbonFootprintPerformanceClass = input.performanceClass;
  passport.aspects.carbonFootprintForBatteries = {
    ...(passport.aspects.carbonFootprintForBatteries ?? { visibility: "public", verification: { state: "verified" as const } }),
    payload: carbon,
  };
  passport.app.charts.carbonFootprint = carbonStageCharts(input.carbonStages);

  technical.originalPowerCapability = [
    { atSoC: 20, powerCapabilityAt: rounded(input.ratedMaximumPower * 0.64) },
    { atSoC: 50, powerCapabilityAt: rounded(input.ratedMaximumPower * 0.88) },
    { atSoC: 80, powerCapabilityAt: input.ratedMaximumPower },
  ];
  technical.ratedEnergy = input.ratedEnergy;
  technical.ratedCapacity = input.ratedCapacity;
  technical.ratedMaximumPower = input.ratedMaximumPower;
  technical.nominalVoltage = input.nominalVoltage;
  technical.maximumVoltage = input.nominalVoltage + 70;
  technical.minimumVoltage = input.nominalVoltage - 120;
  technical.expectedLifetime = input.expectedLifetime;
  technical.expectedNumberOfCycles = input.expectedNumberOfCycles;
  performance.batteryTechicalProperties = technical;

  batteryCondition.stateOfCharge = { stateOfChargeValue: input.stateOfCharge, lastUpdate: now };
  batteryCondition.remainingCapacity = { remainingCapacityValue: input.remainingCapacity, lastUpdate: now };
  batteryCondition.remainingEnergy = { remainingEnergyValue: input.remainingEnergy, lastUpdate: now };
  batteryCondition.numberOfFullCycles = { numberOfFullCyclesValue: input.fullCycles, lastUpdate: now };
  performance.batteryCondition = batteryCondition;
  passport.aspects.performanceAndDurability = {
    ...(passport.aspects.performanceAndDurability ?? { visibility: "public", verification: { state: "verified" as const } }),
    payload: performance,
  };

  if (Array.isArray(circularity.recycledContent)) {
    circularity.recycledContent = circularity.recycledContent.map((entry, index) => ({
      ...(entry as Record<string, unknown>),
      preConsumerShare: Math.min(30, Number((entry as Record<string, unknown>).preConsumerShare ?? 0) + index + Math.round(input.materialScale * 3)),
      postConsumerShare: Math.min(25, Number((entry as Record<string, unknown>).postConsumerShare ?? 0) + Math.round(input.materialScale * 2)),
    }));
    passport.aspects.circularity = {
      ...(passport.aspects.circularity ?? { visibility: "public", verification: { state: "partial" as const } }),
      payload: circularity,
    };
    passport.app.charts.recycledContent = passport.app.charts.recycledContent.map((entry, index) => {
      const preConsumerShare = Math.min(30, entry.preConsumerShare + index + Math.round(input.materialScale * 3));
      const postConsumerShare = Math.min(25, entry.postConsumerShare + Math.round(input.materialScale * 2));
      return {
        ...entry,
        preConsumerShare,
        postConsumerShare,
        primaryMaterialShare: Math.max(0, 100 - preConsumerShare - postConsumerShare),
      };
    });
  }

  return passport;
}

const seedInputs: AdditionalClusterSeedInput[] = [
  {
    cluster: {
      clusterId: "cluster-north-operations",
      name: "North Operations Cluster",
      createdAt: now,
      updatedAt: now,
    },
    passportId: "did:web:acme.battery.pass:sample-customer-north-001",
    registryId: "sample-registry-customer-north-001",
    name: "EV-BAT201",
    modelNumber: "M-201-NORTH",
    serialNumber: "NORTH-201-0001",
    facilityId: "North Operations Facility",
    batteryCategory: "ev",
    batteryStatus: "Original",
    batteryMass: 512,
    manufacturingDate: "2023-11-12T00:00:00.000Z",
    puttingIntoService: "2024-02-19T00:00:00.000Z",
    chemistry: { shortName: "NMC", clearName: "Lithium nickel manganese cobalt oxide" },
    materialScale: 1.03,
    carbonFootprint: 129,
    carbonStages: { rawMaterials: 80, production: 31, distribution: 9, recycling: 9 },
    performanceClass: "A",
    ratedEnergy: 102,
    ratedCapacity: 218,
    ratedMaximumPower: 410,
    nominalVoltage: 740,
    expectedLifetime: 13,
    expectedNumberOfCycles: 3400,
    stateOfCharge: 73,
    remainingCapacity: 89.4,
    remainingEnergy: 83.2,
    fullCycles: 520,
    normalUser: { email: "north.user@example.test", name: "North Normal User", roles: ["viewer"] },
    adminUser: { email: "north.admin@example.test", name: "North Local Admin", roles: ["viewer"] },
  },
  {
    cluster: {
      clusterId: "cluster-south-operations",
      name: "South Operations Cluster",
      createdAt: now,
      updatedAt: now,
    },
    passportId: "did:web:acme.battery.pass:sample-customer-south-001",
    registryId: "sample-registry-customer-south-001",
    name: "ID-BAT302",
    modelNumber: "M-302-SOUTH",
    serialNumber: "SOUTH-302-0001",
    facilityId: "South Assembly Line 2",
    batteryCategory: "industrial",
    batteryStatus: "Repurposed",
    batteryMass: 462,
    manufacturingDate: "2022-08-21T00:00:00.000Z",
    puttingIntoService: "2023-03-07T00:00:00.000Z",
    chemistry: { shortName: "LFP", clearName: "Lithium iron phosphate" },
    materialScale: 0.92,
    carbonFootprint: 148,
    carbonStages: { rawMaterials: 91, production: 38, distribution: 11, recycling: 8 },
    performanceClass: "B",
    ratedEnergy: 88,
    ratedCapacity: 196,
    ratedMaximumPower: 340,
    nominalVoltage: 705,
    expectedLifetime: 10,
    expectedNumberOfCycles: 2850,
    stateOfCharge: 61,
    remainingCapacity: 86.7,
    remainingEnergy: 78.5,
    fullCycles: 710,
    normalUser: { email: "south.user@example.test", name: "South Normal User", roles: ["viewer"] },
    adminUser: { email: "south.admin@example.test", name: "South Local Admin", roles: ["viewer"] },
  },
  {
    cluster: {
      clusterId: "cluster-fleet-operations",
      name: "Fleet Operations Cluster",
      createdAt: now,
      updatedAt: now,
    },
    passportId: "did:web:acme.battery.pass:sample-end-user-fleet-001",
    registryId: "sample-registry-end-user-fleet-001",
    name: "FLT-PACK404",
    modelNumber: "M-404-FLEET",
    serialNumber: "FLEET-404-0001",
    facilityId: "Fleet Depot Charging Hall",
    batteryCategory: "commercial",
    batteryStatus: "Original",
    batteryMass: 536,
    manufacturingDate: "2024-02-15T00:00:00.000Z",
    puttingIntoService: "2024-05-22T00:00:00.000Z",
    chemistry: { shortName: "NCA", clearName: "Lithium nickel cobalt aluminium oxide" },
    materialScale: 1.08,
    carbonFootprint: 121,
    carbonStages: { rawMaterials: 72, production: 31, distribution: 10, recycling: 8 },
    performanceClass: "A",
    ratedEnergy: 110,
    ratedCapacity: 224,
    ratedMaximumPower: 430,
    nominalVoltage: 760,
    expectedLifetime: 14,
    expectedNumberOfCycles: 3600,
    stateOfCharge: 92,
    remainingCapacity: 94.1,
    remainingEnergy: 90.4,
    fullCycles: 305,
    normalUser: { email: "fleet.user@example.test", name: "Fleet Normal User", roles: ["viewer"] },
    adminUser: { email: "fleet.admin@example.test", name: "Fleet Local Admin", roles: ["viewer"] },
  },
  {
    cluster: {
      clusterId: "cluster-storage-operations",
      name: "Storage Operations Cluster",
      createdAt: now,
      updatedAt: now,
    },
    passportId: "did:web:acme.battery.pass:sample-end-user-storage-001",
    registryId: "sample-registry-end-user-storage-001",
    name: "STR-MOD508",
    modelNumber: "M-508-STORAGE",
    serialNumber: "STORAGE-508-0001",
    facilityId: "Storage Site A",
    batteryCategory: "stationary",
    batteryStatus: "Remanufactured",
    batteryMass: 690,
    manufacturingDate: "2021-06-30T00:00:00.000Z",
    puttingIntoService: "2022-01-18T00:00:00.000Z",
    chemistry: { shortName: "LMO", clearName: "Lithium manganese oxide" },
    materialScale: 1.22,
    carbonFootprint: 166,
    carbonStages: { rawMaterials: 101, production: 42, distribution: 13, recycling: 10 },
    performanceClass: "C",
    ratedEnergy: 140,
    ratedCapacity: 260,
    ratedMaximumPower: 290,
    nominalVoltage: 650,
    expectedLifetime: 15,
    expectedNumberOfCycles: 4200,
    stateOfCharge: 48,
    remainingCapacity: 81.3,
    remainingEnergy: 72.9,
    fullCycles: 980,
    normalUser: { email: "storage.user@example.test", name: "Storage Normal User", roles: ["viewer"] },
    adminUser: { email: "storage.admin@example.test", name: "Storage Local Admin", roles: ["viewer"] },
  },
];

export const additionalSampleClusterSeeds = seedInputs.map((input) => ({
  cluster: input.cluster,
  passport: buildPassport(input),
  normalUser: input.normalUser,
  adminUser: input.adminUser,
  memberships: [
    {
      email: input.normalUser.email,
      clusterId: input.cluster.clusterId,
      role: "member",
      createdAt: now,
      updatedAt: now,
    },
    {
      email: input.adminUser.email,
      clusterId: input.cluster.clusterId,
      role: "clusterAdmin",
      createdAt: now,
      updatedAt: now,
    },
  ] satisfies ClusterMembership[],
}));

export const allSampleClusters = [defaultSampleCluster, ...additionalSampleClusterSeeds.map((seed) => seed.cluster)];
