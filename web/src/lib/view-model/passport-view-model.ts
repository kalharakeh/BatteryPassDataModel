import type { BatteryPassport, ChartSegment, Cluster, PassportAppMetadata, PassportDocumentLink } from "../../types/passport";

function valueAt(payload: Record<string, unknown>, key: string) {
  return payload[key] == null ? "" : String(payload[key]);
}

function numberAt(payload: Record<string, unknown>, key: string) {
  const value = Number(payload[key]);
  return Number.isFinite(value) ? value : 0;
}

function dateOnly(value: string) {
  return value ? value.slice(0, 10) : "";
}

function fallbackApp(passport: BatteryPassport): PassportAppMetadata {
  const general = passport.aspects.generalProductInformation?.payload ?? {};
  const carbon = passport.aspects.carbonFootprintForBatteries?.payload ?? {};
  const carbonStages = Array.isArray(carbon.carbonFootprintPerLifecycleStage)
    ? (carbon.carbonFootprintPerLifecycleStage as Array<{ lifecycleStage?: string; carbonFootprint?: number }>)
    : [];

  return {
    display: {
      name: valueAt(general, "productIdentifier"),
      modelNumber: valueAt(general, "productIdentifier"),
      serialNumber: valueAt(general, "batteryPassportIdentifier"),
      facilityId: "",
      manufacturerName: "",
    },
    media: {
      batteryImageUrl: "/sample-battery.png",
      batteryImageAlt: "Industrial EV battery pack",
    },
    documents: {
      conformityAssessment: { label: "Conformity assessment", url: "" },
      euDeclarationOfConformity: { label: "EU declaration of conformity ID", url: "" },
      sustainabilityReport: { label: "Sustainability report", url: "" },
      dueDiligenceReport: { label: "Due diligence report", url: "" },
      thirdPartyAudit: { label: "Third party audit", url: "" },
      taxonomyReport: { label: "Taxonomy report", url: "" },
      co2StudyReference: { label: "CO2 study reference", url: valueAt(carbon, "carbonFootprintStudy") },
    },
    charts: {
      materialComposition: [],
      carbonFootprint: carbonStages.map((stage) => ({
        label: String(stage.lifecycleStage ?? ""),
        value: Number(stage.carbonFootprint ?? 0),
        unit: "gCO2e/kWh",
      })),
      recycledContent: [],
    },
    notes: {
      circularity: {
        separateCollection: "",
        wastePrevention: "",
        recycledContentShareVerification: "unverified",
      },
    },
  };
}

function chartTotal(segments: ChartSegment[]) {
  return segments.reduce((total, segment) => total + segment.value, 0);
}

function withPercentages(segments: ChartSegment[]) {
  const total = chartTotal(segments);
  return segments.map((segment) => ({
    ...segment,
    percentage: total > 0 ? Math.round((segment.value / total) * 1000) / 10 : 0,
  }));
}

export function toPassportViewModel(passport: BatteryPassport, options: { clusters?: Cluster[] } = {}) {
  const app = passport.app ?? fallbackApp(passport);
  const general = passport.aspects.generalProductInformation?.payload ?? {};
  const carbon = passport.aspects.carbonFootprintForBatteries?.payload ?? {};
  const performance = passport.aspects.performanceAndDurability?.payload ?? {};
  const batteryCondition = (performance.batteryCondition ?? {}) as Record<string, Record<string, unknown>>;
  const technical = (performance.batteryTechicalProperties ?? {}) as Record<string, unknown>;

  const documents = Object.fromEntries(
    Object.entries(app.documents).map(([key, document]) => [key, document as PassportDocumentLink]),
  ) as PassportAppMetadata["documents"];
  const clusterNames = passport.clusterId
    ? [options.clusters?.find((cluster) => cluster.clusterId === passport.clusterId)?.name ?? passport.clusterId]
    : [];

  return {
    passportId: passport.passportId,
    clusterId: passport.clusterId,
    clusterNames,
    clusterLabel: clusterNames.length ? clusterNames.join(", ") : "No cluster assigned",
    registryId: passport.registryInfo.registryId,
    status: passport.registryInfo.status,
    name: app.display.name,
    modelNumber: app.display.modelNumber || valueAt(general, "productIdentifier"),
    serialNumber: app.display.serialNumber || valueAt(general, "batteryPassportIdentifier"),
    category: valueAt(general, "batteryCategory").toUpperCase(),
    batteryStatus: valueAt(general, "batteryStatus"),
    manufacturedDate: dateOnly(valueAt(general, "manufacturingDate")),
    manufacturerName: app.display.manufacturerName,
    facilityId: app.display.facilityId,
    weight: numberAt(general, "batteryMass"),
    weightLabel: `${numberAt(general, "batteryMass").toFixed(2)}kg`,
    carbonFootprint: valueAt(carbon, "batteryCarbonFootprint"),
    carbonFootprintLabel: `${Number(valueAt(carbon, "batteryCarbonFootprint") || 0).toFixed(2)}gCO2e/kWh`,
    performanceClass: valueAt(carbon, "carbonFootprintPerformanceClass"),
    isValid: passport.validation.isValid,
    verificationState: passport.validation.isValid ? "verified" : "unverified",
    batteryImageUrl: app.media.batteryImageUrl,
    batteryImageAlt: app.media.batteryImageAlt,
    documents,
    materialComposition: {
      total: chartTotal(app.charts.materialComposition),
      segments: withPercentages(app.charts.materialComposition),
    },
    carbonFootprintStages: {
      total: chartTotal(app.charts.carbonFootprint),
      segments: withPercentages(app.charts.carbonFootprint),
    },
    recycledContent: app.charts.recycledContent,
    circularity: app.notes.circularity,
    performance: {
      ratedEnergy: Number(technical.ratedEnergy ?? 0),
      ratedCapacity: Number(technical.ratedCapacity ?? 0),
      ratedMaximumPower: Number(technical.ratedMaximumPower ?? 0),
      nominalVoltage: Number(technical.nominalVoltage ?? 0),
      expectedLifetime: Number(technical.expectedLifetime ?? 0),
      expectedNumberOfCycles: Number(technical.expectedNumberOfCycles ?? 0),
      stateOfCharge: Number(batteryCondition.stateOfCharge?.stateOfChargeValue ?? 0),
      remainingCapacity: Number(batteryCondition.remainingCapacity?.remainingCapacityValue ?? 0),
      remainingEnergy: Number(batteryCondition.remainingEnergy?.remainingEnergyValue ?? 0),
      cycles: Number(batteryCondition.numberOfFullCycles?.numberOfFullCyclesValue ?? 0),
    },
    sections: ["General", "Material composition", "Performance", "Compliance", "Supply chain", "Circularity", "Carbon Footprint"],
  };
}
