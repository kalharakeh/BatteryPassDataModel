import { randomUUID } from "node:crypto";
import type { BatteryPassport, PassportDocumentKey, VerificationState } from "../../types/passport";
import { sampleDocumentLabels, samplePassport } from "../seed/sample-passport";

type BuildPassportOptions = {
  existingPassport: BatteryPassport | null;
  now?: string;
  documentUrls?: Partial<Record<PassportDocumentKey, { url: string; fileId?: string }>>;
  batteryImageUrl?: string;
  batteryImageFileId?: string;
};

const materialFields = [
  { label: "Nickel", field: "materialNickel", fallback: 134.7, color: "#4f6f7d" },
  { label: "Copper", field: "materialCopper", fallback: 74.9, color: "#d76f3d" },
  { label: "Aluminium", field: "materialAluminium", fallback: 69.9, color: "#aeb4ba" },
  { label: "Graphite", field: "materialGraphite", fallback: 64.9, color: "#27313f" },
  { label: "Manganese", field: "materialManganese", fallback: 49.9, color: "#d9b64e" },
  { label: "Cobalt", field: "materialCobalt", fallback: 34.9, color: "#0aa34f" },
  { label: "Lithium", field: "materialLithium", fallback: 20, color: "#85c7d6" },
  { label: "Electrolyte and separators", field: "materialElectrolyte", fallback: 49.8, color: "#e7d99d" },
] as const;

const carbonFields = [
  { label: "raw material extraction", lifecycleStage: "RawMaterialExtraction", field: "carbonRawMaterial", fallback: 89, color: "#08a348" },
  { label: "main production", lifecycleStage: "MainProduction", field: "carbonMainProduction", fallback: 30, color: "#df6b3b" },
  { label: "distribution", lifecycleStage: "Distribution", field: "carbonDistribution", fallback: 10, color: "#ead9a4" },
  { label: "recycling", lifecycleStage: "Recycling", field: "carbonRecycling", fallback: 8, color: "#4f6f7d" },
] as const;

const recycledFields = [
  { material: "Nickel", fieldPrefix: "recycledNickel", pre: 17, post: 7, primary: 76 },
  { material: "Cobalt", fieldPrefix: "recycledCobalt", pre: 10, post: 9, primary: 81 },
  { material: "Lithium", fieldPrefix: "recycledLithium", pre: 14, post: 10, primary: 76 },
  { material: "Lead", fieldPrefix: "recycledLead", pre: 11, post: 6, primary: 83 },
] as const;

function text(formData: FormData, key: string, fallback = "") {
  const value = formData.get(key);
  return typeof value === "string" && value.trim() ? value.trim() : fallback;
}

function numberValue(formData: FormData, key: string, fallback: number) {
  const parsed = Number(text(formData, key, String(fallback)));
  return Number.isFinite(parsed) ? parsed : fallback;
}

function clonePassport(passport: BatteryPassport) {
  return JSON.parse(JSON.stringify(passport)) as BatteryPassport;
}

function absoluteUrl(url: string) {
  if (!url) return url;
  if (/^[a-z][a-z0-9+.-]*:/i.test(url)) return url;
  return new URL(url, process.env.APP_URL ?? "http://localhost:3000").toString();
}

function ensureAppMetadata(passport: BatteryPassport) {
  if (!passport.app) {
    passport.app = clonePassport(samplePassport).app;
  }
  return passport.app;
}

function documentUrl(existing: BatteryPassport, key: PassportDocumentKey, replacement?: { url: string; fileId?: string }) {
  const app = ensureAppMetadata(existing);
  const current = app.documents[key];
  return {
    url: replacement?.url || current?.url || `/api/files/seed-${key}`,
    fileId: replacement?.fileId || current?.fileId,
  };
}

