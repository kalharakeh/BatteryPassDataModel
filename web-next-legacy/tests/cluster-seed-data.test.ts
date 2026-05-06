import assert from "node:assert/strict";
import { describe, it } from "node:test";
import { additionalSampleClusterSeeds, allSampleClusters, defaultSampleCluster } from "../src/lib/seed/sample-clusters";
import { samplePassport } from "../src/lib/seed/sample-passport";

const expectedImageByPassportId = new Map([
  ["did:web:acme.battery.pass:sample-customer-north-001", { url: "/images/compact7.png", category: "Compact 7M" }],
  ["did:web:acme.battery.pass:sample-customer-south-001", { url: "/images/compact13.png", category: "Compact 13M" }],
  ["did:web:acme.battery.pass:sample-end-user-fleet-001", { url: "/images/core.png", category: "Core" }],
  ["did:web:acme.battery.pass:sample-end-user-storage-001", { url: "/images/compact13.png", category: "Compact 13M" }],
]);

function aspectPayload(passport: (typeof additionalSampleClusterSeeds)[number]["passport"], aspect: "generalProductInformation" | "carbonFootprintForBatteries" | "performanceAndDurability") {
  return passport.aspects[aspect]?.payload ?? {};
}

function generatedValueSet(
  picker: (passport: (typeof additionalSampleClusterSeeds)[number]["passport"]) => unknown,
) {
  return new Set(additionalSampleClusterSeeds.map((seed) => picker(seed.passport)));
}

describe("sample cluster seed data", () => {
  it("creates four extra cluster/battery/account groups with distinct cluster assignments", () => {
    assert.equal(additionalSampleClusterSeeds.length, 4);
    assert.equal(defaultSampleCluster.clusterId, "cluster-default-demonstrator");

    const clusterIds = new Set(additionalSampleClusterSeeds.map((seed) => seed.cluster.clusterId));
    const passportIds = new Set(additionalSampleClusterSeeds.map((seed) => seed.passport.passportId));
    const normalUsers = new Set(additionalSampleClusterSeeds.map((seed) => seed.normalUser.email));
    const adminUsers = new Set(additionalSampleClusterSeeds.map((seed) => seed.adminUser.email));

    assert.equal(clusterIds.size, 4);
    assert.equal(passportIds.size, 4);
    assert.equal(normalUsers.size, 4);
    assert.equal(adminUsers.size, 4);

    for (const seed of additionalSampleClusterSeeds) {
      assert.equal(seed.passport.clusterId, seed.cluster.clusterId);
      assert.deepEqual(
        seed.memberships.map((membership) => [membership.email, membership.clusterId, membership.role]),
        [
          [seed.normalUser.email, seed.cluster.clusterId, "member"],
          [seed.adminUser.email, seed.cluster.clusterId, "clusterAdmin"],
        ],
      );
    }
  });

  it("keeps clusters neutral with no category/kind field", () => {
    for (const cluster of allSampleClusters) {
      assert.equal("kind" in cluster, false);
      assert.ok(cluster.clusterId);
      assert.ok(cluster.name);
    }
  });

  it("assigns each seeded battery to its own distinct cluster", () => {
    const passports = [samplePassport, ...additionalSampleClusterSeeds.map((seed) => seed.passport)];
    const clusterIds = passports.map((passport) => passport.clusterId);

    assert.equal(clusterIds.length, 5);
    assert.equal(new Set(clusterIds).size, 5);
    for (const clusterId of clusterIds) {
      assert.ok(allSampleClusters.some((cluster) => cluster.clusterId === clusterId));
    }
  });

  it("keeps every seeded battery manufacturer as Scania Industrial Batteries", () => {
    const passports = [samplePassport, ...additionalSampleClusterSeeds.map((seed) => seed.passport)];

    for (const passport of passports) {
      const general = passport.aspects.generalProductInformation?.payload ?? {};
      const manufacturerInformation = (general.manufacturerInformation ?? {}) as Record<string, unknown>;

      assert.equal(passport.app.display.manufacturerName, "Scania Industrial Batteries");
      assert.equal(manufacturerInformation.contactName, "Scania Industrial Batteries");
      assert.equal(manufacturerInformation.identifier, "scania-industrial-batteries");
    }
  });

  it("keeps generated battery categories aligned with the assigned image assets", () => {
    for (const seed of additionalSampleClusterSeeds) {
      const expected = expectedImageByPassportId.get(seed.passport.passportId);
      assert.ok(expected);

      assert.equal(seed.passport.app.media.batteryImageUrl, expected.url);
      assert.equal(aspectPayload(seed.passport, "generalProductInformation").batteryCategory, expected.category);
    }
  });

  it("uses visibly different sample battery data for each generated battery", () => {
    assert.equal(generatedValueSet((passport) => passport.app.media.batteryImageUrl).size, 3);
    assert.equal(generatedValueSet((passport) => aspectPayload(passport, "generalProductInformation").batteryMass).size, 4);
    assert.equal(generatedValueSet((passport) => aspectPayload(passport, "generalProductInformation").manufacturingDate).size, 4);
    assert.equal(generatedValueSet((passport) => aspectPayload(passport, "carbonFootprintForBatteries").batteryCarbonFootprint).size, 4);
    assert.equal(generatedValueSet((passport) => {
      const performance = aspectPayload(passport, "performanceAndDurability");
      const technical = (performance.batteryTechicalProperties ?? {}) as Record<string, unknown>;
      return technical.ratedEnergy;
    }).size, 4);
  });
});
