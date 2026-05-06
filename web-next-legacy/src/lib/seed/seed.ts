import bcrypt from "bcryptjs";
import type { PassportDocumentKey, PassportDocumentLink } from "../../types/passport";
import { closeMongoClient } from "../db/client";
import { clusterCollection, clusterMembershipCollection, getDatabase, passportCollection, userCollection } from "../db/collections";
import { getStoredFileBySeedKey, uploadBufferToGridFs } from "../db/files";
import { loadAppEnv } from "../env/load-app-env";
import { createDemoPdfBuffer } from "../files/demo-pdf";
import { ensureClusterIndexes, ensurePassportIndexes } from "./indexes";
import { additionalSampleClusterSeeds, allSampleClusters, getSampleBatteryImageOption, legacySampleClusterIds } from "./sample-clusters";
import { oldLocalSamplePassportId, sampleDocumentLabels, samplePassport } from "./sample-passport";
import type { BatteryPassport } from "../../types/passport";

loadAppEnv();

function appUrl(path: string) {
  const baseUrl = process.env.APP_URL ?? "http://localhost:3000";
  return new URL(path, baseUrl).toString();
}

const documentSeedLines: Record<PassportDocumentKey, string[]> = {
  conformityAssessment: [
    "Conformity assessment for EV-BAT095.",
    "Assessment method: EU Battery Regulation demonstrator profile.",
    "Result: Verified for demo registry use.",
  ],
  euDeclarationOfConformity: [
    "EU declaration of conformity ID for model M-41698615.",
    "Manufacturer: Scania Industrial Batteries.",
    "This generated PDF is sample evidence for the demo passport.",
  ],
  sustainabilityReport: [
    "Sustainability report for the ACME sample battery passport.",
    "Includes representative environmental and social due-diligence statements.",
  ],
  dueDiligenceReport: [
    "Due diligence report for critical raw material sourcing.",
    "Supplier review score: 82 out of 100.",
  ],
  thirdPartyAudit: [
    "Third party audit summary.",
    "No material unresolved findings recorded for the demonstration dataset.",
  ],
  taxonomyReport: [
    "Taxonomy report for industrial EV battery manufacturing.",
    "Demo classification: aligned for sample reporting workflows.",
  ],
  co2StudyReference: [
    "CO2 study reference for EV-BAT095.",
    "Declared carbon footprint: 137 gCO2e/kWh.",
  ],
};

function clonePassport(passport: BatteryPassport) {
  return JSON.parse(JSON.stringify(passport)) as BatteryPassport;
}

async function ensureSeedPdf(passport: BatteryPassport, key: PassportDocumentKey): Promise<PassportDocumentLink> {
  const seedKey = `${passport.passportId}-${key}`;
  const existing = await getStoredFileBySeedKey(seedKey);
  if (existing?._id) {
    const fileId = existing._id.toString();
    return {
      label: sampleDocumentLabels[key],
      fileId,
      url: appUrl(`/api/files/${fileId}`),
      contentType: "application/pdf",
    };
  }

  const saved = await uploadBufferToGridFs({
    buffer: createDemoPdfBuffer(`${passport.app.display.name} ${sampleDocumentLabels[key]}`, documentSeedLines[key]),
    filename: `${passport.app.display.name}-${key}.pdf`,
    contentType: "application/pdf",
    metadata: {
      seedKey,
      label: sampleDocumentLabels[key],
      passportId: passport.passportId,
    },
  });

  return {
    label: sampleDocumentLabels[key],
    fileId: saved.fileId,
    url: appUrl(saved.url),
    contentType: "application/pdf",
  };
}

