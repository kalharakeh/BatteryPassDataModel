import type { BatteryPassport, PassportDocumentKey, PassportDocumentLink } from "../../types/passport";

const now = "2024-09-05T08:03:42.000Z";

export const samplePassportId = "did:web:acme.battery.pass:0226151e-949c-d067-8ef3-162431e28976";
export const oldLocalSamplePassportId = "did:web:local.battery.pass:0226151e-949c-d067-8ef3-162431e28976";

export const sampleDocumentLabels: Record<PassportDocumentKey, string> = {
  conformityAssessment: "Conformity assessment",
  euDeclarationOfConformity: "EU declaration of conformity ID",
  sustainabilityReport: "Sustainability report",
  dueDiligenceReport: "Due diligence report",
  thirdPartyAudit: "Third party audit",
  taxonomyReport: "Taxonomy report",
  co2StudyReference: "CO2 study reference",
};

function documentLink(key: PassportDocumentKey): PassportDocumentLink {
  return {
    label: sampleDocumentLabels[key],
    url: `https://acme.battery.pass.example/api/files/seed-${key}`,
    contentType: "application/pdf",
  };
}

const documents = {
  conformityAssessment: documentLink("conformityAssessment"),
  euDeclarationOfConformity: documentLink("euDeclarationOfConformity"),
  sustainabilityReport: documentLink("sustainabilityReport"),
  dueDiligenceReport: documentLink("dueDiligenceReport"),
  thirdPartyAudit: documentLink("thirdPartyAudit"),
  taxonomyReport: documentLink("taxonomyReport"),
  co2StudyReference: documentLink("co2StudyReference"),
};