export function buildPassportFromFormData(formData: FormData, options: BuildPassportOptions) {
  const now = options.now ?? new Date().toISOString();
  const passport = clonePassport(options.existingPassport ?? samplePassport);
  const app = ensureAppMetadata(passport);

  passport.passportId = text(formData, "passportId", passport.passportId);
  passport.registryInfo.registryId = options.existingPassport ? passport.registryInfo.registryId || randomUUID() : randomUUID();
  passport.registryInfo.createdAt = options.existingPassport ? passport.registryInfo.createdAt || now : now;
  passport.registryInfo.updatedAt = now;
  passport.registryInfo.status = text(formData, "status", passport.registryInfo.status) as BatteryPassport["registryInfo"]["status"];
  passport.validation.isValid = true;
  passport.validation.signedAt = now;

  const display = {
    name: text(formData, "name", app.display.name),
    modelNumber: text(formData, "modelNumber", app.display.modelNumber),
    serialNumber: text(formData, "serialNumber", app.display.serialNumber),
    facilityId: text(formData, "facilityId", app.display.facilityId),
    manufacturerName: text(formData, "manufacturerName", app.display.manufacturerName),
  };
  app.display = display;

  app.media.batteryImageUrl = options.batteryImageUrl || app.media.batteryImageUrl || "/sample-battery.png";
  app.media.batteryImageFileId = options.batteryImageFileId || app.media.batteryImageFileId;
  app.media.batteryImageAlt = `Industrial EV battery pack for sample passport ${display.modelNumber}`;

  for (const key of Object.keys(sampleDocumentLabels) as PassportDocumentKey[]) {
    const document = documentUrl(passport, key, options.documentUrls?.[key]);
    app.documents[key] = {
      ...app.documents[key],
      label: sampleDocumentLabels[key],
      url: document.url,
      fileId: document.fileId,
      contentType: "application/pdf",
    };
  }

  const general = passport.aspects.generalProductInformation?.payload ?? {};
  general.productIdentifier = display.modelNumber;
  general.batteryPassportIdentifier = `urn:acme:${display.serialNumber.toLowerCase().replace(/[^a-z0-9]/g, "")}`;
  general.batteryCategory = text(formData, "category", String(general.batteryCategory ?? "EV")).toLowerCase();
  general.batteryStatus = text(formData, "batteryStatus", String(general.batteryStatus ?? "Original"));
  general.batteryMass = numberValue(formData, "batteryMass", Number(general.batteryMass ?? 499));
  general.manufacturingDate = `${text(formData, "manufacturingDate", String(general.manufacturingDate ?? "2023-09-05").slice(0, 10))}T00:00:00.000Z`;
  general.manufacturerInformation = {
    contactName: display.manufacturerName,
    identifier: display.manufacturerName.toLowerCase().replace(/[^a-z0-9]+/g, "-").replace(/^-|-$/g, ""),
    postalAddress: { addressCountry: "Germany", postalCode: "10115", streetAddress: display.facilityId },
    webAddress: "https://scania-industrial-batteries.example",
    emailAddress: "www@www.ww",
  };
  general.manufacturingPlace = { addressCountry: "Germany", postalCode: "10115", streetAddress: display.facilityId };
  general.operatorInformation = {
    ...((general.operatorInformation ?? {}) as Record<string, unknown>),
    emailAddress: "www@www.ww",
  };
  general.warrentyPeriod = general.warrentyPeriod || "--08";
  passport.aspects.generalProductInformation = {
    visibility: "public",
    verification: { state: "verified", issuer: "did:web:acme.battery.pass:issuer", signedAt: now },
    payload: general,
  };

  const materialComposition = passport.aspects.materialComposition?.payload ?? {};
  const existingMaterials = Array.isArray(materialComposition.batteryMaterials)
    ? (materialComposition.batteryMaterials as Array<Record<string, unknown>>)
    : [];
  const materialMasses = materialFields.map((material) => ({
    label: material.label,
    value: numberValue(formData, material.field, material.fallback),
    unit: "kg",
    color: material.color,
  }));
  app.charts.materialComposition = materialMasses;
  materialComposition.batteryMaterials = materialMasses.map((material) => {
    const existing = existingMaterials.find((entry) => entry.batteryMaterialName === material.label) ?? {};
    return {
      ...existing,
      batteryMaterialName: material.label,
      batteryMaterialMass: material.value,
    };
  });
  passport.aspects.materialComposition = {
    visibility: "public",
    verification: { state: "verified", issuer: "did:web:acme.battery.pass:issuer", signedAt: now },
    payload: materialComposition,
  };

  const carbon = passport.aspects.carbonFootprintForBatteries?.payload ?? {};
  carbon.batteryCarbonFootprint = numberValue(formData, "carbonFootprint", Number(carbon.batteryCarbonFootprint ?? 137));
  carbon.carbonFootprintPerformanceClass = text(formData, "performanceClass", String(carbon.carbonFootprintPerformanceClass ?? "B"));
  app.charts.carbonFootprint = carbonFields.map((stage) => ({
    label: stage.label,
    value: numberValue(formData, stage.field, stage.fallback),
    unit: "gCO2e/kWh",
    color: stage.color,
  }));
  carbon.carbonFootprintPerLifecycleStage = carbonFields.map((stage) => ({
    lifecycleStage: stage.lifecycleStage,
    carbonFootprint: numberValue(formData, stage.field, stage.fallback),
  }));
  carbon.carbonFootprintStudy = absoluteUrl(app.documents.co2StudyReference.url);
  passport.aspects.carbonFootprintForBatteries = {
    visibility: "public",
    verification: { state: "verified", issuer: "did:web:acme.battery.pass:issuer", signedAt: now },
    payload: carbon,
  };

  const performance = passport.aspects.performanceAndDurability?.payload ?? {};
  const technical = (performance.batteryTechicalProperties ?? {}) as Record<string, unknown>;
  technical.ratedEnergy = numberValue(formData, "ratedEnergy", Number(technical.ratedEnergy ?? 95));
  technical.ratedCapacity = numberValue(formData, "ratedCapacity", Number(technical.ratedCapacity ?? 210));
  technical.ratedMaximumPower = numberValue(formData, "ratedMaximumPower", Number(technical.ratedMaximumPower ?? 380));
  technical.nominalVoltage = numberValue(formData, "nominalVoltage", Number(technical.nominalVoltage ?? 730));
  technical.expectedLifetime = numberValue(formData, "expectedLifetime", Number(technical.expectedLifetime ?? 12));
  technical.expectedNumberOfCycles = numberValue(formData, "expectedNumberOfCycles", Number(technical.expectedNumberOfCycles ?? 3000));
  performance.batteryTechicalProperties = technical;
  const batteryCondition = (performance.batteryCondition ?? {}) as Record<string, unknown>;
  batteryCondition.stateOfCharge = {
    stateOfChargeValue: numberValue(formData, "stateOfCharge", 82),
    lastUpdate: now,
  };
  batteryCondition.remainingEnergy = {
    remainingEnergyValue: numberValue(formData, "remainingEnergy", 86.7),
    lastUpdate: now,
  };
  batteryCondition.numberOfFullCycles = {
    numberOfFullCyclesValue: numberValue(formData, "fullCycles", 412),
    lastUpdate: now,
  };
  batteryCondition.remainingCapacity = {
    remainingCapacityValue: numberValue(formData, "remainingCapacity", 91.5),
    lastUpdate: now,
  };
  performance.batteryCondition = batteryCondition;
  passport.aspects.performanceAndDurability = {
    visibility: "public",
    verification: { state: "verified", issuer: "did:web:acme.battery.pass:issuer", signedAt: now },
    payload: performance,
  };

  const labeling = passport.aspects.labeling?.payload ?? {};
  labeling.resultOfTestReport = absoluteUrl(app.documents.conformityAssessment.url);
  labeling.declarationOfConformity = absoluteUrl(app.documents.euDeclarationOfConformity.url);
  passport.aspects.labeling = {
    visibility: "public",
    verification: { state: "verified", issuer: "did:web:acme.battery.pass:issuer", signedAt: now },
    payload: labeling,
  };

  const supplyChain = passport.aspects.supplyChainDueDiligence?.payload ?? {};
  supplyChain.supplyChainDueDiligenceReport = absoluteUrl(app.documents.dueDiligenceReport.url);
  supplyChain.thirdPartyAussurances = absoluteUrl(app.documents.thirdPartyAudit.url);
  supplyChain.sustainabilityReport = absoluteUrl(app.documents.sustainabilityReport.url);
  supplyChain.taxonomyReport = absoluteUrl(app.documents.taxonomyReport.url);
  supplyChain.supplyChainIndicies = numberValue(formData, "supplyChainIndex", Number(supplyChain.supplyChainIndicies ?? 82));
  passport.aspects.supplyChainDueDiligence = {
    visibility: "public",
    verification: { state: "verified", issuer: "did:web:acme.battery.pass:issuer", signedAt: now },
    payload: supplyChain,
  };

  app.charts.recycledContent = recycledFields.map((entry) => ({
    material: entry.material,
    preConsumerShare: numberValue(formData, `${entry.fieldPrefix}Pre`, entry.pre),
    postConsumerShare: numberValue(formData, `${entry.fieldPrefix}Post`, entry.post),
    primaryMaterialShare: numberValue(formData, `${entry.fieldPrefix}Primary`, entry.primary),
  }));

  app.notes.circularity = {
    separateCollection: text(formData, "separateCollection", app.notes.circularity.separateCollection),
    wastePrevention: text(formData, "wastePrevention", app.notes.circularity.wastePrevention),
    recycledContentShareVerification: text(
      formData,
      "recycledContentShareVerification",
      app.notes.circularity.recycledContentShareVerification,
    ) as VerificationState,
  };

  const circularity = passport.aspects.circularity?.payload ?? {};
  circularity.recycledContent = app.charts.recycledContent.map((entry) => ({
    recycledMaterial: entry.material,
    preConsumerShare: entry.preConsumerShare,
    postConsumerShare: entry.postConsumerShare,
  }));
  circularity.endOfLifeInformation = {
    ...((circularity.endOfLifeInformation ?? {}) as Record<string, unknown>),
    separateCollection: absoluteUrl(String((circularity.endOfLifeInformation as Record<string, unknown> | undefined)?.separateCollection ?? "https://acme.battery.pass.example/circularity/separate-collection")),
    wastePrevention: absoluteUrl(String((circularity.endOfLifeInformation as Record<string, unknown> | undefined)?.wastePrevention ?? "https://acme.battery.pass.example/circularity/waste-prevention")),
    informationOnCollection: absoluteUrl(String((circularity.endOfLifeInformation as Record<string, unknown> | undefined)?.informationOnCollection ?? "https://acme.battery.pass.example/circularity/collection-points")),
  };
  passport.aspects.circularity = {
    visibility: "public",
    verification: { state: "partial", issuer: "did:web:acme.battery.pass:issuer", signedAt: now },
    payload: circularity,
  };

  return passport;
}