async function buildSeedPassport(sourcePassport: BatteryPassport) {
  const passport = clonePassport(sourcePassport);
  const imageOption = getSampleBatteryImageOption(passport.passportId);
  passport.app.media.batteryImageUrl = imageOption.url;
  const general = passport.aspects.generalProductInformation?.payload;
  if (general) {
    general.batteryCategory = imageOption.category;
  }

  const documents = Object.fromEntries(
    await Promise.all((Object.keys(sampleDocumentLabels) as PassportDocumentKey[]).map(async (key) => [key, await ensureSeedPdf(passport, key)])),
  ) as Record<PassportDocumentKey, PassportDocumentLink>;

  passport.app.documents = documents;

  const labeling = passport.aspects.labeling?.payload;
  if (labeling) {
    labeling.resultOfTestReport = documents.conformityAssessment.url;
    labeling.declarationOfConformity = documents.euDeclarationOfConformity.url;
  }

  const supplyChain = passport.aspects.supplyChainDueDiligence?.payload;
  if (supplyChain) {
    supplyChain.supplyChainDueDiligenceReport = documents.dueDiligenceReport.url;
    supplyChain.thirdPartyAussurances = documents.thirdPartyAudit.url;
    supplyChain.sustainabilityReport = documents.sustainabilityReport.url;
    supplyChain.taxonomyReport = documents.taxonomyReport.url;
  }

  const carbon = passport.aspects.carbonFootprintForBatteries?.payload;
  if (carbon) {
    carbon.carbonFootprintStudy = documents.co2StudyReference.url;
  }

  return passport;
}

async function seed() {
  const passports = await passportCollection();
  const users = await userCollection();
  const clusters = await clusterCollection();
  const memberships = await clusterMembershipCollection();

  await ensurePassportIndexes(passports);
  await ensureClusterIndexes(await getDatabase());
  await passports.updateOne(
    { passportId: oldLocalSamplePassportId },
    {
      $set: {
        "registryInfo.registryId": "archived-local-sample-0226151e-949c-d067-8ef3-162431e28976",
        "registryInfo.status": "archived",
        "registryInfo.updatedAt": new Date().toISOString(),
      },
    },
  );

  await clusters.updateMany({}, { $unset: { kind: "" } });
  await clusters.deleteMany({ clusterId: { $in: legacySampleClusterIds } });
  await memberships.deleteMany({ clusterId: { $in: legacySampleClusterIds } });
  await Promise.all(
    allSampleClusters.map((cluster) => clusters.updateOne({ clusterId: cluster.clusterId }, { $set: cluster, $unset: { kind: "" } }, { upsert: true })),
  );

  const seededPassport = await buildSeedPassport(samplePassport);
  await passports.updateOne({ passportId: seededPassport.passportId }, { $set: seededPassport }, { upsert: true });

  const password = process.env.DEMO_ADMIN_PASSWORD ?? "Password123!";
  const passwordHash = await bcrypt.hash(password, 10);
  const email = process.env.DEMO_ADMIN_EMAIL ?? "admin@example.test";

  await users.updateOne(
    { email },
    {
      $set: {
        email,
        passwordHash,
        name: "Demo Admin",
        roles: ["admin", "viewer", "issuer", "verifier"],
      },
    },
    { upsert: true },
  );

  for (const seedGroup of additionalSampleClusterSeeds) {
    const passport = await buildSeedPassport(seedGroup.passport);
    await passports.updateOne({ passportId: passport.passportId }, { $set: passport }, { upsert: true });

    for (const user of [seedGroup.normalUser, seedGroup.adminUser]) {
      await users.updateOne(
        { email: user.email },
        {
          $set: {
            email: user.email,
            passwordHash,
            name: user.name,
            roles: user.roles,
          },
        },
        { upsert: true },
      );
    }

    for (const membership of seedGroup.memberships) {
      await memberships.updateOne(
        { email: membership.email, clusterId: membership.clusterId },
        { $set: membership },
        { upsert: true },
      );
    }
  }

  console.log(`Seeded passport ${seededPassport.passportId}`);
  console.log(`Seeded ${additionalSampleClusterSeeds.length} cluster sample passports`);
  console.log(`Seeded admin user ${email}`);
}

seed()
  .then(async () => {
    await closeMongoClient();
    process.exit(0);
  })
  .catch((error) => {
    console.error(error);
    closeMongoClient().finally(() => process.exit(1));
  });