export const samplePassport: BatteryPassport = {
  passportId: samplePassportId,
  clusterId: "cluster-default-demonstrator",
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
      verification: { state: "verified", issuer: "did:web:acme.battery.pass:issuer", signedAt: now },
      payload: {
        productIdentifier: "M-41698615",
        batteryPassportIdentifier: "urn:acme:992356610548948",
        batteryCategory: "ev",
        manufacturerInformation: {
          contactName: "Scania Industrial Batteries",
          identifier: "scania-industrial-batteries",
          postalAddress: { addressCountry: "Germany", postalCode: "10115", streetAddress: "Berlin Battery Campus 1" },
          webAddress: "https://scania-industrial-batteries.example",
          emailAddress: "www@www.ww",
        },
        manufacturingDate: "2023-09-05T00:00:00.000Z",
        batteryStatus: "Original",
        batteryMass: 499,
        manufacturingPlace: { addressCountry: "Germany", postalCode: "10115", streetAddress: "Berlin" },
        operatorInformation: {
          contactName: "ACME Battery Passport Operations",
          identifier: "acme-battery-passport-ops",
          postalAddress: { addressCountry: "Germany", postalCode: "10115", streetAddress: "Berlin Battery Campus 1" },
          webAddress: "https://acme.battery.pass.example",
          emailAddress: "www@www.ww",
        },
        puttingIntoService: "2024-01-10T00:00:00.000Z",
        warrentyPeriod: "--08",
      },
    },
    materialComposition: {
      visibility: "public",
      verification: { state: "verified", issuer: "did:web:acme.battery.pass:issuer", signedAt: now },
      payload: {
        batteryChemistry: {
          shortName: "NMC",
          clearName: "Lithium nickel manganese cobalt oxide",
        },
        hazardousSubstances: [
          {
            hazardousSubstanceClass: "AcuteToxicity",
            hazardousSubstanceConcentration: 0.04,
            hazardousSubstanceImpact: ["Handled inside sealed battery modules"],
            hazardousSubstanceIdentifier: "7440-02-0",
            hazardousSubstanceLocation: { componentName: "Cathode", componentId: "cathode-module" },
            hazardousSubstanceName: "Nickel compounds",
          },
        ],
        batteryMaterials: [
          {
            batteryMaterialIdentifier: "7440-02-0",
            batteryMaterialMass: 134.7,
            batteryMaterialName: "Nickel",
            batteryMaterialLocation: { componentName: "Cathode", componentId: "cathode-module" },
            isCriticalRawMaterial: true,
          },
          {
            batteryMaterialIdentifier: "7440-50-8",
            batteryMaterialMass: 74.9,
            batteryMaterialName: "Copper",
            batteryMaterialLocation: { componentName: "Current collectors", componentId: "collector-pack" },
            isCriticalRawMaterial: false,
          },
          {
            batteryMaterialIdentifier: "7429-90-5",
            batteryMaterialMass: 69.9,
            batteryMaterialName: "Aluminium",
            batteryMaterialLocation: { componentName: "Casing", componentId: "pack-casing" },
            isCriticalRawMaterial: false,
          },
          {
            batteryMaterialIdentifier: "7782-42-5",
            batteryMaterialMass: 64.9,
            batteryMaterialName: "Graphite",
            batteryMaterialLocation: { componentName: "Anode", componentId: "anode-module" },
            isCriticalRawMaterial: true,
          },
          {
            batteryMaterialIdentifier: "7439-96-5",
            batteryMaterialMass: 49.9,
            batteryMaterialName: "Manganese",
            batteryMaterialLocation: { componentName: "Cathode", componentId: "cathode-module" },
            isCriticalRawMaterial: true,
          },
          {
            batteryMaterialIdentifier: "7440-48-4",
            batteryMaterialMass: 34.9,
            batteryMaterialName: "Cobalt",
            batteryMaterialLocation: { componentName: "Cathode", componentId: "cathode-module" },
            isCriticalRawMaterial: true,
          },
          {
            batteryMaterialIdentifier: "7439-93-2",
            batteryMaterialMass: 20,
            batteryMaterialName: "Lithium",
            batteryMaterialLocation: { componentName: "Cathode", componentId: "cathode-module" },
            isCriticalRawMaterial: true,
          },
          {
            batteryMaterialIdentifier: "21324-40-3",
            batteryMaterialMass: 49.8,
            batteryMaterialName: "Electrolyte and separators",
            batteryMaterialLocation: { componentName: "Cell stack", componentId: "cell-stack" },
            isCriticalRawMaterial: false,
          },
        ],
      },
    },
    performanceAndDurability: {
      visibility: "public",
      verification: { state: "verified", issuer: "did:web:acme.battery.pass:issuer", signedAt: now },
      payload: {
        batteryTechicalProperties: {
          originalPowerCapability: [
            { atSoC: 20, powerCapabilityAt: 260 },
            { atSoC: 50, powerCapabilityAt: 360 },
            { atSoC: 80, powerCapabilityAt: 380 },
          ],
          ratedEnergy: 95,
          ratedCapacity: 210,
          ratedMaximumPower: 380,
          nominalVoltage: 730,
          maximumVoltage: 800,
          minimumVoltage: 610,
          roundtripEfficiency: 94.5,
          roundTripEfficiencyat50PerCentCycleLife: 92.8,
          expectedLifetime: 12,
          expectedNumberOfCycles: 3000,
          cRate: 2.5,
          temperatureRangeIdleState: { minimum: -30, maximum: 60 },
          lifetimeReferenceTest: "https://acme.battery.pass.example/api/files/seed-performance-reference",
          cRateLifeCycleTest: 1.5,
          initialInternalResistance: [{ ohmicResistance: 0.42, batteryComponent: "pack" }],
          initialSelfDischarge: 1.8,
          powerFade: 7.5,
          roundTripEfficiencyFade: 3.1,
          capacityThresholdForExhaustion: 70,
          powerCapabilityRatio: 92,
        },
        batteryCondition: {
          numberOfFullCycles: { numberOfFullCyclesValue: 412, lastUpdate: now },
          stateOfCharge: { stateOfChargeValue: 82, lastUpdate: now },
          remainingEnergy: { remainingEnergyValue: 86.7, lastUpdate: now },
          remainingCapacity: { remainingCapacityValue: 91.5, lastUpdate: now },
          capacityFade: { capacityFadeValue: 8.5, lastUpdate: now },
          remainingPowerCapability: {
            remainingPowerCapabilityValue: { atSoC: 80, powerCapabilityAt: 350, rPCLastUpdated: now },
            lastUpdate: now,
          },
          remainingRoundTripEnergyEfficiency: { remainingRoundTripEnergyEfficiencyValue: 92.4, lastUpdate: now },
          currentSelfDischargingRate: { currentSelfDischargingRateValue: 1.6, lastUpdate: now },
          evolutionOfSelfDischarge: { evolutionOfSelfDischargeValue: 0.2, lastUpdate: now },
          temperatureInformation: {
            timeExtremeHighTemp: 0,
            timeExtremeLowTemp: 0,
            timeExtremeHighTempCharging: 0,
            timeExtremeLowTempCharging: 0,
            lastUpdate: now,
          },
          negativeEvents: [{ negativeEvent: "No safety-relevant negative events recorded", lastUpdate: now }],
          energyThroughput: { energyThroughputValue: 38_450, lastUpdate: now },
          capacityThroughput: { capacityThroughputValue: 84_100, lastUpdate: now },
          stateOfCertifiedEnergy: { stateOfCertifiedEnergyValue: 91.2, lastUpdate: now },
          internalResistanceIncrease: [{ batteryComponent: "pack", internalResistanceIncreaseValue: 4.8, lastUpdate: now }],
        },
      },
    },
    labeling: {
      visibility: "public",
      verification: { state: "verified", issuer: "did:web:acme.battery.pass:issuer", signedAt: now },
      payload: {
        resultOfTestReport: documents.conformityAssessment.url,
        declarationOfConformity: documents.euDeclarationOfConformity.url,
        labels: [
          {
            labelingSubject: "SeparateCollection",
            labelingSymbol: "https://example.test/labels/separate-collection.svg",
            labelingMeaning: { en: "Separate collection required" },
          },
        ],
      },
    },
    supplyChainDueDiligence: {
      visibility: "public",
      verification: { state: "verified", issuer: "did:web:acme.battery.pass:issuer", signedAt: now },
      payload: {
        supplyChainDueDiligenceReport: documents.dueDiligenceReport.url,
        thirdPartyAussurances: documents.thirdPartyAudit.url,
        supplyChainIndicies: 82,
      },
    },
    circularity: {
      visibility: "public",
      verification: { state: "partial", issuer: "did:web:acme.battery.pass:issuer", signedAt: now },
      payload: {
        renewableContent: 12,
        dismantlingAndRemovalInformation: [
          {
            documentType: "BillOfMaterial",
            mimeType: "application/pdf",
            documentURL: "https://acme.battery.pass.example/api/files/seed-dismantling",
          },
        ],
        recycledContent: [
          { recycledMaterial: "Nickel", preConsumerShare: 17, postConsumerShare: 7 },
          { recycledMaterial: "Cobalt", preConsumerShare: 10, postConsumerShare: 9 },
          { recycledMaterial: "Lithium", preConsumerShare: 14, postConsumerShare: 10 },
          { recycledMaterial: "Lead", preConsumerShare: 11, postConsumerShare: 6 },
        ],
        endOfLifeInformation: {
          separateCollection: "https://acme.battery.pass.example/circularity/separate-collection",
          wastePrevention: "https://acme.battery.pass.example/circularity/waste-prevention",
          informationOnCollection: "https://acme.battery.pass.example/circularity/collection-points",
        },
        safetyMeasures: {
          safetyInstructions: "https://acme.battery.pass.example/circularity/safety-instructions",
          extinguishingAgent: ["Class D dry powder", "Water mist for cooling adjacent modules"],
        },
        sparePartSources: [
          {
            nameOfSupplier: "Scania Industrial Batteries Service",
            supplierWebAddress: "https://scania-industrial-batteries.example/service",
            emailAddressOfSupplier: "www@www.ww",
            addressOfSupplier: { addressCountry: "Germany", streetAddress: "Berlin Battery Campus 1", postalCode: "10115" },
            components: [{ partName: "Battery management unit", partNumber: "BMU-41698615" }],
          },
        ],
      },
    },
    carbonFootprintForBatteries: {
      visibility: "public",
      verification: { state: "verified", issuer: "did:web:acme.battery.pass:issuer", signedAt: now },
      payload: {
        batteryCarbonFootprint: 137,
        carbonFootprintPerLifecycleStage: [
          { lifecycleStage: "RawMaterialExtraction", carbonFootprint: 89 },
          { lifecycleStage: "MainProduction", carbonFootprint: 30 },
          { lifecycleStage: "Distribution", carbonFootprint: 10 },
          { lifecycleStage: "Recycling", carbonFootprint: 8 },
        ],
        carbonFootprintPerformanceClass: "B",
        carbonFootprintStudy: documents.co2StudyReference.url,
      },
    },
  },
  hiddenProperties: [],
  instanceOnlyFields: ["generalProductInformation.payload.batteryStatus"],
  validation: {
    hash: "demo-verified-hash",
    signature: "demo-verified-signature",
    proof: {
      type: "DataIntegrityProof",
      cryptosuite: "eddsa-jcs-2022",
      verificationMethod: "did:web:acme.battery.pass:issuer#demo-key-1",
    },
    isValid: true,
    signedAt: now,
  },
  dataSource: {
    name: "MongoDB Atlas",
    instanceUrl: `/api/passport-instances/${samplePassportId}`,
  },
  app: {
    display: {
      name: "EV-BAT095",
      modelNumber: "M-41698615",
      serialNumber: "992356610548948",
      facilityId: "Berlin",
      manufacturerName: "Scania Industrial Batteries",
    },
    media: {
      batteryImageUrl: "/sample-battery.png",
      batteryImageAlt: "Industrial EV battery pack for sample passport M-41698615",
    },
    documents,
    charts: {
      materialComposition: [
        { label: "Nickel", value: 134.7, unit: "kg", color: "#4f6f7d" },
        { label: "Copper", value: 74.9, unit: "kg", color: "#d76f3d" },
        { label: "Aluminium", value: 69.9, unit: "kg", color: "#aeb4ba" },
        { label: "Graphite", value: 64.9, unit: "kg", color: "#27313f" },
        { label: "Manganese", value: 49.9, unit: "kg", color: "#d9b64e" },
        { label: "Cobalt", value: 34.9, unit: "kg", color: "#0aa34f" },
        { label: "Lithium", value: 20, unit: "kg", color: "#85c7d6" },
        { label: "Electrolyte and separators", value: 49.8, unit: "kg", color: "#e7d99d" },
      ],
      carbonFootprint: [
        { label: "raw material extraction", value: 89, unit: "gCO2e/kWh", color: "#08a348" },
        { label: "main production", value: 30, unit: "gCO2e/kWh", color: "#df6b3b" },
        { label: "distribution", value: 10, unit: "gCO2e/kWh", color: "#ead9a4" },
        { label: "recycling", value: 8, unit: "gCO2e/kWh", color: "#4f6f7d" },
      ],
      recycledContent: [
        { material: "Nickel", preConsumerShare: 17, postConsumerShare: 7, primaryMaterialShare: 76 },
        { material: "Cobalt", preConsumerShare: 10, postConsumerShare: 9, primaryMaterialShare: 81 },
        { material: "Lithium", preConsumerShare: 14, postConsumerShare: 10, primaryMaterialShare: 76 },
        { material: "Lead", preConsumerShare: 11, postConsumerShare: 6, primaryMaterialShare: 83 },
      ],
    },
    notes: {
      circularity: {
        separateCollection: "Ensure that the waste battery is disposed of according to material composition",
        wastePrevention: "Don't dispose battery at normal waste",
        recycledContentShareVerification: "unverified",
      },
    },
  },
};
